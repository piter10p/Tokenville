using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

/// <summary>
/// Smoke test of the whole engine on the plan's calibration: a simple greedy policy (not the scripted
/// brain) keeps six agents alive for 2,000 ticks on 8 bushes and loses at least one on 3.
/// </summary>
public class CalibrationTests
{
    private const long ReachSquared = (long)Position.Reach * Position.Reach;

    [Theory]
    [InlineData(8, true)]
    [InlineData(3, false)]
    public void Six_agents_for_2000_ticks(int bushCount, bool allSurvive)
    {
        var w = new World(new WorldConfig(BushCount: bushCount));
        var bushTiles = w.Bushes.Select(b => (b.Position.TileX, b.Position.TileY)).ToHashSet();

        for (var tick = 0; tick < 2000; tick++)
        {
            foreach (var id in w.AgentsAwaitingDecision)
                w.Submit(id, Decide(w, w.Agent(id.ToString())));
            w.Step();
            foreach (var a in w.Agents)
                Assert.False(bushTiles.Contains((a.Position.TileX, a.Position.TileY)), $"{a.Id} ended tick {w.Tick - 1} inside a bush tile");
        }

        if (allSurvive) Assert.Equal(6, w.Agents.Count);
        else Assert.True(w.Agents.Count < 6, "expected at least one death with 3 bushes");
    }

    /// <summary>
    /// Eat the nearest in-reach bush with berries when hungry, else walk to the nearest one with berries, else wait.
    /// A blocked move (a bush tile on the straight line) is answered by one step aside, since there is no pathfinding.
    /// </summary>
    private static AgentDecision Decide(World w, Agent agent)
    {
        if (agent.LastAction is { Outcome: ActionOutcome.Failed, Reason: { } r } && r.StartsWith("blocked", StringComparison.Ordinal))
        {
            var (x, y) = (agent.Position.TileX, agent.Position.TileY);
            foreach (var (nx, ny) in new[] { (x, y + 1), (x, y - 1), (x + 1, y), (x - 1, y) })
                if (nx >= 0 && ny >= 0 && nx < w.Config.Width && ny < w.Config.Height && w.Bushes.All(b => (b.Position.TileX, b.Position.TileY) != (nx, ny)))
                    return MoveTo(nx, ny);
        }
        if (agent.Hunger < 30) return Wait(5);
        var nearest = w.Bushes.Where(b => b.Berries > 0).OrderBy(b => agent.Position.DistanceSquaredTo(b.Position)).FirstOrDefault();
        if (nearest is null) return Wait(1);
        return agent.Position.DistanceSquaredTo(nearest.Position) <= ReachSquared
            ? Eat(nearest.Id.ToString())
            : MoveTo(nearest.Id.ToString());
    }
}
