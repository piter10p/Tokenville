using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

public class PipelineTests
{
    /// <summary>9x9 with the plan's hunger pacing (one point every 2 ticks).</summary>
    private static readonly WorldConfig Paced = Grid9 with { HungerTicksPerPoint = 2 };

    [Fact]
    public void Empty_world_returns_no_events_and_increments_tick()
    {
        var w = Layout(bushes: [(Tile(1, 1), 5)]);
        Assert.Empty(w.Step());
        Assert.Equal(1, w.Tick);
    }

    [Fact]
    public void Returned_lists_are_independent_and_carry_the_pre_increment_tick()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)]);
        var first = w.SubmitAndStep(Wait(5));
        var second = w.SubmitAndStep(Wait(1), "agent-1"); // dropped: agent-1 is busy, so step 2 is quiet
        Assert.All(first, e => Assert.Equal(0, e.Tick));
        Assert.Equal([EventKind.ActionStarted], first.Kinds());
        Assert.Empty(second);
        Assert.Equal(2, w.Tick);
        w.StepTimes(3);
        Assert.Equal([EventKind.ActionStarted], first.Kinds()); // still step 1's events
    }

    [Fact]
    public void Default_pacing_kills_a_starving_agent_on_tick_199()
    {
        var w = new World(new WorldConfig());
        w.StepTimes(198);
        Assert.All(w.Agents, a => Assert.Equal(99, a.Hunger));
        w.Step();
        Assert.Equal(6, w.Agents.Count);
        var last = w.Step();
        Assert.Empty(w.Agents);
        Assert.Equal(6, last.Count(e => e.Kind == EventKind.AgentDied));
        Assert.All(last, e => Assert.Equal(199, e.Tick));
        Assert.Empty(w.AgentsAwaitingDecision);
    }

    [Fact]
    public void Death_ends_the_action_silently()
    {
        var w = Layout(Paced, agents: [(Tile(0, 0), 99)]);
        w.Submit(Id("agent-1"), MoveTo(8, 0));
        w.Step(); // tick 0: no hunger growth
        var events = w.Step(); // tick 1: hunger -> 100
        Assert.Equal([EventKind.AgentMoved, EventKind.AgentDied], events.Kinds());
        Assert.Empty(w.Agents);
    }

    [Fact]
    public void All_dead_world_keeps_ticking_and_regrowing()
    {
        var w = Layout(Paced, agents: [(Tile(0, 0), 99)], bushes: [(Tile(5, 5), 0)]);
        w.StepTimes(2);
        Assert.Empty(w.Agents);
        var events = w.StepTimes(40);
        Assert.Equal(42, w.Tick);
        Assert.Contains(events, e => e.Kind == EventKind.BushRegrew);
        Assert.Equal(1, w.Bush("bush-1").Berries);
    }

    [Fact]
    public void Berry_picked_from_a_full_bush_regrows_39_ticks_later()
    {
        var w = Layout(agents: [(Tile(0, 0), 40)], bushes: [(Tile(1, 0), 5)]);
        w.Submit(Id("agent-1"), Eat("bush-1"));
        w.StepTimes(5); // eat completes on tick 4
        Assert.Equal(4, w.Bush("bush-1").Berries);
        var t = w.Tick - 1;
        var quiet = w.StepTimes(38); // ticks t+1 .. t+38
        Assert.DoesNotContain(quiet, e => e.Kind == EventKind.BushRegrew);
        Assert.Equal(4, w.Bush("bush-1").Berries);
        var regrow = w.Step(); // tick t+39
        Assert.Equal([new WorldEvent(t + 39, EventKind.BushRegrew, Id("bush-1"), Tile(1, 0), null, null, 5)], regrow);
        Assert.Equal(5, w.Bush("bush-1").Berries);
    }

    [Fact]
    public void Full_bush_never_regrows()
    {
        var w = Layout(bushes: [(Tile(1, 0), 5)]);
        Assert.Empty(w.StepTimes(100));
        Assert.Equal(5, w.Bush("bush-1").Berries);
    }

    [Fact]
    public void Wait_is_interrupted_at_50()
    {
        var w = Layout(Paced, agents: [(Tile(0, 0), 49)]);
        w.Submit(Id("agent-1"), Wait(50));
        w.Step();
        var events = w.Step();
        Assert.Equal([EventKind.HungerThresholdCrossed, EventKind.ActionInterrupted], events.Kinds());
        Assert.Equal(50, events[0].Value);
        Assert.Equal(ActionKind.Wait, events[1].Action);
        Assert.Equal([Id("agent-1")], w.AgentsAwaitingDecision);
        Assert.Equal(new ActionResult(ActionKind.Wait, ActionOutcome.Interrupted, null), w.Agent("agent-1").LastAction);
    }

    [Fact]
    public void Eat_is_not_interrupted_at_80()
    {
        var w = Layout(Paced, agents: [(Tile(0, 0), 79)], bushes: [(Tile(1, 0), 5)]);
        w.Submit(Id("agent-1"), Eat("bush-1"));
        w.Step();
        var events = w.Step();
        Assert.Equal([EventKind.HungerThresholdCrossed], events.Kinds());
        Assert.Equal(80, events[0].Value);
        Assert.IsType<EatAction>(w.Agent("agent-1").CurrentAction);
        Assert.Empty(w.AgentsAwaitingDecision);
    }

    [Fact]
    public void Idle_agent_is_alerted_without_interrupt()
    {
        var w = Layout(Paced, agents: [(Tile(0, 0), 79)]);
        w.Step();
        var events = w.Step();
        Assert.Equal([EventKind.HungerThresholdCrossed], events.Kinds());
    }

    [Fact]
    public void Re_crossing_after_eating_alerts_again()
    {
        var w = Layout(Paced, agents: [(Tile(0, 0), 49)], bushes: [(Tile(1, 0), 5)]);
        var all = new List<WorldEvent>();
        all.AddRange(w.StepTimes(2)); // crosses 50 at tick 1
        w.Submit(Id("agent-1"), Eat("bush-1"));
        all.AddRange(w.StepUntilIdle()); // back to ~27
        all.AddRange(w.StepTimes(60)); // climbs past 50 again
        Assert.Equal(2, all.Count(e => e.Kind == EventKind.HungerThresholdCrossed && e.Value == 50));
    }

    [Fact]
    public void Order_within_a_step()
    {
        var w = Layout(Paced,
            agents: [(Tile(0, 0), 0), (Tile(2, 0), 99), (Tile(4, 0), 49)],
            bushes: [(Tile(8, 8), 4)]);
        w.Bush("bush-1").RegrowCounter = 38; // regrows on the second step
        w.Submit(Id("agent-1"), MoveTo(0, 5));
        w.Step(); // tick 0: agent-1 starts and moves; counter -> 39; no hunger growth
        w.Submit(Id("agent-1"), Wait(1)); // dropped: agent-1 is busy
        var events = w.Step(); // tick 1
        Assert.Equal(
            [EventKind.AgentMoved, EventKind.AgentDied, EventKind.BushRegrew, EventKind.HungerThresholdCrossed],
            events.Kinds());
        Assert.Equal([Id("agent-1"), Id("agent-2"), Id("bush-1"), Id("agent-3")], events.Select(e => e.Subject));
    }

    [Fact]
    public void Order_within_a_step_including_a_start()
    {
        var w = Layout(Paced, agents: [(Tile(0, 0), 0), (Tile(2, 0), 99), (Tile(4, 0), 49)]);
        w.Step(); // tick 0
        w.Submit(Id("agent-1"), MoveTo(0, 5));
        var events = w.Step(); // tick 1
        Assert.Equal(
            [EventKind.ActionStarted, EventKind.AgentMoved, EventKind.AgentDied, EventKind.HungerThresholdCrossed],
            events.Kinds());
    }

    [Fact]
    public void Mixed_outcomes_decide_who_is_asked()
    {
        var w = Layout(agents: [(Tile(0, 0), 0), (Tile(1, 0), 0), (Tile(2, 0), 0), (Tile(3, 0), 0)]);
        w.Submit(Id("agent-1"), Wait(1));
        w.Submit(Id("agent-2"), Wait(5));
        w.Submit(Id("agent-3"), Wait(0));
        w.Step();
        Assert.Equal([Id("agent-1"), Id("agent-3"), Id("agent-4")], w.AgentsAwaitingDecision);
    }

    [Fact]
    public void Same_decisions_give_equal_events_and_positions()
    {
        var a = new World(new WorldConfig());
        var b = new World(new WorldConfig());
        for (var i = 0; i < 150; i++)
        {
            foreach (var w in new[] { a, b })
                foreach (var id in w.AgentsAwaitingDecision)
                    w.Submit(id, i % 3 == 0 ? Wait(2) : MoveTo(id.Number * 3 % w.Config.Width, i % w.Config.Height));
            Assert.Equal(a.Step(), b.Step());
        }
        Assert.Equal(a.Agents.Select(x => x.Position), b.Agents.Select(x => x.Position));
        Assert.Equal(a.Agents.Select(x => x.Hunger), b.Agents.Select(x => x.Hunger));
    }
}
