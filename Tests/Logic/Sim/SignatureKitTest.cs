namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Game.Battle.Hud;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Signature kits (Full Roster Batch rework): each expansion unit carries ONE
/// data-driven mechanic the sim reads from UnitDef. Kits chosen from the
/// opus-gen / sonnet-red-team exploration (wf_8c8f07c0).
/// </summary>
[TestSuite]
public class SignatureKitTest
{
    private static (BattleSim sim, BattleState state) CreateBattle(params UnitDef[] defs)
    {
        var sim = new BattleSim(BattleConfig.Default with { EdictsPerTier = 0 }, defs);
        var state = sim.CreateInitialState(7UL);
        state.Left.Mana = 5000f;
        state.Left.WalletCap = 50000f;
        state.Right.Mana = 5000f;
        state.Right.WalletCap = 50000f;
        return (sim, state);
    }

    private static SimUnit Field(
        BattleSim sim, BattleState state, PlayerSide side, string defId, float x)
    {
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(side, defId) });
        var unit = state.Units[^1];
        unit.X = x;
        return unit;
    }

    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    private static UnitDef Pillar(string id) => TestUnits.Grunt(id, moveSpeed: 0f) with
    {
        MaxHp = 1000,
        Damage = 0,
    };

    [TestCase]
    public void LobbedPrayerStrikesTheFarthestEnemyInBand()
    {
        var acolyte = TestUnits.Archer("acolyte") with
        {
            PrefersFarthestTarget = true,
            ForeswingTicks = 2,
            MoveSpeed = 0f,
        };
        var (sim, state) = CreateBattle(acolyte, Pillar("near"), Pillar("far"));
        var caster = Field(sim, state, PlayerSide.Left, "acolyte", 5f);
        var near = Field(sim, state, PlayerSide.Right, "near", 7f);
        var far = Field(sim, state, PlayerSide.Right, "far", 10f);
        caster.X = 5f;

        AdvanceTicks(sim, state, 3);

        AssertThat(far.Hp < far.Def.MaxHp).IsTrue();
        AssertThat(near.Hp).IsEqual(near.Def.MaxHp);
    }

    [TestCase]
    public void RevenantRisesOnceAndBothDeathsPayTheKiller()
    {
        var revenant = TestUnits.Grunt("revenant", moveSpeed: 0f) with
        {
            MaxHp = 240,
            Damage = 0,
            ReviveHpPct = 0.5f,
            DeployCost = 100,
        };
        var killer = TestUnits.FastBiter("killer") with { Damage = 999, MoveSpeed = 0f };
        var (sim, state) = CreateBattle(revenant, killer);
        var rev = Field(sim, state, PlayerSide.Left, "revenant", 10f);
        Field(sim, state, PlayerSide.Right, "killer", 10.5f);

        var manaBefore = state.Right.Mana;
        AdvanceTicks(sim, state, 2);

        AssertThat(rev.IsAlive).IsTrue();
        AssertThat(rev.Hp).IsEqual(120);
        // The first death pays bounty in full (100/5 = 20) — no economy denial.
        AssertThat(state.Right.Mana >= manaBefore + 20f).IsTrue();

        AdvanceTicks(sim, state, 30);
        AssertThat(state.Units.Any(u => u.Def.Id == "revenant" && u.IsAlive)).IsFalse();
    }

    [TestCase]
    public void WyrmlingDetonatesOnDeath()
    {
        var wyrmling = TestUnits.Grunt("wyrmling", moveSpeed: 0f) with
        {
            MaxHp = 1,
            Damage = 0,
            OnDeathBlastDamage = 40,
            OnDeathBlastRadius = 2.5f,
        };
        var killer = TestUnits.FastBiter("killer") with { MoveSpeed = 0f };
        var (sim, state) = CreateBattle(wyrmling, killer, Pillar("close"), Pillar("distant"));
        Field(sim, state, PlayerSide.Left, "wyrmling", 10f);
        Field(sim, state, PlayerSide.Right, "killer", 10.5f);
        var close = Field(sim, state, PlayerSide.Right, "close", 11f);
        var distant = Field(sim, state, PlayerSide.Right, "distant", 15f);

        AdvanceTicks(sim, state, 3);

        AssertThat(close.Hp).IsEqual(close.Def.MaxHp - 40);
        AssertThat(distant.Hp).IsEqual(distant.Def.MaxHp);
    }

    [TestCase]
    public void BonfireAuraBurnsBothStrataEveryTick()
    {
        var ogre = TestUnits.Grunt("ogre", moveSpeed: 0f) with
        {
            AuraDamagePerTick = 2,
            AuraRadius = 2.5f,
            Damage = 0,
        };
        var airPillar = TestUnits.Whelp("flyer") with { MoveSpeed = 0f, Damage = 0, MaxHp = 1000 };
        var (sim, state) = CreateBattle(ogre, Pillar("ground"), airPillar);
        Field(sim, state, PlayerSide.Left, "ogre", 10f);
        var ground = Field(sim, state, PlayerSide.Right, "ground", 11f);
        var flyer = Field(sim, state, PlayerSide.Right, "flyer", 11.5f);
        var groundBefore = ground.Hp;
        var flyerBefore = flyer.Hp;

        AdvanceTicks(sim, state, 5);

        AssertThat(groundBefore - ground.Hp).IsEqual(10);
        AssertThat(flyerBefore - flyer.Hp).IsEqual(10);
    }

    [TestCase]
    public void VigilHardensTheWatchAndWrathBreaksIt()
    {
        var sentry = TestUnits.Grunt("sentry", moveSpeed: 0f) with
        {
            MaxHp = 1000,
            Damage = 1,
            VigilDrPerSecond = 0.2f,
            VigilDrMaxPct = 0.4f,
        };
        var (sim, state) = CreateBattle(sentry, Pillar("post"));
        var watcher = Field(sim, state, PlayerSide.Left, "sentry", 25f);
        Field(sim, state, PlayerSide.Right, "post", 25.5f);

        AdvanceTicks(sim, state, 150);
        state.Right.WrathCooldownTicks = 0;
        var hpBefore = watcher.Hp;
        sim.Advance(state, new List<SimCommand> { SimCommand.CastWrath(PlayerSide.Right) });

        // 30 Wrath damage at the 40% vigil cap lands for 18; the push wipes the watch.
        AssertThat(hpBefore - watcher.Hp).IsEqual(18);
        AssertThat(watcher.VigilTicks).IsEqual(0);
    }

    [TestCase]
    public void GlacierZonesLingerSlowAndExpire()
    {
        var adept = TestUnits.Mage("adept") with
        {
            ForeswingTicks = 2,
            MoveSpeed = 0f,
            ZoneSlowPct = 0.5f,
            ZoneRadius = 2f,
            ZoneDurationTicks = 30,
            ZoneDamagePerTick = 1,
        };
        var (sim, state) = CreateBattle(adept, Pillar("victim"));
        var caster = Field(sim, state, PlayerSide.Left, "adept", 5f);
        var victim = Field(sim, state, PlayerSide.Right, "victim", 8f);

        AdvanceTicks(sim, state, 4);

        AssertThat(state.Zones.Count).IsEqual(1);
        AssertThat(victim.SlowPct).IsEqualApprox(0.5f, 0.001f);

        // Zones outlive their caster ("stays forever") but expire on their own clock.
        caster.Hp = 0;
        AdvanceTicks(sim, state, 40);
        AssertThat(state.Zones.Count == 0).IsTrue();
    }

    [TestCase]
    public void AvalanchePlowShovesEverythingAheadExceptTheUnstaggerable()
    {
        var colossus = TestUnits.Grunt("colossus", moveSpeed: 0f) with
        {
            ForeswingTicks = 2,
            Range = 0.9f,
            ShoveDistance = 2.5f,
            ShoveRadius = 2f,
        };
        var wall = Pillar("wall") with { Unstaggerable = true };
        var (sim, state) = CreateBattle(colossus, Pillar("front"), Pillar("back"), wall);
        Field(sim, state, PlayerSide.Left, "colossus", 10f);
        var front = Field(sim, state, PlayerSide.Right, "front", 10.7f);
        var back = Field(sim, state, PlayerSide.Right, "back", 11.5f);
        var anchored = Field(sim, state, PlayerSide.Right, "wall", 11.8f);

        AdvanceTicks(sim, state, 2);

        AssertThat(front.X).IsEqualApprox(13.2f, 0.05f);
        AssertThat(back.X).IsEqualApprox(14f, 0.05f);
        AssertThat(anchored.X).IsEqualApprox(11.8f, 0.05f);
    }

    [TestCase]
    public void BogStalkerLifestealsCappedAtRemainingHp()
    {
        var stalker = TestUnits.FastBiter("stalker") with
        {
            Damage = 40,
            MoveSpeed = 0f,
            LifestealPct = 0.6f,
        };
        var (sim, state) = CreateBattle(stalker, Pillar("prey"));
        var leech = Field(sim, state, PlayerSide.Left, "stalker", 10f);
        var prey = Field(sim, state, PlayerSide.Right, "prey", 10.5f);
        leech.Hp = 50;
        prey.Hp = 5;

        AdvanceTicks(sim, state, 2);

        // Overkill pays nothing: heal = round(min(40, 5) * 0.6) = 3.
        AssertThat(leech.Hp).IsEqual(53);
    }

    [TestCase]
    public void PlagueTollRampsAndKnockbackResetsIt()
    {
        var bell = TestUnits.Mage("bell") with
        {
            Damage = 10,
            ForeswingTicks = 2,
            BackswingTicks = 2,
            MoveSpeed = 0f,
            TollRampPct = 0.5f,
            TollRampCap = 2f,
        };
        var (sim, state) = CreateBattle(bell, Pillar("victim"), TestUnits.FastBiter("biter") with
        {
            Damage = 40,
            MoveSpeed = 0f,
        });
        var toller = Field(sim, state, PlayerSide.Left, "bell", 5f);
        var victim = Field(sim, state, PlayerSide.Right, "victim", 8f);

        // Two tolls: 10 then 15 (TollCount scales the NEXT toll).
        AdvanceTicks(sim, state, 8);
        AssertThat(victim.Def.MaxHp - victim.Hp).IsEqual(25);
        AssertThat(toller.TollCount).IsEqual(2);

        // A threshold knockback (40 >= 100/3) silences the accrued toll.
        Field(sim, state, PlayerSide.Right, "biter", 5.5f);
        AdvanceTicks(sim, state, 3);
        AssertThat(toller.TollCount).IsEqual(0);
    }

    [TestCase]
    public void QuarrySlingerCrushesWallsNotChaff()
    {
        var slinger = TestUnits.Sniper("slinger") with
        {
            ForeswingTicks = 2,
            MoveSpeed = 0f,
            BonusVsHighHpPct = 0.5f,
            HighHpThreshold = 300,
        };
        var (sim, state) = CreateBattle(slinger, Pillar("tank"));
        Field(sim, state, PlayerSide.Left, "slinger", 5f);
        var tank = Field(sim, state, PlayerSide.Right, "tank", 10f);
        AdvanceTicks(sim, state, 3);
        AssertThat(tank.Def.MaxHp - tank.Hp).IsEqual(45);

        var chaffDef = Pillar("chaff") with { MaxHp = 100 };
        var (sim2, state2) = CreateBattle(slinger, chaffDef);
        Field(sim2, state2, PlayerSide.Left, "slinger", 5f);
        var chaff = Field(sim2, state2, PlayerSide.Right, "chaff", 10f);
        AdvanceTicks(sim2, state2, 3);
        AssertThat(chaff.Def.MaxHp - chaff.Hp).IsEqual(30);
    }

    [TestCase]
    public void UnstaggerableBulwarkNeverFlinchesButWrathMovesIt()
    {
        var bulwark = TestUnits.Grunt("bulwark", moveSpeed: 0f) with
        {
            MaxHp = 400,
            Damage = 0,
            Unstaggerable = true,
        };
        var biter = TestUnits.FastBiter("biter") with { Damage = 200, MoveSpeed = 0f };
        var (sim, state) = CreateBattle(bulwark, biter);
        var wall = Field(sim, state, PlayerSide.Left, "bulwark", 25f);
        Field(sim, state, PlayerSide.Right, "biter", 25.5f);

        AdvanceTicks(sim, state, 2);

        // 200 damage crosses a knockback threshold — the opinion does not move.
        AssertThat(wall.X).IsEqualApprox(25f, 0.001f);
        AssertThat(wall.IFrameTicks).IsEqual(0);

        state.Right.WrathCooldownTicks = 0;
        sim.Advance(state, new List<SimCommand> { SimCommand.CastWrath(PlayerSide.Right) });
        AssertThat(wall.X < 25f).IsTrue();
    }

    [TestCase]
    public void CourierFirstStrikeIsInstantAndOversized()
    {
        var courier = TestUnits.Grunt("courier", moveSpeed: 0f) with
        {
            Damage = 10,
            ForeswingTicks = 8,
            BackswingTicks = 4,
            FirstStrikeBonusPct = 1.5f,
        };
        var (sim, state) = CreateBattle(courier, Pillar("target"));
        Field(sim, state, PlayerSide.Left, "courier", 10f);
        var target = Field(sim, state, PlayerSide.Right, "target", 10.5f);

        sim.Advance(state, SimCommand.None);
        // The message arrives before the wind-up: contact on the first tick, 2.5x.
        AssertThat(target.Def.MaxHp - target.Hp).IsEqual(25);

        AdvanceTicks(sim, state, 13);
        // Second swing pays the full 8-tick foreswing and lands plain.
        AssertThat(target.Def.MaxHp - target.Hp).IsEqual(35);
    }

    [TestCase]
    public void GaleHarrierStrafesPastAfterEveryAttackCycle()
    {
        var harrier = TestUnits.Whelp("harrier") with
        {
            ForeswingTicks = 2,
            BackswingTicks = 2,
            MoveSpeed = 2f,
            StrafeDistance = 3.5f,
        };
        var (sim, state) = CreateBattle(harrier, Pillar("mark"));
        var flyer = Field(sim, state, PlayerSide.Left, "harrier", 10f);
        var mark = Field(sim, state, PlayerSide.Right, "mark", 11f);

        AdvanceTicks(sim, state, 4);

        AssertThat(mark.Hp < mark.Def.MaxHp).IsTrue();
        AssertThat(flyer.X).IsEqualApprox(13.5f, 0.15f);
    }

    [TestCase]
    public void EveryExpansionUnitRendersItsSignature()
    {
        foreach (var def in UnitCatalog.RosterExpansion)
        {
            AssertThat(EffectText.ForSignature(def).Length > 0)
                .OverrideFailureMessage($"{def.Id} has no signature kit text").IsTrue();
        }
        AssertThat(EffectText.ForSignature(UnitCatalog.FirstPlayable[0])).IsEqual(string.Empty);
    }
}
