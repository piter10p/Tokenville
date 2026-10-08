using Tokenville.Brains;
using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

/// <summary>Brain rules on hand-built observations; no world involved.</summary>
public class ScriptedBrainTests
{
    private static Observation Observe(
        int hunger, (int X, int Y) at = default, (string Id, int X, int Y, int Berries, int Distance)[]? bushes = null,
        ActionResult? last = null, int width = 9, int height = 9) =>
        new(0,
            new AgentSelf(Id("agent-1"), "Ada", at.X, at.Y, hunger, null),
            (bushes ?? []).Select(b => new VisibleBush(Id(b.Id), b.X, b.Y, b.Berries, b.Distance)).ToList(),
            [], [], last, width, height);

    private static AgentDecision Decide(ScriptedBrain brain, Observation o) =>
        brain.DecideAsync(o, CancellationToken.None).GetAwaiter().GetResult();

    private static AgentDecision Decide(Observation o) => Decide(new ScriptedBrain(1), o);

    private static readonly ActionResult Blocked = new(ActionKind.MoveTo, ActionOutcome.Failed, "blocked by bush-4");

    // Eat when hungry

    [Fact]
    public void Eat_in_reach()
    {
        var d = Decide(Observe(30, bushes: [("bush-2", 1, 1, 3, 0)]));
        Assert.Equal((ActionKind.Eat, "bush-2"), (d.Action, d.TargetId));
    }

    [Fact]
    public void Eat_at_distance_1()
    {
        var d = Decide(Observe(30, bushes: [("bush-2", 1, 0, 3, 1)]));
        Assert.Equal((ActionKind.Eat, "bush-2"), (d.Action, d.TargetId));
    }

    [Fact]
    public void Walk_after_out_of_reach()
    {
        var outOfReach = new ActionResult(ActionKind.Eat, ActionOutcome.Failed, "bush-2 is out of reach");
        var d = Decide(Observe(30, bushes: [("bush-2", 1, 1, 3, 1)], last: outOfReach));
        Assert.Equal((ActionKind.MoveTo, "bush-2"), (d.Action, d.TargetId));
    }

    [Fact]
    public void Walk_to_food()
    {
        var d = Decide(Observe(45, bushes: [("bush-1", 3, 0, 2, 3)]));
        Assert.Equal((ActionKind.MoveTo, "bush-1"), (d.Action, d.TargetId));
        Assert.Null(d.X);
    }

    [Fact]
    public void Nearest_wins_ties_by_id()
    {
        var d = Decide(Observe(60, bushes: [("bush-1", 1, 0, 1, 1), ("bush-2", 0, 1, 1, 1), ("bush-3", 2, 0, 1, 2)]));
        Assert.Equal("bush-1", d.TargetId);
    }

    [Fact]
    public void Empty_bush_is_skipped()
    {
        var d = Decide(Observe(60, bushes: [("bush-1", 0, 0, 0, 0), ("bush-2", 4, 0, 1, 4)]));
        Assert.Equal((ActionKind.MoveTo, "bush-2"), (d.Action, d.TargetId));
    }

    // Wander otherwise

    [Fact]
    public void Not_hungry_wanders()
    {
        var brain = new ScriptedBrain(3);
        for (var i = 0; i < 50; i++)
        {
            var d = Decide(brain, Observe(29, bushes: [("bush-1", 0, 0, 5, 0)]));
            Assert.Equal(ActionKind.MoveTo, d.Action);
            Assert.Null(d.TargetId);
            Assert.InRange(d.X!.Value, 0, 8);
            Assert.InRange(d.Y!.Value, 0, 8);
            Assert.NotEqual((0, 0), (d.X.Value, d.Y.Value));
        }
    }

    [Fact]
    public void Hungry_but_blind_wanders()
    {
        var d = Decide(Observe(70));
        Assert.Equal(ActionKind.MoveTo, d.Action);
        Assert.InRange(d.X!.Value, 0, 8);
        Assert.InRange(d.Y!.Value, 0, 8);
    }

    [Fact]
    public void Visible_bushes_are_avoided()
    {
        var o = Observe(10, bushes: [("bush-1", 0, 0, 5, 0), ("bush-2", 1, 0, 5, 1), ("bush-3", 0, 1, 5, 1)], width: 2, height: 2);
        var d = Decide(o);
        Assert.Equal((1, 1), (d.X, d.Y));
    }

    // Sidestep after a blocked move

    [Fact]
    public void Blocked_while_hungry_steps_aside()
    {
        var d = Decide(Observe(80, at: (3, 3), last: Blocked));
        Assert.Equal(ActionKind.MoveTo, d.Action);
        Assert.Contains((d.X!.Value, d.Y!.Value), new[] { (3, 4), (3, 2), (4, 3), (2, 3) });
    }

    [Fact]
    public void Blocked_neighbours_are_excluded()
    {
        var d = Decide(Observe(80, at: (0, 0), bushes: [("bush-1", 1, 0, 2, 1)], last: Blocked));
        Assert.Equal((ActionKind.MoveTo, 0, 1), (d.Action, d.X, d.Y));
    }

    [Fact]
    public void Boxed_in_waits()
    {
        var d = Decide(Observe(80, at: (0, 0), bushes: [("bush-1", 1, 0, 2, 1), ("bush-2", 0, 1, 2, 1)], last: Blocked));
        Assert.Equal((ActionKind.Wait, 1), (d.Action, d.Ticks));
    }

    // Brain contract

    [Fact]
    public void Same_seed_same_decisions()
    {
        var a = new ScriptedBrain(7);
        var b = new ScriptedBrain(7);
        for (var i = 0; i < 100; i++)
        {
            var o = i % 3 == 0 ? Observe(80, at: (4, 4), last: Blocked) : Observe(i % 2 == 0 ? 10 : 70);
            Assert.Equal(Decide(a, o), Decide(b, o));
        }
    }

    [Fact]
    public void Different_seeds_diverge()
    {
        var a = new ScriptedBrain(7);
        var b = new ScriptedBrain(8);
        var o = Observe(10);
        Assert.Contains(Enumerable.Range(0, 20), _ => Decide(a, o) != Decide(b, o));
    }
}
