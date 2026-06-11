namespace DraconicWars.Sim.Battle;

using System;
using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Core;
using DraconicWars.Sim.Units;

/// <summary>
/// The deterministic battle simulation. Pure C#: no Godot types, no wall clock, no
/// unseeded randomness — Advance() is a pure function of (state, commands) so that
/// identical inputs replay identically (design.md §3).
/// </summary>
public sealed class BattleSim
{
    private static readonly PlayerSide[] Sides = { PlayerSide.Left, PlayerSide.Right };

    private readonly BattleConfig _config;
    private readonly Dictionary<string, UnitDef> _defs;

    public BattleSim(BattleConfig config, IEnumerable<UnitDef> defs)
    {
        _config = config;
        _defs = defs.ToDictionary(def => def.Id);
    }

    public BattleState CreateInitialState(ulong seed)
    {
        return new BattleState
        {
            Config = _config,
            Rng = new SimRng(seed),
            Left = NewPlayer(),
            Right = NewPlayer(),
            LeftSpireHp = _config.SpireMaxHp,
            RightSpireHp = _config.SpireMaxHp,
        };

        PlayerState NewPlayer()
        {
            return new PlayerState
            {
                Mana = _config.StartingMana,
                WalletCap = _config.StartingWalletCap,
            };
        }
    }

    public void Advance(BattleState state, IReadOnlyList<SimCommand> commands)
    {
        if (state.Outcome != BattleOutcome.Ongoing)
        {
            return;
        }

        ProcessCommands(state, commands);
        TickEconomy(state);
        ProcessCombat(state);
        MoveUnits(state);
        ProcessTimeline(state);
        EvaluateOutcome(state);
        if (state.Outcome == BattleOutcome.Ongoing)
        {
            state.Tick++;
        }
    }

    private void ProcessCommands(BattleState state, IReadOnlyList<SimCommand> commands)
    {
        foreach (var command in commands)
        {
            if (command.Kind == SimCommandKind.Deploy)
            {
                TryDeploy(state, command.Side, command.UnitDefId);
            }
        }
    }

    private void TryDeploy(BattleState state, PlayerSide side, string unitDefId)
    {
        if (!_defs.TryGetValue(unitDefId, out var def))
        {
            return;
        }
        var player = state.Player(side);
        if (player.Mana < def.DeployCost)
        {
            return;
        }
        if (player.DeployCooldowns.TryGetValue(unitDefId, out var remaining) && remaining > 0)
        {
            return;
        }

        player.Mana -= def.DeployCost;
        player.DeployCooldowns[unitDefId] = def.DeployCooldownTicks;
        var spawnX = side == PlayerSide.Left
            ? _config.DeploySpawnOffset
            : _config.LaneLength - _config.DeploySpawnOffset;
        state.Units.Add(new SimUnit
        {
            InstanceId = state.NextInstanceId++,
            Def = def,
            Side = side,
            X = spawnX,
            Hp = def.MaxHp,
        });
    }

    private void TickEconomy(BattleState state)
    {
        var drip = _config.DripPerTick * DripMultiplier(state.Tick);
        foreach (var side in Sides)
        {
            var player = state.Player(side);
            player.Mana = MathF.Min(player.Mana + drip, player.WalletCap);

            if (player.DeployCooldowns.Count == 0)
            {
                continue;
            }
            foreach (var unitDefId in player.DeployCooldowns.Keys.ToArray())
            {
                var ticksLeft = player.DeployCooldowns[unitDefId];
                if (ticksLeft > 0)
                {
                    player.DeployCooldowns[unitDefId] = ticksLeft - 1;
                }
            }
        }
    }

    private float DripMultiplier(int tick)
    {
        if (tick < _config.CrescendoStartTick)
        {
            return 1f;
        }
        if (tick < _config.SuddenDeathStartTick)
        {
            return _config.CrescendoDripMultiplier;
        }
        var windows = 1 + (tick - _config.SuddenDeathStartTick) / _config.SuddenDeathEscalationTicks;
        return _config.CrescendoDripMultiplier
            * MathF.Pow(_config.SuddenDeathEscalationFactor, windows);
    }

