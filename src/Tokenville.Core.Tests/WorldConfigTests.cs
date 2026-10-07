namespace Tokenville.Core.Tests;

public class WorldConfigTests
{
    [Fact]
    public void Defaults_match_the_plan()
    {
        var c = new WorldConfig();
        Assert.Equal((32, 32, 42, 6, 8), (c.Width, c.Height, c.Seed, c.AgentCount, c.BushCount));
        Assert.Equal((2, 25, 5, 5, 40, 5), (c.HungerTicksPerPoint, c.BerryNutrition, c.EatTicks, c.BushMaxBerries, c.BushRegrowTicks, c.PerceptionRadius));
        Assert.Null(c.HungerAlertThresholds);
    }

    [Fact]
    public void Partial_override_keeps_other_defaults()
    {
        var c = new WorldConfig(BushCount: 3);
        Assert.Equal(3, c.BushCount);
        Assert.Equal(new WorldConfig() with { BushCount = 3 }, c);
    }
}
