namespace Tokenville.Core.Tests;

/// <summary>Builders for hand-placed worlds and decisions, so tests read as the spec scenarios do.</summary>
internal static class TestWorld
{
    /// <summary>9x9, no placement, and hunger effectively frozen so action tests are not disturbed by it.</summary>
    public static readonly WorldConfig Grid9 = new(Width: 9, Height: 9, AgentCount: 0, BushCount: 0, HungerTicksPerPoint: 1_000_000);

    public static EntityId Id(string text) => EntityId.TryParse(text, out var id) ? id : throw new ArgumentException(text);

    public static Position Tile(int x, int y) => Position.FromTileCenter(x, y);

    public static World Layout(
        WorldConfig? config = null,
        (Position Position, int Hunger)[]? agents = null,
        (Position Position, int Berries)[]? bushes = null) =>
        World.FromLayout(config ?? Grid9, agents ?? [], bushes ?? []);

    public static AgentDecision MoveTo(int x, int y) => new(ActionKind.MoveTo, null, x, y, null, null);
    public static AgentDecision MoveTo(string targetId) => new(ActionKind.MoveTo, targetId, null, null, null, null);
    public static AgentDecision Eat(string bushId) => new(ActionKind.Eat, bushId, null, null, null, null);
    public static AgentDecision Wait(int ticks) => new(ActionKind.Wait, null, null, null, ticks, null);

    public static Agent Agent(this World w, string id) => w.Agents.Single(a => a.Id == Id(id));
    public static BerryBush Bush(this World w, string id) => w.Bushes.Single(b => b.Id == Id(id));

    /// <summary>Submits for agent-1 and steps once.</summary>
    public static IReadOnlyList<WorldEvent> SubmitAndStep(this World w, AgentDecision decision, string agentId = "agent-1")
    {
        w.Submit(Id(agentId), decision);
        return w.Step();
    }

    public static IReadOnlyList<WorldEvent> StepTimes(this World w, int n)
    {
        var all = new List<WorldEvent>();
        for (var i = 0; i < n; i++) all.AddRange(w.Step());
        return all;
    }

    /// <summary>Steps until the agent is idle, returning all events; fails the test after <paramref name="max"/> steps.</summary>
    public static IReadOnlyList<WorldEvent> StepUntilIdle(this World w, string agentId = "agent-1", int max = 100)
    {
        var all = new List<WorldEvent>();
        var id = Id(agentId);
        for (var i = 0; i < max; i++)
        {
            all.AddRange(w.Step());
            if (w.Agents.All(a => a.Id != id) || w.Agent(agentId).CurrentAction is null) return all;
        }
        throw new Xunit.Sdk.XunitException($"{agentId} still busy after {max} steps");
    }

    public static IEnumerable<EventKind> Kinds(this IEnumerable<WorldEvent> events) => events.Select(e => e.Kind);
}
