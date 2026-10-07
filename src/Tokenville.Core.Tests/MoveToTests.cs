using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

public class MoveToTests
{
    private const long ReachSquared = (long)Position.Reach * Position.Reach;

    [Fact]
    public void Straight_run_moves_one_tile_per_step_and_arrives_exactly()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        w.SubmitAndStep(MoveTo(3, 0));
        Assert.Equal(new Position(384, 128), w.Agent("agent-1").Position);
        w.Step();
        Assert.Equal(new Position(640, 128), w.Agent("agent-1").Position);
        var third = w.Step();
        Assert.Equal(new Position(896, 128), w.Agent("agent-1").Position);
        Assert.Equal([EventKind.AgentMoved, EventKind.ActionCompleted], third.Kinds());
        Assert.Null(w.Agent("agent-1").CurrentAction);
    }

    [Fact]
    public void Diagonal_rounding_never_accumulates()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        w.Submit(Id("agent-1"), MoveTo(5, 3));
        var previous = w.Agent("agent-1").Position;
        foreach (var e in w.StepUntilIdle().Where(e => e.Kind == EventKind.AgentMoved))
        {
            Assert.True(previous.DistanceSquaredTo(e.Position) <= (long)Position.Speed * Position.Speed, $"step to {e.Position} longer than Speed");
            previous = e.Position;
        }
        Assert.Equal(new Position(1408, 896), w.Agent("agent-1").Position);
        Assert.Equal(ActionOutcome.Completed, w.Agent("agent-1").LastAction!.Outcome);
    }

    [Fact]
    public void Already_there_completes_without_moving()
    {
        var w = Layout(agents: [(Tile(4, 4), 0)]);
        var events = w.SubmitAndStep(MoveTo(4, 4));
        Assert.Equal([EventKind.ActionStarted, EventKind.ActionCompleted], events.Kinds());
    }

    [Fact]
    public void Within_reach_of_an_agent_target_completes_without_moving()
    {
        var w = Layout(agents: [(new Position(128, 128), 0), (new Position(328, 128), 0)]);
        var events = w.SubmitAndStep(MoveTo("agent-2"));
        Assert.Equal([EventKind.ActionStarted, EventKind.ActionCompleted], events.Kinds());
        Assert.Equal(Id("agent-2"), events[0].Target);
        Assert.Equal(new Position(128, 128), w.Agent("agent-1").Position);
    }

    [Fact]
    public void Step_ending_in_a_bush_tile_is_blocked()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(1, 0), 5)]);
        var events = w.SubmitAndStep(MoveTo(2, 0));
        Assert.Equal([EventKind.ActionStarted, EventKind.ActionFailed], events.Kinds());
        Assert.Equal("blocked by bush-1", events[1].Reason);
        Assert.Equal(new Position(128, 128), w.Agent("agent-1").Position);
        Assert.Null(w.Agent("agent-1").CurrentAction);
        Assert.Equal(new ActionResult(ActionKind.MoveTo, ActionOutcome.Failed, "blocked by bush-1"), w.Agent("agent-1").LastAction);
    }

    [Fact]
    public void Crossing_a_bush_corner_without_ending_inside_is_allowed()
    {
        // (0,0) -> (2,2) passes through the corner point (256, 256) shared with tile (1,0);
        // the intermediate positions (309,309) and (490,490) both lie in tile (1,1).
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(1, 0), 5)]);
        w.Submit(Id("agent-1"), MoveTo(2, 2));
        var events = w.StepUntilIdle();
        Assert.Equal(ActionOutcome.Completed, w.Agent("agent-1").LastAction!.Outcome);
        Assert.Equal(Tile(2, 2), w.Agent("agent-1").Position);
        Assert.DoesNotContain(events, e => e.Kind == EventKind.ActionFailed);
    }

    [Fact]
    public void Diagonal_approach_to_a_bush_ends_within_reach_outside_its_tile()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(3, 1), 5)]);
        w.Submit(Id("agent-1"), MoveTo("bush-1"));
        w.StepUntilIdle(max: 4);
        var agent = w.Agent("agent-1");
        Assert.Equal(ActionOutcome.Completed, agent.LastAction!.Outcome);
        Assert.True(agent.Position.DistanceSquaredTo(Tile(3, 1)) <= ReachSquared);
        Assert.NotEqual((3, 1), (agent.Position.TileX, agent.Position.TileY));
    }

    [Fact]
    public void Sweep_every_start_tile_around_a_lone_bush()
    {
        for (var x = 0; x < 9; x++)
        for (var y = 0; y < 9; y++)
        {
            if ((x, y) == (4, 4)) continue;
            var w = Layout(agents: [(Tile(x, y), 0)], bushes: [(Tile(4, 4), 5)]);
            w.Submit(Id("agent-1"), MoveTo("bush-1"));
            var events = w.StepUntilIdle(max: 7);
            var agent = w.Agent("agent-1");
            Assert.True(agent.LastAction!.Outcome == ActionOutcome.Completed, $"from ({x},{y}): {agent.LastAction}");
            Assert.True(agent.Position.DistanceSquaredTo(Tile(4, 4)) <= ReachSquared, $"from ({x},{y}) ended out of reach at {agent.Position}");
            Assert.True((agent.Position.TileX, agent.Position.TileY) != (4, 4), $"from ({x},{y}) ended inside the bush tile");
            Assert.DoesNotContain(events, e => e.Kind == EventKind.ActionFailed);
        }
    }

    [Fact]
    public void Moved_events_carry_the_new_position()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        var e = w.SubmitAndStep(MoveTo(1, 0)).Single(e => e.Kind == EventKind.AgentMoved);
        Assert.Equal(new WorldEvent(0, EventKind.AgentMoved, Id("agent-1"), new Position(384, 128)), e);
    }
}
