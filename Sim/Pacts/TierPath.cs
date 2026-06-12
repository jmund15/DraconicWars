namespace DraconicWars.Sim.Pacts;

using System.Collections.Generic;
using DraconicWars.Sim.Core;

/// <summary>
/// The pre-rolled battle tier sequence (design.md §9): rolled once per battle from a
/// published weighted table; every sequence carries at least one Drake-or-better term;
/// in PvP both sides share the rolled path.
/// </summary>
public static class TierPath
{
    public readonly record struct WeightedSequence(float Weight, PactTier[] Tiers);

    private const PactTier E = PactTier.Ember;
    private const PactTier D = PactTier.Drake;
    private const PactTier W = PactTier.Wyrm;

    public static readonly IReadOnlyList<WeightedSequence> WeightedSequences = new WeightedSequence[]
    {
        new(0.25f, new[] { E, E, D }),
        new(0.15f, new[] { E, D, D }),
        new(0.12f, new[] { D, E, D }),
        new(0.10f, new[] { D, D, D }),
        new(0.08f, new[] { E, E, W }),
        new(0.07f, new[] { E, D, W }),
        new(0.07f, new[] { D, D, W }),
        new(0.05f, new[] { E, W, D }),
        new(0.04f, new[] { D, W, D }),
        new(0.03f, new[] { W, E, D }),
        new(0.02f, new[] { W, D, D }),
        new(0.02f, new[] { W, W, W }),
    };

    public static PactTier[] Roll(SimRng rng)
    {
        var roll = rng.NextFloat();
        var cumulative = 0f;
        foreach (var sequence in WeightedSequences)
        {
            cumulative += sequence.Weight;
            if (roll < cumulative)
            {
                return sequence.Tiers;
            }
        }
        return WeightedSequences[^1].Tiers;
    }
}
