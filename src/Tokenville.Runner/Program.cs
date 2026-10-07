using System.Text.Json;
using System.Text.Json.Serialization;
using Tokenville.Core;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
};

WorldConfig config;
try
{
    config = args.Length > 0
        ? JsonSerializer.Deserialize<WorldConfig>(File.ReadAllText(args[0]), jsonOptions)
          ?? throw new JsonException($"'{args[0]}' holds no config object.")
        : new WorldConfig();
}
catch (JsonException e)
{
    Console.Error.WriteLine($"Config error: {e.Message}");
    return 1;
}

Console.WriteLine(JsonSerializer.Serialize(config));
return 0;
