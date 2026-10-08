namespace Tokenville.Core;

/// <summary>
/// What an agent is told about the world when asked for a decision. Part of the engine–brain
/// contract. Positions are tile coordinates and distances are whole tiles; brains never see sub-tile
/// units. Pure data: serializes to JSON in declaration order and compares by value.
/// </summary>
/// <param name="RecentEvents">Events perceived since this agent's last decision. Empty until per-agent event history exists.</param>
public sealed record Observation(
    long Tick,
    AgentSelf Self,
    IReadOnlyList<VisibleBush> Bushes,
    IReadOnlyList<VisibleAgent> Agents,
    IReadOnlyList<WorldEvent> RecentEvents,
    ActionResult? LastAction,
    int WorldWidth,
    int WorldHeight);

/// <summary>The observing agent: its tile, hunger (0 = full, 100 = dead) and the kind of action it is running, if any.</summary>
public sealed record AgentSelf(EntityId Id, string Name, int X, int Y, int Hunger, ActionKind? CurrentAction);

/// <summary>A bush within perception. <see cref="Distance"/> is whole tiles, rounded down; 0 is always within reach.</summary>
public sealed record VisibleBush(EntityId Id, int X, int Y, int Berries, int Distance);

/// <summary>Another agent within perception. <see cref="Distance"/> is whole tiles, rounded down.</summary>
public sealed record VisibleAgent(EntityId Id, string Name, int X, int Y, int Distance, ActionKind? CurrentAction);
