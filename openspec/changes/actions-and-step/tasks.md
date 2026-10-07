# Tasks

## 1. Types

- [ ] 1.1 Add `ActionKind` enum and `AgentDecision` record to Core exactly as written in the PoC plan, with XML doc comments; verify the solution builds with warnings as errors
- [ ] 1.2 Add `ActionOutcome` enum (`Completed`, `Failed`, `Interrupted`) and `ActionResult` record (`ActionKind Action, ActionOutcome Outcome, string? Reason`); add internal `AgentAction` (`Kind`, `TargetId?`, `Target` position, `TicksRemaining`); change `Agent.CurrentAction` to `AgentAction?` and add `Agent.LastAction` (`ActionResult?`); verify build and that the existing `Fresh_world_initial_state` test still passes
- [ ] 1.3 Add `EventKind` enum with the nine kinds and the flat `WorldEvent` record (`Tick, Kind, Subject, Position, Target?, Action?, Value?, Reason?`) with a doc comment stating what `Value` means per kind; verify with a test that two events built from the same arguments are equal and that `JsonSerializer.Serialize` emits properties in declaration order starting with `Tick`
- [ ] 1.4 Add `const int Speed = 256` and `const int Reach = 256` to `Position`; verify build

## 2. World plumbing and hand-placed worlds

- [ ] 2.1 Add `InternalsVisibleTo("Tokenville.Core.Tests")` to `Tokenville.Core.csproj` and the internal `World.FromLayout(config, agents, bushes)` factory sharing a private initializer with the seeded constructor; verify with a test that `FromLayout` with two agents at given positions and hungers and one bush with given berries yields ids `agent-1`, `agent-2`, `bush-1` with exactly those values, and that all existing `WorldSetupTests` still pass
- [ ] 2.2 Add `Submit(EntityId, AgentDecision)` backed by a `SortedDictionary` pending map (last write wins, non-living ids dropped) and the computed `AgentsAwaitingDecision`; verify with tests for the three "Decision submission" scenarios' non-throwing behavior and the three "Agents awaiting a decision" scenarios (fresh world lists all; running `Wait(5)` not listed; completed `Wait(1)` listed)
- [ ] 2.3 Add `IReadOnlyList<WorldEvent> Step()` with the six-phase skeleton, the per-step `_events` list cleared and copied out, and `Tick` increment; verify with tests that a world with no agents returns an empty list and increments `Tick`, that two kept lists from consecutive steps are independent, and that events of each step carry that step's pre-increment tick

## 3. Action start and Wait

- [ ] 3.1 Implement phase 1: validate each pending decision for idle agents in ascending order, start valid ones (`ActionStarted` with target id when present) and fail invalid ones (`ActionFailed` with the reason strings listed in design.md, `LastAction` set, agent stays idle); verify with tests for every validation scenario in the agent-actions spec: no target, out-of-bounds tile, bush tile, unknown id, eat out of reach, eat empty bush, eat with non-bush target, `Wait(0)`, `Wait(51)`
- [ ] 3.2 Implement `Wait` progress and completion in phase 2; verify with tests that `Wait(1)` starts and completes in one step (events in order started, completed), `Wait(3)` completes on the third step, and `LastAction` is `Completed`

## 4. MoveTo

- [ ] 4.1 Implement `MoveTo` to a tile center: integer step vector, exact arrival when `d <= Speed`, `AgentMoved` per position change, `ActionCompleted` on arrival; verify with tests for the straight run `(0,0)->(3,0)` positions `(384,128)`, `(640,128)`, `(896,128)`, the diagonal `(0,0)->(5,3)` ending exactly at `(1408, 896)` with every step at most 256 units, and `MoveTo(4,4)` from `(4,4)` completing with no move event
- [ ] 4.2 Implement the end-of-step bush block: `ActionFailed("blocked by bush-k")`, agent unmoved, action cleared; verify with tests for the blocked scenario (`(0,0)->(2,0)` with a bush at `(1,0)`) and an allowed corner crossing whose steps all end outside the bush tile
- [ ] 4.3 Implement `MoveTo(targetId)`: snapshot the entity position at start, complete when within `Reach` before moving, final-step shortening `min(Speed, d - Reach + 3)` with the margin as a named, commented constant; verify with tests for "within reach of an agent target completes without moving", the diagonal bush approach from `(0,0)` to a bush at `(3,1)` (completes within 4 steps, final squared distance <= 65536, final tile != bush tile), and the sweep over every start tile within 4 tiles of a lone bush on a 9x9 grid (all complete within 7 steps, none fail, all end within reach and outside the tile)

## 5. Eat, hunger, death, regrowth, alerts

- [ ] 5.1 Implement `Eat` countdown and completion with the berry re-check, hunger floor at 0, `AgentAte` (target bush, hunger after) then `ActionCompleted`, or `ActionFailed("bush is empty")`; verify with tests for the successful eat (hunger 40 -> 15, berries 2 -> 1, event order), the hunger floor, and the last-berry tie (`agent-1` eats, `agent-2` fails, bush at 0)
- [ ] 5.2 Implement phase 3 hunger growth on `(Tick + 1) % HungerTicksPerPoint == 0`, death at 100 with `AgentDied`, silent action drop and pending-decision drop for the dead; verify with tests that a default world with no decisions has every agent at hunger 99 after 198 steps and all dead after 200 with `AgentDied` at tick 199, that a dying agent mid-`MoveTo` emits only `AgentDied`, and that an all-dead world keeps ticking
- [ ] 5.3 Implement phase 4 regrowth with the counter running only below max; verify with tests that a berry picked from a full bush on tick `t` regrows with one `BushRegrew` at tick `t + 39` (berries after `t`..`t + 38` are max minus one) and that a full bush over 100 steps emits nothing
- [ ] 5.4 Implement threshold detection in phase 3 (upward crossings against `HungerAlertThresholds ?? [50, 80]`) and phase 5 emission: `HungerThresholdCrossed` then `ActionInterrupted` for non-`Eat` actions, action cleared, `LastAction` = `Interrupted`; verify with tests for `Wait` interrupted at 50 (event order, agent awaiting), `Eat` not interrupted at 80 (still running, not awaiting), an idle agent alerted with no interrupt, and re-crossing after eating producing two alerts

## 6. Integration

- [ ] 6.1 Add the "Order within a step" test: one step in which `agent-1` starts and moves, `agent-2` dies, a bush regrows and `agent-3` crosses a threshold, asserting the event kinds in the order started, moved, died, regrew, threshold-crossed; and the "Mixed outcomes" awaiting-decision test from the tick-pipeline spec
- [ ] 6.2 Add the determinism-by-construction test: two default worlds fed identical decisions each step for 200 steps return equal event lists and equal agent positions
- [ ] 6.3 Add the calibration smoke test: a default world driven 2,000 ticks by the in-test policy from design.md (eat nearest in-reach bush with berries at hunger >= 30, else move toward the nearest bush with berries, else wait) has all six agents alive and no agent ever ends a step inside a bush tile; verify it passes with 8 bushes and that at least one agent dies with 3 bushes

## 7. Wrap-up

- [ ] 7.1 Run `dotnet build` and `dotnet test` from `src/` with zero warnings; verify all tests pass
- [ ] 7.2 Draft the PR description stating: the three-item bundling exception, the MoveTo final-step deviation from the plan's sentence and why, and the chosen defaults (actions progress on their starting tick, hunger on `(tick+1) mod n`, upward-only alerts, no decision request during `Eat`, target snapshot at start, silent action end on death, regrow counter only below max, flat event record, no all-time event log), and that no config defaults changed and the contract types match the plan verbatim
