namespace Tokenville.Core.Tests;

public class WorldSetupTests
{
    private static bool IsTileCenter(Position p) => p.X % Position.TileSize == 128 && p.Y % Position.TileSize == 128;

    [Fact]
    public void Default_world_has_sequential_ids_at_in_bounds_tile_centers()
    {
        var w = new World(new WorldConfig());
        Assert.Equal(Enumerable.Range(1, 6).Select(n => $"agent-{n}"), w.Agents.Select(a => a.Id.ToString()));
        Assert.Equal(Enumerable.Range(1, 8).Select(n => $"bush-{n}"), w.Bushes.Select(b => b.Id.ToString()));
        foreach (var e in w.Agents.Cast<IEntity>().Concat(w.Bushes))
        {
            Assert.True(IsTileCenter(e.Position), $"{e.Id} at {e.Position} is not a tile center");
            Assert.InRange(e.Position.X, 0, 32 * Position.TileSize - 1);
            Assert.InRange(e.Position.Y, 0, 32 * Position.TileSize - 1);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(42)]
    public void Full_grid_of_bushes_covers_every_tile_once(int seed)
    {
        var w = new World(new WorldConfig(Width: 4, Height: 4, Seed: seed, AgentCount: 0, BushCount: 16));
        var all = from x in Enumerable.Range(0, 4) from y in Enumerable.Range(0, 4) select Position.FromTileCenter(x, y);
        Assert.Equal(all.OrderBy(p => (p.X, p.Y)), w.Bushes.Select(b => b.Position).OrderBy(p => (p.X, p.Y)));
    }

    [Fact]
    public void Agents_spawn_at_the_only_bush_free_tile()
    {
        var w = new World(new WorldConfig(Width: 4, Height: 4, AgentCount: 6, BushCount: 15));
        var bushTiles = w.Bushes.Select(b => b.Position).ToHashSet();
        var free = Assert.Single(
            from x in Enumerable.Range(0, 4) from y in Enumerable.Range(0, 4)
            let c = Position.FromTileCenter(x, y) where !bushTiles.Contains(c) select c);
        Assert.All(w.Agents, a => Assert.Equal(free, a.Position));
    }

    [Fact]
    public void Same_config_gives_same_layout()
    {
        var a = new World(new WorldConfig());
        var b = new World(new WorldConfig());
        Assert.Equal(a.Agents.Select(x => x.Position), b.Agents.Select(x => x.Position));
        Assert.Equal(a.Bushes.Select(x => x.Position), b.Bushes.Select(x => x.Position));
    }

    [Fact]
    public void Different_seed_gives_different_layout()
    {
        var a = new World(new WorldConfig(Seed: 1));
        var b = new World(new WorldConfig(Seed: 2));
        var positions = (World w) => w.Agents.Cast<IEntity>().Concat(w.Bushes).Select(e => e.Position);
        Assert.NotEqual(positions(a), positions(b));
    }

    [Fact]
    public void Fresh_world_initial_state()
    {
        var w = new World(new WorldConfig());
        Assert.Equal(0, w.Tick);
        Assert.All(w.Bushes, b => Assert.Equal(5, b.Berries));
        Assert.All(w.Agents, a =>
        {
            Assert.Equal(0, a.Hunger);
            Assert.Null(a.CurrentAction);
        });
    }

    [Fact]
    public void Twelve_agents_enumerate_in_ascending_id_order()
    {
        var w = new World(new WorldConfig(AgentCount: 12));
        Assert.Equal(Enumerable.Range(1, 12).Select(n => $"agent-{n}"), w.Agents.Select(a => a.Id.ToString()));
    }

    [Fact]
    public void Names_are_stable_across_seeds_and_wrap()
    {
        var a = new World(new WorldConfig(Seed: 1, AgentCount: 13));
        var b = new World(new WorldConfig(Seed: 2, AgentCount: 13));
        Assert.Equal(a.Agents.First().Name, b.Agents.First().Name);
        Assert.Equal(a.Agents.First().Name, a.Agents.Last().Name); // agent-13 wraps onto agent-1's name
    }

    [Fact]
    public void Too_many_bushes_is_rejected_naming_BushCount()
    {
        var ex = Assert.Throws<ArgumentException>(() => new World(new WorldConfig(Width: 3, Height: 3, BushCount: 10)));
        Assert.Contains("BushCount", ex.Message);
    }

    [Fact]
    public void No_room_for_agents_is_rejected_naming_AgentCount()
    {
        var ex = Assert.Throws<ArgumentException>(() => new World(new WorldConfig(Width: 2, Height: 2, BushCount: 4, AgentCount: 1)));
        Assert.Contains("AgentCount", ex.Message);
    }

    [Theory]
    [InlineData(0, 4, 1, 1)]
    [InlineData(4, 0, 1, 1)]
    [InlineData(4, 4, -1, 1)]
    [InlineData(4, 4, 1, -1)]
    public void Invalid_sizes_and_counts_are_rejected(int width, int height, int agents, int bushes)
    {
        Assert.Throws<ArgumentException>(() => new World(new WorldConfig(Width: width, Height: height, AgentCount: agents, BushCount: bushes)));
    }
}
