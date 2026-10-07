namespace Tokenville.Core;

/// <summary>
/// The simulation state. Construction places bushes, then agents, from one RNG seeded with
/// <see cref="WorldConfig.Seed"/>; entities iterate in ascending id order. The world changes only
/// inside <see cref="Step"/>, which runs the plan's six phases and returns that tick's events.
/// </summary>
public sealed class World
{
    /// <summary>
    /// Extra units on the final step toward an entity so the agent lands inside <see cref="Position.Reach"/>.
    /// A step of exactly <c>d - Reach</c> can end at <c>Reach + 1</c>: <see cref="Position.DistanceTo"/> floors
    /// (under 1 unit) and each truncated component loses under 1 unit (under 1.42 together), after which the next
    /// step length is 1 and truncates to a zero move forever. With 3 the final position is in
    /// <c>[Reach - 3, Reach - 0.58)</c>: always within reach, always at least 253 units from the center and so
    /// outside the target's tile, whose corners are 181 units out.
    /// </summary>
    private const int ArrivalMargin = 3;

    private readonly Random _rng;
    private readonly SortedDictionary<EntityId, Agent> _agents = [];
    private readonly SortedDictionary<EntityId, BerryBush> _bushes = [];
    private readonly SortedDictionary<EntityId, AgentDecision> _pending = [];
    private readonly List<WorldEvent> _events = [];
    private readonly int[] _thresholds;

    public World(WorldConfig config) : this(config, placeEntities: true) { }

    private World(WorldConfig config, bool placeEntities)
    {
        Validate(config);
        Config = config;
        _rng = new Random(config.Seed);
        _thresholds = config.HungerAlertThresholds ?? [50, 80];
        if (!placeEntities) return;

        for (var n = 1; n <= config.BushCount; n++)
            AddBush(DrawBushFreeTileCenter(), config.BushMaxBerries);
        for (var n = 1; n <= config.AgentCount; n++)
            AddAgent(DrawBushFreeTileCenter(), hunger: 0);
    }

    /// <summary>A hand-placed world for tests: ids are assigned in list order, the RNG is untouched.</summary>
    internal static World FromLayout(
        WorldConfig config,
        IEnumerable<(Position Position, int Hunger)> agents,
        IEnumerable<(Position Position, int Berries)> bushes)
    {
        var world = new World(config, placeEntities: false);
        foreach (var (position, berries) in bushes) world.AddBush(position, berries);
        foreach (var (position, hunger) in agents) world.AddAgent(position, hunger);
        return world;
    }

    public WorldConfig Config { get; }
    public long Tick { get; private set; }
    public IReadOnlyCollection<Agent> Agents => _agents.Values;
    public IReadOnlyCollection<BerryBush> Bushes => _bushes.Values;

    /// <summary>Living agents with no current action, ascending by id. Computed on read; the world only changes in <see cref="Step"/>.</summary>
    public IReadOnlyList<EntityId> AgentsAwaitingDecision =>
        _agents.Values.Where(a => a.CurrentAction is null).Select(a => a.Id).ToList();

    /// <summary>
    /// Hands a decision to an agent for the next <see cref="Step"/>. Never throws: a decision for an id that is not a
    /// living agent is dropped, and a second decision for the same agent replaces the first.
    /// </summary>
    public void Submit(EntityId agentId, AgentDecision decision)
    {
        if (_agents.ContainsKey(agentId)) _pending[agentId] = decision;
    }

    /// <summary>Advances the world one tick and returns the events of that tick, in emission order.</summary>
    public IReadOnlyList<WorldEvent> Step()
    {
        _events.Clear();
        StartActions();
        ProgressActions();
        var crossed = GrowHunger();
        RegrowBushes();
        EmitAlerts(crossed);
        Tick++;
        return _events.ToArray();
    }

    // Phase 1

    private void StartActions()
    {
        foreach (var (id, decision) in _pending)
        {
            if (!_agents.TryGetValue(id, out var agent) || agent.CurrentAction is not null) continue;
            var (action, reason) = Validate(agent, decision);
            if (action is null)
            {
                agent.LastAction = new ActionResult(decision.Action, ActionOutcome.Failed, reason);
                Emit(EventKind.ActionFailed, agent, action: decision.Action, reason: reason);
                continue;
            }
            agent.CurrentAction = action;
            Emit(EventKind.ActionStarted, agent, target: action.TargetId, action: action.Kind);
        }
        _pending.Clear();
    }

