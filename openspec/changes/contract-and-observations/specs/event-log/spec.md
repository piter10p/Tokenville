# Spec Delta

## MODIFIED Requirements

### Requirement: One line per event in emission order
The runner SHALL write one JSON object per line and nothing else. For every tick it SHALL write one decision line per decision submitted for that tick, in ascending agent id order, followed by one event line per world event of that tick in the order the engine emitted them. Ticks SHALL ascend through the file. Each line SHALL end with a single line feed, and the file SHALL be UTF-8 without a byte order mark.

#### Scenario: Line count matches decision and event count
- **WHEN** a run's steps submit 2, 0 and 1 decisions and emit 3, 0 and 2 events
- **THEN** the log holds exactly 8 lines: the first tick's two decision lines, then its three event lines, then the last tick's one decision line and two event lines

#### Scenario: Decisions precede their tick's events
- **WHEN** a default world runs one tick with six idle agents
- **THEN** the first six lines are decision lines for agent-1 through agent-6 with tick 0, and every later line of tick 0 is an event line

#### Scenario: Line endings
- **WHEN** a log is written on any operating system
- **THEN** every line ends with `\n`, no `\r` appears, and the file does not start with a byte order mark

### Requirement: Identical runs produce identical logs
Two runs with the same configuration and the same brains SHALL produce byte-identical logs, decision lines included.

#### Scenario: Two scripted runs
- **WHEN** the default world is run twice for 200 ticks with scripted brains
- **THEN** the two log contents are equal and contain at least one decision line and one `AgentAte` line

## ADDED Requirements

### Requirement: Decision line format
A decision line SHALL be a compact JSON object with properties in this fixed order: `Tick` (the tick the decision was submitted for, equal to the tick of the step that consumes it), `Kind` with the value `Decision`, `Subject` (the agent id in text form) and `Decision` (the decision object with properties `Action`, `TargetId`, `X`, `Y`, `Ticks`, `Reason` in that order, absent values omitted, kinds by name). No whitespace SHALL appear between tokens.

#### Scenario: Move-to-entity decision line
- **WHEN** agent-1 submits `MoveTo` with target `bush-1` and reason `hungry` at tick 5
- **THEN** the line is `{"Tick":5,"Kind":"Decision","Subject":"agent-1","Decision":{"Action":"MoveTo","TargetId":"bush-1","Reason":"hungry"}}`

#### Scenario: Wait decision line without a reason
- **WHEN** agent-2 submits `Wait` for 3 ticks with no reason at tick 0
- **THEN** the line is `{"Tick":0,"Kind":"Decision","Subject":"agent-2","Decision":{"Action":"Wait","Ticks":3}}`

#### Scenario: Decision lines are distinguishable from event lines
- **WHEN** a reader inspects any line's `Kind` property
- **THEN** it is `Decision` for a decision line and one of the event kinds for an event line, and no event kind is named `Decision`

### Requirement: Decision lines round-trip
Reading a decision line back SHALL yield the tick, the agent id and a decision equal to the one that was submitted, so that a log can drive a replay.

#### Scenario: Round trip
- **WHEN** a `MoveTo` to tile (4, 5) with reason `wander` for agent-3 at tick 12 is written and read back
- **THEN** the read value is tick 12, agent-3 and a decision equal to the original
