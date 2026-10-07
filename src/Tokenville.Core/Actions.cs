namespace Tokenville.Core;

/// <summary>The three things an agent can do. Part of the engine–brain contract.</summary>
public enum ActionKind
{
    MoveTo,
    Eat,
    Wait,
}

/// <summary>
/// A brain's answer for one agent. Part of the engine–brain contract. <paramref name="X"/> and
/// <paramref name="Y"/> are tile coordinates, never sub-tile units. A bad decision never throws:
/// the action fails at the next <see cref="World.Step"/> with a readable reason.
/// <paramref name="Reason"/> is logged and has no effect on the world.
/// </summary>
public sealed record AgentDecision(
    ActionKind Action, string? TargetId, int? X, int? Y, int? Ticks, string? Reason);

/// <summary>How an action ended. Failures are normal outcomes, never exceptions.</summary>
public enum ActionOutcome
{
    Completed,
    Failed,
    Interrupted,
}

/// <summary>The outcome of an agent's most recent action, kept until the next one ends.</summary>
public sealed record ActionResult(ActionKind Action, ActionOutcome Outcome, string? Reason);

/// <summary>
/// A running action. Pure data: <see cref="World"/> creates one when a decision validates and
/// advances it with <c>with</c> expressions. <see cref="TargetId"/> is the entity the action is
/// about, when it has one.
/// </summary>
public abstract record AgentAction(ActionKind Kind, EntityId? TargetId);

/// <summary>Walk in a straight line to <see cref="Target"/>: a tile center, or an entity's position as it was at the start.</summary>
public sealed record MoveToAction(Position Target, EntityId? TargetId) : AgentAction(ActionKind.MoveTo, TargetId);

/// <summary>Eat one berry from <see cref="Bush"/> after <see cref="TicksRemaining"/> more steps.</summary>
public sealed record EatAction(EntityId Bush, int TicksRemaining) : AgentAction(ActionKind.Eat, Bush);

/// <summary>Do nothing for <see cref="TicksRemaining"/> more steps.</summary>
public sealed record WaitAction(int TicksRemaining) : AgentAction(ActionKind.Wait, null);
