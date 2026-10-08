using System.Text.Json;
using Tokenville.Brains;
using Tokenville.Runner;
using static Tokenville.Core.Tests.TestWorld;

namespace Tokenville.Core.Tests;

public class SimulationTests
{
    private static readonly WorldConfig Paced = Grid9 with { HungerTicksPerPoint = 2 };

    private static Func<World, Agent, AgentDecision> Scripted(World world)
    {
        var brains = world.Agents.ToDictionary(a => a.Id, a => new ScriptedBrain(world.Config.Seed, a.Id.Number));
        return (w, a) => brains[a.Id].Decide(w, a);
    }

    private static string RunLog(World world, Func<World, Agent, AgentDecision> decide, int maxTicks, int every = 0, StringWriter? view = null)
    {
        var log = new StringWriter();
        Simulation.Run(world, decide, log, maxTicks, every, view);
        return log.ToString();
    }

    private static int Snapshots(StringWriter view) =>
        view.ToString().Split('\n').Count(l => l.StartsWith("tick ", StringComparison.Ordinal));

    [Fact]
    public void EntityId_round_trips_through_json()
    {
        var json = JsonSerializer.Serialize(new EntityId(EntityKind.Agent, 3), JsonFormat.Options);
        Assert.Equal("\"agent-3\"", json);
        Assert.Equal(new EntityId(EntityKind.Agent, 3), JsonSerializer.Deserialize<EntityId>(json, JsonFormat.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EntityId>("\"tree-1\"", JsonFormat.Options));
    }

    [Fact]
    public void Position_round_trips_without_tile_properties()
    {
        var json = JsonSerializer.Serialize(new Position(384, 128), JsonFormat.Options);
        Assert.Equal("""{"X":384,"Y":128}""", json);
        Assert.Equal(new Position(384, 128), JsonSerializer.Deserialize<Position>(json, JsonFormat.Options));
    }

    [Fact]
    public void Config_still_loads_case_insensitively_and_rejects_unknown_members()
    {
        var c = JsonSerializer.Deserialize<WorldConfig>("""{"bushCount": 3, "seed": 7}""", JsonFormat.Options);
        Assert.Equal(new WorldConfig(BushCount: 3, Seed: 7), c);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorldConfig>("""{"Widht": 5}""", JsonFormat.Options));
    }

    [Fact]
    public void Action_started_line_is_exact()
    {
        var w = Layout(agents: [(Tile(0, 0), 0)], bushes: [(Tile(5, 0), 5)]);
        var log = RunLog(w, (_, _) => MoveTo("bush-1"), maxTicks: 1);
        Assert.Equal(
            """{"Tick":0,"Kind":"ActionStarted","Subject":"agent-1","Position":{"X":128,"Y":128},"Target":"bush-1","Action":"MoveTo"}""",
            log.Split('\n')[0]);
    }

    [Fact]
    public void Regrew_and_failed_lines_are_exact()
    {
        var regrew = new WorldEvent(39, EventKind.BushRegrew, Id("bush-1"), new Position(384, 128), Value: 5);
        var failed = new WorldEvent(7, EventKind.ActionFailed, Id("agent-2"), new Position(640, 128), Action: ActionKind.Eat, Reason: "bush is empty");
        Assert.Equal(
            """{"Tick":39,"Kind":"BushRegrew","Subject":"bush-1","Position":{"X":384,"Y":128},"Value":5}""",
            JsonSerializer.Serialize(regrew, JsonFormat.Options));
        Assert.Equal(
            """{"Tick":7,"Kind":"ActionFailed","Subject":"agent-2","Position":{"X":640,"Y":128},"Action":"Eat","Reason":"bush is empty"}""",
            JsonSerializer.Serialize(failed, JsonFormat.Options));
    }

