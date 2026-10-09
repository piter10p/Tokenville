using Tokenville.Brains;
using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

public class ScriptedBrainTests
{
    private static ScriptedBrain Brain(int seed = 42, int agent = 1) => new(seed, agent);

    [Fact]
    public void Hungry_and_in_reach_eats()
    {
        var w = Layout(agents: [(Tile(0, 0), 30)], bushes: [(Tile(1, 0), 2)]);
        var d = Brain().Decide(w, w.Agent("agent-1"));
        Assert.Equal((ActionKind.Eat, "bush-1"), (d.Action, d.TargetId));
    }

    [Fact]
    public void Hungry_and_out_of_reach_moves_to_the_bush()
    {
        var w = Layout(agents: [(Tile(0, 0), 60)], bushes: [(Tile(5, 0), 2)]);
        var d = Brain().Decide(w, w.Agent("agent-1"));
        Assert.Equal((ActionKind.MoveTo, "bush-1"), (d.Action, d.TargetId));
        Assert.Null(d.X);
        Assert.Null(d.Y);
    }

    [Fact]
    public void Empty_bushes_are_ignored()
    {
        var w = Layout(agents: [(Tile(0, 0), 60)], bushes: [(Tile(1, 0), 0), (Tile(6, 0), 1)]);
        var d = Brain().Decide(w, w.Agent("agent-1"));
        Assert.Equal((ActionKind.MoveTo, "bush-2"), (d.Action, d.TargetId));
    }

    [Fact]
    public void Ties_go_to_the_lower_id()
    {
        var w = Layout(agents: [(Tile(4, 0), 60)], bushes: [(Tile(0, 0), 1), (Tile(8, 0), 1)]);
        Assert.Equal("bush-1", Brain().Decide(w, w.Agent("agent-1")).TargetId);
    }

    [Fact]
    public void Hunger_29_beside_food_is_not_eat()
    {
        var w = Layout(agents: [(Tile(0, 0), 29)], bushes: [(Tile(1, 0), 2)]);
        var d = Brain().Decide(w, w.Agent("agent-1"));
        Assert.Equal(ActionKind.MoveTo, d.Action);
        Assert.NotNull(d.X);
    }

    [Fact]
    public void Wander_targets_are_in_bounds_and_bush_free()
    {
        var w = Layout(Grid9 with { Width = 3, Height = 3 }, agents: [(Tile(0, 0), 0)], bushes: [(Tile(1, 1), 5)]);
        var brain = Brain();
        for (var i = 0; i < 200; i++)
        {
            var d = brain.Decide(w, w.Agent("agent-1"));
            Assert.Equal(ActionKind.MoveTo, d.Action);
            Assert.Null(d.TargetId);
            Assert.InRange(d.X!.Value, 0, 2);
            Assert.InRange(d.Y!.Value, 0, 2);
            Assert.NotEqual((1, 1), (d.X.Value, d.Y.Value));
        }
    }

    [Fact]
    public void No_berries_anywhere_wanders()
    {
        var w = Layout(agents: [(Tile(0, 0), 90)], bushes: [(Tile(1, 0), 0)]);
        var d = Brain().Decide(w, w.Agent("agent-1"));
        Assert.Equal(ActionKind.MoveTo, d.Action);
        Assert.Null(d.TargetId);
        Assert.NotEqual((1, 0), (d.X!.Value, d.Y!.Value));
    }

    [Fact]
    public void Blocked_sidesteps_below()
    {
        var w = Layout(agents: [(Tile(3, 3), 0)], bushes: [(Tile(5, 3), 5)]);
        w.Agent("agent-1").LastAction = new ActionResult(ActionKind.MoveTo, ActionOutcome.Failed, "blocked by bush-1");
        var d = Brain().Decide(w, w.Agent("agent-1"));
        Assert.Equal((ActionKind.MoveTo, 3, 4, "sidestep"), (d.Action, d.X, d.Y, d.Reason));
    }

    [Fact]
    public void Blocked_sidesteps_above_when_below_is_a_bush()
    {
        var w = Layout(agents: [(Tile(3, 3), 0)], bushes: [(Tile(5, 3), 5), (Tile(3, 4), 5)]);
        w.Agent("agent-1").LastAction = new ActionResult(ActionKind.MoveTo, ActionOutcome.Failed, "blocked by bush-1");
        var d = Brain().Decide(w, w.Agent("agent-1"));
        Assert.Equal((ActionKind.MoveTo, 3, 2), (d.Action, d.X, d.Y));
    }

    [Fact]
    public void Boxed_in_waits_one_tick()
    {
        var w = Layout(Grid9 with { Width = 3, Height = 3 },
            agents: [(Tile(1, 1), 0)],
            bushes: [(Tile(1, 2), 5), (Tile(1, 0), 5), (Tile(2, 1), 5), (Tile(0, 1), 5)]);
        w.Agent("agent-1").LastAction = new ActionResult(ActionKind.MoveTo, ActionOutcome.Failed, "blocked by bush-1");
        var d = Brain().Decide(w, w.Agent("agent-1"));
        Assert.Equal((ActionKind.Wait, 1), (d.Action, d.Ticks));
    }

    [Fact]
    public void Same_seed_same_decisions()
    {
        var a = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(4, 4), 5)]);
        var b = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(4, 4), 5)]);
        var (ba, bb) = (Brain(), Brain());
        var da = Enumerable.Range(0, 100).Select(_ => ba.Decide(a, a.Agent("agent-1"))).ToList();
        var db = Enumerable.Range(0, 100).Select(_ => bb.Decide(b, b.Agent("agent-1"))).ToList();
        Assert.Equal(da, db);
        Assert.True(da.Select(d => (d.X, d.Y)).Distinct().Count() > 1, "wander targets should vary");
    }

    [Fact]
    public void Every_decision_has_a_reason()
    {
        var w = Layout(agents: [(Tile(0, 0), 60)], bushes: [(Tile(1, 0), 2)]);
        var brain = Brain();
        Assert.False(string.IsNullOrEmpty(brain.Decide(w, w.Agent("agent-1")).Reason));
        w.Bush("bush-1").Berries = 0;
        Assert.False(string.IsNullOrEmpty(brain.Decide(w, w.Agent("agent-1")).Reason));
        w.Agent("agent-1").LastAction = new ActionResult(ActionKind.MoveTo, ActionOutcome.Failed, "blocked by bush-1");
        Assert.False(string.IsNullOrEmpty(brain.Decide(w, w.Agent("agent-1")).Reason));
    }
}