    private (AgentAction? Action, string? Reason) Validate(Agent agent, AgentDecision d)
    {
        switch (d.Action)
        {
            case ActionKind.Wait:
                return d.Ticks is >= 1 and <= 50
                    ? (new WaitAction(d.Ticks.Value), null)
                    : (null, "ticks must be between 1 and 50");

            case ActionKind.Eat:
            {
                if (d.TargetId is null) return (null, "Eat needs a bush id");
                if (!EntityId.TryParse(d.TargetId, out var id)) return (null, $"unknown target '{d.TargetId}'");
                if (id.Kind != EntityKind.Bush) return (null, $"{id} is not a bush");
                if (!_bushes.TryGetValue(id, out var bush)) return (null, $"unknown target '{d.TargetId}'");
                if (agent.Position.DistanceSquaredTo(bush.Position) > (long)Position.Reach * Position.Reach)
                    return (null, $"{id} is out of reach");
                if (bush.Berries < 1) return (null, "bush is empty");
                return (new EatAction(id, Config.EatTicks), null);
            }

            case ActionKind.MoveTo:
            {
                if (d.TargetId is not null)
                {
                    if (!EntityId.TryParse(d.TargetId, out var id)) return (null, $"unknown target '{d.TargetId}'");
                    IEntity? entity = id.Kind == EntityKind.Bush
                        ? _bushes.GetValueOrDefault(id)
                        : _agents.GetValueOrDefault(id);
                    return entity is null
                        ? (null, $"unknown target '{d.TargetId}'")
                        : (new MoveToAction(entity.Position, id), null);
                }
                if (d.X is null || d.Y is null) return (null, "MoveTo needs a target id or coordinates");
                var (x, y) = (d.X.Value, d.Y.Value);
                if (x < 0 || y < 0 || x >= Config.Width || y >= Config.Height)
                    return (null, $"tile ({x}, {y}) is out of bounds");
                var blocker = BushAt(x, y);
                return blocker is null
                    ? (new MoveToAction(Position.FromTileCenter(x, y), null), null)
                    : (null, $"tile ({x}, {y}) holds {blocker.Id}");
            }

            default:
                return (null, $"unknown action '{d.Action}'");
        }
    }

    // Phase 2

    private void ProgressActions()
    {
        foreach (var agent in _agents.Values)
        {
            switch (agent.CurrentAction)
            {
                case WaitAction w when w.TicksRemaining <= 1:
                    End(agent, ActionOutcome.Completed);
                    break;
                case WaitAction w:
                    agent.CurrentAction = w with { TicksRemaining = w.TicksRemaining - 1 };
                    break;
                case EatAction e when e.TicksRemaining <= 1:
                    FinishEat(agent, _bushes[e.Bush]);
                    break;
                case EatAction e:
                    agent.CurrentAction = e with { TicksRemaining = e.TicksRemaining - 1 };
                    break;
                case MoveToAction m:
                    ProgressMove(agent, m);
                    break;
            }
        }
    }

    private void FinishEat(Agent agent, BerryBush bush)
    {
        if (bush.Berries < 1)
        {
            End(agent, ActionOutcome.Failed, "bush is empty");
            return;
        }
        bush.Berries--;
        agent.Hunger = Math.Max(0, agent.Hunger - Config.BerryNutrition);
        Emit(EventKind.AgentAte, agent, target: bush.Id, value: agent.Hunger);
        End(agent, ActionOutcome.Completed);
    }

    private void ProgressMove(Agent agent, MoveToAction m)
    {
        var toEntity = m.TargetId is not null;
        var d2 = agent.Position.DistanceSquaredTo(m.Target);
        if (toEntity ? d2 <= (long)Position.Reach * Position.Reach : d2 == 0)
        {
            End(agent, ActionOutcome.Completed);
            return;
        }

        var d = agent.Position.DistanceTo(m.Target);
        Position next;
        if (!toEntity && d <= Position.Speed)
        {
            next = m.Target;
        }
        else
        {
            var step = toEntity ? Math.Min(Position.Speed, d - Position.Reach + ArrivalMargin) : Position.Speed;
            long dx = m.Target.X - agent.Position.X;
            long dy = m.Target.Y - agent.Position.Y;
            next = new Position(agent.Position.X + (int)(dx * step / d), agent.Position.Y + (int)(dy * step / d));
        }

        var blocker = BushAt(next.TileX, next.TileY);
        if (blocker is not null)
        {
            End(agent, ActionOutcome.Failed, $"blocked by {blocker.Id}");
            return;
        }

        if (next != agent.Position)
        {
            agent.Position = next;
            Emit(EventKind.AgentMoved, agent);
        }
        if (!toEntity && next == m.Target) End(agent, ActionOutcome.Completed);
    }

