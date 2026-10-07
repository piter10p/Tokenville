namespace Tokenville.Core;

/// <summary>Read-only view shared by every entity: where it is and what it is called.</summary>
public interface IEntity
{
    EntityId Id { get; }
    Position Position { get; }
}
