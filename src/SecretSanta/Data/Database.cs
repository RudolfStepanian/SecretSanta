using Npgsql;

namespace SecretSanta.Data;

public static class Database
{
    // DATABASE_URL (postgres://user:pass@host:port/db?sslmode=require, as Render and Neon give it) wins over ConnectionStrings:Default.
    public static string ConnectionString(IConfiguration config)
    {
        if (config["DATABASE_URL"] is { Length: > 0 } url) return FromUrl(url);
        return config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Set DATABASE_URL or ConnectionStrings:Default.");
    }

    public static string FromUrl(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var cs = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";
            switch (kv[0].ToLowerInvariant())
            {
                case "sslmode": cs.SslMode = Enum.Parse<SslMode>(value.Replace("-", ""), ignoreCase: true); break;
                case "channel_binding": cs.ChannelBinding = Enum.Parse<ChannelBinding>(value, ignoreCase: true); break;
            }
        }

        return cs.ConnectionString;
    }
}
