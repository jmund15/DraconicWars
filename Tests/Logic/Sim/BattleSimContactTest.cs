namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// SimUnit.ContactTriggered (Attack-Form View Spawn §6): a one-tick pulse the sim
/// raises at the foreswing-end damage moment so the view layer can spawn a cosmetic
/// element form. Additive — it does NOT change hitscan resolution (the sim's combat
/// math is unchanged); it only surfaces the existing contact timing for the view.
/// </summary>
[TestSuite]
public class BattleSimContactTest
{
    private static UnitDef Def(string id) => UnitCatalog.FullRoster.First(u => u.Id == id);

    private static (BattleSim sim, BattleState state) Battle(params UnitDef[] defs)
    {
        var sim = new BattleSim(BattleConfig.Default with { EdictsPerTier = 0 }, defs);
        var state = sim.CreateInitialState(7UL);
        state.Left.Mana = 5000f;
        state.Left.WalletCap = 50000f;
        state.Right.Mana = 5000f;
        state.Right.WalletCap = 50000f;
        return (sim, state);
    }

    [TestCase]
    public void ContactTriggered_PulsesOnContactTick_ThenClears()
    {
        var atk = Def("kobold_spearman");
        var tgt = Def("stone_warden");           // 220 HP — survives many hits, so many pulses
        var (sim, state) = Battle(atk, tgt);
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, atk.Id) });
        var attacker = state.Units[^1];
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Right, tgt.Id) });
        var target = state.Units[^1];
        attacker.X = 10f;                          // place them inside melee band
        target.X = 10.5f;

        var sawContact = false;
        var prev = false;
        for (var t = 0; t < 120 && attacker.IsAlive && target.IsAlive; t++)
        {
            sim.Advance(state, new List<SimCommand>());
            if (attacker.ContactTriggered)
            {
                sawContact = true;
                AssertThat(prev)
                    .OverrideFailureMessage("ContactTriggered must be a single-tick pulse, not sustained")
                    .IsFalse();
            }
            prev = attacker.ContactTriggered;
        }

        AssertThat(sawContact)
            .OverrideFailureMessage("attacker never reached an attack contact within 120 ticks")
            .IsTrue();
    }

    [TestCase]
    public void ContactTriggered_FalseWhenNoTargetInBand()
    {
        var atk = Def("kobold_spearman");
        var (sim, state) = Battle(atk);
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, atk.Id) });
        var u = state.Units[^1];
        for (var t = 0; t < 20; t++)
        {
            sim.Advance(state, new List<SimCommand>());
            AssertThat(u.ContactTriggered).IsFalse();  // no enemy → never contacts
        }
    }
}