    // Phase 3

    private List<(Agent Agent, int Threshold)> GrowHunger()
    {
        var crossed = new List<(Agent, int)>();
        if ((Tick + 1) % Config.HungerTicksPerPoint != 0) return crossed;

        foreach (var agent in _agents.Values.ToList())
        {
            var before = agent.Hunger;
            agent.Hunger++;
            if (agent.Hunger >= 100)
            {
                _agents.Remove(agent.Id);
                agent.CurrentAction = null;
                Emit(EventKind.AgentDied, agent);
                continue;
            }
            foreach (var t in _thresholds)
                if (before < t && agent.Hunger >= t) crossed.Add((agent, t));
        }
        return crossed;
    }

    // Phase 4

    private void RegrowBushes()
    {
        foreach (var bush in _bushes.Values)
        {
            if (bush.Berries >= Config.BushMaxBerries) continue;
            if (++bush.RegrowCounter < Config.BushRegrowTicks) continue;
            bush.RegrowCounter = 0;
            bush.Berries++;
            Emit(EventKind.BushRegrew, bush, value: bush.Berries);
        }
    }

    // Phase 5

    private void EmitAlerts(List<(Agent Agent, int Threshold)> crossed)
    {
        foreach (var (agent, threshold) in crossed)
        {
            Emit(EventKind.HungerThresholdCrossed, agent, value: threshold);
            if (agent.CurrentAction is not null and not EatAction)
                End(agent, ActionOutcome.Interrupted);
        }
    }

    // Helpers

    private void End(Agent agent, ActionOutcome outcome, string? reason = null)
    {
        var kind = agent.CurrentAction!.Kind;
        agent.CurrentAction = null;
        agent.LastAction = new ActionResult(kind, outcome, reason);
        var eventKind = outcome switch
        {
            ActionOutcome.Completed => EventKind.ActionCompleted,
            ActionOutcome.Failed => EventKind.ActionFailed,
            _ => EventKind.ActionInterrupted,
        };
        Emit(eventKind, agent, action: kind, reason: reason);
    }

    private void Emit(EventKind kind, IEntity subject, EntityId? target = null, ActionKind? action = null, int? value = null, string? reason = null) =>
        _events.Add(new WorldEvent(Tick, kind, subject.Id, subject.Position, target, action, value, reason));

    private BerryBush? BushAt(int tileX, int tileY)
    {
        var center = Position.FromTileCenter(tileX, tileY);
        foreach (var bush in _bushes.Values)
            if (bush.Position == center) return bush;
        return null;
    }

    private void AddBush(Position position, int berries)
    {
        var id = new EntityId(EntityKind.Bush, _bushes.Count + 1);
        _bushes.Add(id, new BerryBush(id, position, berries));
    }

    private void AddAgent(Position position, int hunger)
    {
        var id = new EntityId(EntityKind.Agent, _agents.Count + 1);
        _agents.Add(id, new Agent(id, position) { Hunger = hunger });
    }

    /// <summary>Draw order is x then y; both are redrawn if the tile holds a bush. Byte-identical logs depend on this.</summary>
    private Position DrawBushFreeTileCenter()
    {
        while (true)
        {
            var x = _rng.Next(Config.Width);
            var y = _rng.Next(Config.Height);
            if (BushAt(x, y) is null) return Position.FromTileCenter(x, y);
        }
    }

    private static void Validate(WorldConfig c)
    {
        if (c.Width < 1 || c.Height < 1)
            throw new ArgumentException($"Width and Height must be at least 1 (got {c.Width}x{c.Height}).", nameof(c));
        if (c.AgentCount < 0 || c.BushCount < 0)
            throw new ArgumentException($"AgentCount and BushCount must not be negative (got {c.AgentCount}, {c.BushCount}).", nameof(c));
        if (c.HungerTicksPerPoint < 1 || c.EatTicks < 1 || c.BushRegrowTicks < 1)
            throw new ArgumentException("HungerTicksPerPoint, EatTicks and BushRegrowTicks must be at least 1.", nameof(c));

        var tiles = checked(c.Width * c.Height);
        _ = checked(c.Width * Position.TileSize);
        _ = checked(c.Height * Position.TileSize);

        if (c.BushCount > tiles)
            throw new ArgumentException($"BushCount {c.BushCount} exceeds the {tiles} tiles of a {c.Width}x{c.Height} grid.", nameof(c));
        if (c.AgentCount > 0 && c.BushCount == tiles)
            throw new ArgumentException($"AgentCount {c.AgentCount} needs a bush-free tile, but all {tiles} tiles hold bushes.", nameof(c));
    }
}
