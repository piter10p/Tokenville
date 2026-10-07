# Design

## Context

See proposal.md for motivation. `Tokenville.Core` is empty; the runner prints `Hello, World!`; the test project has one smoke test. The binding constraints come from the PoC plan's engineering rules: one seeded `Random` owned by the world, ascending id iteration, integer math, no I/O or packages in Core, immutable records for value types, mutable state only in `World` and its entities. .NET 10 SDK 10.0.401 is installed.

## Goals / Non-Goals

**Goals:**
- Fix the shapes that later tasks build on and cannot easily change: how ids sort and parse, how positions measure distance, where config lives, and the exact RNG draw order at setup.
- Keep everything in this change testable without `Step()`.

**Non-Goals:**
- Anything that consumes the world: actions, tick pipeline, events, observation, runner loop, ASCII map.
- Resolving `HungerAlertThresholds ?? [50, 80]`; the field exists on the record but nothing reads it yet.
- Reach checks (`DistanceSquaredTo <= Reach * Reach`), bush-tile collision and movement stepping; they arrive with the actions change and only need what `Position` exposes here.

## Decisions

**`EntityId` is `readonly record struct EntityId(EntityKind Kind, int Number) : IComparable<EntityId>`.**
Kind is an enum (`Agent`, `Bush`). `ToString()` renders `agent-1`; `TryParse` accepts exactly that form with a positive number. Comparison is kind, then number.
Alternatives: a wrapped string orders `agent-10` before `agent-2`, which breaks the tick pipeline's "lower id wins" rule; separate `AgentId`/`BushId` structs need a union for `MoveTo(targetId)`, which can target either kind; a length-then-ordinal string comparer is numeric for same-kind ids but is the kind of cleverness that gets decoded at 3am. Kind is stored because the string form has a prefix that must round-trip, and because the engine must route a parsed `TargetId` to the right dictionary.

**`Position` is `readonly record struct Position(int X, int Y)` in fixed-point sub-tile units, `const int TileSize = 256`.**
Members: `TileX`/`TileY` (`X >> 8`, `Y >> 8`; positions are never negative), `static FromTileCenter(tx, ty)`, `long DistanceSquaredTo(Position)` and `int DistanceTo(Position)` as the floor of an integer square root. The square root is a private Newton iteration on `long` inside `Position`; nothing else needs it, and MoveTo's step vector reuses `DistanceTo`.
Alternatives: `Math.Sqrt` is correctly rounded on every platform and would be deterministic in practice, but the plan's no-float rule exists so nobody has to reason about that case by case. A separate `IntMath` type would be public surface with one caller. 256 over 1000: shift and mask for tile conversion, exact binary fractions, and the Factorio precedent; the cost is that `3456` does not read as tile 13.5 at a glance.

**`IEntity` with `EntityId Id` and `Position Position`, implemented by `Agent` and `BerryBush`.**
Requested during exploration. Its first real caller is MoveTo target resolution in the actions change; perception filtering and the ASCII map follow. It is a view, not storage: the world keeps two typed dictionaries, never one `SortedDictionary<EntityId, IEntity>`, because Eat and the hunger step would otherwise cast back on every tick.

**`WorldConfig` is copied verbatim from the plan**, including `int[]? HungerAlertThresholds = null`. The plan forbids changing config or contract shape without flagging it, so the array stays even though it makes record equality reference-based for that one field. Nothing in this change compares configs.

**Config loading is one `JsonSerializer.Deserialize<WorldConfig>` call in the runner** with `PropertyNameCaseInsensitive = true` and `UnmappedMemberHandling = Disallow`. System.Text.Json binds positional records through the constructor and fills absent properties from constructor defaults, so a sparse file works with no custom code. Disallowing unmapped members is the only validation worth having at this trust boundary: a typo silently falling back to a default would be a hard-to-notice wrong run. No `Spectre.Console.Cli`; `args` is read directly. The loader stays in the runner so Core keeps zero I/O.

**Placement is rejection sampling with a fixed draw order.** For each entity: `x = rng.Next(Width)`, then `y = rng.Next(Height)`; if the tile holds a bush, redraw both. Bushes are placed first (`bush-1..M`), then agents (`agent-1..N`). Ids are assigned in placement order. Both kinds get the drawn tile's center as their position (`Position.FromTileCenter`), so the occupancy check during agent placement is a comparison of tile centers against the bushes already placed; eight bushes make a `HashSet` unnecessary. The constructor's validation (`BushCount <= Width*Height`, and a free tile exists when `AgentCount > 0`) guarantees termination.
Alternative: Fisher-Yates over all tiles has fixed cost and no retry loop, but is more code for a problem the guard already removes, and with 8 bushes on 1024 tiles retries are vanishingly rare. The draw order is documented in code because it is what byte-identical logs depend on.

**RNG is `new Random(config.Seed)`, owned by `World`.** As the plan specifies. Seeded `System.Random` is stable across platforms and has been stable across .NET versions, but Microsoft does not formally promise it forever. A ten-line PCG32 would be ours permanently; we follow the plan and accept the trade. If a future .NET version changes the seeded algorithm, swapping in a private generator is a one-file change and the determinism test will catch it immediately.

**Entities are plain mutable classes** (`Agent`, `BerryBush`) with `{ get; internal set; }` for fields the engine mutates. They live in two `SortedDictionary<EntityId, T>` fields on `World`, exposed as `IReadOnlyCollection<T>` via `.Values`. One stdlib type gives both id lookup and the ascending iteration the rules demand. `World` exposes `Config`, `Tick` (`long`, matching `Observation.Tick`), `Agents` and `Bushes`.

**Initial hunger is 0 for every agent.** The plan says `0 = full` and is silent on start value; 0 is the simplest deterministic option. Side effect: all agents cross the 50 threshold on the same tick, so early Phase 1 logs will look synchronized. Noted, not a problem for a PoC.

**Agent names come from a static list in Core**, indexed `(Number - 1) % Names.Length`. Wrapping produces duplicate names past the list length; harmless because ids are the identity.

**Agents never start on a bush tile.** The plan says a bush "blocks its own tile"; we read that as applying at setup too.

## Risks / Trade-offs

- [Seeded `System.Random` algorithm changes in a future .NET] → Phase 2 determinism test fails loudly; swap in a private PCG32 behind the same `Next(int)` calls.
- [Golden-coordinate tests would pin draw order but break on any refactor] → Tests assert invariants (same seed same layout, different seed different layout, no overlap, bounds, ids) rather than exact coordinates. Exact-output pinning is the Phase 2 determinism test's job.
- [Squared distances overflow] → Positions are bounded by `Width * 256` and `Height * 256`, so `dx² + dy²` fits in `long` whenever those extents fit in `int`. World validation computes them with `checked` arithmetic, so an absurd grid throws instead of wrapping silently.
- [Rejection sampling could spin on a nearly full grid] → Validation rejects configs with no free tile; a 4x4 grid with 15 bushes and agents is the worst allowed case and still terminates in expected 16 draws per agent.
- [`IEntity` lands with no caller in this change] → Accepted deliberately at the user's request; cost is two interface members and two `: IEntity` clauses.
- [Bundling three checklist items in one PR] → Stated in proposal and PR description; the items have no independently testable behavior.
