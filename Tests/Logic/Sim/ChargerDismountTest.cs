namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// plaguecharger's two-phase sacrifice (roster-expansion-40.md §4): on its FIRST contact the
/// steed is consumed — a venom slow/chip wake (a seeded zone) drops once and the rider
/// dismounts to a slower footman (MoveSpeedOverride = DismountSpeed). Later contacts neither
/// re-seed the wake nor re-dismount.
/// </summary>
[TestSuite]
public class ChargerDismountTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void FirstContactSeedsOneWakeAndDismounts()
    {
        var charger = TestUnits.Grunt("charger") with
        {
            MoveSpeed = 3f, Range = 1f, Damage = 5, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0, DismountSpeed = 0.5f,
            ZoneRadius = 2f, ZoneSlowPct = 0.3f, ZoneDurationTicks = 600, ZoneDamagePerTick = 1,
        };
        var enemy = TestUnits.Grunt("enemy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { charger, enemy });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "charger"),
            SimCommand.Deploy(PlayerSide.Right, "enemy"),
        });
        var chargerUnit = state.Units.First(u => u.Def.Id == "charger");
        var enemyUnit = state.Units.First(u => u.Def.Id == "enemy");
        chargerUnit.X = 5f;
        enemyUnit.X = 5.8f; // already in melee range — contacts immediately

        AssertThat(chargerUnit.MoveSpeedOverride).IsNull(); // still mounted

        AdvanceTicks(sim, state, 24); // several attack cycles

        AssertThat(chargerUnit.MoveSpeedOverride).IsEqual(0.5f); // dismounted to footman pace
        AssertThat(state.Zones.Count).IsEqual(1);                // exactly one sacrifice wake
    }
}
