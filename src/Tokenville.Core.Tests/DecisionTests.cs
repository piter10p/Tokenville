using System.Text.Json;
using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

public class DecisionTests
{
    [Fact]
    public void Events_are_value_equal_and_serialize_tick_first()
    {
        var a = new WorldEvent(3, EventKind.AgentAte, Id("agent-1"), Tile(1, 1), Id("bush-1"), null, 15);
        var b = new WorldEvent(3, EventKind.AgentAte, Id("agent-1"), Tile(1, 1), Id("bush-1"), null, 15);
        Assert.Equal(a, b);
        Assert.StartsWith("{\"Tick\":3,\"Kind\":", JsonSerializer.Serialize(a));
    }

    [Fact]
    public void FromLayout_assigns_ids_in_list_order_with_given_state()
    {
        var w = Layout(agents: [(Tile(0, 0), 10), (Tile(2, 2), 20)], bushes: [(Tile(5, 5), 3)]);
        Assert.Equal(["agent-1", "agent-2"], w.Agents.Select(a => a.Id.ToString()));
        Assert.Equal([10, 20], w.Agents.Select(a => a.Hunger));
        Assert.Equal([Tile(0, 0), Tile(2, 2)], w.Agents.Select(a => a.Position));
        var bush = Assert.Single(w.Bushes);
        Assert.Equal("bush-1", bush.Id.ToString());
        Assert.Equal(3, bush.Berries);
        Assert.Equal(Tile(5, 5), bush.Position);
    }

    [Fact]
    public void Last_decision_wins()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        w.Submit(Id("agent-1"), Wait(3));
        w.Submit(Id("agent-1"), Wait(7));
        var events = w.Step();
        Assert.Single(events, e => e.Kind == EventKind.ActionStarted);
        Assert.Equal(6, Assert.IsType<WaitAction>(w.Agent("agent-1").CurrentAction).TicksRemaining);
    }

    [Fact]
    public void Unknown_agent_is_ignored_without_throwing()
    {
        var w = new World(new WorldConfig());
        w.Submit(Id("agent-99"), Wait(1));
        var events = w.Step();
        Assert.DoesNotContain(events, e => e.Subject == Id("agent-99"));
    }

    [Fact]
    public void Garbage_decision_fails_with_reason_instead_of_throwing()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        var e = Assert.Single(w.SubmitAndStep(new AgentDecision(ActionKind.MoveTo, null, null, null, null, null)));
        Assert.Equal(EventKind.ActionFailed, e.Kind);
        Assert.Equal("MoveTo needs a target id or coordinates", e.Reason);
    }

    [Fact]
    public void Fresh_world_lists_every_agent_in_order()
    {
        var w = Layout(agents: [(Tile(0, 0), 0), (Tile(1, 0), 0), (Tile(2, 0), 0)]);
        Assert.Equal([Id("agent-1"), Id("agent-2"), Id("agent-3")], w.AgentsAwaitingDecision);
    }

    [Fact]
    public void Running_action_is_not_awaiting_and_ended_action_is()
    {
        var w = Layout(agents: [(Tile(0, 0), 0), (Tile(1, 0), 0)]);
        w.Submit(Id("agent-1"), Wait(5));
        w.Submit(Id("agent-2"), Wait(1));
        w.Step();
        Assert.Equal([Id("agent-2")], w.AgentsAwaitingDecision);
    }

    [Theory]
    [InlineData(32, 5, "tile (32, 5) is out of bounds")]
    [InlineData(-1, 0, "tile (-1, 0) is out of bounds")]
    [InlineData(4, 4, "tile (4, 4) holds bush-1")]
    public void Invalid_move_targets_fail(int x, int y, string reason)
    {
        var w = Layout(new WorldConfig(AgentCount: 0, BushCount: 0), agents: [(Tile(0, 0), 0)], bushes: [(Tile(4, 4), 5)]);
        var e = Assert.Single(w.SubmitAndStep(MoveTo(x, y)));
        Assert.Equal(EventKind.ActionFailed, e.Kind);
        Assert.Equal(ActionKind.MoveTo, e.Action);
        Assert.Equal(reason, e.Reason);
        Assert.Null(w.Agent("agent-1").CurrentAction);
        Assert.Equal(new ActionResult(ActionKind.MoveTo, ActionOutcome.Failed, reason), w.Agent("agent-1").LastAction);
    }

    [Theory]
    [InlineData("bush-42")]
    [InlineData("tree-1")]
    [InlineData("agent-7")]
    public void Unknown_target_id_fails_naming_it(string target)
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(4, 4), 5), (Tile(5, 5), 5), (Tile(6, 6), 5)]);
        var e = Assert.Single(w.SubmitAndStep(MoveTo(target)));
        Assert.Equal($"unknown target '{target}'", e.Reason);
    }

    [Fact]
    public void Eat_out_of_reach_fails()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(2, 0), 5)]);
        var e = Assert.Single(w.SubmitAndStep(Eat("bush-1")));
        Assert.Equal("bush-1 is out of reach", e.Reason);
    }

    [Fact]
    public void Eat_from_empty_bush_fails()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(1, 0), 0)]);
        var e = Assert.Single(w.SubmitAndStep(Eat("bush-1")));
        Assert.Equal("bush is empty", e.Reason);
    }

    [Fact]
    public void Eat_needs_a_bush()
    {
        var w = Layout(agents: [(Tile(0, 0), 0), (Tile(0, 1), 0)], bushes: [(Tile(1, 0), 5)]);
        Assert.Equal("Eat needs a bush id", Assert.Single(w.SubmitAndStep(new AgentDecision(ActionKind.Eat, null, null, null, null, null))).Reason);
        Assert.Equal("agent-2 is not a bush", Assert.Single(w.SubmitAndStep(Eat("agent-2"))).Reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Wait_out_of_range_fails(int ticks)
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        var e = Assert.Single(w.SubmitAndStep(Wait(ticks)));
        Assert.Equal("ticks must be between 1 and 50", e.Reason);
        Assert.Null(w.Agent("agent-1").CurrentAction);
    }

    [Fact]
    public void Wait_one_starts_and_completes_in_one_step()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        var events = w.SubmitAndStep(Wait(1));
        Assert.Equal([EventKind.ActionStarted, EventKind.ActionCompleted], events.Kinds());
        Assert.Equal(new ActionResult(ActionKind.Wait, ActionOutcome.Completed, null), w.Agent("agent-1").LastAction);
    }

    [Fact]
    public void Wait_three_completes_on_the_third_step()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        w.SubmitAndStep(Wait(3));
        w.Step();
        Assert.NotNull(w.Agent("agent-1").CurrentAction);
        var third = w.Step();
        Assert.Null(w.Agent("agent-1").CurrentAction);
        Assert.Equal([EventKind.ActionCompleted], third.Kinds());
    }
}
