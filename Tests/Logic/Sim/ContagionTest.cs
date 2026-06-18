namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Sythraal's contagion (roster-expansion-40.md §4): its hits poison enemies; when a
/// POISONED enemy dies it spreads the poison to fresh neighbours (deferred chain), so the
/// plague reaches units the dragon never touched. ContagionRadius 0 = an ordinary poison DoT.
/// </summary>
[TestSuite]
public class ContagionTest
{
    private static int BystanderDamage(float contagionRadius)
    {
        var poisoner = TestUnits.Grunt("poisoner") with
        {
            MoveSpeed = 0f, Range = 1f, Damage = 1, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0,
            PoisonOnHitTicks = 60, PoisonDamagePerTick = 5,
            ContagionRadius = contagionRadius, ContagionDepthBonus = 0.5f,
        };
        // 'victim' dies fast from poison; 'bystander' sits outside the poisoner's reach but
        // within contagion range of the victim — only the death-spread can touch it.
        var victim = TestUnits.Grunt("victim") with { MoveSpeed = 0f, MaxHp = 30, KnockbackCount = 0 };
        var bystander = TestUnits.Grunt("bystander") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { poisoner, victim, bystander });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "poisoner"),
            SimCommand.Deploy(PlayerSide.Right, "victim"),
            SimCommand.Deploy(PlayerSide.Right, "bystander"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 6f;   // in the poisoner's melee range
        state.Units[2].X = 7.5f; // dist 2.5 from poisoner (outside Range 1); near the victim
        var bystanderUnit = state.Units.First(u => u.Def.Id == "bystander");
        for (var i = 0; i < 120; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
        return bystanderUnit.Def.MaxHp - bystanderUnit.Hp;
    }

    [TestCase]
    public void PoisonSpreadsToUntouchedNeighbourOnDeath()
    {
        AssertThat(BystanderDamage(contagionRadius: 3f) > 0).IsTrue();
    }

    [TestCase]
    public void WithoutContagionTheBystanderIsUntouched()
    {
        AssertThat(BystanderDamage(contagionRadius: 0f)).IsEqual(0);
    }
}
