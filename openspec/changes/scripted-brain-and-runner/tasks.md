# Tasks

## 1. Project wiring

- [x] 1.1 Add project references from `Tokenville.Core.Tests` to `Tokenville.Brains` and `Tokenville.Runner`; verify `dotnet build src/Tokenville.slnx` succeeds with warnings as errors and `dotnet test` still passes all existing tests

## 2. Scripted brain

- [x] 2.1 Add `ScriptedBrain` to `Tokenville.Brains` with a constructor taking the world seed and agent number, the documented seed formula `unchecked(seed * 1000 + agentNumber)`, and `Decide(World, Agent)` implementing rules in design.md order: sidestep, hungry, wander, each with its one-word `Reason`; verify build
- [x] 2.2 Add `ScriptedBrainTests` on hand-placed worlds covering every scripted-brain spec scenario: hungry in reach gives `Eat`, out of reach gives `MoveTo` by id, empty bushes ignored, tie goes to the lower id, hunger 29 beside food is not `Eat`, 200 wander decisions on a 3x3 with a center bush are all in bounds and never the bush tile, no berries anywhere gives a wander `MoveTo`, sidestep below, sidestep above when below is a bush, boxed-in gives `Wait(1)`, two brains with the same seed give equal 100-decision sequences, and every decision has a non-empty reason; verify all pass

## 3. JSON options and event log

- [x] 3.1 Add a shared `JsonSerializerOptions` to the runner (case-insensitive read, unmapped members rejected, string enums, `EntityId` string converter with `TryParse` on read, nulls omitted) and switch config loading to it; verify a round trip of `new EntityId(Agent, 3)` through serialize and deserialize and that the existing config loading behavior is unchanged
- [x] 3.2 Add `Simulation.Run(World, Func<World, Agent, AgentDecision>, TextWriter log, int maxTicks, int every, TextWriter? view)` with the lockstep loop, per-event line writing with `\n`, death tracking and the stop conditions; verify with tests for the three exact event lines from the event-log spec, that line count equals the total event count over a short run, that no `\r` appears, that a 10-tick run stops at tick 10 with six agents, and that a lone 99-hunger agent under default pacing stops the run at tick 2
- [x] 3.3 Add the determinism test: two default worlds run 200 ticks with scripted brains into two `StringWriter`s produce equal strings; verify it passes

## 4. Console view

- [x] 4.1 Add `ConsoleView.Render(World, deaths, startingAgents)` returning the map, header and per-agent lines with the glyph and action text rules from design.md; verify with tests for the 3x3 map rows `A..`, `.4.`, `..B`, the shared-tile case rendering `A`, a berry count above 9 rendering `9`, a living agent line containing `agent-1`, `Ada`, `(12, 7)`, `34` and `Eat bush-3`, a dead agent line containing `died at tick 312`, and a header containing `tick 250`, `alive 5/6` and `berries 8`
- [x] 4.2 Wire rendering into `Simulation.Run` with the condition `finished || (every > 0 && Tick % every == 0)` followed by the summary line at the end; verify with tests that interval 50 to tick 120 renders exactly three snapshots and interval 0 renders exactly one, counted by occurrences of the header prefix in the view writer

## 5. Command line

- [x] 5.1 Rewrite `Program.cs`: parse `--config`, `--max-ticks` (2000), `--every` (50), `--log` (`events.jsonl`), build the world and one `ScriptedBrain` per agent, open the log with UTF-8 without BOM, call `Simulation.Run` with `Console.Out` as the view, exit 2 on usage errors, 1 on config errors, 0 otherwise; verify by running `dotnet run --project src/Tokenville.Runner -- --max-ticks 100 --every 50` and checking two snapshots, a summary line, exit code 0 and a non-empty `events.jsonl` whose first bytes are `{"Tick":0`
- [x] 5.2 Verify the error paths by hand: `--ticks 10` prints usage and exits 2; `--config` pointing at a file containing `{"Widht": 5}` prints the config error and exits 1; `--every 0 --max-ticks 20` prints exactly one snapshot

## 6. Acceptance and wrap-up

- [x] 6.1 Add the Phase 1 acceptance tests driven by `ScriptedBrain` through `Simulation.Run`: default config has six living agents at tick 2000, and `BushCount = 3` has fewer than six; verify both pass and remove nothing from `CalibrationTests`
- [x] 6.2 Run `dotnet build` and `dotnet test` from `src/` with zero warnings; verify all tests pass and `git status` shows no generated `events.jsonl` to commit (add it to `.gitignore` if the run in 5.1 left one in the repo)
- [x] 6.3 Draft the PR description stating: the two-item bundling, the brain reading `World`/`Agent` instead of `Observation` and being omniscient until Phase 2, no `IAgentBrain` yet and why, Spectre left unused, the single test project referencing Brains and Runner, the chosen defaults (hunger threshold 30, sidestep order below/above/right/left, seed formula, PascalCase JSON with omitted nulls and string enums, LF endings, snapshot every 50, exit codes), and that Core, config defaults and contract types are untouched
