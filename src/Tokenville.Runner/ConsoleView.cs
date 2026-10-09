using System.Text;
using Tokenville.Core;

namespace Tokenville.Runner;

/// <summary>
/// Plain-text map and stats panel. Glyphs: <c>.</c> empty, berry count digit (capped at 9) for a bush, first
/// letter of the name for an agent; on a shared tile the lowest id shows. Spectre stays unused until a live view
/// is worth it (Phase 3).
/// </summary>
public static class ConsoleView
{
    public static string Render(World world, IReadOnlyDictionary<EntityId, (string Name, long Tick)> deaths, int startingAgents)
    {
        var (width, height) = (world.Config.Width, world.Config.Height);
        var map = new char[height, width];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                map[y, x] = '.';
        foreach (var bush in world.Bushes)
            map[bush.Position.TileY, bush.Position.TileX] = (char)('0' + Math.Min(9, bush.Berries));
        foreach (var agent in world.Agents)
            if (map[agent.Position.TileY, agent.Position.TileX] == '.')
                map[agent.Position.TileY, agent.Position.TileX] = agent.Name[0];

        var sb = new StringBuilder();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++) sb.Append(map[y, x]);
            sb.Append('\n');
        }
        sb.Append('\n');
        sb.Append($"tick {world.Tick}  alive {world.Agents.Count}/{startingAgents}  berries {world.Bushes.Sum(b => b.Berries)}\n");

        var lines = new SortedDictionary<EntityId, string>();
        foreach (var a in world.Agents)
            lines[a.Id] = $"{a.Id} {a.Name}  ({a.Position.TileX}, {a.Position.TileY})  hunger {a.Hunger}  {Describe(a.CurrentAction)}";
        foreach (var (id, (name, tick)) in deaths)
            lines[id] = $"{id} {name}  died at tick {tick}";
        foreach (var line in lines.Values) sb.Append(line).Append('\n');
        return sb.ToString();
    }

    private static string Describe(AgentAction? action) => action switch
    {
        null => "idle",
        EatAction e => $"Eat {e.Bush} ({e.TicksRemaining})",
        WaitAction w => $"Wait {w.TicksRemaining}",
        MoveToAction { TargetId: { } target } => $"MoveTo {target}",
        MoveToAction m => $"MoveTo ({m.Target.TileX}, {m.Target.TileY})",
        _ => action.Kind.ToString(),
    };
}
