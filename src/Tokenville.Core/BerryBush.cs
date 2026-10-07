namespace Tokenville.Core;

/// <summary>A block welded to one tile; its position is that tile's center.</summary>
public sealed class BerryBush : IEntity
{
    internal BerryBush(EntityId id, Position position, int berries)
    {
        Id = id;
        Position = position;
        Berries = berries;
    }

    public EntityId Id { get; }
    public Position Position { get; }
    public int Berries { get; internal set; }
    public int RegrowCounter { get; internal set; }
}
