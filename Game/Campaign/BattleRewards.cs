namespace DraconicWars.Game.Campaign;

using System;

/// <summary>
/// Battle gold payout (design-meta.md §1): wins pay full; losses pay 40-60%,
/// scaling with battle duration and damage dealt to the enemy spire — a deep loss
/// is never a zero (anti-dark-pattern charter).
/// </summary>
public static class BattleRewards
{
    public static int ComputeGold(
        int baseGold, bool won, int battleTicks, int hardEndTick, float enemySpireDamagePct)
    {
        if (won)
        {
            return baseGold;
        }

        var duration = Math.Clamp((float)battleTicks / hardEndTick, 0f, 1f);
        var damage = Math.Clamp(enemySpireDamagePct, 0f, 1f);
        var fraction = 0.4f + 0.2f * (0.5f * duration + 0.5f * damage);
        return (int)MathF.Round(baseGold * fraction);
    }
}
