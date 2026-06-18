namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Glacereth's freeze fields (roster-expansion-40.md §4): a seeded lane zone with
/// ZoneFreezeDwellTicks freezes (StunTicks) any enemy that has dwelt in / overlapped frost
/// fields for that many accumulated ticks. Plain slow zones (no freeze kit) never stun.
/// </summary>
[TestSuite]
public class FreezeZoneTest
{
    private static SimUnit RunSeeder(bool withFreeze)
    {
        var seeder = TestUnits.Grunt("seeder") with
        {
            MoveSpeed = 0f, Range = 2f, Damage = 1, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0,
            ZoneRadius = 3f, ZoneSlowPct = 0.3f, ZoneDurationTicks = 1000, ZoneDamagePerTick = 0,
            ZoneFreezeDwellTicks = withFreeze ? 10 : 0, ZoneFreezeTicks = withFreeze ? 30 : 0,
        };
        var enemy = TestUnits.Grunt("enemy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { seeder, enemy });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "seeder"),
            SimCommand.Deploy(PlayerSide.Right, "enemy"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 6f; // in the seeder's melee range; sits in the seeded frost field
        var enemyUnit = state.Units.First(u => u.Def.Id == "enemy");
        for (var i = 0; i < 40; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
        return enemyUnit;
    }

    [TestCase]
    public void EnemyDwellingInFrostFieldsFreezes()
    {
        AssertThat(RunSeeder(withFreeze: true).StunTicks > 0).IsTrue();
    }

    [TestCase]
    public void PlainSlowZoneNeverFreezes()
    {
        AssertThat(RunSeeder(withFreeze: false).StunTicks).IsEqual(0);
    }
}
