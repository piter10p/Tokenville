# Spec Delta

## Purpose

Defines how the console runner drives a world with brains in lockstep, when it stops, what it prints while running, and how it is invoked from the command line.

## ADDED Requirements

### Requirement: Lockstep loop
On every tick the runner SHALL, for each agent awaiting a decision in ascending id order, obtain a decision from that agent's brain and submit it, then step the world once, then log the step's events. The world SHALL never step while an agent awaiting a decision has none.

#### Scenario: Every idle agent is asked
- **WHEN** a default world with 6 agents is run for one tick
- **THEN** the first tick's events include an `ActionStarted` or `ActionFailed` for each of the six agents

#### Scenario: Busy agents are not asked
- **WHEN** an agent is in the middle of a 5-tick wait
- **THEN** its brain is not consulted on the next tick

### Requirement: The run ends on all dead or max ticks
The runner SHALL stop after the step in which the last living agent died, or after the step that brings the tick count to the configured maximum, whichever comes first, and SHALL then print a one-line summary stating the final tick and the number of living agents.

#### Scenario: Tick limit
- **WHEN** a default world is run with a maximum of 10 ticks
- **THEN** the run stops with the world's tick at 10 and six living agents

#### Scenario: Everyone dies first
- **WHEN** a world whose only agent has hunger 99 and no bush is run with a maximum of 100 ticks under default hunger pacing
- **THEN** the run stops with the world's tick at 2 and no living agents

### Requirement: ASCII map
The map SHALL have one text row per world row from top to bottom and one character per tile from left to right. An empty tile SHALL render as `.`, a bush as its berry count as a single digit (counts above 9 render as `9`), and an agent as the first letter of its name. When several agents share a tile, the one with the lowest id SHALL be shown.

#### Scenario: Small map
- **WHEN** a 3x3 world has `bush-1` with 4 berries at `(1, 1)`, `agent-1` (Ada) at tile `(0, 0)` and `agent-2` (Bram) at tile `(2, 2)`
- **THEN** the map rows are `A..`, `.4.` and `..B`

#### Scenario: Shared tile
- **WHEN** `agent-1` (Ada) and `agent-2` (Bram) stand on the same tile
- **THEN** that tile renders as `A`

### Requirement: Stats panel
Below the map the runner SHALL print a header with the current tick, the number of living agents out of the starting count, and the total berries over all bushes; then one line per agent in ascending id order giving its id, name, tile coordinates, hunger and current action (the action kind and its target id, coordinates or remaining ticks, or `idle`); dead agents SHALL be listed with the tick at which they died instead.

#### Scenario: Living agent line
- **WHEN** `agent-1` (Ada) at tile `(12, 7)` with hunger 34 is eating from `bush-3`
- **THEN** its panel line contains `agent-1`, `Ada`, `(12, 7)`, `34` and `Eat bush-3`

#### Scenario: Dead agent line
- **WHEN** `agent-4` died at tick 312
- **THEN** its panel line contains `agent-4` and `died at tick 312`

#### Scenario: Header
- **WHEN** the world is at tick 250 with 5 of 6 agents alive and bushes holding 3, 5 and 0 berries
- **THEN** the header contains `tick 250`, `alive 5/6` and `berries 8`

### Requirement: Snapshot cadence
With a snapshot interval N greater than zero, the runner SHALL print the map and panel after every step whose resulting tick is a multiple of N, and once more when the run ends if the final tick is not such a multiple. With N equal to zero it SHALL print only the final snapshot.

#### Scenario: Periodic snapshots
- **WHEN** a world runs to tick 120 with interval 50
- **THEN** snapshots are printed at ticks 50, 100 and 120

#### Scenario: Only the final snapshot
- **WHEN** a world runs to tick 120 with interval 0
- **THEN** exactly one snapshot is printed, at tick 120

### Requirement: Command line
The runner SHALL accept `--config <path>` (JSON world config; defaults when absent), `--max-ticks <n>` (default 2000), `--every <n>` (snapshot interval, default 50) and `--log <path>` (default `events.jsonl`, overwritten). An unknown flag, a missing flag value or a non-integer count SHALL print usage to standard error and exit with code 2. An unreadable or invalid config file SHALL print the error to standard error and exit with code 1. A completed run SHALL exit with code 0 whether or not agents survived.

#### Scenario: Defaults
- **WHEN** the runner is started with no arguments
- **THEN** it runs the default config for 2000 ticks, writes `events.jsonl` in the working directory, prints a snapshot every 50 ticks and exits with code 0

#### Scenario: Bad flag
- **WHEN** the runner is started with `--ticks 10`
- **THEN** it prints usage to standard error and exits with code 2

#### Scenario: Bad config
- **WHEN** the runner is started with `--config` pointing at a file containing `{"Widht": 5}`
- **THEN** it prints the config error to standard error and exits with code 1
