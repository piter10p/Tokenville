using Tokenville.Runner;
using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

public class ConsoleViewTests
{
    private static readonly WorldConfig Grid3 = Grid9 with { Width = 3, Height = 3 };
    private static readonly IReadOnlyDictionary<EntityId, (string Name, long Tick)> NoDeaths = new Dictionary<EntityId, (string, long)>();

    private static string[] Lines(string text) => text.TrimEnd('\n').Split('\n');

    [Fact]
    public void Small_map()
    {
        var w = Layout(Grid3, agents: [(Tile(0, 0), 0), (Tile(2, 2), 0)], bushes: [(Tile(1, 1), 4)]);
        var lines = Lines(ConsoleView.Render(w, NoDeaths, 2));
        Assert.Equal(["A..", ".4.", "..B"], lines[..3]);
        Assert.Equal("", lines[3]);
    }

    [Fact]
    public void Shared_tile_shows_the_lower_id()
    {
        var w = Layout(Grid3, agents: [(Tile(0, 0), 0), (Tile(0, 0), 0)]);
        Assert.Equal("A..", Lines(ConsoleView.Render(w, NoDeaths, 2))[0]);
    }

    [Fact]
    public void Berries_above_nine_render_as_nine()
    {
        var w = Layout(Grid3, bushes: [(Tile(0, 0), 12)]);
        Assert.Equal("9..", Lines(ConsoleView.Render(w, NoDeaths, 0))[0]);
    }

    [Fact]
    public void Living_agent_line()
    {
        var w = Layout(new WorldConfig(AgentCount: 0, BushCount: 0), agents: [(Tile(12, 7), 34)]);
        w.Agent("agent-1").CurrentAction = new EatAction(Id("bush-3"), 2);
        var line = Lines(ConsoleView.Render(w, NoDeaths, 1))[^1];
        Assert.Contains("agent-1", line);
        Assert.Contains("Ada", line);
        Assert.Contains("(12, 7)", line);
        Assert.Contains("34", line);
        Assert.Contains("Eat bush-3", line);
    }

    [Fact]
    public void Dead_agent_line_sorts_by_id()
    {
        var w = Layout(Grid3, agents: [(Tile(0, 0), 0), (Tile(1, 0), 0), (Tile(2, 0), 0), (Tile(0, 1), 0), (Tile(1, 1), 0)]);
        var deaths = new Dictionary<EntityId, (string, long)> { [Id("agent-4")] = ("Dov", 312) };
        var lines = Lines(ConsoleView.Render(w, deaths, 6));
        var dead = lines.Single(l => l.StartsWith("agent-4", StringComparison.Ordinal));
        Assert.Contains("died at tick 312", dead);
        Assert.Equal(["agent-1", "agent-2", "agent-3", "agent-4", "agent-5"], lines[^5..].Select(l => l.Split(' ')[0]));
    }

    [Fact]
    public void Header()
    {
        var frozen = new WorldConfig(AgentCount: 0, BushCount: 0, HungerTicksPerPoint: 1_000_000, BushRegrowTicks: 1_000_000);
        var w = Layout(frozen,
            agents: [(Tile(0, 0), 0), (Tile(1, 0), 0), (Tile(2, 0), 0), (Tile(3, 0), 0), (Tile(4, 0), 0)],
            bushes: [(Tile(0, 5), 3), (Tile(1, 5), 5), (Tile(2, 5), 0)]);
        w.StepTimes(250);
        var header = Lines(ConsoleView.Render(w, NoDeaths, 6)).Single(l => l.StartsWith("tick ", StringComparison.Ordinal));
        Assert.Contains("tick 250", header);
        Assert.Contains("alive 5/6", header);
        Assert.Contains("berries 8", header);
    }

    [Fact]
    public void Action_descriptions()
    {
        var w = Layout(Grid3, agents: [(Tile(0, 0), 0), (Tile(1, 0), 0), (Tile(2, 0), 0), (Tile(0, 1), 0)], bushes: [(Tile(2, 2), 1)]);
        w.Agent("agent-2").CurrentAction = new WaitAction(3);
        w.Agent("agent-3").CurrentAction = new MoveToAction(Tile(2, 2), Id("bush-1"));
        w.Agent("agent-4").CurrentAction = new MoveToAction(Tile(0, 2), null);
        var lines = Lines(ConsoleView.Render(w, NoDeaths, 4))[^4..];
        Assert.EndsWith("idle", lines[0]);
        Assert.EndsWith("Wait 3", lines[1]);
        Assert.EndsWith("MoveTo bush-1", lines[2]);
        Assert.EndsWith("MoveTo (0, 2)", lines[3]);
    }
}
