namespace DraconicWars.Game.Battle;

using System;
using System.Collections.Generic;
using DraconicWars.Sim.Battle;
using DraconicWars.Sim.Conduits;
using DraconicWars.Sim.Units;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Owns the deterministic sim inside the scene tree: accumulates wall time into
/// fixed 30tps Advance() calls and exposes spawn/death diffs for the view layer.
/// The sim never sees Godot; the view never mutates the sim.
/// </summary>
public partial class BattleRunner : Node
{
    public event Action<SimUnit> UnitSpawned = delegate { };

    public event Action<int, float, Stratum, PlayerSide> UnitDied = delegate { };

    public event Action TickAdvanced = delegate { };

    public BattleSim Sim { get; private set; } = null!;

    public BattleState State { get; private set; } = null!;

    public bool Paused { get; set; }

    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Optional authored enemy script (campaign); feeds Right-side commands.</summary>
    public Campaign.WaveDirector? Director { get; set; }

    private readonly List<SimCommand> _pendingCommands = new();
    private readonly Dictionary<int, SimUnit> _knownUnits = new();
    private float _accumulator;

    public void Initialize(
        BattleConfig config,
        IEnumerable<UnitDef> defs,
        IEnumerable<ConduitDef> conduits,
        ulong seed,
        IEnumerable<DraconicWars.Sim.Pacts.PactDef>? pacts = null)
    {
        Sim = new BattleSim(config, defs, conduits, pacts);
        State = Sim.CreateInitialState(seed);
        _knownUnits.Clear();
        _accumulator = 0f;
        JmoLogger.Info(this, $"[Battle] Initialized (seed {seed})");
    }

    public void EnqueueCommand(SimCommand command)
    {
        _pendingCommands.Add(command);
    }

    // _Process (not _PhysicsProcess): the sim carries its own fixed-tick accumulator,
    // so it needs wall delta only — and test runners pump process frames.
    public override void _Process(double delta)
    {
        if (State is null || Paused || State.Outcome != BattleOutcome.Ongoing)
        {
            return;
        }

        _accumulator += (float)delta * SpeedMultiplier;
        var tickDuration = 1f / State.Config.TickRate;
        while (_accumulator >= tickDuration)
        {
            _accumulator -= tickDuration;
            StepOneTick();
        }
    }

    private void StepOneTick()
    {
        var outcomeBefore = State.Outcome;
        if (Director is { } director)
        {
            _pendingCommands.AddRange(director.CommandsForTick(State.Tick));
        }
        Sim.Advance(State, _pendingCommands);
        _pendingCommands.Clear();
        DiffUnits();
        TickAdvanced();
        if (outcomeBefore == BattleOutcome.Ongoing && State.Outcome != BattleOutcome.Ongoing)
        {
            JmoLogger.Info(this, $"[Battle] Outcome: {State.Outcome} at tick {State.Tick}");
        }
    }

    private void DiffUnits()
    {
        foreach (var unit in State.Units)
        {
            if (_knownUnits.ContainsKey(unit.InstanceId))
            {
                continue;
            }
            _knownUnits[unit.InstanceId] = unit;
            UnitSpawned(unit);
        }

        if (_knownUnits.Count == State.Units.Count)
        {
            return;
        }
        var alive = new HashSet<int>();
        foreach (var unit in State.Units)
        {
            alive.Add(unit.InstanceId);
        }
        foreach (var (instanceId, unit) in _knownUnits)
        {
            if (!alive.Contains(instanceId))
            {
                UnitDied(instanceId, unit.X, unit.Stratum, unit.Side);
            }
        }
        _knownUnits.Clear();
        foreach (var unit in State.Units)
        {
            _knownUnits[unit.InstanceId] = unit;
        }
    }
}