    private void ProcessCombat(BattleState state)
    {
        foreach (var unit in state.Units)
        {
            if (!unit.IsAlive)
            {
                continue;
            }
            if (unit.IFrameTicks > 0)
            {
                unit.IFrameTicks--;
            }

            if (unit.AttackPhase == AttackPhase.None && HasAnyTargetInBand(state, unit))
            {
                unit.AttackPhase = AttackPhase.Foreswing;
                unit.PhaseTicksLeft = unit.Def.ForeswingTicks;
            }

            switch (unit.AttackPhase)
            {
                case AttackPhase.Foreswing:
                    unit.PhaseTicksLeft--;
                    if (unit.PhaseTicksLeft <= 0)
                    {
                        ResolveContact(state, unit);
                        unit.AttackPhase = AttackPhase.Backswing;
                        unit.PhaseTicksLeft = unit.Def.BackswingTicks;
                        if (unit.PhaseTicksLeft <= 0)
                        {
                            unit.AttackPhase = AttackPhase.None;
                        }
                    }
                    break;
                case AttackPhase.Backswing:
                    unit.PhaseTicksLeft--;
                    if (unit.PhaseTicksLeft <= 0)
                    {
                        unit.AttackPhase = AttackPhase.None;
                    }
                    break;
            }
        }

        state.Units.RemoveAll(unit => !unit.IsAlive);
    }

    private void ResolveContact(BattleState state, SimUnit attacker)
    {
        var targets = EnemiesInBand(state, attacker).ToList();
        if (targets.Count > 0)
        {
            if (attacker.Def.IsArea)
            {
                foreach (var target in targets)
                {
                    DealDamage(state, attacker, target);
                }
            }
            else
            {
                DealDamage(state, attacker, targets[0]);
            }
            return;
        }

        if (SpireDistance(state, attacker) is { } spireDistance
            && spireDistance >= attacker.Def.RangeMin
            && spireDistance <= attacker.Def.Range)
        {
            DamageSpire(state, attacker.Side, attacker.Def.Damage);
        }
    }

    private void DealDamage(BattleState state, SimUnit attacker, SimUnit defender)
    {
        if (defender.IFrameTicks > 0)
        {
            return;
        }

        defender.Hp -= attacker.Def.Damage;
        if (!defender.IsAlive)
        {
            return;
        }

        var def = defender.Def;
        var newKbIndex = (int)((long)(def.MaxHp - defender.Hp) * def.KnockbackCount / def.MaxHp);
        if (newKbIndex <= defender.KbIndex)
        {
            return;
        }

        // All thresholds crossed by one hit collapse into a single knockback (design.md §4).
        defender.KbIndex = newKbIndex;
        var pushDirection = defender.Side == PlayerSide.Left ? -1f : 1f;
        defender.X = Math.Clamp(
            defender.X + pushDirection * _config.KnockbackDistance, 0f, _config.LaneLength);
        defender.IFrameTicks = _config.KnockbackIFrameTicks;
        defender.AttackPhase = AttackPhase.None;
        defender.PhaseTicksLeft = 0;
    }

    private static void DamageSpire(BattleState state, PlayerSide attackerSide, float damage)
    {
        if (attackerSide == PlayerSide.Left)
        {
            state.RightSpireHp -= damage;
        }
        else
        {
            state.LeftSpireHp -= damage;
        }
    }

    private bool HasAnyTargetInBand(BattleState state, SimUnit unit)
    {
        if (EnemiesInBand(state, unit).Any())
        {
            return true;
        }
        return SpireDistance(state, unit) is { } spireDistance
            && spireDistance >= unit.Def.RangeMin
            && spireDistance <= unit.Def.Range;
    }

