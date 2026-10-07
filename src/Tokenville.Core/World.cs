namespace Tokenville.Core;

/// <summary>
/// The simulation state. Construction places bushes, then agents, from one RNG seeded with
/// <see cref="WorldConfig.Seed"/>; entities iterate in ascending id order.
/// </summary>
public sealed class World
{
    private readonly Random _rng;
    private readonly SortedDictionary<EntityId, Agent> _agents = [];
    private readonly SortedDictionary<EntityId, BerryBush> _bushes = [];

    public World(WorldConfig config)
    {
        Validate(config);
        Config = config;
        _rng = new Random(config.Seed);

        for (var n = 1; n <= config.BushCount; n++)
        {
            var id = new EntityId(EntityKind.Bush, n);
            _bushes.Add(id, new BerryBush(id, DrawBushFreeTileCenter(), config.BushMaxBerries));
        }

        for (var n = 1; n <= config.AgentCount; n++)
        {
            var id = new EntityId(EntityKind.Agent, n);
            _agents.Add(id, new Agent(id, DrawBushFreeTileCenter()));
        }
    }

    public WorldConfig Config { get; }
    public long Tick { get; private set; }
    public IReadOnlyCollection<Agent> Agents => _agents.Values;
    public IReadOnlyCollection<BerryBush> Bushes => _bushes.Values;

    /// <summary>Draw order is x then y; both are redrawn if the tile holds a bush. Byte-identical logs depend on this.</summary>
    private Position DrawBushFreeTileCenter()
    {
        while (true)
        {
            var x = _rng.Next(Config.Width);
            var y = _rng.Next(Config.Height);
            var center = Position.FromTileCenter(x, y);
            if (!_bushes.Values.Any(b => b.Position == center))
                return center;
        }
    }

    private static void Validate(WorldConfig c)
    {
        if (c.Width < 1 || c.Height < 1)
            throw new ArgumentException($"Width and Height must be at least 1 (got {c.Width}x{c.Height}).", nameof(c));
        if (c.AgentCount < 0 || c.BushCount < 0)
            throw new ArgumentException($"AgentCount and BushCount must not be negative (got {c.AgentCount}, {c.BushCount}).", nameof(c));

        var tiles = checked(c.Width * c.Height);
        _ = checked(c.Width * Position.TileSize);
        _ = checked(c.Height * Position.TileSize);

        if (c.BushCount > tiles)
            throw new ArgumentException($"BushCount {c.BushCount} exceeds the {tiles} tiles of a {c.Width}x{c.Height} grid.", nameof(c));
        if (c.AgentCount > 0 && c.BushCount == tiles)
            throw new ArgumentException($"AgentCount {c.AgentCount} needs a bush-free tile, but all {tiles} tiles hold bushes.", nameof(c));
    }
}
