namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class SynergyTest
{
    private static UnitDef Fire(string id) => TestUnits.Grunt(id) with { Element = Element.Fire };

    private static UnitDef Frost(string id) => TestUnits.Grunt(id) with { Element = Element.Frost };

    private static UnitDef Stone(string id) => TestUnits.Grunt(id) with { Element = Element.Stone };

    private static (BattleSim sim, BattleState state) CreateBattle(params UnitDef[] defs)
    {
        var sim = new BattleSim(BattleConfig.Default, defs);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 50000f;
        state.Left.WalletCap = 50000f;
        state.Right.Mana = 50000f;
        state.Right.WalletCap = 50000f;
        return (sim, state);
    }

    private static SimUnit Spawn(
        BattleSim sim, BattleState state, PlayerSide side, string defId, float x)
    {
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(side, defId) });
        var unit = state.Units[^1];
        unit.X = x;
        return unit;
    }

    [TestCase]
    public void DistinctTypesCountNotCopies()
    {
        var (sim, state) = CreateBattle(Fire("f1"), Fire("f2"));
        Spawn(sim, state, PlayerSide.Left, "f1", 5f);
        Spawn(sim, state, PlayerSide.Left, "f1", 5.2f);
        sim.Advance(state, SimCommand.None);
        AssertThat(ElementSynergies.TierFor(state, PlayerSide.Left, Element.Fire)).IsEqual(0);

        Spawn(sim, state, PlayerSide.Left, "f2", 5.4f);
        sim.Advance(state, SimCommand.None);
        AssertThat(ElementSynergies.TierFor(state, PlayerSide.Left, Element.Fire)).IsEqual(1);
    }

    [TestCase]
    public void FireTierTwoBoostsFireUnitDamage()
    {
        var pillar = TestUnits.Grunt(id: "pillar", moveSpeed: 0f);
        var (sim, state) = CreateBattle(Fire("f1"), Fire("f2"), pillar);
        var attacker = Spawn(sim, state, PlayerSide.Left, "f1", 10f);
        Spawn(sim, state, PlayerSide.Left, "f2", 2f);
        var defender = Spawn(sim, state, PlayerSide.Right, "pillar", 10.5f);
        attacker.X = 10f;

        AdvanceTicks(sim, state, Fire("f1").ForeswingTicks + 1);

        // Two distinct Fire types fielded -> tier 1 (threshold 2): +10% damage. 10 -> 11.
        AssertThat(defender.Hp).IsEqual(defender.Def.MaxHp - 11);
    }

    [TestCase]
    public void StoneTierTwoReducesIncomingDamage()
    {
        var biter = TestUnits.FastBiter();
        var (sim, state) = CreateBattle(Stone("s1"), Stone("s2"), biter);
        var defender = Spawn(sim, state, PlayerSide.Left, "s1", 10f);
        Spawn(sim, state, PlayerSide.Left, "s2", 2f);
        var attacker = Spawn(sim, state, PlayerSide.Right, "biter", 10.5f);
        defender.X = 10f;

        AdvanceTicks(sim, state, biter.ForeswingTicks + 1);

        // Stone tier 1: 10% damage reduction. 40 -> 36.
        AssertThat(defender.Hp).IsEqual(defender.Def.MaxHp - 36);
    }

    [TestCase]
    public void FrostHitsApplySlowThatDecays()
    {
        var (sim, state) = CreateBattle(
            Frost("fr1"), Frost("fr2"), TestUnits.Grunt(id: "runner"));
        var attacker = Spawn(sim, state, PlayerSide.Left, "fr1", 10f);
        Spawn(sim, state, PlayerSide.Left, "fr2", 2f);
        var victim = Spawn(sim, state, PlayerSide.Right, "runner", 10.5f);
        attacker.X = 10f;

        AdvanceTicks(sim, state, Frost("fr1").ForeswingTicks + 1);

        AssertThat(victim.SlowTicks > 0).IsTrue();
        AssertThat(victim.SlowPct).IsEqualApprox(0.15f, 0.001f);

        // Separate the pair so no further hit refreshes the slow during decay.
        victim.X = 30f;
        attacker.X = 2f;
        AdvanceTicks(sim, state, 40);
        AssertThat(victim.SlowTicks).IsEqual(0);
    }

    [TestCase]
    public void VenomTierExecutesLowHealthTargets()
    {
        var venomA = TestUnits.Grunt("v1") with { Element = Element.Venom };
        var venomB = TestUnits.Grunt("v2") with { Element = Element.Venom };
        var pillar = TestUnits.Grunt(id: "pillar", moveSpeed: 0f);
        var (sim, state) = CreateBattle(venomA, venomB, pillar);
        var attacker = Spawn(sim, state, PlayerSide.Left, "v1", 10f);
        Spawn(sim, state, PlayerSide.Left, "v2", 2f);
        var defender = Spawn(sim, state, PlayerSide.Right, "pillar", 10.5f);
        attacker.X = 10f;
        defender.Hp = 40;

        AdvanceTicks(sim, state, venomA.ForeswingTicks + 1);

        // Venom tier 1 vs target below 50%: +10% damage. 10 -> 11.
        AssertThat(defender.Hp).IsEqual(40 - 11);
    }

    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }
}
