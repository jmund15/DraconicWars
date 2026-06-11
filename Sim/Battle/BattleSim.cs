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
        ProcessCommands(state, commands);
        TickEconomy(state);
        MoveUnits(state);
        state.Tick++;
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
        foreach (var side in Sides)
        {
            var player = state.Player(side);
            player.Mana = MathF.Min(player.Mana + _config.DripPerTick, player.WalletCap);

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

    private void MoveUnits(BattleState state)
    {
        foreach (var unit in state.Units)
        {
            if (!unit.IsAlive)
            {
                continue;
            }

            var dir = unit.Side == PlayerSide.Left ? 1f : -1f;
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
            var canTarget = other.Stratum == Stratum.Air
                ? unit.Def.CanTargetAir
                : unit.Def.CanTargetGround;
            if (!canTarget)
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
}