    [Fact]
    public void One_line_per_event_with_lf_endings()
    {
        var a = new World(new WorldConfig());
        var b = new World(new WorldConfig());
        var expected = 0;
        for (var i = 0; i < 30; i++)
        {
            foreach (var id in b.AgentsAwaitingDecision) b.Submit(id, Wait(2));
            expected += b.Step().Count;
        }
        var log = RunLog(a, (_, _) => Wait(2), maxTicks: 30);
        Assert.True(expected > 0);
        Assert.Equal(expected, log.Count(c => c == '\n'));
        Assert.EndsWith("\n", log);
        Assert.DoesNotContain('\r', log);
    }

    [Fact]
    public void Every_idle_agent_is_asked_and_busy_ones_are_not()
    {
        var w = new World(new WorldConfig());
        var asked = new List<EntityId>();
        RunLog(w, (_, a) => { asked.Add(a.Id); return Wait(5); }, maxTicks: 3);
        Assert.Equal(Enumerable.Range(1, 6).Select(n => new EntityId(EntityKind.Agent, n)), asked);
        Assert.Equal(3, w.Tick);
    }

    [Fact]
    public void Stops_at_the_tick_limit()
    {
        var w = new World(new WorldConfig());
        RunLog(w, Scripted(w), maxTicks: 10);
        Assert.Equal(10, w.Tick);
        Assert.Equal(6, w.Agents.Count);
    }

    [Fact]
    public void Stops_when_everyone_is_dead()
    {
        var w = Layout(Paced, agents: [(Tile(0, 0), 99)]);
        var view = new StringWriter();
        RunLog(w, (_, _) => Wait(1), maxTicks: 100, every: 0, view: view);
        Assert.Equal(2, w.Tick);
        Assert.Empty(w.Agents);
        Assert.Contains("run ended at tick 2, alive 0/1", view.ToString());
    }

    [Fact]
    public void Two_scripted_runs_give_identical_logs()
    {
        var a = new World(new WorldConfig());
        var b = new World(new WorldConfig());
        var la = RunLog(a, Scripted(a), maxTicks: 200);
        var lb = RunLog(b, Scripted(b), maxTicks: 200);
        Assert.Equal(la, lb);
        Assert.Contains("\"Kind\":\"AgentAte\"", la);
    }

    [Fact]
    public void Snapshots_every_50_ticks_plus_final()
    {
        var w = new World(new WorldConfig());
        var view = new StringWriter();
        RunLog(w, (_, _) => Wait(1), maxTicks: 120, every: 50, view: view);
        Assert.Equal(3, Snapshots(view));
        Assert.Contains("tick 50 ", view.ToString());
        Assert.Contains("tick 100 ", view.ToString());
        Assert.Contains("tick 120 ", view.ToString());
    }

    [Fact]
    public void Interval_zero_renders_only_the_final_snapshot()
    {
        var w = new World(new WorldConfig());
        var view = new StringWriter();
        RunLog(w, (_, _) => Wait(1), maxTicks: 120, every: 0, view: view);
        Assert.Equal(1, Snapshots(view));
        Assert.Contains("tick 120 ", view.ToString());
    }

    [Fact]
    public void Final_snapshot_is_not_doubled_on_a_multiple()
    {
        var w = new World(new WorldConfig());
        var view = new StringWriter();
        RunLog(w, (_, _) => Wait(1), maxTicks: 100, every: 50, view: view);
        Assert.Equal(2, Snapshots(view));
    }

    /// <summary>Phase 1 acceptance: six scripted agents survive 2,000 ticks on 8 bushes and someone starves on 3.</summary>
    [Theory]
    [InlineData(8, true)]
    [InlineData(3, false)]
    public void Phase_1_acceptance(int bushCount, bool allSurvive)
    {
        var w = new World(new WorldConfig(BushCount: bushCount));
        Simulation.Run(w, Scripted(w), TextWriter.Null, maxTicks: 2000, every: 0, view: null);
        Assert.Equal(2000, w.Tick);
        if (allSurvive) Assert.Equal(6, w.Agents.Count);
        else Assert.True(w.Agents.Count < 6, "expected at least one death with 3 bushes");
    }
}
