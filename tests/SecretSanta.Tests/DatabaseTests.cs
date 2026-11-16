using Microsoft.Extensions.Configuration;
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

    [Theory]
    [InlineData("DATABASE_URL", "postgres://u:p@db.example/santa?sslmode=require")]
    [InlineData("ConnectionStrings:Default", "Host=db.example;Database=santa;Username=u;Password=p")]
    public void Gss_encryption_is_off_whatever_the_source(string key, string value)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection([new(key, value)]).Build();

        var cs = new NpgsqlConnectionStringBuilder(Database.ConnectionString(config));

        Assert.Equal(GssEncryptionMode.Disable, cs.GssEncryptionMode);
        Assert.Equal("db.example", cs.Host);
        Assert.Equal("santa", cs.Database);
    }
}
