namespace Tokenville.Core;

/// <summary>An agent that must eat to survive. Mutable state is changed only by <see cref="World"/>.</summary>
public sealed class Agent : IEntity
{
    private static readonly string[] Names =
    [
        "Ada", "Bram", "Cleo", "Dov", "Esme", "Finn",
        "Greta", "Hugo", "Ines", "Jory", "Kaia", "Lior",
    ];

    internal Agent(EntityId id, Position position)
    {
        Id = id;
        Name = Names[(id.Number - 1) % Names.Length];
        Position = position;
    }

    public EntityId Id { get; }
    public string Name { get; }
    public Position Position { get; internal set; }

    /// <summary>0 = full, 100 = dead.</summary>
    public int Hunger { get; internal set; }

    /// <summary>The running action, or null when the agent is idle and awaiting a decision.</summary>
    public AgentAction? CurrentAction { get; internal set; }

    /// <summary>How the most recent action ended; null until the first one ends.</summary>
    public ActionResult? LastAction { get; internal set; }
}
