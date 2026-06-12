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
    public void ArmamentsAreOrdinaryConduits_TheyStackAndShareSockets()
    {
        var (sim, state) = CreateBattle();

        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "skyward_flak"),
            SimCommand.BuildConduit(PlayerSide.Left, "siege_mortar"),
            SimCommand.BuildConduit(PlayerSide.Left, "mana_well"),
        });
        AssertThat(state.Left.Conduits.Count).IsEqual(3);

        // The sockets are full: defense ate the whole build.
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "war_horn"),
        });
        AssertThat(state.Left.Conduits.ContainsKey("war_horn")).IsFalse();
    }

    [TestCase]
    public void BreathStaysLiveAlongsideTurrets()
    {
        var (sim, state) = CreateBattle();
        state.Left.BreathEnergySeconds = 4f;
        var intruder = Spawn(sim, state, PlayerSide.Right, "pillar", 3f);
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "skyward_flak"),
        });

        var hpBefore = intruder.Hp;
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
    public void BothTurretsFireIndependentlyOnTheirOwnCadence()
    {
        var (sim, state) = CreateBattle();
        var airTarget = Spawn(sim, state, PlayerSide.Right, "whelp", 6f);
        var groundTarget = Spawn(sim, state, PlayerSide.Right, "pillar", 12f);
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "skyward_flak"),
            SimCommand.BuildConduit(PlayerSide.Left, "siege_mortar"),
        });
        var flak = ConduitDefs.ById("skyward_flak");
        var mortar = ConduitDefs.ById("siege_mortar");
        AssertThat(flak.TurretCadenceTicks < mortar.TurretCadenceTicks).IsTrue();

        // The faster flak fires first; the mortar hasn't.
        AdvanceTicks(sim, state, flak.TurretCadenceTicks + 1);
        AssertThat(airTarget.Hp < airTarget.Def.MaxHp).IsTrue();
        AssertThat(airTarget.SlowTicks > 0).IsTrue();
        AssertThat(groundTarget.Hp).IsEqual(groundTarget.Def.MaxHp);

        // The mortar lands on its own cadence.
        AdvanceTicks(sim, state, mortar.TurretCadenceTicks - flak.TurretCadenceTicks);
        AssertThat(groundTarget.Hp).IsEqual(groundTarget.Def.MaxHp - mortar.TurretDamagePerTier);
    }

    [TestCase]
    public void MachineKillsFeedNoAscensionAndNoEdicts_ButPayBounty()
    {
        var (sim, state) = CreateBattle();
        var victim = Spawn(sim, state, PlayerSide.Right, "whelp", 6f);
        victim.Hp = 1;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.BuildConduit(PlayerSide.Left, "skyward_flak"),
        });
        var flak = ConduitDefs.ById("skyward_flak");
        var kills = state.Left.Kills;
        var fromKills = state.Left.AscensionFromKills;
        var manaBefore = state.Left.Mana;

        AdvanceTicks(sim, state, flak.TurretCadenceTicks + 1);

        AssertThat(victim.IsAlive).IsFalse();
        AssertThat(state.Left.AscensionFromKills).IsEqualApprox(fromKills, 0.001f);
        AssertThat(state.Left.Kills).IsEqual(kills);
        // Bounty still pays (config knob, default 100%).
        AssertThat(state.Left.Mana > manaBefore).IsTrue();
    }

    [TestCase]
    public void BreathKillsStillCreditAscension()
    {
        var (sim, state) = CreateBattle();
        state.Left.BreathEnergySeconds = 4f;
        var victim = Spawn(sim, state, PlayerSide.Right, "pillar", 3f);
        victim.Hp = 1;
        var fromKills = state.Left.AscensionFromKills;

        for (var i = 0; i < 8; i++)
        {
            sim.Advance(state, new List<SimCommand>
            {
                SimCommand.FireBreath(PlayerSide.Left, victim.X),
            });
        }

        AssertThat(victim.IsAlive).IsFalse();
        AssertThat(state.Left.AscensionFromKills > fromKills).IsTrue();
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

        sim.Advance(state, new List<SimCommand> { SimCommand.BuySocket(PlayerSide.Left) });
        AssertThat(state.Left.BonusSockets).IsEqual(0);

        state.Left.AscensionTier = 3;
        var manaBefore = state.Left.Mana;
        sim.Advance(state, new List<SimCommand> { SimCommand.BuySocket(PlayerSide.Left) });
        AssertThat(state.Left.BonusSockets).IsEqual(1);
        AssertThat(manaBefore - state.Left.Mana
            >= BattleConfig.Default.SocketPurchaseCost - 1f).IsTrue();

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
