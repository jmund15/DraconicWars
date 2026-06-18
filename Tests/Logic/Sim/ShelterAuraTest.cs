namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// frostbarge_cloudwhale's shelter aura (roster-expansion-40.md §4): allies in its lee take
/// reduced damage (ShelterDrPct) and regenerate (ShelterRegenPerTick) within ShelterRadius.
/// A pure enabler — zero offense.
/// </summary>
[TestSuite]
public class ShelterAuraTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    private static int AllyDamageTaken(bool withShelter)
    {
        var ally = TestUnits.Grunt("ally") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var attacker = TestUnits.Grunt("attacker") with
        {
            MoveSpeed = 0f, Range = 2f, Damage = 40, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0,
        };
        var shelter = TestUnits.Grunt("shelter") with
        {
            MoveSpeed = 0f, Damage = 0, DeployCost = 0, CanTargetGround = false, CanTargetAir = false,
            ShelterDrPct = 0.5f, ShelterRadius = 6f,
        };
        var defs = withShelter ? new[] { ally, attacker, shelter } : new[] { ally, attacker };
        var sim = new BattleSim(BattleConfig.Default, defs);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        var cmds = new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "ally"),
            SimCommand.Deploy(PlayerSide.Right, "attacker"),
        };
        if (withShelter)
        {
            cmds.Add(SimCommand.Deploy(PlayerSide.Left, "shelter"));
        }
        sim.Advance(state, cmds);
        var allyUnit = state.Units.First(u => u.Def.Id == "ally");
        allyUnit.X = 5f;
        state.Units.First(u => u.Def.Id == "attacker").X = 6f;
        if (withShelter)
        {
            state.Units.First(u => u.Def.Id == "shelter").X = 4f;
        }
        AdvanceTicks(sim, state, 60);
        return allyUnit.Def.MaxHp - allyUnit.Hp;
    }

    [TestCase]
    public void ShelterReducesIncomingDamageToAllies()
    {
        var plain = AllyDamageTaken(withShelter: false);
        var sheltered = AllyDamageTaken(withShelter: true);
        AssertThat(sheltered < plain).IsTrue();
    }

    // Same 3-unit structure each time; only the shelter's distance from the ally changes.
    private static int AllyDamageWithShelterAt(float shelterX)
    {
        var ally = TestUnits.Grunt("ally") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var attacker = TestUnits.Grunt("attacker") with
        {
            MoveSpeed = 0f, Range = 2f, Damage = 40, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0,
        };
        var shelter = TestUnits.Grunt("shelter") with
        {
            MoveSpeed = 0f, Damage = 0, DeployCost = 0, CanTargetGround = false, CanTargetAir = false,
            ShelterDrPct = 0.5f, ShelterRadius = 6f,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { ally, attacker, shelter });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "ally"),
            SimCommand.Deploy(PlayerSide.Right, "attacker"),
            SimCommand.Deploy(PlayerSide.Left, "shelter"),
        });
        var allyUnit = state.Units.First(u => u.Def.Id == "ally");
        allyUnit.X = 5f;
        state.Units.First(u => u.Def.Id == "attacker").X = 6f;
        state.Units.First(u => u.Def.Id == "shelter").X = shelterX;
        AdvanceTicks(sim, state, 40);
        return allyUnit.Def.MaxHp - allyUnit.Hp;
    }

    [TestCase]
    public void ShelterDoesNotReachAlliesBeyondRadius()
    {
        var inRange = AllyDamageWithShelterAt(4f);            // dist 1 — within radius 6, DR applies
        var outOfRange = AllyDamageWithShelterAt(5f + 6f + 2f); // dist 8 — beyond radius, no DR
        AssertThat(outOfRange > inRange).IsTrue(); // the distance gate bounds the lee
    }

    [TestCase]
    public void ShelterRegeneratesNearbyAllies()
    {
        var ally = TestUnits.Grunt("ally") with { MoveSpeed = 0f, MaxHp = 1000, KnockbackCount = 0 };
        var shelter = TestUnits.Grunt("shelter") with
        {
            MoveSpeed = 0f, Damage = 0, DeployCost = 0, CanTargetGround = false, CanTargetAir = false,
            ShelterRegenPerTick = 3, ShelterRadius = 6f,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { ally, shelter });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "ally"),
            SimCommand.Deploy(PlayerSide.Left, "shelter"),
        });
        var allyUnit = state.Units.First(u => u.Def.Id == "ally");
        var shelterUnit = state.Units.First(u => u.Def.Id == "shelter");
        allyUnit.X = 5f;
        shelterUnit.X = 4f;
        allyUnit.Hp = 500; // wounded

        AdvanceTicks(sim, state, 30);

        AssertThat(allyUnit.Hp > 500).IsTrue();
    }
}
