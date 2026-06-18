namespace DraconicWars.Tests.Logic.Sim;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// vale_chanter's rally aura (roster-expansion-40.md §4): allies within RallyDamageAuraRadius
/// deal RallyDamageAuraPct more damage. A pure enabler fed into the shared ScaledDamage
/// multiplier — non-stacking (best overlapping aura wins), mirroring the haste-halo.
/// </summary>
[TestSuite]
public class RallyAuraTest
{
    private static void AdvanceTicks(BattleSim sim, BattleState state, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            sim.Advance(state, SimCommand.None);
        }
    }

    private static int EnemyDamageOver(bool withRally, int ticks)
    {
        // A stationary ally chips a high-HP dummy; the only variable is whether a rally
        // source sits within radius. Fixed swing cadence => any damage delta is per-hit.
        var attacker = TestUnits.Grunt("atk") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 10, ForeswingTicks = 4, BackswingTicks = 4,
            KnockbackCount = 0,
        };
        var dummy = TestUnits.Grunt("dummy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var rally = TestUnits.Grunt("rally") with
        {
            MoveSpeed = 0f, DeployCost = 0, CanTargetGround = false, CanTargetAir = false,
            RallyDamageAuraPct = 0.5f, RallyDamageAuraRadius = 6f,
        };
        var defs = withRally ? new[] { attacker, dummy, rally } : new[] { attacker, dummy };
        var sim = new BattleSim(BattleConfig.Default, defs);
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        var cmds = new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "atk"),
            SimCommand.Deploy(PlayerSide.Right, "dummy"),
        };
        if (withRally)
        {
            cmds.Add(SimCommand.Deploy(PlayerSide.Left, "rally"));
        }
        sim.Advance(state, cmds);
        var atkUnit = state.Units.First(u => u.Def.Id == "atk");
        var dummyUnit = state.Units.First(u => u.Def.Id == "dummy");
        atkUnit.X = 5f;
        dummyUnit.X = 8f;
        if (withRally)
        {
            state.Units.First(u => u.Def.Id == "rally").X = 4f;
        }
        AdvanceTicks(sim, state, ticks);
        return dummyUnit.Def.MaxHp - dummyUnit.Hp;
    }

    [TestCase]
    public void RallyAuraMakesNearbyAllyHitHarder()
    {
        var plain = EnemyDamageOver(withRally: false, ticks: 120);
        var rallied = EnemyDamageOver(withRally: true, ticks: 120);

        // Same swing cadence over the same window => the only difference is per-hit damage.
        AssertThat(rallied > plain).IsTrue();
    }

    // Same 3-unit structure each time; only the rally source's distance from the ally changes.
    private static int AllyDamageWithRallyAt(float rallyX, int ticks)
    {
        var attacker = TestUnits.Grunt("atk") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 10, ForeswingTicks = 4, BackswingTicks = 4,
            KnockbackCount = 0,
        };
        var dummy = TestUnits.Grunt("dummy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var rally = TestUnits.Grunt("rally") with
        {
            MoveSpeed = 0f, DeployCost = 0, CanTargetGround = false, CanTargetAir = false,
            RallyDamageAuraPct = 0.5f, RallyDamageAuraRadius = 6f,
        };
        var sim = new BattleSim(BattleConfig.Default, new[] { attacker, dummy, rally });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "atk"),
            SimCommand.Deploy(PlayerSide.Right, "dummy"),
            SimCommand.Deploy(PlayerSide.Left, "rally"),
        });
        var atkUnit = state.Units.First(u => u.Def.Id == "atk");
        var dummyUnit = state.Units.First(u => u.Def.Id == "dummy");
        atkUnit.X = 5f;
        dummyUnit.X = 8f;
        state.Units.First(u => u.Def.Id == "rally").X = rallyX;
        AdvanceTicks(sim, state, ticks);
        return dummyUnit.Def.MaxHp - dummyUnit.Hp;
    }

    [TestCase]
    public void RallyAuraDoesNotReachAlliesBeyondRadius()
    {
        var inRange = AllyDamageWithRallyAt(4f, 120);            // dist 1 — within radius 6
        var outOfRange = AllyDamageWithRallyAt(5f - 6f - 2f, 120); // dist 8 — beyond radius
        AssertThat(inRange > outOfRange).IsTrue(); // the distance gate bounds the rally
    }

    // A unit that BOTH carries a rally aura AND attacks. It falls within its own radius, so
    // self-exclusion is the only thing keeping it from boosting its own output.
    private static int SelfDamageOver(float ownRallyPct, int ticks)
    {
        var attacker = TestUnits.Grunt("atk") with
        {
            MoveSpeed = 0f, Range = 30f, Damage = 10, ForeswingTicks = 4, BackswingTicks = 4,
            KnockbackCount = 0, RallyDamageAuraPct = ownRallyPct, RallyDamageAuraRadius = 6f,
        };
        var dummy = TestUnits.Grunt("dummy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 };
        var sim = new BattleSim(BattleConfig.Default, new[] { attacker, dummy });
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        sim.Advance(state, new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "atk"),
            SimCommand.Deploy(PlayerSide.Right, "dummy"),
        });
        var atkUnit = state.Units.First(u => u.Def.Id == "atk");
        var dummyUnit = state.Units.First(u => u.Def.Id == "dummy");
        atkUnit.X = 5f;
        dummyUnit.X = 8f;
        AdvanceTicks(sim, state, ticks);
        return dummyUnit.Def.MaxHp - dummyUnit.Hp;
    }

    [TestCase]
    public void RallyAuraDoesNotBoostItsOwnSource()
    {
        var noAura = SelfDamageOver(0f, 120);
        var withOwnAura = SelfDamageOver(0.5f, 120);
        // The source excludes itself => carrying its own aura changes nothing about its damage.
        AssertThat(withOwnAura).IsEqual(noAura);
    }

    // Measures attacker damage with N rally sources (none of which attack) all within radius.
    private static int DamageWithRallySources(int ticks, params float[] rallyPcts)
    {
        var units = new List<UnitDef>
        {
            TestUnits.Grunt("atk") with
            {
                MoveSpeed = 0f, Range = 30f, Damage = 10, ForeswingTicks = 4, BackswingTicks = 4,
                KnockbackCount = 0,
            },
            TestUnits.Grunt("dummy") with { MoveSpeed = 0f, MaxHp = 100000, KnockbackCount = 0 },
        };
        for (var i = 0; i < rallyPcts.Length; i++)
        {
            units.Add(TestUnits.Grunt($"rally{i}") with
            {
                MoveSpeed = 0f, DeployCost = 0, CanTargetGround = false, CanTargetAir = false,
                RallyDamageAuraPct = rallyPcts[i], RallyDamageAuraRadius = 6f,
            });
        }
        var sim = new BattleSim(BattleConfig.Default, units.ToArray());
        var state = sim.CreateInitialState(1UL);
        state.Left.Mana = 100000f;
        state.Right.Mana = 100000f;
        var cmds = new List<SimCommand>
        {
            SimCommand.Deploy(PlayerSide.Left, "atk"),
            SimCommand.Deploy(PlayerSide.Right, "dummy"),
        };
        for (var i = 0; i < rallyPcts.Length; i++)
        {
            cmds.Add(SimCommand.Deploy(PlayerSide.Left, $"rally{i}"));
        }
        sim.Advance(state, cmds);
        var atkUnit = state.Units.First(u => u.Def.Id == "atk");
        var dummyUnit = state.Units.First(u => u.Def.Id == "dummy");
        atkUnit.X = 5f;
        dummyUnit.X = 8f;
        foreach (var r in state.Units.Where(u => u.Def.Id.StartsWith("rally")))
        {
            r.X = 4f; // all within radius 6 of the attacker at X=5
        }
        AdvanceTicks(sim, state, ticks);
        return dummyUnit.Def.MaxHp - dummyUnit.Hp;
    }

    [TestCase]
    public void OverlappingRallyAurasDoNotStack()
    {
        var strongOnly = DamageWithRallySources(120, 0.5f);
        var weakPlusStrong = DamageWithRallySources(120, 0.2f, 0.5f);
        // Best-aura-wins: the weaker overlapping aura adds nothing on top of the stronger.
        AssertThat(weakPlusStrong).IsEqual(strongOnly);
    }
}
