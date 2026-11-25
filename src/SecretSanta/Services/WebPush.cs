using System.Buffers.Binary;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecretSanta.Data;

namespace SecretSanta.Services;

// Web Push with the platform's own crypto: message encryption (RFC 8291 on top of the aes128gcm coding of RFC 8188)
// and the VAPID authorization header (RFC 8292). All keys are P-256; public keys are 65-byte uncompressed points.
public static class WebPush
{
    private const int RecordSize = 4096;

    // Browsers' push services. Any other endpoint is refused, so a crafted subscription can't make the server
    // send requests to an arbitrary host.
    private static readonly string[] PushHosts = ["fcm.googleapis.com", "push.services.mozilla.com", "notify.windows.com", "push.apple.com"];

    public static bool IsPushEndpoint(string? endpoint) =>
        endpoint is { Length: <= Limits.PushEndpoint }
        && Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && uri.IsDefaultPort
        && PushHosts.Any(host => uri.Host == host || uri.Host.EndsWith("." + host, StringComparison.Ordinal));

    // The browser's subscription keys, or null when they are not a P-256 public key and a 16-byte secret.
    public static (byte[] Public, byte[] Auth)? ReadKeys(string? p256dh, string? auth)
    {
        try
        {
            var publicKey = Base64Url.DecodeFromChars(p256dh);
            var secret = Base64Url.DecodeFromChars(auth);
            return publicKey is [4, ..] && publicKey.Length == 65 && secret.Length == 16 ? (publicKey, secret) : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // Encrypts one message for one subscription. The server key pair and the salt must be new for every message;
    // tests pass fixed ones to compare with the RFC example.
    public static byte[] Encrypt(byte[] plaintext, byte[] uaPublic, byte[] authSecret, ECDiffieHellman serverKey, byte[] salt)
    {
        using var userAgent = ECDiffieHellman.Create(PublicKey(uaPublic));
        var ecdhSecret = serverKey.DeriveRawSecretAgreement(userAgent.PublicKey);
        var asPublic = Export(serverKey);

        var keyInfo = Concat(Encoding.ASCII.GetBytes("WebPush: info\0"), uaPublic, asPublic);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, ecdhSecret, 32, authSecret, keyInfo);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        // One record: the message and the 0x02 delimiter that marks the last record.
        var record = Concat(plaintext, [2]);
        var ciphertext = new byte[record.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(cek, tag.Length)) aes.Encrypt(nonce, record, ciphertext, tag);

        // Header: salt, record size, key id length and the server's public key as the key id.
        var header = new byte[16 + 4 + 1 + asPublic.Length];
        salt.CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(16), RecordSize);
        header[20] = (byte)asPublic.Length;
        asPublic.CopyTo(header, 21);
        return Concat(header, ciphertext, tag);
    }

    // "vapid t=<JWT signed with ES256>, k=<public key>": proves to the push service which server sends the message.
    public static string VapidAuthorization(string endpoint, string subject, ECDsa key, DateTimeOffset expires)
    {
        var audience = new Uri(endpoint).GetLeftPart(UriPartial.Authority);
        var header = Base64Url.EncodeToString("""{"typ":"JWT","alg":"ES256"}"""u8);
        var claims = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(new { aud = audience, exp = expires.ToUnixTimeSeconds(), sub = subject }));
        var signature = Base64Url.EncodeToString(key.SignData(Encoding.ASCII.GetBytes($"{header}.{claims}"), HashAlgorithmName.SHA256));
        return $"vapid t={header}.{claims}.{signature}, k={Base64Url.EncodeToString(Export(key))}";
    }

    public static ECParameters PublicKey(byte[] uncompressed) => new()
    {
        Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint { X = uncompressed[1..33], Y = uncompressed[33..65] },
    };

    public static byte[] Export(ECAlgorithm key)
    {
        var point = key.ExportParameters(false).Q;
        return Concat([4], point.X!, point.Y!);
    }

    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(part => part).ToArray();
}
