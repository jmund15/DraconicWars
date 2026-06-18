namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Terravossk's shockwave discharge (roster-expansion-40.md §4): when its Vigil DR-ramp
/// reaches the cap, the stored charge releases as a lane shockwave — damaging every enemy
/// within ShockwaveRange — and the ramp resets to charge again ("the wall hits back").
/// </summary>
[TestSuite]
public class ShockwaveDischargeTest
{
    [TestCase]
    public void VigilRampDischargesAShockwaveAtCapHittingBystanders()
    {
        var terra = TestUnits.Grunt("terra") with
        {
            MoveSpeed = 0f, Range = 1f, Damage = 1, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0, Unstaggerable = true,
            VigilDrPerSecond = 0.1f, VigilDrMaxPct = 0.4f, // cap at 120 ticks of engagement
            ShockwaveDamage = 50, ShockwaveRange = 10f,
        };
        // Melee target keeps terra in an attack phase (so Vigil accrues); high HP so it lives.
        var meleeTarget = TestUnits.Grunt("melee") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        // Bystander is OUTSIDE melee range but INSIDE shockwave range — only the shockwave reaches it.
        var bystander = TestUnits.Grunt("bystander") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { terra, meleeTarget, bystander });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "terra"),
            SimCommand.Deploy(PlayerSide.Right, "melee"),
            SimCommand.Deploy(PlayerSide.Right, "bystander"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 5.5f; // in melee range
        state.Units[2].X = 12f;  // 7 away — outside melee Range(1), inside ShockwaveRange(10)

        var bystanderUnit = state.Units.First(u => u.Def.Id == "bystander");
        for (var i = 0; i < 200; i++)
        {
            sim.Advance(state, SimCommand.None);
        }

        // The bystander could only have been hit by the lane shockwave at Vigil cap.
        AssertThat(bystanderUnit.Hp < bystanderUnit.Def.MaxHp).IsTrue();
    }

    [TestCase]
    public void ShockwaveSparesEnemiesBeyondItsRange()
    {
        var terra = TestUnits.Grunt("terra") with
        {
            MoveSpeed = 0f, Range = 1f, Damage = 1, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0, Unstaggerable = true,
            VigilDrPerSecond = 0.1f, VigilDrMaxPct = 0.4f,
            ShockwaveDamage = 50, ShockwaveRange = 6f,
        };
        var meleeTarget = TestUnits.Grunt("melee") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var farEnemy = TestUnits.Grunt("far") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { terra, meleeTarget, farEnemy });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "terra"),
            SimCommand.Deploy(PlayerSide.Right, "melee"),
            SimCommand.Deploy(PlayerSide.Right, "far"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 5.5f;  // in melee range, keeps terra engaged
        state.Units[2].X = 5f + 6f + 2f; // beyond ShockwaveRange (6)
        var farUnit = state.Units.First(u => u.Def.Id == "far");

        for (var i = 0; i < 200; i++)
        {
            sim.Advance(state, SimCommand.None);
        }

        AssertThat(farUnit.Hp).IsEqual(farUnit.Def.MaxHp); // the range gate spared it
    }

    [TestCase]
    public void NoShockwaveWithoutTheKit()
    {
        var plain = TestUnits.Grunt("plain") with
        {
            MoveSpeed = 0f, Range = 1f, Damage = 1, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0, Unstaggerable = true,
            VigilDrPerSecond = 0.1f, VigilDrMaxPct = 0.4f, // ramps DR but no shockwave fields
        };
        var meleeTarget = TestUnits.Grunt("melee") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var bystander = TestUnits.Grunt("bystander") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { plain, meleeTarget, bystander });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "plain"),
            SimCommand.Deploy(PlayerSide.Right, "melee"),
            SimCommand.Deploy(PlayerSide.Right, "bystander"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 5.5f;
        state.Units[2].X = 12f;

        var bystanderUnit = state.Units.First(u => u.Def.Id == "bystander");
        for (var i = 0; i < 200; i++)
        {
            sim.Advance(state, SimCommand.None);
        }

        AssertThat(bystanderUnit.Hp).IsEqual(bystanderUnit.Def.MaxHp);
    }
}
