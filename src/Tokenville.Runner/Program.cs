using System.Globalization;
using System.Text;
using System.Text.Json;
using Tokenville.Brains;
using Tokenville.Core;
using Tokenville.Runner;

const string Usage = "usage: Tokenville.Runner [--config <path>] [--max-ticks <n>] [--every <n>] [--log <path>]";
string? configPath = null;
var logPath = "events.jsonl";
var maxTicks = 2000;
var every = 50;

for (var i = 0; i < args.Length; i++)
{
    var value = i + 1 < args.Length ? args[i + 1] : null;
    switch (args[i])
    {
        case "--config" when value is not null: configPath = value; i++; break;
        case "--log" when value is not null: logPath = value; i++; break;
        case "--max-ticks" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out maxTicks): i++; break;
        case "--every" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out every): i++; break;
        default:
            Console.Error.WriteLine(Usage);
            return 2;
    }
}

World world;
try
{
    var config = configPath is null
        ? new WorldConfig()
        : JsonSerializer.Deserialize<WorldConfig>(File.ReadAllText(configPath), JsonFormat.Options)
          ?? throw new JsonException($"'{configPath}' holds no config object.");
    world = new World(config);
}
catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or ArgumentException)
{
    Console.Error.WriteLine($"Config error: {e.Message}");
    return 1;
}

var brains = world.Agents.ToDictionary(a => a.Id, a => new ScriptedBrain(world.Config.Seed, a.Id.Number));
using (var log = new StreamWriter(logPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
    Simulation.Run(world, (w, a) => brains[a.Id].Decide(w, a), log, maxTicks, every, Console.Out);
return 0;
