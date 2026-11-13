using Npgsql;
using SecretSanta.Data;

namespace SecretSanta.Tests;

public class DatabaseTests
{
    [Fact]
    public void Neon_url_becomes_npgsql_connection_string()
    {
        var cs = new NpgsqlConnectionStringBuilder(Database.FromUrl(
            "postgresql://santa:p%40ss@ep-cool-1.eu-central-1.aws.neon.tech/neondb?sslmode=require&channel_binding=require"));

        Assert.Equal("ep-cool-1.eu-central-1.aws.neon.tech", cs.Host);
        Assert.Equal(5432, cs.Port);
        Assert.Equal("neondb", cs.Database);
        Assert.Equal("santa", cs.Username);
        Assert.Equal("p@ss", cs.Password);
        Assert.Equal(SslMode.Require, cs.SslMode);
        Assert.Equal(ChannelBinding.Require, cs.ChannelBinding);
    }

    [Fact]
    public void Render_url_keeps_explicit_port()
    {
        var cs = new NpgsqlConnectionStringBuilder(Database.FromUrl("postgres://u:p@dpg-x-a.oregon-postgres.render.com:6543/santa"));

        Assert.Equal(6543, cs.Port);
        Assert.Equal("santa", cs.Database);
    }
}
