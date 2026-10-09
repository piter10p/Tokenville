# Spec Delta

## Purpose

Defines how a recorded decision log drives a world again without brains, so that any run can be reproduced from its configuration and log and the result compared to the original.

## ADDED Requirements

### Requirement: Decisions come from the log
In replay the decision source SHALL be the decision lines of the given log: when the world asks for a decision for an agent at a tick, the replay SHALL submit the decision recorded for that agent and tick. Event lines in the log SHALL be ignored. No brain SHALL be constructed or consulted.

#### Scenario: Recorded decision is submitted
- **WHEN** a log holds a decision line for agent-2 at tick 7 with `Wait` for 4 ticks and the replay reaches tick 7 with agent-2 awaiting a decision
- **THEN** agent-2 starts a 4-tick wait on that step

#### Scenario: Events in the log are ignored
- **WHEN** the log's event lines are shuffled or deleted before replay
- **THEN** the replay produces the same output as from the untouched log

### Requirement: Replay reproduces the run
A replay from the same configuration and the log of a run SHALL produce a log byte-identical to the original, decision lines and event lines alike.

#### Scenario: Scripted run replayed
- **WHEN** the default world is run 200 ticks with scripted brains, and a second default world is replayed from that log for 200 ticks
- **THEN** the second log equals the first

#### Scenario: Replay of a replay
- **WHEN** the log produced by a replay is itself replayed
- **THEN** the output equals it again

### Requirement: A missing decision is an error
When the world asks for a decision for an agent and tick that the log does not hold, the replay SHALL stop with an error naming the agent and tick, rather than substituting a decision. A world that is deterministic given its configuration only asks for decisions the recording made, so a miss means the configuration or log does not match.

#### Scenario: Different seed
- **WHEN** a log recorded with seed 42 is replayed against a configuration with seed 43
- **THEN** the replay fails with an error naming an agent and a tick

#### Scenario: Truncated log
- **WHEN** a 200-tick log is cut after tick 50 and replayed for 200 ticks
- **THEN** the replay fails with an error naming tick 51 or later

### Requirement: Replay rejects a malformed log
A log line that is neither a valid decision line nor a valid event line SHALL make the replay fail with an error giving the line number, before the world steps.

#### Scenario: Garbage line
- **WHEN** the log's third line is `not json`
- **THEN** the replay fails with an error mentioning line 3 and the world is never stepped
