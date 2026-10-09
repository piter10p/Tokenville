using System.Text.Json;
using Tokenville.Core;

namespace Tokenville.Runner;

/// <summary>
/// The plan's lockstep loop: ask every idle agent's brain, submit, step, log the events, maybe render. Stops after
/// the step that kills the last agent or reaches the tick limit. Takes writers rather than paths so tests run it
/// against strings; Phase 2's determinism and replay tests go through here.
/// </summary>
public static class Simulation
{
    /// <summary>
    /// Runs until no agent lives or <c>world.Tick >= maxTicks</c>. Writes one JSON line per event to
    /// <paramref name="log"/>. When <paramref name="view"/> is given, renders a snapshot after every step whose tick
    /// is a multiple of <paramref name="every"/> (0 = never) and once at the end, then a one-line summary.
    /// </summary>
    public static void Run(World world, Func<World, Agent, AgentDecision> decide, TextWriter log, int maxTicks, int every, TextWriter? view)
    {
        var names = world.Agents.ToDictionary(a => a.Id, a => a.Name);
        var deaths = new SortedDictionary<EntityId, (string Name, long Tick)>();
        var starting = names.Count;

        while (world.Agents.Count > 0 && world.Tick < maxTicks)
        {
            foreach (var id in world.AgentsAwaitingDecision)
                world.Submit(id, decide(world, world.Agents.Single(a => a.Id == id)));

            foreach (var e in world.Step())
            {
                log.Write(JsonSerializer.Serialize(e, JsonFormat.Options));
                log.Write('\n');
                if (e.Kind == EventKind.AgentDied) deaths[e.Subject] = (names[e.Subject], e.Tick);
            }

            var finished = world.Agents.Count == 0 || world.Tick >= maxTicks;
            if (view is not null && (finished || (every > 0 && world.Tick % every == 0)))
                view.Write(ConsoleView.Render(world, deaths, starting));
        }

        view?.Write($"run ended at tick {world.Tick}, alive {world.Agents.Count}/{starting}\n");
    }
}
