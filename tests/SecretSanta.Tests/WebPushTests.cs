using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SecretSanta.Services;

namespace SecretSanta.Tests;

public class WebPushTests
{
    private static byte[] B64(string value) => Base64Url.DecodeFromChars(value.Replace(" ", ""));

    // The example of RFC 8291, Section 5 and Appendix A: the same keys and salt must give the same bytes.
    [Fact]
    public void Encryption_matches_the_rfc_8291_example()
    {
        var asPublic = B64("BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8");
        var serverParameters = WebPush.PublicKey(asPublic);
        serverParameters.D = B64("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw");
        using var serverKey = ECDiffieHellman.Create(serverParameters);

        var body = WebPush.Encrypt(
            B64("V2hlbiBJIGdyb3cgdXAsIEkgd2FudCB0byBiZSBhIHdhdGVybWVsb24"),
            B64("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4"),
            B64("BTBZMqHH6r4Tts7J_aSIgg"),
            serverKey,
            B64("DGv6ra1nlYgDCS1FRnbzlw"));

        var header = "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z 9KsN6nGRTbVYI_c7VJSPQTBtkgcy27ml mlMoZIIgDll6e3vCYLocInmYWAmS6Tlz AC8wEqKK6PBru3jl7A8";
        var ciphertext = "8pfeW0KbunFT06SuDKoJH9Ql87S1QUrd irN6GcG7sFz1y1sqLgVi1VhjVkHsUoEs bI_0LpXMuGvnzQ";
        Assert.Equal(B64(header).Concat(B64(ciphertext)).ToArray(), body);
    }

    [Fact]
    public void Vapid_header_is_a_jwt_signed_by_the_given_key()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var expires = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        var value = WebPush.VapidAuthorization("https://fcm.googleapis.com/fcm/send/abc", "https://santa.example/", key, expires);

        var parts = value.Split(", ");
        Assert.StartsWith("vapid t=", parts[0]);
        Assert.Equal("k=" + Base64Url.EncodeToString(WebPush.Export(key)), parts[1]);
        var jwt = parts[0]["vapid t=".Length..].Split('.');
        Assert.True(key.VerifyData(Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), Base64Url.DecodeFromChars(jwt[2]), HashAlgorithmName.SHA256));
        using var claims = JsonDocument.Parse(Base64Url.DecodeFromChars(jwt[1]));
        Assert.Equal("https://fcm.googleapis.com", claims.RootElement.GetProperty("aud").GetString());
        Assert.Equal(1_800_000_000, claims.RootElement.GetProperty("exp").GetInt64());
        Assert.Equal("https://santa.example/", claims.RootElement.GetProperty("sub").GetString());
    }

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc:def", true)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/gAAAA", true)]
    [InlineData("https://wns2-par02p.notify.windows.com/w/?token=BQYAAA", true)]
    [InlineData("https://web.push.apple.com/QGuQyavXutnMH", true)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com:8443/fcm/send/abc", false)]
    [InlineData("https://fcm.googleapis.com.evil.example/x", false)]
    [InlineData("https://evil.example/fcm.googleapis.com", false)]
    [InlineData("https://localhost/push", false)]
    [InlineData("not a url", false)]
    [InlineData(null, false)]
    public void Only_browser_push_services_are_accepted(string? endpoint, bool accepted)
    {
        Assert.Equal(accepted, WebPush.IsPushEndpoint(endpoint));
    }

    [Fact]
    public void Subscription_keys_must_be_a_p256_point_and_a_16_byte_secret()
    {
        using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var p256dh = Base64Url.EncodeToString(WebPush.Export(key));
        var auth = Base64Url.EncodeToString(new byte[16]);

        Assert.NotNull(WebPush.ReadKeys(p256dh, auth));
        Assert.Null(WebPush.ReadKeys(p256dh, Base64Url.EncodeToString(new byte[8])));
        Assert.Null(WebPush.ReadKeys(Base64Url.EncodeToString(new byte[65]), auth));
        Assert.Null(WebPush.ReadKeys("not base64 at all!", auth));
        Assert.Null(WebPush.ReadKeys(null, null));
    }
}
