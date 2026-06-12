namespace DraconicWars.Game.Campaign;

using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Core;
using DraconicWars.Sim.Units;

public enum AiPersona
{
    Rusher,
    Powerhouse,
    Streamer,
}

/// <summary>
/// The honest AI Dragonlord (design-meta.md §9): plays through the same SimCommands as
/// the player — no hidden stat or economy cheats. Breath aiming carries a published
/// accuracy handicap (±AimJitter lane meters). Pure logic; deterministic per seed.
/// </summary>
public sealed class AiCommander
{
    public const float AimJitterMeters = 3f;
    private const int ChannelPerTick = 20;
    private const int WrathPressureCount = 3;

    private readonly AiPersona _persona;
    private readonly PlayerSide _side;
    private readonly SimRng _rng;

    public AiCommander(AiPersona persona, PlayerSide side, ulong seed)
    {
        _persona = persona;
        _side = side;
        _rng = new SimRng(seed);
    }

    public IEnumerable<SimCommand> CommandsForTick(BattleState state)
    {
        var player = state.Player(_side);

        // Parleys no longer pause the sim — answer the Broker AND keep fighting.
        if (player.AwaitingParley && player.PendingOffers.Count > 0)
        {
            var preferredOffer = FindPreferredOffer(player.PendingOffers);
            if (preferredOffer is null
                && player.TithesPaidThisParley == 0
                && player.Mana >= state.Config.TitheCostMana * 3f)
            {
                yield return SimCommand.PayTithe(_side);
            }
            else
            {
                yield return SimCommand.SealPact(_side, preferredOffer ?? player.PendingOffers[0]);
            }
        }

        foreach (var command in EconomyCommands(state, player))
        {
            yield return command;
        }
        foreach (var command in DeployCommands(state, player))
        {
            yield return command;
        }
        if (player.AscensionTier >= 4
            && player.EquippedDragonId is not null
            && player.Mana > 100f)
        {
            yield return SimCommand.ChannelMana(_side, ChannelPerTick);
        }
        if (player.WrathCooldownTicks == 0 && EnemiesOnOwnHalf(state) >= WrathPressureCount)
        {
            yield return SimCommand.CastWrath(_side);
        }
        if (player.BreathEnergySeconds > state.Config.BreathMaxSeconds * 0.5f
            && FindEnemyClusterX(state) is { } clusterX)
        {
            var jitter = (_rng.NextFloat() * 2f - 1f) * AimJitterMeters;
            yield return SimCommand.FireBreath(_side, clusterX + jitter);
        }
    }

    /// <summary>First offer matching the persona's doctrine category, or null —
    /// a null answer with a deep wallet is what makes the AI tithe the Broker once.</summary>
    private string? FindPreferredOffer(IReadOnlyList<string> offers)
    {
        var preferred = _persona switch
        {
            AiPersona.Rusher => DraconicWars.Sim.Pacts.PactCategory.Deployment,
            AiPersona.Powerhouse => DraconicWars.Sim.Pacts.PactCategory.Economy,
            _ => DraconicWars.Sim.Pacts.PactCategory.Combat,
        };
        foreach (var offer in offers)
        {
            if (DraconicWars.Sim.Pacts.PactCatalog.ById(offer).Category == preferred)
            {
                return offer;
            }
        }
        return null;
    }

    private IEnumerable<SimCommand> EconomyCommands(BattleState state, PlayerState player)
    {
        if (_persona == AiPersona.Rusher)
        {
            yield break;
        }

        var desired = _persona == AiPersona.Powerhouse
            ? new[] { "mana_well", "aurum_vault", "war_horn" }
            : new[] { "mana_well", "war_horn" };
        foreach (var conduitId in desired)
        {
            if (player.Conduits.ContainsKey(conduitId))
            {
                continue;
            }
            if (player.Conduits.Count >= state.Config.ConduitSockets)
            {
                yield break;
            }
            yield return SimCommand.BuildConduit(_side, conduitId);
            yield break;
        }
    }

    private IEnumerable<SimCommand> DeployCommands(BattleState state, PlayerState player)
    {
        var deployable = AffordableReadyUnits(state, player).ToList();
        if (deployable.Count == 0)
        {
            yield break;
        }

        switch (_persona)
        {
            case AiPersona.Rusher:
                yield return SimCommand.Deploy(_side, deployable.OrderBy(d => d.DeployCost).First().Id);
                break;
            case AiPersona.Powerhouse:
                // Banks toward elites: deploys only when sitting on a deep wallet.
                if (player.Mana >= player.EffectiveWalletCap * 0.7f)
                {
                    yield return SimCommand.Deploy(
                        _side, deployable.OrderByDescending(d => d.DeployCost).First().Id);
                }
                break;
            default:
                yield return SimCommand.Deploy(
                    _side, deployable[_rng.NextInt(deployable.Count)].Id);
                break;
        }
    }

    private IEnumerable<UnitDef> AffordableReadyUnits(BattleState state, PlayerState player)
    {
        foreach (var def in DraconicWars.Game.Content.UnitCatalog.FirstPlayable)
        {
            if (def.Tier >= 4 || def.Tier > player.AscensionTier || def.DeployCost > player.Mana)
            {
                continue;
            }
            if (player.DeployCooldowns.TryGetValue(def.Id, out var cooldown) && cooldown > 0)
            {
                continue;
            }
            yield return def;
        }
    }

    private int EnemiesOnOwnHalf(BattleState state)
    {
        var midfield = state.Config.LaneLength * 0.5f;
        var count = 0;
        foreach (var unit in state.Units)
        {
            if (unit.Side == _side || !unit.IsAlive)
            {
                continue;
            }
            var onOwnHalf = _side == PlayerSide.Right ? unit.X >= midfield : unit.X <= midfield;
            if (onOwnHalf)
            {
                count++;
            }
        }
        return count;
    }

    private float? FindEnemyClusterX(BattleState state)
    {
        float sum = 0f;
        var count = 0;
        foreach (var unit in state.Units)
        {
            if (unit.Side != _side && unit.IsAlive)
            {
                sum += unit.X;
                count++;
            }
        }
        return count >= 2 ? sum / count : null;
    }
}
