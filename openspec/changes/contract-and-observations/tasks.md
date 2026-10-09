# Tasks

## 1. OpenSpec prerequisite

- [ ] 1.1 Archive the merged `scripted-brain-and-runner` change (`openspec archive scripted-brain-and-runner`) so `event-log` and `simulation-runner` land in `openspec/specs/`; verify `openspec list --specs` lists both and `openspec validate contract-and-observations` no longer reports "target spec does not exist"

## 2. Recent events in Core

- [ ] 2.1 Add `internal List<WorldEvent> RecentEvents` to `Agent`; make `World.Emit` append the event to every living agent whose exact position is within `PerceptionRadius` of the event's position (same squared-distance test `Observe` uses); make `StartActions` clear the buffer of each agent whose pending decision it consumes, before validation; make `Observe` return a snapshot array of the buffer; verify `dotnet build src/Tokenville.slnx` passes with warnings as errors and all existing tests still pass
- [ ] 2.2 Replace `Recent_events_are_empty_for_now` in `ObservationTests` with tests for every scenario of the "Recent events since the last decision" requirement: own move seen (`ActionStarted` then `AgentMoved`), far `ActionStarted` not seen with radius 5 and agents at (0, 0) and (8, 8), near `AgentAte` seen, filtered at emission time (radius 2, walker from (0, 0) toward (6, 0), waiter's `ActionCompleted` absent after approach), events accumulate across a 3-tick wait, cleared when the next decision is consumed, `ActionFailed` for an unknown bush id reported, `HungerThresholdCrossed` then `ActionInterrupted` at the end; also assert a held observation's `RecentEvents` is unchanged after a further step; verify all pass
- [ ] 2.3 Add `<summary>` doc comments to `World.Config`, `World.Tick`, `World.Agents`, `World.Bushes`, `EntityKind`, `IEntity` and its members, and extend the `Observation` `<param name="RecentEvents">` with the since-last-decision rule and the sub-tile position note; verify the build is clean and `grep -c '///'` on those files increased

## 3. Decision lines in the log

- [ ] 3.1 Add `internal sealed record DecisionLine(long Tick, string Kind, EntityId Subject, AgentDecision Decision)` to the runner with a `Kind` constant `"Decision"` and a doc comment; make `Simulation.Run` write one serialized line plus `\n` right after each `Submit`; verify with `SimulationTests` for the two exact lines from the event-log spec (`MoveTo bush-1 hungry` at tick 5, `Wait 3` with no reason at tick 0), that a default world's first six lines are decision lines for agent-1..agent-6 at tick 0 followed only by event lines for tick 0, and that a 20-tick scripted run's decision-line count equals the number of delegate calls
- [ ] 3.2 Update `One_line_per_event_with_lf_endings` to count decision plus event lines and still assert no `\r`; keep `Two_scripted_runs_give_identical_logs` and add the assertion that the log contains `"Kind":"Decision"`; verify both pass

## 4. Replay

- [ ] 4.1 Add `Simulation.ReadDecisions(TextReader)` returning `IReadOnlyDictionary<(long, EntityId), AgentDecision>`: parse each line with `JsonDocument`, branch on `Kind`, deserialize `DecisionLine` or validate as `WorldEvent`, throw `FormatException` naming the 1-based line number on any failure; verify with a new `ReplayTests` class: a written `MoveTo (4, 5) wander agent-3 tick 12` line reads back equal, a log with `not json` as line 3 throws mentioning `line 3`, event lines are accepted and ignored
- [ ] 4.2 Add a replay decide delegate (lookup by `(world.Tick, agent.Id)`, `InvalidOperationException` naming agent and tick on a miss) as a static helper on `Simulation`; verify in `ReplayTests` that a 200-tick scripted default log replayed into a fresh default world for 200 ticks produces an identical string, that replaying the replay's output is identical again, that replaying against seed 43 throws naming an agent and a tick, that a log cut after tick 50 throws naming a tick above 50, and that shuffling the event lines of a log before replay changes nothing

## 5. Command line

- [ ] 5.1 Extend `Program.cs`: parse `--replay <path>` and `--dump-observation <agentId> <tick>` (usage exit 2 on a bad id or count), read the replay log inside the config `try` (exit 1 on `IOException`/`FormatException`), choose the replay delegate when `--replay` is given and scripted brains otherwise, catch `InvalidOperationException` around the run (message to stderr, exit 1), and update the usage string; verify by hand that `--replay events.jsonl` after a default run reproduces a byte-identical `events.jsonl` (`cmp`), that `--dump-observation seven 10` exits 2, and that `--replay missing.jsonl` exits 1
- [ ] 5.2 Implement the dump path: run with `maxTicks = tick`, `every = 0`, no view; exit 1 with "run ended at tick N" if the tick was not reached; exit 1 with the `Observe` error if the agent is dead; else print the compact JSON observation plus newline to stdout and exit 0; verify by hand that `--dump-observation agent-1 0` prints one line starting `{"Tick":0,"Self":{"Id":"agent-1"` with `"RecentEvents":[]`, that `--dump-observation agent-3 100` prints one line with `"Tick":100` and non-empty `RecentEvents` and no `tick ` header text, and that `--config` with `BushCount: 0` and `--dump-observation agent-1 2000` exits 1 naming the agent or the end tick

## 6. Integration

- [ ] 6.1 Run `dotnet build` and `dotnet test` from `src/` with zero warnings; verify all tests pass, the Phase 1 acceptance theory still passes, and `git status` shows no stray `events.jsonl`
- [ ] 6.2 Draft the PR description stating: the four stated choices from the proposal (sub-tile `WorldEvent` in `RecentEvents`, clear-on-consume with no cap, one log file, brain port deferred), the breaking log format, the two new flags and exit codes, the archive of `scripted-brain-and-runner` done in this PR, and that config defaults and contract type shapes are untouched

## Workflow follow-up

- Archive this change after review; the main `agent-observation`, `event-log` and `simulation-runner` specs and the new `run-replay` spec are synced then.
- Next change: port `ScriptedBrain` to `Observation` and re-run the Phase 1 acceptance under perception.
