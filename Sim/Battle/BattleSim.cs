namespace DraconicWars.Sim.Battle;

using System;
using System.Collections.Generic;
using System.Linq;
using DraconicWars.Sim.Pacts;
using DraconicWars.Sim.Conduits;
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
    private readonly Dictionary<string, ConduitDef> _conduits;
    private readonly Dictionary<string, PactDef> _pacts;

    public BattleSim(
        BattleConfig config,
        IEnumerable<UnitDef> defs,
        IEnumerable<ConduitDef>? conduits = null,
        IEnumerable<PactDef>? pacts = null)
    {
        _config = config;
        _defs = defs.ToDictionary(def => def.Id);
        _conduits = (conduits ?? Array.Empty<ConduitDef>()).ToDictionary(def => def.Id);
        _pacts = (pacts ?? Array.Empty<PactDef>()).ToDictionary(def => def.Id);
    }

    public BattleState CreateInitialState(ulong seed)
    {
        var rng = new SimRng(seed);
        var pactRng = rng.DeriveChild("pacts");
        var state = new BattleState
        {
            Config = _config,
            Rng = rng,
            PactRng = pactRng,
            ParleyTierPath = TierPath.Roll(pactRng),
            Left = NewPlayer(),
            Right = NewPlayer(),
            LeftSpireHp = _config.SpireMaxHp,
            RightSpireHp = _config.SpireMaxHp,
        };
        RollEdicts(state, rng.DeriveChild("edicts"));
        return state;

        PlayerState NewPlayer()
        {
            return new PlayerState
            {
                Mana = _config.StartingMana,
                WalletCap = _config.StartingWalletCap,
                WrathCooldownTicks = _config.WrathCooldownTicks,
            };
        }
    }

    /// <summary>Rolls the Court's trials: the same published set for both sides;
    /// element-keyed edicts only enter when the def pool can field that element.</summary>
    private void RollEdicts(BattleState state, SimRng edictRng)
    {
        if (_config.EdictsPerTier <= 0)
        {
            return;
        }
        var fieldableElements = _defs.Values.Select(def => def.Element).ToHashSet();
        var eligible = Edicts.EdictCatalog.All
            .Where(def => def.RequiredElement is not { } element
                || fieldableElements.Contains(element))
            .ToList();

        for (var tierIndex = 0; tierIndex < 3; tierIndex++)
        {
            for (var i = 0; i < _config.EdictsPerTier && eligible.Count > 0; i++)
            {
                var pick = eligible[edictRng.NextInt(eligible.Count)];
                eligible.Remove(pick);
                state.Edicts.Add(new Edicts.ActiveEdict { Def = pick, TierIndex = tierIndex });
            }
        }
    }

    public void Advance(BattleState state, IReadOnlyList<SimCommand> commands)
    {
        if (state.Outcome != BattleOutcome.Ongoing)
        {
            return;
        }

        ProcessCommands(state, commands);
        ProcessLastStand(state);
        TickEconomy(state);
        ProcessPhasing(state);
        ProcessCombat(state);
        ProcessProjectiles(state);
        ProcessFieldEffects(state);
        ProcessSummons(state);
        ProcessTurrets(state);
        ProcessEdicts(state);
        ProcessAscension(state);
        ProcessParleys(state);
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
            switch (command.Kind)
            {
                case SimCommandKind.Deploy:
                    TryDeploy(state, command.Side, command.TargetId);
                    break;
                case SimCommandKind.BuildConduit:
                    TryBuildConduit(state, command.Side, command.TargetId);
                    break;
                case SimCommandKind.UpgradeConduit:
                    TryUpgradeConduit(state, command.Side, command.TargetId);
                    break;
                case SimCommandKind.SellConduit:
                    TrySellConduit(state, command.Side, command.TargetId);
                    break;
                case SimCommandKind.ChannelMana:
                    TryChannelMana(state, command.Side, command.Amount);
                    break;
                case SimCommandKind.FireBreath:
                    TryFireBreath(state, command.Side, command.X);
                    break;
                case SimCommandKind.CastWrath:
                    TryCastWrath(state, command.Side);
                    break;
                case SimCommandKind.SealPact:
                    TrySealPact(state, command.Side, command.TargetId);
                    break;
                case SimCommandKind.PayTithe:
                    TryPayTithe(state, command.Side);
                    break;
                case SimCommandKind.AttuneUnit:
                    TryAttuneUnit(state, command.Side, command.TargetId, (Element)command.Amount);
                    break;
                case SimCommandKind.BuySocket:
                    TryBuySocket(state, command.Side);
                    break;
            }
        }
    }

    private void TryFireBreath(BattleState state, PlayerSide side, float x)
    {
        var player = state.Player(side);
        var drainPerTick = 1f / _config.TickRate;
        if (player.BreathEnergySeconds < drainPerTick)
        {
            return;
        }

        // A spire's Breath reaches only to the span's midpoint (playtest: infinite
        // reach made breath a free cross-map execute).
        var midfield = _config.LaneLength * 0.5f;
        x = side == PlayerSide.Left ? MathF.Min(x, midfield) : MathF.Max(x, midfield);

        player.BreathEnergySeconds -= drainPerTick;
        player.BreathPulseCounter++;
        if (player.BreathPulseCounter < _config.BreathPulseTicks)
        {
            return;
        }
        player.BreathPulseCounter = 0;

        // Friendly fire is ON by design (§10a): the pulse hits every unit in radius.
        var pulseDamage = (int)MathF.Round(
            _config.BreathPulseDamage * (1f + player.Buffs.BreathDamagePct));
        var anyHit = false;
        foreach (var unit in state.Units)
        {
            if (!unit.IsAlive || MathF.Abs(unit.X - x) > _config.BreathRadius)
            {
                continue;
            }
            ApplyDirectDamage(state, side, unit, pulseDamage);
            anyHit = true;
        }
        if (anyHit)
        {
            player.BreathPulsesLanded++;
        }
    }

    private void TryCastWrath(BattleState state, PlayerSide side)
    {
        var player = state.Player(side);
        if (player.WrathCooldownTicks > 0)
        {
            return;
        }
        player.WrathCooldownTicks = (int)(_config.WrathCooldownTicks
            * (1f - player.Buffs.WrathCooldownPct));

        var midfield = _config.LaneLength * 0.5f;
        var pushDirection = side == PlayerSide.Left ? 1f : -1f;
        foreach (var unit in state.Units)
        {
            if (unit.Side == side || !unit.IsAlive)
            {
                continue;
            }
            var onCasterHalf = side == PlayerSide.Left ? unit.X <= midfield : unit.X >= midfield;
            if (!onCasterHalf)
            {
                continue;
            }

            if (ApplyDirectDamage(state, side, unit, _config.WrathDamage))
            {
                // Forced knockback: pushes without i-frames and without consuming
                // HP-threshold knockbacks (design.md §4); still interrupts the attack.
                // Moves even the Unstaggerable — Wrath is the one force that does.
                unit.X = Math.Clamp(
                    unit.X + pushDirection * _config.WrathKnockbackDistance,
                    0f, _config.LaneLength);
                unit.AttackPhase = AttackPhase.None;
                unit.PhaseTicksLeft = 0;
                unit.VigilTicks = 0;
                unit.TollCount = 0;
            }
        }
    }

    /// <summary>
    /// Damage without knockback-threshold mechanics (breath, Wrath, turrets). Returns
    /// true if the defender survived. Kill rewards credit only cross-side kills;
    /// machine kills (armaments) pay configurable bounty but NEVER feed Ascension or
    /// edict counters — the Court credits no ascent for machine-work.
    /// </summary>
    private bool ApplyDirectDamage(
        BattleState state, PlayerSide attackerSide, SimUnit defender, int damage,
        bool machineKill = false)
    {
        if (defender.IFrameTicks > 0)
        {
            return defender.IsAlive;
        }

        if (!defender.Targetable)
        {
            return defender.IsAlive;
        }

        if (defender.Def.VigilDrMaxPct > 0f && defender.VigilTicks > 0)
        {
            var vigilDr = MathF.Min(
                defender.Def.VigilDrMaxPct,
                defender.VigilTicks / (float)_config.TickRate * defender.Def.VigilDrPerSecond);
            damage = (int)MathF.Round(damage * (1f - vigilDr));
        }

        defender.Hp -= damage;
        if (defender.IsAlive)
        {
            return true;
        }

        if (defender.Side != attackerSide)
        {
            PayKillReward(state, attackerSide, defender.Def, machineKill);
        }

        if (defender.Def.ReviveHpPct > 0f && !defender.RevivedOnce)
        {
            defender.RevivedOnce = true;
            defender.Hp = (int)(defender.Def.MaxHp * defender.Def.ReviveHpPct);
            defender.KbIndex = (int)((long)(defender.Def.MaxHp - defender.Hp)
                * defender.Def.KnockbackCount / defender.Def.MaxHp);
            defender.IFrameTicks = _config.KnockbackIFrameTicks;
            defender.AttackPhase = AttackPhase.None;
            defender.PhaseTicksLeft = 0;
            return true;
        }

        if (defender.Def.Tier >= 4)
        {
            state.Player(defender.Side).SummoningProgress = 0f;
        }
        if (defender.Def.OnDeathBlastDamage > 0)
        {
            foreach (var other in state.Units.ToList())
            {
                if (other.Side == defender.Side || !other.IsAlive
                    || MathF.Abs(other.X - defender.X) > defender.Def.OnDeathBlastRadius)
                {
                    continue;
                }
                ApplyDirectDamage(state, defender.Side, other, defender.Def.OnDeathBlastDamage);
            }
        }
        return false;
    }

    private void PayKillReward(
        BattleState state, PlayerSide killerSide, UnitDef victim, bool machineKill)
    {
        var killer = state.Player(killerSide);
        var bounty = victim.DeployCost / 5f * (1f + killer.Buffs.KillBountyPct);
        if (machineKill)
        {
            AddMana(killer, bounty * _config.ArmamentKillBountyPct);
        }
        else
        {
            AddMana(killer, bounty);
            CreditKillAscension(state, killerSide, victim);
        }
    }

    /// <summary>Rebreathing (design directive): once per duel a company re-swears its
    /// Breath for mana; FUTURE deploys spawn attuned, fielded units keep their oath.
    /// Ownership of attunement options is a meta concern enforced at the command
    /// source (UI/AI) — the sim enforces only the in-battle rules.</summary>
    private void TryAttuneUnit(BattleState state, PlayerSide side, string unitDefId, Element element)
    {
        if (!_defs.TryGetValue(unitDefId, out var def))
        {
            return;
        }
        var player = state.Player(side);
        var cost = player.FreeAttunements > 0
            ? 0f
            : def.DeployCost * _config.RebreathCostFactor
                * (1f + player.PaidRebreaths * _config.RebreathCostStepPct);
        if (player.AttunedThisBattle.ContainsKey(unitDefId)
            || element == def.Element
            || def.Tier >= 4
            || player.Mana < cost)
        {
            return;
        }
        player.Mana -= cost;
        if (player.FreeAttunements > 0)
        {
            player.FreeAttunements--;
        }
        else
        {
            player.PaidRebreaths++;
        }
        player.AttunedThisBattle[unitDefId] = element;
    }

    private void TryDeploy(BattleState state, PlayerSide side, string unitDefId)
    {
        if (!_defs.TryGetValue(unitDefId, out var def))
        {
            return;
        }
        var player = state.Player(side);
        if (player.AttunedThisBattle.TryGetValue(unitDefId, out var attunedElement))
        {
            def = def with { Element = attunedElement, NativeElement = def.Element };
        }
        if (def.Tier > player.AscensionTier)
        {
            return;
        }
        if (player.Mana < def.DeployCost)
        {
            return;
        }
        if (player.DeployCooldowns.TryGetValue(unitDefId, out var remaining) && remaining > 0)
        {
            return;
        }
        // Squad cap: harness probe showed unlimited fields reward chaff-spam (76% of
        // all deploys were the cheapest unit, battles ended pre-crescendo).
        var fielded = 0;
        foreach (var unit in state.Units)
        {
            if (unit.Side == side && unit.IsAlive)
            {
                fielded++;
            }
        }
        if (fielded >= _config.MaxFieldedPerSide)
        {
            return;
        }

        player.Mana -= def.DeployCost;
        player.DeployCooldowns[unitDefId] = Math.Max(
            0, (int)(def.DeployCooldownTicks * (1f - player.Buffs.DeployCooldownPct)));
        player.ManaDeployedByElement[def.Element] =
            player.ManaDeployedByElement.GetValueOrDefault(def.Element) + def.DeployCost;
        player.MaxSingleDeployCost = MathF.Max(player.MaxSingleDeployCost, def.DeployCost);
        SpawnUnit(state, side, def);
    }

    private void SpawnUnit(BattleState state, PlayerSide side, UnitDef def)
    {
        var spawnX = side == PlayerSide.Left
            ? _config.DeploySpawnOffset
            : _config.LaneLength - _config.DeploySpawnOffset;
        SpawnUnit(state, side, def, spawnX);
    }

    private void SpawnUnit(BattleState state, PlayerSide side, UnitDef def, float x)
    {
        state.Units.Add(new SimUnit
        {
            InstanceId = state.NextInstanceId++,
            Def = def,
            Side = side,
            X = x,
            Hp = def.MaxHp,
            SpawnTimer = def.SpawnCadenceTicks,
        });
    }

    /// <summary>Deferred-spawn phase: spawner units periodically birth chaff at their own X,
    /// capped at SpawnCap live spawns per side. Spawns are queued and appended AFTER the loop
    /// so the unit list is never mutated mid-iteration; chaff carries no SpawnDefId, so it
    /// never recurses. See roster-expansion-40.md §5.</summary>
    private void ProcessSummons(BattleState state)
    {
        List<(PlayerSide Side, UnitDef Def, float X)>? pending = null;
        foreach (var unit in state.Units)
        {
            if (!unit.IsAlive || unit.Def.SpawnDefId is null || unit.Def.SpawnCadenceTicks <= 0)
            {
                continue;
            }
            unit.SpawnTimer--;
            if (unit.SpawnTimer > 0)
            {
                continue;
            }
            unit.SpawnTimer = unit.Def.SpawnCadenceTicks;
            if (!_defs.TryGetValue(unit.Def.SpawnDefId, out var spawnDef))
            {
                continue;
            }
            var liveCount = 0;
            foreach (var other in state.Units)
            {
                if (other.Side == unit.Side && other.IsAlive
                    && other.Def.Id == unit.Def.SpawnDefId)
                {
                    liveCount++;
                }
            }
            if (liveCount >= unit.Def.SpawnCap)
            {
                continue;
            }
            pending ??= new List<(PlayerSide, UnitDef, float)>();
            pending.Add((unit.Side, spawnDef, unit.X));
        }
        if (pending is null)
        {
            return;
        }
        foreach (var spawn in pending)
        {
            SpawnUnit(state, spawn.Side, spawn.Def, spawn.X);
        }
    }

    private void TryChannelMana(BattleState state, PlayerSide side, int amount)
    {
        var player = state.Player(side);
        if (player.EscrowStallTicks > 0)
        {
            return;
        }
        if (player.AscensionTier < 4
            || player.EquippedDragonId is null
            || !_defs.TryGetValue(player.EquippedDragonId, out var dragonDef)
            || DragonOnField(state, side))
        {
            return;
        }

        var effectiveCost = _config.SummoningCost * (1f - player.Buffs.SummonCostPct);
        var remaining = effectiveCost - player.SummoningProgress;
        var channeled = MathF.Min(MathF.Min(amount, player.Mana), remaining);
        if (channeled <= 0f)
        {
            return;
        }

        player.Mana -= channeled;
        player.SummoningProgress += channeled;
        if (player.SummoningProgress >= effectiveCost)
        {
            SpawnUnit(state, side, dragonDef);
        }
    }

    private static bool DragonOnField(BattleState state, PlayerSide side)
    {
        foreach (var unit in state.Units)
        {
            if (unit.Side == side && unit.IsAlive && unit.Def.Tier >= 4)
            {
                return true;
            }
        }
        return false;
    }

    private void TryBuildConduit(BattleState state, PlayerSide side, string conduitId)
    {
        if (!_conduits.TryGetValue(conduitId, out var def))
        {
            return;
        }
        var player = state.Player(side);
        if (player.Conduits.ContainsKey(conduitId)
            || player.Conduits.Count >= _config.ConduitSockets + player.BonusSockets)
        {
            return;
        }
        var cost = def.CostForTier(1);
        if (player.Mana < cost)
        {
            return;
        }

        player.Mana -= cost;
        player.Conduits[conduitId] = 1;
        player.ConduitSpent[conduitId] = cost;
        player.SpireShield += def.SpireShieldPerTier;
        player.ConduitGrafts++;
        if (def.IsArmament)
        {
            player.TurretCooldowns[conduitId] = def.TurretCadenceTicks;
        }
        RecomputeBuffs(player);
    }

    private void TryUpgradeConduit(BattleState state, PlayerSide side, string conduitId)
    {
        if (!_conduits.TryGetValue(conduitId, out var def))
        {
            return;
        }
        var player = state.Player(side);
        if (!player.Conduits.TryGetValue(conduitId, out var tier) || tier >= ConduitDef.MaxTier)
        {
            return;
        }
        var cost = def.CostForTier(tier + 1);
        if (player.Mana < cost)
        {
            return;
        }

        player.Mana -= cost;
        player.Conduits[conduitId] = tier + 1;
        player.ConduitSpent[conduitId] += cost;
        player.SpireShield += def.SpireShieldPerTier;
        player.ConduitGrafts++;
        RecomputeBuffs(player);
    }

    private void TrySellConduit(BattleState state, PlayerSide side, string conduitId)
    {
        var player = state.Player(side);
        if (!player.Conduits.ContainsKey(conduitId))
        {
            return;
        }

        var refund = player.ConduitSpent.GetValueOrDefault(conduitId)
            * (0.5f + player.Buffs.ConduitRefundBonusPct);
        player.Conduits.Remove(conduitId);
        player.ConduitSpent.Remove(conduitId);
        player.TurretCooldowns.Remove(conduitId);
        RecomputeBuffs(player);
        AddMana(player, refund);
    }

    private void RecomputeBuffs(PlayerState player)
    {
        float drip = 0f, cap = 0f, bounty = 0f, damage = 0f, speed = 0f, slow = 0f, breath = 0f;
        float breathDmg = 0f, deployCd = 0f, trickle = 0f, summon = 0f, wrath = 0f;
        float dripPrice = 0f;
        var cadence = 0f;
        var refund = 0f;
        foreach (var (conduitId, tier) in player.Conduits)
        {
            var def = _conduits[conduitId];
            drip += def.DripBonusPerTier * tier;
            cap += def.WalletCapPerTier * tier;
            bounty += def.KillBountyPctPerTier * tier;
            damage += def.DamagePctPerTier * tier;
            speed += def.SpeedPctPerTier * tier;
            slow += def.SlowAuraPctPerTier * tier;
            breath += def.BreathRegenPctPerTier * tier;
            cadence += def.TurretCadencePctPerTier * tier;
        }
        foreach (var pactId in player.SealedPacts)
        {
            var def = _pacts[pactId];
            drip += def.DripBonusPerSecond;
            cap += def.WalletCapBonus;
            bounty += def.KillBountyPct;
            damage += def.DamagePct;
            speed += def.SpeedPct;
            breath += def.BreathRegenPct;
            breathDmg += def.BreathDamagePct;
            deployCd += def.DeployCooldownPct;
            trickle += def.AscensionTricklePct;
            summon += def.SummonCostPct;
            wrath += def.WrathCooldownPct;
            refund += def.ConduitRefundBonusPct;
            cadence += def.TurretCadencePct;
            dripPrice += def.PriceDripPerSecond;
        }
        player.Buffs = new PlayerBuffs
        {
            DripBonusPerSecond = drip,
            WalletCapBonus = cap,
            KillBountyPct = bounty,
            DamagePct = damage,
            SpeedPct = speed,
            SlowAuraPct = slow,
            BreathRegenPct = breath,
            BreathDamagePct = breathDmg,
            DeployCooldownPct = deployCd,
            AscensionTricklePct = trickle,
            SummonCostPct = summon,
            WrathCooldownPct = wrath,
            DripPricePerSecond = dripPrice,
            TurretCadencePct = cadence,
            ConduitRefundBonusPct = refund,
        };
    }

    private void TryBuySocket(BattleState state, PlayerSide side)
    {
        var player = state.Player(side);
        if (player.BonusSockets >= 1
            || player.AscensionTier < _config.SocketPurchaseTierGate
            || player.Mana < _config.SocketPurchaseCost)
        {
            return;
        }
        player.Mana -= _config.SocketPurchaseCost;
        player.BonusSockets++;
    }

    /// <summary>Built armaments auto-fire deterministically: cadence-gated target
    /// selection by lane distance from the firing spire with the InstanceId tiebreak
    /// (same contract as unit targeting — replay/PvP-safe, no per-frame micro).
    /// Machine kills pay bounty but never feed Ascension or edicts — the Court
    /// credits no ascent for machine-work, which is what keeps turtling honest.</summary>
    private void ProcessTurrets(BattleState state)
    {
        foreach (var side in Sides)
        {
            var player = state.Player(side);
            if (player.TurretCooldowns.Count == 0)
            {
                continue;
            }
            foreach (var armamentId in player.TurretCooldowns.Keys.ToList())
            {
                if (!player.Conduits.TryGetValue(armamentId, out var tier))
                {
                    continue;
                }
                var def = _conduits[armamentId];
                var remaining = player.TurretCooldowns[armamentId] - 1;
                if (remaining > 0)
                {
                    player.TurretCooldowns[armamentId] = remaining;
                    continue;
                }
                player.TurretCooldowns[armamentId] = Math.Max(
                    6, (int)(def.TurretCadenceTicks * (1f - player.Buffs.TurretCadencePct)));
                FireTurret(state, side, def, tier);
            }
        }
    }

    private void FireTurret(BattleState state, PlayerSide side, ConduitDef def, int tier)
    {
        var spireX = side == PlayerSide.Left ? 0f : _config.LaneLength;
        SimUnit? primary = null;
        var primaryDistance = float.MaxValue;
        foreach (var unit in state.Units)
        {
            if (unit.Side == side || !unit.IsAlive)
            {
                continue;
            }
            var strataOk = unit.Stratum == Stratum.Air ? def.TargetsAir : def.TargetsGround;
            if (!strataOk)
            {
                continue;
            }
            var distance = MathF.Abs(unit.X - spireX);
            if (distance < def.TurretRangeMin || distance > def.TurretRange)
            {
                continue;
            }
            if (distance < primaryDistance
                || (distance == primaryDistance
                    && unit.InstanceId < (primary?.InstanceId ?? int.MaxValue)))
            {
                primary = unit;
                primaryDistance = distance;
            }
        }
        if (primary is null)
        {
            return;
        }

        var damage = def.TurretDamagePerTier * tier;
        if (def.TurretAoeRadius > 0f)
        {
            var impactX = primary.X;
            foreach (var unit in state.Units.ToList())
            {
                if (unit.Side == side || !unit.IsAlive
                    || (unit.Stratum == Stratum.Air ? !def.TargetsAir : !def.TargetsGround)
                    || MathF.Abs(unit.X - impactX) > def.TurretAoeRadius)
                {
                    continue;
                }
                HitWithTurret(state, side, unit, damage, def);
            }
        }
        else
        {
            HitWithTurret(state, side, primary, damage, def);
        }
    }

    private void HitWithTurret(
        BattleState state, PlayerSide side, SimUnit target, int damage, ConduitDef def)
    {
        ApplyDirectDamage(state, side, target, damage, machineKill: true);
        if (def.OnHitSlowPct > 0f && target.IsAlive)
        {
            target.SlowPct = MathF.Max(target.SlowPct, def.OnHitSlowPct);
            target.SlowTicks = Math.Max(target.SlowTicks, def.OnHitSlowTicks);
        }
    }

    /// <summary>Tier-ups queue parleys; one opens per side as soon as no parley is
    /// already on the table, and an open parley auto-seals its first offer at the
    /// deadline — gameplay never pauses, replays never need an extra command.</summary>
    private void ProcessParleys(BattleState state)
    {
        foreach (var side in Sides)
        {
            var player = state.Player(side);
            if (player.AwaitingParley && state.Tick >= player.ParleyDeadlineTick)
            {
                if (player.PendingOffers.Count > 0)
                {
                    TrySealPact(state, side, player.PendingOffers[0]);
                }
                else
                {
                    player.AwaitingParley = false;
                }
            }
            if (!player.AwaitingParley && player.PendingParleys > 0)
            {
                player.PendingParleys--;
                GenerateOffers(state, side, player.ParleysOpened);
                player.ParleysOpened++;
                player.AwaitingParley = true;
                player.TithesPaidThisParley = 0;
                player.ParleyDeadlineTick = state.Tick + _config.ParleyPickTicks;
            }
        }
    }

    private void GenerateOffers(BattleState state, PlayerSide side, int windowIndex)
    {
        var player = state.Player(side);
        var tier = state.ParleyTierPath[Math.Min(windowIndex, state.ParleyTierPath.Count - 1)];

        var pool = _pacts.Values
            .Where(def => def.Tier == tier && !player.SealedPacts.Contains(def.Id))
            .OrderBy(def => def.Id)
            .ToList();

        var fielded = new HashSet<Element>();
        foreach (var unit in state.Units)
        {
            if (unit.Side == side && unit.IsAlive)
            {
                fielded.Add(unit.Def.Element);
            }
        }

        var relevant = pool
            .Where(def => def.RelevantElement is { } element && fielded.Contains(element))
            .ToList();

        player.PendingOffers.Clear();
        foreach (var pick in PickDistinct(relevant, Math.Min(2, relevant.Count), state.PactRng))
        {
            player.PendingOffers.Add(pick.Id);
        }
        var remaining = pool.Where(def => !player.PendingOffers.Contains(def.Id)).ToList();
        var fill = Math.Min(3 - player.PendingOffers.Count, remaining.Count);
        foreach (var pick in PickDistinct(remaining, fill, state.PactRng))
        {
            player.PendingOffers.Add(pick.Id);
        }
    }

    private static List<PactDef> PickDistinct(List<PactDef> source, int count, SimRng rng)
    {
        var working = new List<PactDef>(source);
        var picks = new List<PactDef>(count);
        for (var i = 0; i < count && working.Count > 0; i++)
        {
            var index = rng.NextInt(working.Count);
            picks.Add(working[index]);
            working.RemoveAt(index);
        }
        return picks;
    }

    private void TrySealPact(BattleState state, PlayerSide side, string pactId)
    {
        var player = state.Player(side);
        if (!player.AwaitingParley || !player.PendingOffers.Contains(pactId))
        {
            return;
        }
        player.SealedPacts.Add(pactId);
        player.PendingOffers.Clear();
        player.AwaitingParley = false;
        RecomputeBuffs(player);
        var sealedPact = _pacts[pactId];
        player.FreeAttunements += sealedPact.FreeAttunements;
        TakeBloodPrice(state, side, sealedPact);
    }

    private void TryPayTithe(BattleState state, PlayerSide side)
    {
        var player = state.Player(side);
        if (!player.AwaitingParley || player.Mana < _config.TitheCostMana)
        {
            return;
        }
        player.Mana -= _config.TitheCostMana;
        player.TithesPaidThisParley++;
        // ParleysOpened already advanced past the open window — redraw the same row.
        GenerateOffers(state, side, player.ParleysOpened - 1);
    }

    /// <summary>One-time Wyrm-pact blood price: the sealing side's spire pays in HP.
    /// Clamped to 1 — the Broker collects, it does not execute.</summary>
    private static void TakeBloodPrice(BattleState state, PlayerSide side, PactDef sealedPact)
    {
        if (sealedPact.PriceSpireHpPct <= 0f)
        {
            return;
        }
        if (side == PlayerSide.Left)
        {
            state.LeftSpireHp = MathF.Max(1f, state.LeftSpireHp * (1f - sealedPact.PriceSpireHpPct));
        }
        else
        {
            state.RightSpireHp = MathF.Max(1f, state.RightSpireHp * (1f - sealedPact.PriceSpireHpPct));
        }
    }

    private void ProcessLastStand(BattleState state)
    {
        if (!_config.LastStandEnabled)
        {
            return;
        }
        var threshold = _config.SpireMaxHp * 0.1f;
        if (!state.Left.LastStandUsed && state.LeftSpireHp < threshold)
        {
            state.Left.LastStandUsed = true;
        }
        if (!state.Right.LastStandUsed && state.RightSpireHp < threshold)
        {
            state.Right.LastStandUsed = true;
        }
    }

    private void TickEconomy(BattleState state)
    {
        var maxTier = Math.Max(state.Left.AscensionTier, state.Right.AscensionTier);
        var ascensionMultiplier = MathF.Pow(_config.AscensionDripEscalation, maxTier - 1);
        var multiplier = DripMultiplier(state.Tick) * ascensionMultiplier;
        foreach (var side in Sides)
        {
            var player = state.Player(side);
            // Wyrm-pact tithes can never starve a side completely — floor before escalation.
            var dripPerSecond = MathF.Max(
                _config.DripFloorPerSecond,
                _config.BaseDripPerSecond
                + player.Buffs.DripBonusPerSecond
                + (player.LastStandUsed ? _config.LastStandDripBonus : 0f)
                - player.Buffs.DripPricePerSecond);
            AddMana(player, dripPerSecond / _config.TickRate * multiplier);

            var breathRegenPerTick = _config.BreathMaxSeconds
                / _config.BreathRechargeSeconds / _config.TickRate
                * (1f + player.Buffs.BreathRegenPct);
            player.BreathEnergySeconds = MathF.Min(
                player.BreathEnergySeconds + breathRegenPerTick, _config.BreathMaxSeconds);
            if (player.WrathCooldownTicks > 0)
            {
                player.WrathCooldownTicks--;
            }

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

    /// <summary>Clamps gains at the effective cap without cutting pre-existing overfill.</summary>
    private static void AddMana(PlayerState player, float amount)
    {
        var cap = MathF.Max(player.Mana, player.EffectiveWalletCap);
        player.Mana = MathF.Min(player.Mana + amount, cap);
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

    /// <summary>Advances each phase-shifter's cycle and toggles its Targetable gate: solid
    /// for the leading window, untargetable (phased, immune) for the trailing
    /// PhaseDurationTicks. Units without the kit are untouched, so the_tithe's submerge gate
    /// is never stomped.</summary>
    private void ProcessPhasing(BattleState state)
    {
        foreach (var unit in state.Units)
        {
            if (!unit.IsAlive || unit.Def.PhaseCadenceTicks <= 0)
            {
                continue;
            }
            unit.PhaseClock = (unit.PhaseClock + 1) % unit.Def.PhaseCadenceTicks;
            var solidTicks = unit.Def.PhaseCadenceTicks - unit.Def.PhaseDurationTicks;
            unit.Targetable = unit.PhaseClock < solidTicks;
        }
    }

    private void ProcessCombat(BattleState state)
    {
        foreach (var unit in state.Units)
        {
            if (!unit.IsAlive)
            {
                continue;
            }
            unit.ContactTriggered = false;   // one-tick view pulse; cleared each tick, set at contact below
            if (unit.IFrameTicks > 0)
            {
                unit.IFrameTicks--;
            }
            if (unit.SlowTicks > 0)
            {
                unit.SlowTicks--;
                if (unit.SlowTicks == 0)
                {
                    unit.SlowPct = 0f;
                }
            }

            if (unit.ElementOverrideTicks > 0)
            {
                unit.ElementOverrideTicks--;
                if (unit.ElementOverrideTicks == 0)
                {
                    unit.ElementOverride = null;
                }
            }

            if (unit.StunTicks > 0)
            {
                // Frozen: tick the timer but advance no attack phase this tick.
                unit.StunTicks--;
                continue;
            }

            if (unit.AttackPhase == AttackPhase.None && HasAnyTargetInBand(state, unit))
            {
                unit.AttackPhase = AttackPhase.Foreswing;
                unit.PhaseTicksLeft = ScaledTicks(state, unit, unit.Def.ForeswingTicks);
                if (unit.Def.FirstStrikeBonusPct > 0f && !unit.HasStruck)
                {
                    // The first message skips the wind-up entirely.
                    unit.PhaseTicksLeft = 1;
                }
            }

            if (unit.Def.VigilDrMaxPct > 0f && unit.AttackPhase != AttackPhase.None)
            {
                unit.VigilTicks++;
            }

            switch (unit.AttackPhase)
            {
                case AttackPhase.Foreswing:
                    unit.PhaseTicksLeft--;
                    if (unit.PhaseTicksLeft <= 0)
                    {
                        ResolveContact(state, unit);
                        unit.ContactTriggered = true;   // surface the contact moment for the view layer
                        unit.AttackPhase = AttackPhase.Backswing;
                        unit.PhaseTicksLeft = ScaledTicks(state, unit, unit.Def.BackswingTicks);
                        if (unit.PhaseTicksLeft <= 0)
                        {
                            CompleteAttackCycle(state, unit);
                        }
                    }
                    break;
                case AttackPhase.Backswing:
                    unit.PhaseTicksLeft--;
                    if (unit.PhaseTicksLeft <= 0)
                    {
                        CompleteAttackCycle(state, unit);
                    }
                    break;
            }
        }

        state.Units.RemoveAll(unit => !unit.IsAlive);
    }

    private void CompleteAttackCycle(BattleState state, SimUnit unit)
    {
        unit.AttackPhase = AttackPhase.None;
        if (unit.Def.StrafeDistance > 0f && unit.IsAlive)
        {
            unit.X = Math.Clamp(
                unit.X + Direction(unit.Side) * unit.Def.StrafeDistance,
                0f, _config.LaneLength);
        }
    }

    /// <summary>Signature-kit field pass: walking auras burn, lingering zones slow and
    /// chip, both on the sim clock — after combat, before turrets.</summary>
    private void ProcessFieldEffects(BattleState state)
    {
        foreach (var unit in state.Units.ToList())
        {
            if (!unit.IsAlive || unit.Def.AuraDamagePerTick <= 0)
            {
                continue;
            }
            foreach (var other in state.Units.ToList())
            {
                if (other.Side == unit.Side || !other.IsAlive
                    || MathF.Abs(other.X - unit.X) > unit.Def.AuraRadius)
                {
                    continue;
                }
                ApplyDirectDamage(state, unit.Side, other, unit.Def.AuraDamagePerTick);
            }
        }

        for (var i = state.Zones.Count - 1; i >= 0; i--)
        {
            var zone = state.Zones[i];
            zone.TicksLeft--;
            foreach (var unit in state.Units.ToList())
            {
                if (unit.Side == zone.Side || !unit.IsAlive || !unit.Targetable
                    || MathF.Abs(unit.X - zone.X) > zone.Radius)
                {
                    continue;
                }
                if (zone.DamagePerTick > 0)
                {
                    ApplyDirectDamage(state, zone.Side, unit, zone.DamagePerTick);
                }
                if (zone.SlowPct >= unit.SlowPct)
                {
                    unit.SlowPct = zone.SlowPct;
                    unit.SlowTicks = Math.Max(unit.SlowTicks, 8);
                }
            }
            if (zone.TicksLeft <= 0)
            {
                state.Zones.RemoveAt(i);
            }
        }
        state.Units.RemoveAll(unit => !unit.IsAlive);
    }

    private static int ScaledTicks(BattleState state, SimUnit unit, int baseTicks)
    {
        var speedPct = state.Player(unit.Side).Buffs.SpeedPct;
        if (unit.Def.Element == Element.Storm)
        {
            speedPct += ElementSynergies.StormAttackSpeedPct[
                ElementSynergies.TierFor(state, unit.Side, Element.Storm)];
        }
        if (speedPct <= 0f || baseTicks <= 0)
        {
            return baseTicks;
        }
        return Math.Max(1, (int)MathF.Round(baseTicks / (1f + speedPct)));
    }

    private void ResolveContact(BattleState state, SimUnit attacker)
    {
        var targets = EnemiesInBand(state, attacker).ToList();
        if (targets.Count > 0)
        {
            if (attacker.Def.ProjectileSpeed > 0f)
            {
                SpawnProjectile(state, attacker, SelectTarget(attacker, targets));
                FinishContact(state, attacker);
                return;
            }
            if (attacker.Def.IsArea)
            {
                foreach (var target in targets)
                {
                    DealDamage(state, attacker, target);
                }
            }
            else
            {
                DealDamage(state, attacker, SelectTarget(attacker, targets));
            }
            SeedZone(state, attacker, targets[0].X);
            ApplyShove(state, attacker);
            FinishContact(state, attacker);
            return;
        }

        if (SpireDistance(state, attacker) is { } spireDistance
            && spireDistance >= attacker.Def.RangeMin
            && spireDistance <= attacker.Def.Range)
        {
            DamageSpire(state, attacker.Side, ScaledDamage(state, attacker));
            ApplyShove(state, attacker);
            FinishContact(state, attacker);
        }
    }

    private void FinishContact(BattleState state, SimUnit attacker)
    {
        attacker.HasStruck = true;
        if (attacker.Def.TollRampPct > 0f)
        {
            attacker.TollCount++;
        }
        if (attacker.Def.DrainManaOnContact > 0 || attacker.Def.EscrowStallOnContact > 0)
        {
            var enemy = state.Player(Opponent(attacker.Side));
            if (attacker.Def.DrainManaOnContact > 0)
            {
                enemy.Mana = MathF.Max(0f, enemy.Mana - attacker.Def.DrainManaOnContact);
            }
            if (attacker.Def.EscrowStallOnContact > 0)
            {
                enemy.EscrowStallTicks =
                    Math.Max(enemy.EscrowStallTicks, attacker.Def.EscrowStallOnContact);
            }
        }
    }

    private void SeedZone(BattleState state, SimUnit attacker, float impactX)
    {
        if (attacker.Def.ZoneRadius <= 0f)
        {
            return;
        }
        state.Zones.Add(new LaneZone
        {
            Side = attacker.Side,
            X = impactX,
            Radius = attacker.Def.ZoneRadius,
            SlowPct = attacker.Def.ZoneSlowPct,
            DamagePerTick = attacker.Def.ZoneDamagePerTick,
            TicksLeft = attacker.Def.ZoneDurationTicks,
        });
        // Cap 3 per side — the lane can be tolled, never tiled.
        var owned = state.Zones.Where(zone => zone.Side == attacker.Side).ToList();
        if (owned.Count > 3)
        {
            state.Zones.Remove(owned[0]);
        }
    }

    private void ApplyShove(BattleState state, SimUnit attacker)
    {
        if (attacker.Def.ShoveDistance <= 0f)
        {
            return;
        }
        var dir = Direction(attacker.Side);
        foreach (var other in state.Units)
        {
            if (other.Side == attacker.Side || !other.IsAlive || other.Def.Unstaggerable
                || !other.Targetable)
            {
                continue;
            }
            var ahead = (other.X - attacker.X) * dir;
            if (ahead <= 0f || ahead > attacker.Def.ShoveRadius)
            {
                continue;
            }
            other.X = Math.Clamp(
                other.X + dir * attacker.Def.ShoveDistance, 0f, _config.LaneLength);
            other.AttackPhase = AttackPhase.None;
            other.PhaseTicksLeft = 0;
            other.VigilTicks = 0;
            other.TollCount = 0;
        }
    }

    private void SpawnProjectile(BattleState state, SimUnit attacker, SimUnit target)
    {
        state.Projectiles.Add(new SimProjectile
        {
            InstanceId = state.NextInstanceId++,
            Side = attacker.Side,
            X = attacker.X,
            VelocityPerTick = Direction(attacker.Side) * attacker.Def.ProjectileSpeed,
            Damage = ScaledDamage(state, attacker, target),
            Element = attacker.Def.Element,
            Pierces = attacker.Def.ProjectilePierces,
            SplashRadius = attacker.Def.ProjectileSplashRadius,
            HitGround = attacker.Def.CanTargetGround,
            HitAir = attacker.Def.CanTargetAir,
        });
    }

    /// <summary>Real-projectile phase: sweep each shot from its old X to its new X, hitting
    /// the first enemy body crossed (body-blockable). Pierce hits every body in the path;
    /// splash applies an AoE at the impact. Despawns on hit (non-pierce) or at lane bounds.</summary>
    private void ProcessProjectiles(BattleState state)
    {
        for (var i = state.Projectiles.Count - 1; i >= 0; i--)
        {
            var proj = state.Projectiles[i];
            var oldX = proj.X;
            var newX = oldX + proj.VelocityPerTick;
            var lo = MathF.Min(oldX, newX);
            var hi = MathF.Max(oldX, newX);

            SimUnit? firstHit = null;
            var firstDist = float.MaxValue;
            foreach (var unit in state.Units)
            {
                if (unit.Side == proj.Side || !unit.IsAlive || !unit.Targetable
                    || !ProjectileCanHit(proj, unit))
                {
                    continue;
                }
                if (unit.X < lo || unit.X > hi || proj.AlreadyHit.Contains(unit.InstanceId))
                {
                    continue;
                }
                if (proj.Pierces)
                {
                    proj.AlreadyHit.Add(unit.InstanceId);
                    ApplyDirectDamage(state, proj.Side, unit, proj.Damage);
                    continue;
                }
                var dist = MathF.Abs(unit.X - oldX);
                if (dist < firstDist)
                {
                    firstDist = dist;
                    firstHit = unit;
                }
            }

            if (!proj.Pierces && firstHit is not null)
            {
                ApplyDirectDamage(state, proj.Side, firstHit, proj.Damage);
                if (proj.SplashRadius > 0f)
                {
                    foreach (var unit in state.Units)
                    {
                        if (unit.Side == proj.Side || !unit.IsAlive
                            || ReferenceEquals(unit, firstHit) || !ProjectileCanHit(proj, unit))
                        {
                            continue;
                        }
                        if (MathF.Abs(unit.X - firstHit.X) <= proj.SplashRadius)
                        {
                            ApplyDirectDamage(state, proj.Side, unit, proj.Damage);
                        }
                    }
                }
                state.Projectiles.RemoveAt(i);
                continue;
            }

            proj.X = newX;
            if (newX < 0f || newX > _config.LaneLength)
            {
                state.Projectiles.RemoveAt(i);
            }
        }

        state.Units.RemoveAll(unit => !unit.IsAlive);
    }

    private static bool ProjectileCanHit(SimProjectile proj, SimUnit unit)
    {
        return unit.Stratum == Stratum.Ground ? proj.HitGround : proj.HitAir;
    }

    private static int ScaledDamage(BattleState state, SimUnit attacker, SimUnit? defender = null)
    {
        var multiplier = 1f + state.Player(attacker.Side).Buffs.DamagePct;

        if (attacker.Def.TollRampPct > 0f)
        {
            multiplier += MathF.Min(
                attacker.Def.TollRampCap, attacker.TollCount * attacker.Def.TollRampPct);
        }
        if (attacker.Def.FirstStrikeBonusPct > 0f && !attacker.HasStruck)
        {
            multiplier += attacker.Def.FirstStrikeBonusPct;
        }
        if (attacker.Def.BonusVsHighHpPct > 0f && defender is not null
            && defender.Def.MaxHp >= attacker.Def.HighHpThreshold)
        {
            multiplier += attacker.Def.BonusVsHighHpPct;
        }

        if (attacker.Def.Element == Element.Fire)
        {
            multiplier += ElementSynergies.FireDamagePct[
                ElementSynergies.TierFor(state, attacker.Side, Element.Fire)];
        }
        if (attacker.Def.Element == Element.Venom
            && defender is not null
            && defender.Hp < defender.Def.MaxHp * ElementSynergies.VenomExecuteHpThreshold)
        {
            multiplier += ElementSynergies.VenomExecutePct[
                ElementSynergies.TierFor(state, attacker.Side, Element.Venom)];
        }

        if (defender is not null)
        {
            var effElement = defender.ElementOverride ?? defender.Def.Element;
            if (attacker.Def.MassiveVsElement == effElement)
            {
                multiplier += 2f;
            }
            else if (attacker.Def.StrongVsElement == effElement)
            {
                multiplier += 0.5f;
            }
        }

        var reduction = 0f;
        if (defender is not null && defender.Def.Element == Element.Stone)
        {
            reduction = ElementSynergies.StoneDamageReductionPct[
                ElementSynergies.TierFor(state, defender.Side, Element.Stone)];
        }
        if (defender is not null && defender.Def.ResistantVsElement == attacker.Def.Element)
        {
            reduction = MathF.Max(reduction, 0.75f);
        }

        return (int)MathF.Round(attacker.Def.Damage * multiplier * (1f - reduction));
    }

    private void DealDamage(BattleState state, SimUnit attacker, SimUnit defender)
    {
        if (defender.IFrameTicks > 0)
        {
            return;
        }

        var preHitHp = defender.Hp;
        var survived = ApplyDirectDamage(
            state, attacker.Side, defender, ScaledDamage(state, attacker, defender));
        if (attacker.Def.LifestealPct > 0f)
        {
            var dealt = Math.Max(0, preHitHp - Math.Max(0, defender.Hp));
            dealt = Math.Min(dealt, preHitHp);
            attacker.Hp = Math.Min(
                attacker.Def.MaxHp,
                attacker.Hp + (int)MathF.Round(dealt * attacker.Def.LifestealPct));
        }
        if (!survived)
        {
            return;
        }

        if (attacker.Def.OverrideTargetElement is { } overrideElement)
        {
            defender.ElementOverride = overrideElement;
            defender.ElementOverrideTicks = attacker.Def.OverrideTargetTicks;
        }

        if (attacker.Def.Element == Element.Frost)
        {
            var frostTier = ElementSynergies.TierFor(state, attacker.Side, Element.Frost);
            var slowPct = ElementSynergies.FrostSlowPct[frostTier];
            if (slowPct > 0f && slowPct >= defender.SlowPct)
            {
                defender.SlowPct = slowPct;
                defender.SlowTicks = ElementSynergies.FrostSlowTicks;
            }
        }

        var def = defender.Def;
        if (def.Unstaggerable)
        {
            return;
        }
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
        defender.VigilTicks = 0;
        defender.TollCount = 0;
    }

    private void DamageSpire(BattleState state, PlayerSide attackerSide, float damage)
    {
        var defender = state.Player(attackerSide == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left);
        var absorbed = MathF.Min(defender.SpireShield, damage);
        defender.SpireShield -= absorbed;
        var remainder = damage - absorbed;
        if (remainder <= 0f)
        {
            return;
        }

        if (attackerSide == PlayerSide.Left)
        {
            state.RightSpireHp -= remainder;
        }
        else
        {
            state.LeftSpireHp -= remainder;
        }

        // Contact is always +EV for someone: chip damage pays the attacker tier-time.
        var chipCredit = remainder * _config.ChipDamageAscensionRate;
        state.Player(attackerSide).AscensionMeter += chipCredit;
        state.Player(attackerSide).AscensionFromChip += chipCredit;
    }

    private void CreditKillAscension(BattleState state, PlayerSide killerSide, UnitDef victim)
    {
        var player = state.Player(killerSide);
        player.Kills++;
        if (player.AscensionTier >= 4)
        {
            return;
        }

        var segmentSize = SegmentSize(player.AscensionTier);
        var segmentCap = segmentSize * _config.KillAscensionCapPct;
        var allowed = MathF.Min(
            _config.KillAscensionPerTier * victim.Tier,
            segmentCap - player.KillMeterThisSegment);
        if (allowed <= 0f)
        {
            return;
        }
        player.AscensionMeter += allowed;
        player.KillMeterThisSegment += allowed;
        player.AscensionFromKills += allowed;
    }

    private float SegmentSize(int tier)
    {
        var thresholds = _config.AscensionThresholds;
        var target = thresholds[tier - 1];
        var previous = tier >= 2 ? thresholds[tier - 2] : 0f;
        return target - previous;
    }

    /// <summary>The Court's trials: first claimant takes the full surge, the
    /// runner-up still collects half — the race stays live without snowballing.
    /// Surges feed the meter (and can cascade straight into a tier-up + parley).</summary>
    private void ProcessEdicts(BattleState state)
    {
        foreach (var edict in state.Edicts)
        {
            foreach (var side in Sides)
            {
                var player = state.Player(side);
                if (edict.ClaimedBy(side)
                    || !Edicts.EdictProgress.Completed(player, edict.Def))
                {
                    continue;
                }
                var isFirst = edict.FirstClaimant is null;
                edict.MarkClaimed(side);
                if (player.AscensionTier >= 4)
                {
                    continue;
                }
                var gap = SegmentSize(edict.TierIndex + 1);
                var surge = gap * _config.EdictSurgePct
                    * (isFirst ? 1f : _config.EdictRunnerUpPct);
                player.AscensionMeter += surge;
                player.AscensionFromEdicts += surge;
            }
        }
    }

    private void ProcessAscension(BattleState state)
    {
        foreach (var side in Sides)
        {
            var player = state.Player(side);
            if (player.EscrowStallTicks > 0)
            {
                player.EscrowStallTicks--;
            }
            if (player.AscensionTier >= 4)
            {
                continue;
            }

            var enemyTier = state.Player(Opponent(side)).AscensionTier;
            var tricklePerSecond = _config.AscensionTricklePerSecond
                * (1f + player.Buffs.AscensionTricklePct);
            if (enemyTier - player.AscensionTier >= 1)
            {
                tricklePerSecond *= _config.TierBehindTrickleMultiplier;
            }
            var laneBonus = HasFrontlinePastMidfield(state, side)
                ? _config.LaneControlBonusPerSecond
                : 0f;
            player.AscensionMeter += (tricklePerSecond + laneBonus) / _config.TickRate;
            player.AscensionFromTrickle += tricklePerSecond / _config.TickRate;
            player.AscensionFromLane += laneBonus / _config.TickRate;

            while (player.AscensionTier < 4
                && player.AscensionMeter >= _config.AscensionThresholds[player.AscensionTier - 1])
            {
                player.AscensionTier++;
                player.KillMeterThisSegment = 0f;
                if (_pacts.Count > 0 && _config.ParleyTiers.Contains(player.AscensionTier))
                {
                    player.PendingParleys++;
                }
            }
        }
    }

    private bool HasFrontlinePastMidfield(BattleState state, PlayerSide side)
    {
        var midfield = _config.LaneLength * 0.5f;
        foreach (var unit in state.Units)
        {
            if (unit.Side != side || !unit.IsAlive)
            {
                continue;
            }
            var past = side == PlayerSide.Left ? unit.X > midfield : unit.X < midfield;
            if (past)
            {
                return true;
            }
        }
        return false;
    }

    private static PlayerSide Opponent(PlayerSide side)
    {
        return side == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left;
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

    /// <summary>Living targetable enemies inside [RangeMin, Range] by ABSOLUTE lane
    /// distance, nearest first. Units engage intruders behind the line too — forward-only
    /// scanning let enemies park at the spire while fresh deploys marched past them.</summary>
    private static IEnumerable<SimUnit> EnemiesInBand(BattleState state, SimUnit unit)
    {
        return state.Units
            .Where(other => other.Side != unit.Side && other.IsAlive && other.Targetable)
            .Where(other => CanTarget(unit, other))
            .Select(other => (Unit: other, Distance: MathF.Abs(other.X - unit.X)))
            .Where(pair => pair.Distance >= unit.Def.RangeMin && pair.Distance <= unit.Def.Range)
            .OrderBy(pair => pair.Distance)
            .ThenBy(pair => pair.Unit.InstanceId)
            .Select(pair => pair.Unit);
    }

    /// <summary>Picks one target from an in-band list (already nearest-first, InstanceId
    /// tie-broken). PrefersAirTarget narrows to air-stratum enemies when any are present;
    /// PrefersFarthestTarget then takes the farthest rather than the nearest.</summary>
    private static SimUnit SelectTarget(SimUnit attacker, List<SimUnit> targets)
    {
        IReadOnlyList<SimUnit> pool = targets;
        if (attacker.Def.PrefersAirTarget)
        {
            var air = targets.Where(t => t.Stratum == Stratum.Air).ToList();
            if (air.Count > 0)
            {
                pool = air;
            }
        }
        return attacker.Def.PrefersFarthestTarget ? pool[^1] : pool[0];
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
            if (!unit.IsAlive || unit.AttackPhase != AttackPhase.None || unit.StunTicks > 0)
            {
                continue;
            }

            var dir = Direction(unit.Side);
            var nearestEnemy = FindNearestTargetableDistance(state, unit);
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

            var step = (unit.MoveSpeedOverride ?? unit.Def.MoveSpeed) / state.Config.TickRate
                * (1f + state.Player(unit.Side).Buffs.SpeedPct);
            if (unit.SlowTicks > 0)
            {
                step *= 1f - unit.SlowPct;
            }
            var enemyPlayer = state.Player(
                unit.Side == PlayerSide.Left ? PlayerSide.Right : PlayerSide.Left);
            if (enemyPlayer.Buffs.SlowAuraPct > 0f && spireDistance <= _config.SlowAuraRange)
            {
                step *= 1f - enemyPlayer.Buffs.SlowAuraPct;
            }

            var maxAdvance = MathF.Max(0f, spireDistance - unit.Def.Range);
            unit.X += MathF.Min(step, maxAdvance) * dir;
            unit.VigilTicks = 0;
        }
    }

    private static float? FindNearestTargetableDistance(BattleState state, SimUnit unit)
    {
        float? nearest = null;
        foreach (var other in state.Units)
        {
            if (other.Side == unit.Side || !other.IsAlive || !other.Targetable)
            {
                continue;
            }
            if (!CanTarget(unit, other))
            {
                continue;
            }
            var distance = MathF.Abs(other.X - unit.X);
            if (nearest is null || distance < nearest)
            {
                nearest = distance;
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

    internal void _TestCreditKillAscension(BattleState state, PlayerSide killerSide, UnitDef victim)
        => CreditKillAscension(state, killerSide, victim);
#endif
    #endregion
}
