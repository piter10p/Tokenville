using Tokenville.Brains;

namespace Tokenville.Core.Tests;

/// <summary>Phase 1 acceptance: scripted agents driven through the real contract path, in lockstep.</summary>
public class AcceptanceTests
{
    /// <summary>Runs <paramref name="ticks"/> steps of the runner's lockstep loop, returning every step's events.</summary>
    private static List<IReadOnlyList<WorldEvent>> Run(World w, int ticks, Action<World>? afterStep = null)
    {
        var brains = w.Agents.ToDictionary(a => a.Id, a => (IAgentBrain)new ScriptedBrain(w.Config.Seed + a.Id.Number));
        var log = new List<IReadOnlyList<WorldEvent>>(ticks);
        for (var t = 0; t < ticks; t++)
        {
            foreach (var id in w.AgentsAwaitingDecision)
                w.Submit(id, brains[id].DecideAsync(w.Observe(id), CancellationToken.None).GetAwaiter().GetResult());
            log.Add(w.Step());
            afterStep?.Invoke(w);
        }
        return log;
    }

    [Theory]
    [InlineData(8, true)]
    [InlineData(3, false)]
    public void Six_scripted_agents_for_2000_ticks(int bushCount, bool allSurvive)
    {
        var w = new World(new WorldConfig(BushCount: bushCount));
        var bushTiles = w.Bushes.Select(b => (b.Position.TileX, b.Position.TileY)).ToHashSet();

        Run(w, 2000, afterStep: world =>
        {
            foreach (var a in world.Agents)
                Assert.False(bushTiles.Contains((a.Position.TileX, a.Position.TileY)), $"{a.Id} ended tick {world.Tick - 1} inside a bush tile");
        });

        if (allSurvive) Assert.Equal(6, w.Agents.Count);
        else Assert.True(w.Agents.Count < 6, "expected at least one death with 3 bushes");
    }

    [Fact]
    public void Scripted_runs_are_deterministic()
    {
        var a = Run(new World(new WorldConfig()), 500);
        var b = Run(new World(new WorldConfig()), 500);
        Assert.Equal(a.Count, b.Count);
        for (var t = 0; t < a.Count; t++)
            Assert.Equal(a[t], b[t]);
    }
}
