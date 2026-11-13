using SecretSanta.Localization;
using SecretSanta.Services;

namespace SecretSanta.Tests;

public class DrawTests
{
    [Fact]
    public void Too_few_participants_returns_null()
    {
        Assert.Null(Draw.Assign([1, 2], [], new Random(1)));
    }

    [Fact]
    public void Impossible_exclusions_return_null()
    {
        Assert.Null(Draw.Assign([1, 2, 3], [(1, 2)], new Random(1)));
    }

    [Fact]
    public void Excluded_from_everyone_returns_null()
    {
        Assert.Null(Draw.Assign([1, 2, 3, 4, 5], [(1, 2), (1, 3), (1, 4), (1, 5)], new Random(1)));
    }

    [Theory]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(25, 10)]
    [InlineData(60, 40)]
    public void Produces_single_cycle_respecting_exclusions(int count, int exclusionCount)
    {
        for (var seed = 0; seed < 200; seed++)
        {
            var rng = new Random(seed);
            var ids = Enumerable.Range(100, count).ToArray();
            var exclusions = new List<(int, int)>();
            while (exclusions.Count < exclusionCount)
            {
                var a = ids[rng.Next(count)];
                var b = ids[rng.Next(count)];
                if (a != b) exclusions.Add((a, b));
            }

            var result = Draw.Assign(ids, exclusions, new Random(seed));

            Assert.NotNull(result);
            Assert.Equal(ids.OrderBy(x => x), result.Keys.OrderBy(x => x));
            Assert.Equal(ids.OrderBy(x => x), result.Values.OrderBy(x => x));
            Assert.All(result, kv => Assert.NotEqual(kv.Key, kv.Value));
            foreach (var (a, b) in exclusions)
            {
                Assert.NotEqual(b, result[a]);
                Assert.NotEqual(a, result[b]);
            }

            var current = ids[0];
            for (var i = 1; i < count; i++)
            {
                current = result[current];
                Assert.NotEqual(ids[0], current);
            }
            Assert.Equal(ids[0], result[current]);
        }
    }

    [Fact]
    public void Draw_is_random()
    {
        var ids = Enumerable.Range(1, 10).ToArray();
        var outcomes = Enumerable.Range(0, 50).Select(s => Draw.Assign(ids, [], new Random(s))![1]).Distinct().Count();
        Assert.True(outcomes > 3);
    }
}

public class StringsTests
{
    [Fact]
    public void Every_string_has_all_languages()
    {
        Assert.All(Strings.All, kv =>
        {
            Assert.Equal(Languages.All.Length, kv.Value.Length);
            Assert.All(kv.Value, v => Assert.False(string.IsNullOrWhiteSpace(v), kv.Key));
        });
    }
}
