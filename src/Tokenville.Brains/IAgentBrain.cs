using Tokenville.Core;

namespace Tokenville.Brains;

/// <summary>
/// Decides what one agent does next from what it perceives. The engine never calls a brain; the
/// runner observes, awaits every brain, submits every decision, then steps the world.
/// </summary>
public interface IAgentBrain
{
    /// <summary>Returns a decision for the observed agent. A bad decision never throws: it fails at the next step with a reason.</summary>
    Task<AgentDecision> DecideAsync(Observation observation, CancellationToken ct);
}
