namespace DraconicWars.Sim.Augments;

using System.Collections.Generic;
using DraconicWars.Sim.Core;

/// <summary>
/// The pre-rolled battle tier sequence (design.md §9): rolled once per battle from a
/// published weighted table; every sequence carries at least one Gold+; in PvP both
/// sides share the rolled path.
/// </summary>
public static class TierPath
{
    public readonly record struct WeightedSequence(float Weight, AugmentTier[] Tiers);

    private const AugmentTier S = AugmentTier.Silver;
    private const AugmentTier G = AugmentTier.Gold;
    private const AugmentTier P = AugmentTier.Prismatic;

    public static readonly IReadOnlyList<WeightedSequence> WeightedSequences = new WeightedSequence[]
    {
        new(0.25f, new[] { S, S, G }),
        new(0.15f, new[] { S, G, G }),
        new(0.12f, new[] { G, S, G }),
        new(0.10f, new[] { G, G, G }),
        new(0.08f, new[] { S, S, P }),
        new(0.07f, new[] { S, G, P }),
        new(0.07f, new[] { G, G, P }),
        new(0.05f, new[] { S, P, G }),
        new(0.04f, new[] { G, P, G }),
        new(0.03f, new[] { P, S, G }),
        new(0.02f, new[] { P, G, G }),
        new(0.02f, new[] { P, P, P }),
    };

    public static AugmentTier[] Roll(SimRng rng)
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
