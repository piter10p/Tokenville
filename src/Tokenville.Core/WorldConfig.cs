namespace Tokenville.Core;

/// <summary>Immutable simulation configuration. Defaults are the PoC plan's; the runner loads overrides from JSON.</summary>
public sealed record WorldConfig(
    int Width = 32, int Height = 32, int Seed = 42,
    int AgentCount = 6, int BushCount = 8,
    int HungerTicksPerPoint = 2, int BerryNutrition = 25,
    int EatTicks = 5, int BushMaxBerries = 5, int BushRegrowTicks = 40,
    int PerceptionRadius = 5, int[]? HungerAlertThresholds = null); // default [50, 80]
