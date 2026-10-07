namespace Tokenville.Core.Tests;

public class PositionTests
{
    [Fact]
    public void Distance_on_3_4_5_triangle_is_exact_and_symmetric()
    {
        var a = new Position(0, 0);
        var b = new Position(768, 1024);
        Assert.Equal(1280, a.DistanceTo(b));
        Assert.Equal(1280, b.DistanceTo(a));
    }

    [Fact]
    public void Distance_rounds_down()
    {
        Assert.Equal(1, new Position(0, 0).DistanceTo(new Position(1, 1)));
        Assert.Equal(2, new Position(0, 0).DistanceSquaredTo(new Position(1, 1)));
    }

    [Fact]
    public void Distance_to_self_is_zero()
    {
        Assert.Equal(0, new Position(5, 5).DistanceTo(new Position(5, 5)));
    }

    [Theory]
    [InlineData(15, 0, 15)]
    [InlineData(16, 0, 16)]
    [InlineData(255, 0, 255)]
    [InlineData(256, 0, 256)]
    [InlineData(46340, 0, 46340)]
    [InlineData(int.MaxValue, 0, int.MaxValue)]
    [InlineData(2, 2, 2)]           // sqrt(8)  = 2.83
    [InlineData(3, 3, 4)]           // sqrt(18) = 4.24
    [InlineData(46340, 46340, 65534)] // sqrt(4294791200) = 65534.66
    public void Distance_is_floor_of_square_root(int dx, int dy, int expected)
    {
        Assert.Equal(expected, new Position(0, 0).DistanceTo(new Position(dx, dy)));
    }

    [Fact]
    public void Tile_center_and_index()
    {
        Assert.Equal(new Position(3456, 640), Position.FromTileCenter(13, 2));
        Assert.Equal(13, new Position(3583, 0).TileX);
        Assert.Equal(14, new Position(3584, 0).TileX);
        Assert.Equal(2, new Position(0, 640).TileY);
    }
}
