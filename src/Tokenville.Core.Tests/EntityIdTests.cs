namespace Tokenville.Core.Tests;

public class EntityIdTests
{
    [Fact]
    public void Renders_as_kind_dash_number()
    {
        Assert.Equal("agent-1", new EntityId(EntityKind.Agent, 1).ToString());
        Assert.Equal("bush-7", new EntityId(EntityKind.Bush, 7).ToString());
    }

    [Fact]
    public void Round_trips_through_text()
    {
        Assert.True(EntityId.TryParse("bush-12", out var id));
        Assert.Equal(new EntityId(EntityKind.Bush, 12), id);
        Assert.Equal("bush-12", id.ToString());
    }

    [Theory]
    [InlineData("tree-1")]
    [InlineData("agent-")]
    [InlineData("agent-x")]
    [InlineData("agent-0")]
    [InlineData("agent--1")]
    [InlineData("agent")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_other_text_without_throwing(string? text)
    {
        Assert.False(EntityId.TryParse(text, out _));
    }

    [Fact]
    public void Orders_numerically_within_kind_and_by_kind_first()
    {
        var ids = new[]
        {
            new EntityId(EntityKind.Bush, 1),
            new EntityId(EntityKind.Agent, 10),
            new EntityId(EntityKind.Agent, 2),
            new EntityId(EntityKind.Agent, 1),
        };
        Array.Sort(ids);
        Assert.Equal(["agent-1", "agent-2", "agent-10", "bush-1"], ids.Select(i => i.ToString()));
    }
}
