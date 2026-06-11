namespace DraconicWars.Sim.Core;

using System;

/// <summary>
/// Deterministic xorshift128+ PRNG for the battle sim. Pure C# by design — the sim
/// must stay engine-free (JmoRng requires the Godot runtime), and the sequence must
/// be contract-stable across engine versions for replays and mirrored-pool fairness.
/// </summary>
public sealed class SimRng
{
    private readonly ulong _seed;
    private ulong _s0;
    private ulong _s1;

    public SimRng(ulong seed)
    {
        _seed = seed;
        var state = seed;
        _s0 = SplitMix64(ref state);
        _s1 = SplitMix64(ref state);
        if (_s0 == 0UL && _s1 == 0UL)
        {
            _s1 = 0x9E3779B97F4A7C15UL;
        }
    }

    public ulong NextULong()
    {
        var x = _s0;
        var y = _s1;
        _s0 = y;
        x ^= x << 23;
        _s1 = x ^ y ^ (x >> 17) ^ (y >> 26);
        return _s1 + y;
    }

    public float NextFloat()
    {
        return (NextULong() >> 40) * (1.0f / (1 << 24));
    }

    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "maxExclusive must be positive");
        }
        return (int)(NextULong() % (ulong)maxExclusive);
    }

    /// <summary>
    /// Derives an independent stream keyed by name from the ORIGINAL seed — consuming
    /// the parent never shifts a child, so subsystems can replay independently.
    /// </summary>
    public SimRng DeriveChild(string streamName)
    {
        return new SimRng(_seed ^ Fnv1A64(streamName));
    }

    private static ulong SplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        var z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong Fnv1A64(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }
}
