using System.Buffers.Text;
using System.Security.Cryptography;
using SecretSanta.Data;

namespace SecretSanta.Services;

// The server's VAPID key pair for push notifications. It is created on the first start and kept in the database,
// so browsers' subscriptions stay valid across restarts and deploys.
public sealed class VapidKeys
{
    private const string PublicName = "VapidPublicKey";
    private const string PrivateName = "VapidPrivateKey";

    private readonly byte[] privateKey;

    private VapidKeys(string publicKey, byte[] privateKey)
    {
        PublicKey = publicKey;
        this.privateKey = privateKey;
    }

    // Base64url of the 65-byte public key: the applicationServerKey for the browser.
    public string PublicKey { get; }

    public static VapidKeys LoadOrCreate(AppDb db)
    {
        var saved = db.Settings.Where(s => s.Key == PublicName || s.Key == PrivateName).ToDictionary(s => s.Key, s => s.Value);
        if (saved.TryGetValue(PublicName, out var publicKey) && saved.TryGetValue(PrivateName, out var privateKey))
            return new VapidKeys(publicKey, Base64Url.DecodeFromChars(privateKey));

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var d = key.ExportParameters(true).D!;
        var created = new VapidKeys(Base64Url.EncodeToString(WebPush.Export(key)), d);
        db.Settings.AddRange(
            new AppSetting { Key = PublicName, Value = created.PublicKey },
            new AppSetting { Key = PrivateName, Value = Base64Url.EncodeToString(d) });
        db.SaveChanges();
        return created;
    }

    public ECDsa CreateSigner()
    {
        var parameters = WebPush.PublicKey(Base64Url.DecodeFromChars(PublicKey));
        parameters.D = privateKey;
        return ECDsa.Create(parameters);
    }
}
