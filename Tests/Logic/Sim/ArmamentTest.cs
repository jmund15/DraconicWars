namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class ArmamentTest
{
    private static (BattleSim sim, BattleState state) CreateBattle()
    {
        var sim = new BattleSim(
            BattleConfig.Default with { EdictsPerTier = 0 },
            new[]
            {
                TestUnits.Grunt(),
                TestUnits.Grunt(id: "pillar", moveSpeed: 0f),
                TestUnits.Grunt(id: "pillar2", moveSpeed: 0f),
                TestUnits.Grunt(id: "pillar3", moveSpeed: 0f),
                TestUnits.Whelp() with { MoveSpeed = 0f },
            },
            ConduitDefs.All);
        var state = sim.CreateInitialState(9UL);
        state.Left.Mana = 50000f;
        state.Left.WalletCap = 50000f;
        state.Right.Mana = 50000f;
        state.Right.WalletCap = 50000f;
        return (sim, state);
    }

    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    [TestCase]
    public void MountingATurretSilencesBreath_SellingRestoresIt()
    {
        var (sim, state) = CreateBattle();
        state.Left.BreathEnergySeconds = 4f;
        var intruder = Spawn(sim, state, PlayerSide.Right, "pillar", 3f);

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "skyward_flak"),
        });
        AssertThat(state.Left.MountedArmamentId).IsEqual("skyward_flak");

        var hpBefore = intruder.Hp;
        for (var i = 0; i < 12; i++)
        {
            sim.Advance(state, new List<SimCommand>
            {
                SimCommand.FireBreath(PlayerSide.Left, intruder.X),
            });
        }
        AssertThat(intruder.Hp).IsEqual(hpBefore);

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.SellConduit(PlayerSide.Left, "skyward_flak"),
        });
        AssertThat(state.Left.MountedArmamentId).IsEqual(null);
        for (var i = 0; i < 12; i++)
        {
            sim.Advance(state, new List<SimCommand>
            {
                SimCommand.FireBreath(PlayerSide.Left, intruder.X),
            });
        }
        AssertThat(intruder.Hp < hpBefore).IsTrue();
    }

    [TestCase]
    public void TheSpireBearsOneCrown_AndArmamentsSkipUtilitySockets()
    {
        var (sim, state) = CreateBattle();

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "skyward_flak"),
            SimCommand.BuildConduit(PlayerSide.Left, "siege_mortar"),
        });
        AssertThat(state.Left.MountedArmamentId).IsEqual("skyward_flak");
        AssertThat(state.Left.Conduits.ContainsKey("siege_mortar")).IsFalse();

        // The Crownmount is its own socket: three utilities still fit beside it.
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "mana_well"),
            SimCommand.BuildConduit(PlayerSide.Left, "war_horn"),
            SimCommand.BuildConduit(PlayerSide.Left, "rampart"),
        });
        AssertThat(state.Left.Conduits.Count).IsEqual(4);
    }

    [TestCase]
    public void FlakFiresOnCadence_AirOnly_Deterministically()
    {
        var (sim, state) = CreateBattle();
        var airTarget = Spawn(sim, state, PlayerSide.Right, "whelp", 6f);
        var groundTarget = Spawn(sim, state, PlayerSide.Right, "pillar", 5f);
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "skyward_flak"),
        });
        var flak = ConduitDefs.ById("skyward_flak");

        AdvanceTicks(sim, state, flak.TurretCadenceTicks + 1);

        AssertThat(airTarget.Hp).IsEqual(airTarget.Def.MaxHp - flak.TurretDamagePerTier);
        AssertThat(airTarget.SlowTicks > 0).IsTrue();
        AssertThat(groundTarget.Hp).IsEqual(groundTarget.Def.MaxHp);
    }

    [TestCase]
    public void MortarRespectsItsArcBandAndSplashes()
    {
        var (sim, state) = CreateBattle();
        // Distinct ids: same-id deploys are rejected by the per-unit cooldown.
        var tooClose = Spawn(sim, state, PlayerSide.Right, "pillar", 2f);
        var farA = Spawn(sim, state, PlayerSide.Right, "pillar2", 12f);
        var farB = Spawn(sim, state, PlayerSide.Right, "pillar3", 12.8f);
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "siege_mortar"),
        });
        var mortar = ConduitDefs.ById("siege_mortar");

        AdvanceTicks(sim, state, mortar.TurretCadenceTicks + 1);

        AssertThat(tooClose.Hp).IsEqual(tooClose.Def.MaxHp);
        AssertThat(farA.Hp).IsEqual(farA.Def.MaxHp - mortar.TurretDamagePerTier);
        AssertThat(farB.Hp).IsEqual(farB.Def.MaxHp - mortar.TurretDamagePerTier);
    }

    [TestCase]
    public void ArmamentCoilQuickensTheCadence()
    {
        var (sim, state) = CreateBattle();
        var airTarget = Spawn(sim, state, PlayerSide.Right, "whelp", 6f);
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "skyward_flak"),
            SimCommand.BuildConduit(PlayerSide.Left, "breath_coil"),
        });
        var flak = ConduitDefs.ById("skyward_flak");
        var coil = ConduitDefs.ById("breath_coil");
        var quickened = (int)(flak.TurretCadenceTicks * (1f - coil.TurretCadencePctPerTier));

        AdvanceTicks(sim, state, quickened + 1);

        AssertThat(airTarget.Hp < airTarget.Def.MaxHp).IsTrue();
    }

    [TestCase]
    public void AFourthSocketCanBeBoughtOnceAtDragonTierGate()
    {
        var (sim, state) = CreateBattle();
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "mana_well"),
            SimCommand.BuildConduit(PlayerSide.Left, "war_horn"),
            SimCommand.BuildConduit(PlayerSide.Left, "rampart"),
        });

        // Gated: tier 1 cannot buy.
        sim.Advance(state, new List<SimCommand> { SimCommand.BuySocket(PlayerSide.Left) });
        AssertThat(state.Left.BonusSockets).IsEqual(0);

        state.Left.AscensionTier = 3;
        var manaBefore = state.Left.Mana;
        sim.Advance(state, new List<SimCommand> { SimCommand.BuySocket(PlayerSide.Left) });
        AssertThat(state.Left.BonusSockets).IsEqual(1);
        AssertThat(manaBefore - state.Left.Mana
            >= BattleConfig.Default.SocketPurchaseCost - 1f).IsTrue();

        // Once per battle.
        sim.Advance(state, new List<SimCommand> { SimCommand.BuySocket(PlayerSide.Left) });
        AssertThat(state.Left.BonusSockets).IsEqual(1);

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "swift_banner"),
        });
        AssertThat(state.Left.Conduits.Count).IsEqual(4);
    }

    private static SimUnit Spawn(
        BattleSim sim, BattleState state, PlayerSide side, string defId, float x)
    {
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(side, defId) });
        var unit = state.Units[^1];
        unit.X = x;
        return unit;
    }
}
