using System.Text.Json;
using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

public class ObservationTests
{
    [Fact]
    public void Serializes_tick_first_and_compares_by_value()
    {
        var self = new AgentSelf(Id("agent-1"), "Ada", 4, 4, 12, null);
        var a = new Observation(7, self, [], [], [], null, 9, 9);
        var b = new Observation(7, self, [], [], [], null, 9, 9);
        Assert.Equal(a, b);
        Assert.StartsWith("{\"Tick\":7,\"Self\":", JsonSerializer.Serialize(a));
    }

    [Fact]
    public void Fresh_agent()
    {
        var w = Layout(agents: [(Tile(4, 4), 12)]);
        w.StepTimes(7);
        var o = w.Observe(Id("agent-1"));
        Assert.Equal(7, o.Tick);
        Assert.Equal(new AgentSelf(Id("agent-1"), "Ada", 4, 4, 12, null), o.Self);
        Assert.Null(o.LastAction);
        Assert.Equal((9, 9), (o.WorldWidth, o.WorldHeight));
        Assert.Empty(o.Bushes);
        Assert.Empty(o.Agents);
    }

    [Fact]
    public void Last_action_and_current_action_are_reported()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(1, 0), 3)]);
        w.SubmitAndStep(MoveTo(2, 0));
        var o = w.Observe(Id("agent-1"));
        Assert.Equal(new ActionResult(ActionKind.MoveTo, ActionOutcome.Failed, "blocked by bush-1"), o.LastAction);
        Assert.Null(o.Self.CurrentAction);

        w.SubmitAndStep(Wait(5));
        Assert.Equal(ActionKind.Wait, w.Observe(Id("agent-1")).Self.CurrentAction);
    }

    [Fact]
    public void Observing_is_read_only()
    {
        var w = new World(new WorldConfig());
        var tick = w.Tick;
        var first = w.Observe(Id("agent-1"));
        var second = w.Observe(Id("agent-1"));
        Assert.Equal(first.Self, second.Self);
        Assert.Equal(first.Bushes, second.Bushes);
        Assert.Equal(first.Agents, second.Agents);
        Assert.Equal(tick, w.Tick);
        Assert.Equal(6, w.Agents.Count);
    }

    [Fact]
    public void Dead_agent_throws()
    {
        var w = Layout(agents: [(Tile(0, 0), 99)], config: Grid9 with { HungerTicksPerPoint = 1 });
        w.Step();
        Assert.Empty(w.Agents);
        var ex = Assert.Throws<ArgumentException>(() => w.Observe(Id("agent-1")));
        Assert.Contains("agent-1", ex.Message);
        Assert.Contains("agent-42", Assert.Throws<ArgumentException>(() => w.Observe(Id("agent-42"))).Message);
    }

    [Fact]
    public void Sub_tile_position_rounds_to_its_tile()
    {
        var w = Layout(agents: [(new Position(700, 300), 0)]);
        var o = w.Observe(Id("agent-1"));
        Assert.Equal((2, 1), (o.Self.X, o.Self.Y));
    }

    [Fact]
    public void Recent_events_are_empty_for_now()
    {
        var w = Layout(agents: [(Tile(0, 0), 40)], bushes: [(Tile(1, 1), 3)]);
        w.SubmitAndStep(MoveTo(0, 1));
        w.SubmitAndStep(Eat("bush-1"));
        w.StepUntilIdle();
        Assert.Equal(ActionKind.Eat, w.Agent("agent-1").LastAction!.Action);
        Assert.Empty(w.Observe(Id("agent-1")).RecentEvents);
    }

    [Fact]
    public void Inside_and_outside_the_radius()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(5, 0), 5), (Tile(3, 4), 5), (Tile(6, 0), 5)]);
        var o = w.Observe(Id("agent-1"));
        Assert.Equal(
            [new VisibleBush(Id("bush-1"), 5, 0, 5, 5), new VisibleBush(Id("bush-2"), 3, 4, 5, 5)],
            o.Bushes);
    }

    [Fact]
    public void Self_is_excluded()
    {
        var w = Layout(agents: [(Tile(2, 2), 0), (Tile(2, 2), 7)]);
        var o = w.Observe(Id("agent-1"));
        Assert.Equal([new VisibleAgent(Id("agent-2"), "Bram", 2, 2, 0, null)], o.Agents);
    }

    [Fact]
    public void Empty_bush_is_visible()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(2, 0), 0)]);
        var bush = Assert.Single(w.Observe(Id("agent-1")).Bushes);
        Assert.Equal((0, 2), (bush.Berries, bush.Distance));
    }

    [Fact]
    public void Adjacent_diagonal_bush_shows_distance_1()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(1, 1), 1)]);
        Assert.Equal(1, Assert.Single(w.Observe(Id("agent-1")).Bushes).Distance);
    }
}
