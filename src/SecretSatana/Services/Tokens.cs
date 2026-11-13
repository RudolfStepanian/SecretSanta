using System.Buffers.Text;
using System.Security.Cryptography;

namespace SecretSatana.Services;

public static class Tokens
{
    public static string New() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));
}
