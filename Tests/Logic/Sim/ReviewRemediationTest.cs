namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Regressions caught by the adversarial review of the roster expansion. Each test pins the
/// CORRECT behaviour for a confirmed defect so the fix is verified and stays fixed.
/// </summary>
[TestSuite]
public class ReviewRemediationTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    // #1: a dismount unit whose FIRST contact is the enemy spire (undefended lane) must still
    // seed its one-time venom wake — the sacrifice fires on any first contact, not just enemies.
    [TestCase]
    public void DismountWakeSeedsEvenWhenFirstContactIsTheSpire()
    {
        var charger = TestUnits.Grunt("charger") with
        {
            MoveSpeed = 0f, Range = 2f, Damage = 5, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0, DismountSpeed = 1.4f,
            ZoneRadius = 2f, ZoneSlowPct = 0.35f, ZoneDurationTicks = 600, ZoneDamagePerTick = 1,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { charger });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        sim.Advance(state, new List<SimCommand> { SimCommand.Deploy(PlayerSide.Left, "charger") });
        var u = state.Units[0];
        u.X = state.Config.LaneLength - 1f; // adjacent to the enemy (Right) spire, no enemies in band

        AdvanceTicks(sim, state, 12);

        AssertThat(u.MoveSpeedOverride).IsEqual(1.4f);   // dismounted on the spire contact
        AssertThat(state.Zones.Count).IsEqual(1);        // and the sacrifice wake DID seed
    }

    // #14: terravossk's charter discharge fires "at cap OR when Wrath displaces it". A Vigil
    // shockwave unit displaced by enemy Wrath releases its shockwave.
    [TestCase]
    public void WrathDisplaceDischargesTheShockwave()
    {
        var terra = TestUnits.Grunt("terra") with
        {
            MoveSpeed = 0f, Range = 1f, Damage = 1, ForeswingTicks = 2, BackswingTicks = 2,
            MaxHp = 100000, KnockbackCount = 0, Unstaggerable = true, // survives the Wrath hit
            VigilDrPerSecond = 0.1f, VigilDrMaxPct = 0.4f,
            ShockwaveDamage = 50, ShockwaveRange = 10f,
        };
        var bystander = TestUnits.Grunt("bystander") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { terra, bystander });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "terra"),
            SimCommand.Deploy(PlayerSide.Right, "bystander"),
        });
        var mid = state.Config.LaneLength * 0.5f;
        state.Units[0].X = mid + 3f; // on the Right's half so enemy Wrath reaches terra
        state.Units[1].X = mid + 5f; // a Right unit near terra, inside ShockwaveRange
        var bystanderUnit = state.Units.First(u => u.Def.Id == "bystander");
        state.Right.WrathCooldownTicks = 0; // Wrath starts on cooldown; clear it so the cast lands

        sim.Advance(state, new List<SimCommand> { SimCommand.CastWrath(PlayerSide.Right) });

        // Right's Wrath hits Left units only; the bystander (Right) could only be hurt by
        // terra's shockwave discharging on the displace.
        AssertThat(bystanderUnit.Hp < bystanderUnit.Def.MaxHp).IsTrue();
    }

    // #16: a shelter unit must NOT shelter itself — it dies to anti-air as designed.
    [TestCase]
    public void ShelterDoesNotProtectItsOwnSource()
    {
        int DamageTakenBy(bool isShelterer)
        {
            var target = TestUnits.Grunt("target") with
            {
                MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0,
                ShelterDrPct = isShelterer ? 0.5f : 0f, ShelterRadius = isShelterer ? 7f : 0f,
            };
            var attacker = TestUnits.Grunt("attacker") with
            {
                MoveSpeed = 0f, Range = 2f, Damage = 40, ForeswingTicks = 2, BackswingTicks = 2,
                KnockbackCount = 0,
            };
            var sim = new BattleSim(BattleConfig.Default, new[] { target, attacker });
            var state = sim.CreateInitialState(1UL);
            state.Left.Mana = 100000f;
            state.Right.Mana = 100000f;
            sim.Advance(state, new List<SimCommand>
            {
                SimCommand.Deploy(PlayerSide.Left, "target"),
                SimCommand.Deploy(PlayerSide.Right, "attacker"),
            });
            var t = state.Units.First(u => u.Def.Id == "target");
            t.X = 5f;
            state.Units.First(u => u.Def.Id == "attacker").X = 6f;
            AdvanceTicks(sim, state, 40);
            return t.Def.MaxHp - t.Hp;
        }

        // The shelterer takes the SAME damage as a plain unit — its aura doesn't cover itself.
        AssertThat(DamageTakenBy(isShelterer: true)).IsEqual(DamageTakenBy(isShelterer: false));
    }

    // #17: the_tithe siphons only when SURFACED — a burrowed (untargetable) sapper drains nothing.
    [TestCase]
    public void BurrowedSapperDoesNotSiphonUntilSurfaced()
    {
        var sapper = TestUnits.Grunt("sapper") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 1, ForeswingTicks = 2, BackswingTicks = 2,
            KnockbackCount = 0, DrainManaOnContact = 25,
        };
        var enemy = TestUnits.Grunt("enemy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { sapper, enemy });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 5000f;
        state.Right.Mana = 5000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "sapper"),
            SimCommand.Deploy(PlayerSide.Right, "enemy"),
        });
        var sapperUnit = state.Units.First(u => u.Def.Id == "sapper");
        sapperUnit.X = 5f;
        state.Units.First(u => u.Def.Id == "enemy").X = 8f;

        sapperUnit.Targetable = false; // burrowed
        state.Right.Mana = 5000f;      // baseline after setup, before the burrowed window
        AdvanceTicks(sim, state, 40);
        AssertThat(state.Right.Mana).IsEqual(5000f); // no siphon while submerged

        sapperUnit.Targetable = true; // surfaced
        AdvanceTicks(sim, state, 40);
        AssertThat(state.Right.Mana < 5000f).IsTrue(); // now it siphons
    }
}
