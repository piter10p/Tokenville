using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

public class EatTests
{
    [Fact]
    public void Successful_eat_takes_a_berry_and_feeds_the_agent()
    {
        var w = Layout(agents: [(Tile(0, 0), 40)], bushes: [(Tile(1, 0), 2)]);
        var first = w.SubmitAndStep(Eat("bush-1"));
        Assert.Equal([EventKind.ActionStarted], first.Kinds());
        Assert.Equal(Id("bush-1"), first[0].Target);
        w.StepTimes(3);
        Assert.Equal(2, w.Bush("bush-1").Berries);
        Assert.NotNull(w.Agent("agent-1").CurrentAction);

        var last = w.Step();
        Assert.Equal([EventKind.AgentAte, EventKind.ActionCompleted], last.Kinds());
        Assert.Equal(new WorldEvent(4, EventKind.AgentAte, Id("agent-1"), Tile(0, 0), Id("bush-1"), null, 15), last[0]);
        Assert.Equal(1, w.Bush("bush-1").Berries);
        Assert.Equal(15, w.Agent("agent-1").Hunger);
        Assert.Null(w.Agent("agent-1").CurrentAction);
    }

    [Fact]
    public void Hunger_floors_at_zero()
    {
        var w = Layout(agents: [(Tile(0, 0), 10)], bushes: [(Tile(1, 0), 5)]);
        w.Submit(Id("agent-1"), Eat("bush-1"));
        w.StepUntilIdle();
        Assert.Equal(0, w.Agent("agent-1").Hunger);
    }

    [Fact]
    public void Last_berry_tie_goes_to_the_lower_id()
    {
        var w = Layout(agents: [(Tile(0, 0), 40), (Tile(2, 0), 40)], bushes: [(Tile(1, 0), 1)]);
        w.Submit(Id("agent-1"), Eat("bush-1"));
        w.Submit(Id("agent-2"), Eat("bush-1"));
        w.StepTimes(4);
        var last = w.Step();
        Assert.Equal([EventKind.AgentAte, EventKind.ActionCompleted, EventKind.ActionFailed], last.Kinds());
        Assert.Equal(Id("agent-1"), last[0].Subject);
        Assert.Equal(Id("agent-2"), last[2].Subject);
        Assert.Equal("bush is empty", last[2].Reason);
        Assert.Equal(0, w.Bush("bush-1").Berries);
        Assert.Equal(15, w.Agent("agent-1").Hunger);
        Assert.Equal(40, w.Agent("agent-2").Hunger);
    }

    [Fact]
    public void Eat_lasts_exactly_EatTicks_steps_from_config()
    {
        var w = Layout(new WorldConfig(EatTicks: 2), agents: [(Tile(0, 0), 40)], bushes: [(Tile(1, 0), 5)]);
        w.SubmitAndStep(Eat("bush-1"));
        Assert.NotNull(w.Agent("agent-1").CurrentAction);
        w.Step();
        Assert.Null(w.Agent("agent-1").CurrentAction);
        Assert.Equal(4, w.Bush("bush-1").Berries);
    }
}
