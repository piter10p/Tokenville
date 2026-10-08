using Tokenville.Core;

namespace Tokenville.Brains;

/// <summary>
/// The Phase 1 policy: step aside after a blocked move, eat from the nearest visible bush with berries
/// when hunger is 30 or more (walking there first if needed), otherwise wander to a random bush-free tile.
/// One instance per agent, seeded from the world seed plus the agent number so runs are reproducible.
/// Decides from the observation alone.
/// </summary>
public sealed class ScriptedBrain(int seed) : IAgentBrain
{
    private const int HungryAt = 30;

    private readonly Random _rng = new(seed);

    public Task<AgentDecision> DecideAsync(Observation observation, CancellationToken ct) =>
        Task.FromResult(Decide(observation));

    private AgentDecision Decide(Observation o)
    {
        if (o.LastAction is { Outcome: ActionOutcome.Failed, Reason: { } reason } && reason.StartsWith("blocked", StringComparison.Ordinal))
            return Sidestep(o);

        if (o.Self.Hunger >= HungryAt)
        {
            // Bushes arrive in ascending id order and OrderBy is stable, so ties go to the lowest id.
            var food = o.Bushes.Where(b => b.Berries > 0).OrderBy(b => b.Distance).FirstOrDefault();
            if (food is not null)
            {
                // Tile distance 1 spans 256..511 units and reach is 256, so distance 1 may or may not be in reach.
                // Try to eat at distance <= 1; if the engine says "out of reach", walk there next tick. The walk
                // ends within reach, so the eat after it succeeds.
                var outOfReach = o.LastAction is { Outcome: ActionOutcome.Failed, Reason: { } r } && r.EndsWith("out of reach", StringComparison.Ordinal);
                return food.Distance <= 1 && !outOfReach
                    ? new AgentDecision(ActionKind.Eat, food.Id.ToString(), null, null, null, "hungry, food in reach")
                    : new AgentDecision(ActionKind.MoveTo, food.Id.ToString(), null, null, null, "hungry, walking to food");
            }
        }

        return Wander(o);
    }

    private AgentDecision Sidestep(Observation o)
    {
        var (x, y) = (o.Self.X, o.Self.Y);
        var free = new List<(int X, int Y)>(4);
        foreach (var (nx, ny) in new[] { (x, y + 1), (x, y - 1), (x + 1, y), (x - 1, y) })
            if (nx >= 0 && ny >= 0 && nx < o.WorldWidth && ny < o.WorldHeight && !BushAt(o, nx, ny))
                free.Add((nx, ny));
        if (free.Count == 0)
            return new AgentDecision(ActionKind.Wait, null, null, null, 1, "blocked and boxed in");
        var (sx, sy) = free[_rng.Next(free.Count)];
        return new AgentDecision(ActionKind.MoveTo, null, sx, sy, null, "stepping aside after a blocked move");
    }

    private AgentDecision Wander(Observation o)
    {
        // ponytail: redraw until the tile holds no visible bush; at most ~81 visible tiles can be bushes, so this
        // ends fast. Unseen bushes are the engine's problem: the move fails at start and we redraw next tick.
        int x, y;
        do
        {
            x = _rng.Next(o.WorldWidth);
            y = _rng.Next(o.WorldHeight);
        } while (BushAt(o, x, y));
        return new AgentDecision(ActionKind.MoveTo, null, x, y, null, "wandering");
    }

    private static bool BushAt(Observation o, int x, int y) => o.Bushes.Any(b => b.X == x && b.Y == y);
}
