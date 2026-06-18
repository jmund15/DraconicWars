namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Element counter mechanism (roster-expansion-40.md §5): per-unit counter declarations —
/// StrongVsElement (x1.5) / MassiveVsElement (x3) offensive, ResistantVsElement (x0.25)
/// defensive — keyed on the defender's EFFECTIVE element, which mossmite's ElementOverride
/// can rewrite so allied counters land. Comparative assertions (no brittle exact-multiplier).
/// </summary>
[TestSuite]
public class CounterMatrixTest
{
    private static int HpDrop(Element? attackerStrongVs, Element defenderElement, bool overrideToFire)
    {
        var attacker = TestUnits.Grunt("attacker") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 100, ForeswingTicks = 2, BackswingTicks = 2,
            StrongVsElement = attackerStrongVs,
        };
        var defender = TestUnits.Grunt("defender") with
        {
            MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0, Element = defenderElement,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { attacker, defender });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        state.Right.Mana = 5000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "attacker"),
            SimCommand.Deploy(PlayerSide.Right, "defender"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 8f;
        if (overrideToFire)
        {
            state.Units[1].ElementOverride = Element.Fire;
        }

        var start = state.Units[1].Hp;
        for (var i = 0; i < 40; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
        return start - state.Units[1].Hp;
    }

    [TestCase]
    public void StrongVsElementDealsBonusToMatchingDefender()
    {
        var plain = HpDrop(attackerStrongVs: null, defenderElement: Element.Fire, overrideToFire: false);
        var strong = HpDrop(attackerStrongVs: Element.Fire, defenderElement: Element.Fire, overrideToFire: false);
        AssertThat(strong > plain).IsTrue();
    }

    [TestCase]
    public void StrongVsDoesNothingToAMismatchedDefender()
    {
        var plain = HpDrop(attackerStrongVs: null, defenderElement: Element.Frost, overrideToFire: false);
        var strongButWrong = HpDrop(attackerStrongVs: Element.Fire, defenderElement: Element.Frost, overrideToFire: false);
        AssertThat(strongButWrong).IsEqual(plain);
    }

    [TestCase]
    public void ElementOverrideMakesCountersLandOnAMismatchedDefender()
    {
        var noOverride = HpDrop(attackerStrongVs: Element.Fire, defenderElement: Element.Frost, overrideToFire: false);
        var withOverride = HpDrop(attackerStrongVs: Element.Fire, defenderElement: Element.Frost, overrideToFire: true);
        AssertThat(withOverride > noOverride).IsTrue();
    }

    // Like HpDrop but exercises Massive (x3) and Resistant (x0.25) instead of only Strong.
    private static int HpDropTuned(Element? strongVs, Element? massiveVs, Element? resistantVs,
        Element attackerElement, Element defenderElement)
    {
        var attacker = TestUnits.Grunt("attacker") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 100, ForeswingTicks = 2, BackswingTicks = 2,
            Element = attackerElement, StrongVsElement = strongVs, MassiveVsElement = massiveVs,
        };
        var defender = TestUnits.Grunt("defender") with
        {
            MoveSpeed = 0f, MaxHp = 1000000, KnockbackCount = 0, Element = defenderElement,
            ResistantVsElement = resistantVs,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { attacker, defender });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        state.Right.Mana = 5000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "attacker"),
            SimCommand.Deploy(PlayerSide.Right, "defender"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 8f;
        var start = state.Units[1].Hp;
        for (var i = 0; i < 40; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
        return start - state.Units[1].Hp;
    }

    [TestCase]
    public void MassiveVsOutdamagesStrongVsOutdamagesPlain()
    {
        var plain = HpDropTuned(null, null, null, Element.Storm, Element.Fire);
        var strong = HpDropTuned(Element.Fire, null, null, Element.Storm, Element.Fire);
        var massive = HpDropTuned(null, Element.Fire, null, Element.Storm, Element.Fire);
        AssertThat(strong > plain).IsTrue();
        AssertThat(massive > strong).IsTrue(); // x3 beats x1.5
    }

    [TestCase]
    public void ResistantVsElementReducesIncomingOfThatElement()
    {
        // A Frost defender resistant to Fire takes less from a Fire attacker than a
        // non-resistant Frost defender does.
        var plain = HpDropTuned(null, null, null, Element.Fire, Element.Frost);
        var resisted = HpDropTuned(null, null, Element.Fire, Element.Fire, Element.Frost);
        AssertThat(resisted < plain).IsTrue();
    }

    [TestCase]
    public void OverrideTargetElementMarksTheDefenderOnContact()
    {
        // mossmite's counter-flip: contact stamps the target's defensive element (no need to
        // set ElementOverride by hand — the kit field applies it through DealDamage).
        var marker = TestUnits.Grunt("marker") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 10, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0, OverrideTargetElement = Element.Fire, OverrideTargetTicks = 60,
        };
        var defender = TestUnits.Grunt("defender") with
        {
            MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0, Element = Element.Frost,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { marker, defender });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        state.Right.Mana = 5000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "marker"),
            SimCommand.Deploy(PlayerSide.Right, "defender"),
        });
        state.Units[0].X = 5f;
        state.Units[1].X = 8f;

        AssertThat(state.Units[1].ElementOverride).IsNull(); // starts on its native element
        for (var i = 0; i < 10; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
        AssertThat(state.Units[1].ElementOverride).IsEqual(Element.Fire);
        AssertThat(state.Units[1].ElementOverrideTicks > 0).IsTrue();
    }
}
