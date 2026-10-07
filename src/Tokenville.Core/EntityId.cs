using System.Globalization;

namespace Tokenville.Core;

/// <summary>The two kinds of thing that exist in the world.</summary>
public enum EntityKind
{
    Agent,
    Bush,
}

/// <summary>
/// Stable, readable identity of an entity. Renders as <c>agent-1</c> or <c>bush-1</c> and
/// orders by kind, then numerically by number, so <c>agent-2</c> precedes <c>agent-10</c>.
/// </summary>
public readonly record struct EntityId(EntityKind Kind, int Number) : IComparable<EntityId>
{
    public int CompareTo(EntityId other)
    {
        var byKind = Kind.CompareTo(other.Kind);
        return byKind != 0 ? byKind : Number.CompareTo(other.Number);
    }

    public override string ToString() => $"{Prefix(Kind)}-{Number.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Parses the rendered form. Any other text, or a non-positive number, yields false.</summary>
    public static bool TryParse(string? text, out EntityId id)
    {
        id = default;
        if (text is null) return false;
        var dash = text.IndexOf('-');
        if (dash < 0) return false;

        EntityKind kind;
        switch (text.AsSpan(0, dash))
        {
            case "agent": kind = EntityKind.Agent; break;
            case "bush": kind = EntityKind.Bush; break;
            default: return false;
        }

        if (!int.TryParse(text.AsSpan(dash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < 1)
            return false;

        id = new EntityId(kind, number);
        return true;
    }

    private static string Prefix(EntityKind kind) => kind == EntityKind.Agent ? "agent" : "bush";
}