    /// <summary>Living targetable enemies inside [RangeMin, Range] ahead, nearest first.</summary>
    private static IEnumerable<SimUnit> EnemiesInBand(BattleState state, SimUnit unit)
    {
        var dir = Direction(unit.Side);
        return state.Units
            .Where(other => other.Side != unit.Side && other.IsAlive)
            .Where(other => CanTarget(unit, other))
            .Select(other => (Unit: other, Distance: (other.X - unit.X) * dir))
            .Where(pair => pair.Distance >= unit.Def.RangeMin && pair.Distance <= unit.Def.Range)
            .OrderBy(pair => pair.Distance)
            .ThenBy(pair => pair.Unit.InstanceId)
            .Select(pair => pair.Unit);
    }

    private float? SpireDistance(BattleState state, SimUnit unit)
    {
        var enemySpireX = unit.Side == PlayerSide.Left ? _config.LaneLength : 0f;
        var distance = (enemySpireX - unit.X) * Direction(unit.Side);
        return distance >= 0f ? distance : null;
    }

    private static bool CanTarget(SimUnit attacker, SimUnit defender)
    {
        return defender.Stratum == Stratum.Air
            ? attacker.Def.CanTargetAir
            : attacker.Def.CanTargetGround;
    }

    private static float Direction(PlayerSide side)
    {
        return side == PlayerSide.Left ? 1f : -1f;
    }

    private void MoveUnits(BattleState state)
    {
        foreach (var unit in state.Units)
        {
            if (!unit.IsAlive || unit.AttackPhase != AttackPhase.None)
            {
                continue;
            }

            var dir = Direction(unit.Side);
            var nearestEnemy = FindNearestTargetableDistance(state, unit, dir);
            if (nearestEnemy is { } enemyDistance && enemyDistance <= unit.Def.Range)
            {
                continue;
            }

            var enemySpireX = unit.Side == PlayerSide.Left ? state.Config.LaneLength : 0f;
            var spireDistance = (enemySpireX - unit.X) * dir;
            if (spireDistance <= unit.Def.Range)
            {
                continue;
            }

            var step = unit.Def.MoveSpeed / state.Config.TickRate;
            var maxAdvance = MathF.Max(0f, spireDistance - unit.Def.Range);
            unit.X += MathF.Min(step, maxAdvance) * dir;
        }
    }

    private static float? FindNearestTargetableDistance(BattleState state, SimUnit unit, float dir)
    {
        float? nearest = null;
        foreach (var other in state.Units)
        {
            if (other.Side == unit.Side || !other.IsAlive)
            {
                continue;
            }
            if (!CanTarget(unit, other))
            {
                continue;
            }
            var distanceAhead = (other.X - unit.X) * dir;
            if (distanceAhead < 0f)
            {
                continue;
            }
            if (nearest is null || distanceAhead < nearest)
            {
                nearest = distanceAhead;
            }
        }
        return nearest;
    }

    private void ProcessTimeline(BattleState state)
    {
        if (state.Tick >= _config.SuddenDeathStartTick)
        {
            var windows = 1 + (state.Tick - _config.SuddenDeathStartTick)
                / _config.SuddenDeathEscalationTicks;
            var decayPerTick = _config.SuddenDeathDecayPerSecond * windows / _config.TickRate;
            state.LeftSpireHp -= decayPerTick;
            state.RightSpireHp -= decayPerTick;
        }
    }

    private void EvaluateOutcome(BattleState state)
    {
        var leftDown = state.LeftSpireHp <= 0f;
        var rightDown = state.RightSpireHp <= 0f;
        if (leftDown || rightDown)
        {
            state.Outcome = (leftDown, rightDown) switch
            {
                (true, true) => BattleOutcome.Draw,
                (true, false) => BattleOutcome.RightVictory,
                _ => BattleOutcome.LeftVictory,
            };
            return;
        }

        if (state.Tick >= _config.HardEndTick)
        {
            state.Outcome = state.LeftSpireHp > state.RightSpireHp
                ? BattleOutcome.LeftVictory
                : state.RightSpireHp > state.LeftSpireHp
                    ? BattleOutcome.RightVictory
                    : BattleOutcome.Draw;
        }
    }

    #region Test Helpers
#if TOOLS
    internal void _TestDealDamage(BattleState state, SimUnit attacker, SimUnit defender)
        => DealDamage(state, attacker, defender);
#endif
    #endregion
}
