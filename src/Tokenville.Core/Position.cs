using System.Numerics;

namespace Tokenville.Core;

/// <summary>
/// Fixed-point position in sub-tile units, <see cref="TileSize"/> units per tile, (0, 0) at the
/// top-left corner of the top-left tile. Integer arithmetic only; never floating point.
/// </summary>
public readonly record struct Position(int X, int Y)
{
    /// <summary>Units per tile. Tile index is <c>units >> 8</c>, sub-tile offset is <c>units &amp; 255</c>.</summary>
    public const int TileSize = 256;

    public int TileX => X >> 8;
    public int TileY => Y >> 8;

    /// <summary>The center of tile <paramref name="tileX"/>, <paramref name="tileY"/>.</summary>
    public static Position FromTileCenter(int tileX, int tileY) =>
        new(tileX * TileSize + TileSize / 2, tileY * TileSize + TileSize / 2);

    /// <summary>Squared Euclidean distance; use for range checks so no root is taken.</summary>
    public long DistanceSquaredTo(Position other)
    {
        long dx = (long)X - other.X;
        long dy = (long)Y - other.Y;
        return dx * dx + dy * dy;
    }

    /// <summary>Euclidean distance in units, rounded down.</summary>
    public int DistanceTo(Position other) => (int)ISqrt(DistanceSquaredTo(other));

    /// <summary>Floor of the square root, by Newton iteration from an upper bound.</summary>
    private static long ISqrt(long n)
    {
        if (n < 2) return n;
        var x = 1L << (BitOperations.Log2((ulong)n) / 2 + 1); // >= sqrt(n)
        while (true)
        {
            var y = (x + n / x) / 2;
            if (y >= x) return x;
            x = y;
        }
    }
}
