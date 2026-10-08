using Tokenville.Core;

namespace Tokenville.Brains;

/// <summary>
/// The plan's model-free brain: eat from the nearest bush with berries when hungry, otherwise wander to a random
/// bush-free tile. One instance per agent, each with its own seeded RNG. Reads <see cref="World"/> directly until
/// <c>Observation</c> exists (Phase 2), so it sees every bush; the policy ports unchanged.
/// </summary>
public sealed class ScriptedBrain
{
    /// <summary>Hunger at or above which the agent goes for food. Fixed by the plan, not config.</summary>
    public const int HungerThreshold = 30;

    private const long ReachSquared = (long)Position.Reach * Position.Reach;
    private readonly Random _rng;

    /// <summary>
    /// Seeds the RNG as <c>worldSeed * 1000 + agentNumber</c>, unchecked. An explicit formula, never
    /// <c>HashCode.Combine</c>, which is randomized per process and would break determinism silently.
    /// </summary>
    public ScriptedBrain(int worldSeed, int agentNumber) =>
        _rng = new Random(unchecked(worldSeed * 1000 + agentNumber));

    /// <summary>Rules in priority order: sidestep after a blocked move, go for food when hungry, otherwise wander.</summary>
    public AgentDecision Decide(World world, Agent agent)
    {
        if (agent.LastAction is { Outcome: ActionOutcome.Failed, Reason: { } reason }
            && reason.StartsWith("blocked by", StringComparison.Ordinal))
            return Sidestep(world, agent);

        if (agent.Hunger >= HungerThreshold && NearestWithBerries(world, agent) is { } bush)
            return agent.Position.DistanceSquaredTo(bush.Position) <= ReachSquared
                ? new AgentDecision(ActionKind.Eat, bush.Id.ToString(), null, null, null, "eat")
                : new AgentDecision(ActionKind.MoveTo, bush.Id.ToString(), null, null, null, "hungry");

        return Wander(world);
    }

    /// <summary>First free tile of below, above, right, left. Fixed order so the sidestep never touches the RNG.</summary>
    private static AgentDecision Sidestep(World world, Agent agent)
    {
        var (x, y) = (agent.Position.TileX, agent.Position.TileY);
        foreach (var (nx, ny) in new[] { (x, y + 1), (x, y - 1), (x + 1, y), (x - 1, y) })
            if (nx >= 0 && ny >= 0 && nx < world.Config.Width && ny < world.Config.Height && !HasBush(world, nx, ny))
                return new AgentDecision(ActionKind.MoveTo, null, nx, ny, null, "sidestep");
        return new AgentDecision(ActionKind.Wait, null, null, null, 1, "boxed-in");
    }

    /// <summary>Ascending-id scan with a strict comparison, so ties go to the lower id.</summary>
    private static BerryBush? NearestWithBerries(World world, Agent agent)
    {
        BerryBush? best = null;
        var bestD2 = long.MaxValue;
        foreach (var bush in world.Bushes)
        {
            if (bush.Berries < 1) continue;
            var d2 = agent.Position.DistanceSquaredTo(bush.Position);
            if (d2 < bestD2) (best, bestD2) = (bush, d2);
        }
        return best;
    }

    /// <summary>Draw x then y, redraw while the tile holds a bush, as the world's own placement does. The world guarantees a free tile.</summary>
    private AgentDecision Wander(World world)
    {
        while (true)
        {
            var x = _rng.Next(world.Config.Width);
            var y = _rng.Next(world.Config.Height);
            if (!HasBush(world, x, y)) return new AgentDecision(ActionKind.MoveTo, null, x, y, null, "wander");
        }
    }

    private static bool HasBush(World world, int tileX, int tileY)
    {
        var center = Position.FromTileCenter(tileX, tileY);
        foreach (var bush in world.Bushes)
            if (bush.Position == center) return true;
        return false;
    }
}
