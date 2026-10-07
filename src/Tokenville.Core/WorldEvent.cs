namespace Tokenville.Core;

/// <summary>Everything the engine reports. The order here is the order kinds are documented in the plan.</summary>
public enum EventKind
{
    AgentMoved,
    ActionStarted,
    ActionCompleted,
    ActionFailed,
    ActionInterrupted,
    AgentAte,
    BushRegrew,
    HungerThresholdCrossed,
    AgentDied,
}

/// <summary>
/// One thing that happened during a step. <see cref="Position"/> is the subject's position at that
/// moment in exact sub-tile units (for <see cref="EventKind.AgentMoved"/>, the new position).
/// <see cref="Target"/> is the action's target for <see cref="EventKind.ActionStarted"/> and the bush
/// for <see cref="EventKind.AgentAte"/>. <see cref="Action"/> is set on the four action events.
/// <see cref="Value"/> is the agent's hunger after <see cref="EventKind.AgentAte"/>, the berry count
/// after <see cref="EventKind.BushRegrew"/>, and the threshold for <see cref="EventKind.HungerThresholdCrossed"/>.
/// <see cref="Reason"/> is set on <see cref="EventKind.ActionFailed"/>.
/// </summary>
public sealed record WorldEvent(
    long Tick,
    EventKind Kind,
    EntityId Subject,
    Position Position,
    EntityId? Target = null,
    ActionKind? Action = null,
    int? Value = null,
    string? Reason = null);
