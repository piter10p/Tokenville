# Spec Delta

## Purpose

Defines the events the engine emits while stepping, what each carries, and how a step hands them back, so that the runner can write an exact log and later phases can build observations from them.

## ADDED Requirements

### Requirement: Event kinds and payloads
The engine SHALL emit exactly these event kinds: `AgentMoved`, `ActionStarted`, `ActionCompleted`, `ActionFailed`, `ActionInterrupted`, `AgentAte`, `BushRegrew`, `HungerThresholdCrossed`, `AgentDied`. Every event SHALL carry the tick it happened on, the id of its subject (the agent or bush it is about) and the subject's position at that moment in exact sub-tile units, never rounded to tiles. In addition:
- `ActionStarted`, `ActionCompleted`, `ActionFailed`, `ActionInterrupted` SHALL carry the action kind; `ActionStarted` SHALL carry the target id when the action has one; `ActionFailed` SHALL carry a readable reason.
- `AgentMoved` SHALL carry the agent's new position as its position.
- `AgentAte` SHALL carry the bush id as target and the agent's hunger after eating.
- `BushRegrew` SHALL carry the bush's berry count after regrowing.
- `HungerThresholdCrossed` SHALL carry the threshold crossed.
- `AgentDied` SHALL carry the agent's last position.

#### Scenario: Move event
- **WHEN** an agent at `(128, 128)` steps one tile right during `MoveTo`
- **THEN** the step contains an `AgentMoved` event with subject `agent-1`, position `(384, 128)` and the current tick

#### Scenario: Ate event
- **WHEN** an agent with hunger 40 finishes eating from `bush-2`
- **THEN** the step contains an `AgentAte` event with subject `agent-1`, target `bush-2` and hunger 15

#### Scenario: Failure carries its reason
- **WHEN** a `MoveTo` step is blocked by `bush-3`
- **THEN** the step contains an `ActionFailed` event for the agent with action kind `MoveTo` and reason `blocked by bush-3`

### Requirement: Per-step event list
Each step SHALL return the list of events emitted during that step, in emission order, and SHALL return an empty list when nothing happened. Events SHALL be appended as they happen and never reordered, removed or mutated. A returned list SHALL NOT change when the world steps again.

#### Scenario: Quiet step
- **WHEN** a world with no agents and full bushes steps
- **THEN** the returned list is empty

#### Scenario: Lists are stable
- **WHEN** the list from step 1 is kept and the world steps again
- **THEN** the kept list still holds exactly step 1's events

### Requirement: Events are plain data
An event SHALL be an immutable value made only of the tick, an event kind, entity ids, a position, integers and strings, so that it can be serialized to JSON with a fixed property order and compared for equality.

#### Scenario: Equality
- **WHEN** two worlds built from the same configuration and fed the same decisions each step
- **THEN** the event lists returned by each step are equal element by element
