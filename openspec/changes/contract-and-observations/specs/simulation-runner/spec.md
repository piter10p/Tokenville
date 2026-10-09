# Spec Delta

## MODIFIED Requirements

### Requirement: Lockstep loop
On every tick the runner SHALL, for each agent awaiting a decision in ascending id order, obtain a decision from that agent's decision source, submit it and write it to the log as a decision line, then step the world once, then log the step's events. The world SHALL never step while an agent awaiting a decision has none.

#### Scenario: Every idle agent is asked
- **WHEN** a default world with 6 agents is run for one tick
- **THEN** the first tick's events include an `ActionStarted` or `ActionFailed` for each of the six agents

#### Scenario: Busy agents are not asked
- **WHEN** an agent is in the middle of a 5-tick wait
- **THEN** its brain is not consulted on the next tick and no decision line is written for it

#### Scenario: Every submitted decision is logged
- **WHEN** a default world runs 20 ticks with scripted brains
- **THEN** the number of decision lines in the log equals the number of decisions the brains returned

### Requirement: Command line
The runner SHALL accept `--config <path>` (JSON world config; defaults when absent), `--max-ticks <n>` (default 2000), `--every <n>` (snapshot interval, default 50), `--log <path>` (default `events.jsonl`, overwritten), `--replay <path>` (a log to take decisions from instead of brains) and `--dump-observation <agentId> <tick>` (print one observation instead of running to the end). An unknown flag, a missing flag value, a non-integer count or an unparsable agent id SHALL print usage to standard error and exit with code 2. An unreadable or invalid config file or replay log SHALL print the error to standard error and exit with code 1. A completed run SHALL exit with code 0 whether or not agents survived.

#### Scenario: Defaults
- **WHEN** the runner is started with no arguments
- **THEN** it runs the default config for 2000 ticks, writes `events.jsonl` in the working directory, prints a snapshot every 50 ticks and exits with code 0

#### Scenario: Bad flag
- **WHEN** the runner is started with `--ticks 10`
- **THEN** it prints usage to standard error and exits with code 2

#### Scenario: Bad config
- **WHEN** the runner is started with `--config` pointing at a file containing `{"Widht": 5}`
- **THEN** it prints the config error to standard error and exits with code 1

#### Scenario: Bad agent id
- **WHEN** the runner is started with `--dump-observation seven 10`
- **THEN** it prints usage to standard error and exits with code 2

#### Scenario: Missing replay file
- **WHEN** the runner is started with `--replay` pointing at a path that does not exist
- **THEN** it prints the error to standard error and exits with code 1

## ADDED Requirements

### Requirement: Observation dump
With `--dump-observation <agentId> <tick>` the runner SHALL advance the world to exactly that tick using its decision source, print nothing else to standard output, then print the named agent's observation as the JSON form of the contract type (property names and order as declared, ids as text, kinds by name, absent values omitted) and exit with code 0. The log is still written.

#### Scenario: Dump at tick 0
- **WHEN** the runner is started with `--dump-observation agent-1 0`
- **THEN** standard output is a single JSON object whose first property is `"Tick":0`, whose `Self` has id `agent-1`, and whose `RecentEvents` is empty

#### Scenario: Dump mid-run
- **WHEN** the runner is started with `--dump-observation agent-3 100` on the default config
- **THEN** standard output is one JSON object with `"Tick":100` and no map or panel text appears

### Requirement: Observation dump failures
If the named agent is not alive at the requested tick, or the run ends before reaching it, the runner SHALL print an error naming the agent or the final tick to standard error, print nothing to standard output, and exit with code 1.

#### Scenario: Dead agent
- **WHEN** the dump targets an agent that died before the requested tick
- **THEN** the runner prints an error naming the agent to standard error and exits with code 1

#### Scenario: Run ends early
- **WHEN** every agent dies before the requested tick
- **THEN** the runner prints an error giving the tick the run ended at to standard error and exits with code 1
