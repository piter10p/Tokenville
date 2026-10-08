# Spec Delta

## Purpose

Defines the JSON Lines file the runner writes during a run, so that what happened can be inspected afterwards and two runs can be compared byte for byte.

## ADDED Requirements

### Requirement: One line per event in emission order
The runner SHALL write one JSON object per world event, one per line, in the order the engine emitted them, with ticks ascending. The file SHALL contain nothing else. Each line SHALL end with a single line feed, and the file SHALL be UTF-8 without a byte order mark.

#### Scenario: Line count matches event count
- **WHEN** a run's steps emit 3, 0 and 2 events
- **THEN** the log holds exactly 5 lines, the first three from the first step in emission order

#### Scenario: Line endings
- **WHEN** a log is written on any operating system
- **THEN** every line ends with `\n`, no `\r` appears, and the file does not start with a byte order mark

### Requirement: Event line format
Each line SHALL be a compact JSON object whose properties appear in this fixed order: `Tick`, `Kind`, `Subject`, `Position`, `Target`, `Action`, `Value`, `Reason`. Entity ids SHALL be written as their text form (`agent-1`). Event and action kinds SHALL be written as their names. `Position` SHALL be an object with `X` and `Y` in sub-tile units. Properties whose value is absent SHALL be omitted. Numbers SHALL use invariant formatting, and no whitespace SHALL appear between tokens.

#### Scenario: Action started line
- **WHEN** the event is `ActionStarted` at tick 0 for `agent-1` at position `(128, 128)` with target `bush-1` and action `MoveTo`
- **THEN** the line is `{"Tick":0,"Kind":"ActionStarted","Subject":"agent-1","Position":{"X":128,"Y":128},"Target":"bush-1","Action":"MoveTo"}`

#### Scenario: Bush regrew line
- **WHEN** the event is `BushRegrew` at tick 39 for `bush-1` at `(384, 128)` with value 5
- **THEN** the line is `{"Tick":39,"Kind":"BushRegrew","Subject":"bush-1","Position":{"X":384,"Y":128},"Value":5}`

#### Scenario: Failed action line
- **WHEN** the event is `ActionFailed` at tick 7 for `agent-2` at `(640, 128)` with action `Eat` and reason `bush is empty`
- **THEN** the line is `{"Tick":7,"Kind":"ActionFailed","Subject":"agent-2","Position":{"X":640,"Y":128},"Action":"Eat","Reason":"bush is empty"}`

### Requirement: Identical runs produce identical logs
Two runs with the same configuration and the same brains SHALL produce byte-identical logs.

#### Scenario: Two scripted runs
- **WHEN** the default world is run twice for 200 ticks with scripted brains
- **THEN** the two log contents are equal
