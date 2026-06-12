namespace DraconicWars.Sim.Edicts;

using DraconicWars.Sim.Battle;

/// <summary>A rolled trial's claim state. TierIndex 0/1/2 = the segment toward
/// Tiers II/III/IV whose threshold gap sizes the surge.</summary>
public sealed class ActiveEdict
{
    public required EdictDef Def { get; init; }

    public required int TierIndex { get; init; }

    public bool LeftClaimed { get; set; }

    public bool RightClaimed { get; set; }

    public PlayerSide? FirstClaimant { get; set; }

    public bool ClaimedBy(PlayerSide side)
    {
        return side == PlayerSide.Left ? LeftClaimed : RightClaimed;
    }

    public void MarkClaimed(PlayerSide side)
    {
        if (side == PlayerSide.Left)
        {
            LeftClaimed = true;
        }
        else
        {
            RightClaimed = true;
        }
        FirstClaimant ??= side;
    }
}
