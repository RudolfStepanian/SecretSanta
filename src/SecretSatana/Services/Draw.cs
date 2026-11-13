namespace SecretSatana.Services;

public static class Draw
{
    public const int MinParticipants = 3;
    private const int StepLimit = 1_000_000;

    // Builds one random cycle through everyone: no self-gifts, no mutual pairs, exclusions respected both ways.
    // Returns giver -> recipient, or null when no such cycle exists (or none was found within the step limit).
    public static Dictionary<int, int>? Assign(IEnumerable<int> ids, IEnumerable<(int A, int B)> exclusions, Random rng)
    {
        var pool = ids.Distinct().OrderBy(_ => rng.Next()).ToArray();
        var n = pool.Length;
        if (n < MinParticipants) return null;

        var banned = new HashSet<(int, int)>();
        foreach (var (a, b) in exclusions)
        {
            banned.Add((a, b));
            banned.Add((b, a));
        }

        bool Allowed(int giver, int recipient) => giver != recipient && !banned.Contains((giver, recipient));

        var path = new List<int>(n) { pool[0] };
        var used = new HashSet<int> { pool[0] };
        var steps = 0;

        bool Extend()
        {
            if (++steps > StepLimit) return false;
            var last = path[^1];
            if (path.Count == n) return Allowed(last, path[0]);

            var candidates = pool.Where(x => !used.Contains(x) && Allowed(last, x)).OrderBy(_ => rng.Next()).ToArray();
            foreach (var next in candidates)
            {
                path.Add(next);
                used.Add(next);
                if (Extend()) return true;
                path.RemoveAt(path.Count - 1);
                used.Remove(next);
            }
            return false;
        }

        if (!Extend()) return null;

        var result = new Dictionary<int, int>(n);
        for (var i = 0; i < n; i++) result[path[i]] = path[(i + 1) % n];
        return result;
    }
}
