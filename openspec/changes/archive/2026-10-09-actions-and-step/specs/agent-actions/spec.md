# Spec Delta

## Purpose

Defines how a decision submitted for an agent becomes a running action, and how the three actions `MoveTo`, `Eat` and `Wait` start, progress, complete, fail and are interrupted, so that brains get predictable, readable outcomes and never exceptions.

## ADDED Requirements

### Requirement: Decision submission
The world SHALL accept a decision for an agent at any time between steps and SHALL never throw on a bad decision. A decision SHALL be consumed by the next step. Submitting a second decision for the same agent before the next step SHALL replace the first. A decision for an id that is not a living agent SHALL be ignored. A decision SHALL name an action kind (`MoveTo`, `Eat` or `Wait`) and MAY carry a target id, tile coordinates `X` and `Y`, a tick count and a free-text reason. The reason SHALL have no effect on the world.

#### Scenario: Last decision wins
- **WHEN** `Wait(3)` and then `Wait(7)` are submitted for an idle agent and the world steps
- **THEN** the agent starts `Wait` with 7 ticks and only one action starts

#### Scenario: Unknown agent
- **WHEN** a decision is submitted for `agent-99` in a world of 6 agents
- **THEN** nothing is thrown and the next step emits no event for `agent-99`

#### Scenario: Garbage decision never throws
- **WHEN** a `MoveTo` decision with no target id and no coordinates is submitted and the world steps
- **THEN** nothing is thrown and the step emits an action-failed event for that agent with a readable reason

### Requirement: Agents awaiting a decision
The world SHALL expose, in ascending id order, the living agents that have no current action. A freshly created world SHALL list every agent. An agent whose action ended during a step (completed, failed or interrupted) SHALL be listed after that step. An agent whose action is still running SHALL NOT be listed.

#### Scenario: Fresh world
- **WHEN** a world with agents `agent-1` to `agent-3` is created
- **THEN** the agents awaiting a decision are `agent-1`, `agent-2`, `agent-3` in that order

#### Scenario: Running action
- **WHEN** `agent-1` starts `Wait(5)` and the world steps once
- **THEN** `agent-1` is not awaiting a decision

#### Scenario: Action ended
- **WHEN** `agent-1` starts `Wait(1)` and the world steps once
- **THEN** `agent-1` is awaiting a decision

### Requirement: Action start and validation
At the start of a step, each pending decision for an agent with no current action SHALL be checked in ascending agent id order. A valid decision SHALL start the action and emit an action-started event. An invalid decision SHALL emit an action-failed event with a readable reason, leave the agent without a current action, and record the failure as the agent's last action result. Validity rules:
- `MoveTo` with a target id: the id SHALL parse and name a living agent or a bush.
- `MoveTo` with coordinates: `X` and `Y` SHALL both be given, lie within the grid, and name a tile that holds no bush. A target id, when given, SHALL take precedence over coordinates.
- `MoveTo` with neither target id nor coordinates SHALL be invalid.
- `Eat`: the target id SHALL name a bush, the agent SHALL be within `Reach` (256 units) of the bush center measured by squared Euclidean distance, and the bush SHALL have at least one berry.
- `Wait`: the tick count SHALL be given and lie between 1 and 50 inclusive.

#### Scenario: Move to an out-of-bounds tile
- **WHEN** `MoveTo(32, 5)` is submitted on a 32x32 grid and the world steps
- **THEN** an action-failed event names the agent, the agent has no current action, and the reason mentions the tile

#### Scenario: Move onto a bush tile
- **WHEN** `MoveTo(x, y)` targets the tile of `bush-1` and the world steps
- **THEN** the action fails with a reason naming `bush-1`

#### Scenario: Unknown target id
- **WHEN** `MoveTo("bush-42")` is submitted in a world with 3 bushes and the world steps
- **THEN** the action fails with a reason containing `bush-42`

#### Scenario: Eat out of reach
- **WHEN** an agent two tiles from `bush-1` submits `Eat("bush-1")` and the world steps
- **THEN** the action fails and the reason mentions reach

#### Scenario: Eat from an empty bush
- **WHEN** an agent within reach of a bush with 0 berries submits `Eat` for it and the world steps
- **THEN** the action fails with reason `bush is empty`

#### Scenario: Wait out of range
- **WHEN** `Wait(0)` and `Wait(51)` are submitted on two idle agents and the world steps
- **THEN** both actions fail and neither agent has a current action

### Requirement: Actions progress on the tick they start
A running action SHALL progress during the same step in which it started. `Wait(n)` SHALL occupy exactly `n` steps and `Eat` exactly `EatTicks` steps, counting the starting step.

#### Scenario: Wait one tick
- **WHEN** `Wait(1)` is submitted and the world steps once
- **THEN** that step emits action-started and then action-completed for the agent

#### Scenario: Wait three ticks
- **WHEN** `Wait(3)` is submitted and the world steps three times
- **THEN** the action is still running after two steps and completed after the third

### Requirement: MoveTo movement
A running `MoveTo` SHALL move the agent in a straight line toward its target point by at most `Speed` (256 units) per step, using integer arithmetic only: with `d` the integer Euclidean distance to the target point, a step moves by `(dx * Speed / d, dy * Speed / d)` with truncating integer division, recomputed from the new position each step so rounding never accumulates. `MoveTo(x, y)` SHALL target the center of tile `(x, y)` and SHALL complete when the agent is exactly at that center; when `d` is at most `Speed` the agent SHALL arrive exactly in that step. `MoveTo(targetId)` SHALL target the entity's position as it was when the action started and SHALL complete at the first step in which the agent is within `Reach` of that point, checked before moving. Every step that changes the agent's position SHALL emit an agent-moved event carrying the new position in sub-tile units. Completion SHALL emit an action-completed event.

#### Scenario: Straight run
- **WHEN** an agent at the center of tile `(0, 0)` runs `MoveTo(3, 0)`
- **THEN** after one step it is at `(384, 128)`, after two at `(640, 128)`, after three at `(896, 128)` with the action completed

#### Scenario: Diagonal rounding never accumulates
- **WHEN** an agent at the center of tile `(0, 0)` runs `MoveTo(5, 3)` on an empty grid
- **THEN** the action completes with the agent exactly at `(1408, 896)` and at no step did it move more than 256 units

#### Scenario: Already there
- **WHEN** an agent at the center of tile `(4, 4)` runs `MoveTo(4, 4)`
- **THEN** the action completes in the first step with no agent-moved event

#### Scenario: Within reach of an agent target
- **WHEN** `agent-1` is 200 units from `agent-2` and runs `MoveTo("agent-2")`
- **THEN** the action completes in the first step without moving

### Requirement: Bushes block the end of a step
A `MoveTo` step whose end position lies inside a tile that holds a bush SHALL fail with reason `blocked by <bush-id>` and SHALL leave the agent where it was. A step that crosses a bush tile without ending inside it SHALL be allowed. There SHALL be no pathfinding.

#### Scenario: Blocked
- **WHEN** an agent at the center of tile `(0, 0)` runs `MoveTo(2, 0)` and `bush-1` occupies tile `(1, 0)`
- **THEN** the first step fails with reason `blocked by bush-1`, the agent stays at `(128, 128)` and has no current action

#### Scenario: Corner crossing allowed
- **WHEN** an agent runs a diagonal `MoveTo` whose path clips the corner of a bush tile but whose every step ends outside it
- **THEN** the action completes

### Requirement: Approaching a bush always succeeds over open ground
`MoveTo(bushId)` SHALL bring the agent within `Reach` of the bush center without ever ending a step inside the bush's own tile, from any starting position on any approach line, provided no other bush lies on the path. The final approach SHALL therefore shorten its step rather than overshoot into the tile. The action SHALL never fail with `blocked by <that bush>` and SHALL never stall making no progress.

#### Scenario: Diagonal approach
- **WHEN** an agent at the center of tile `(0, 0)` runs `MoveTo("bush-1")` with the bush at tile `(3, 1)`
- **THEN** the action completes within 4 steps, the agent's final squared distance to the bush center is at most `256 * 256`, and the agent's tile is not the bush's tile

#### Scenario: Sweep of approach angles
- **WHEN** for every start tile within 4 tiles of a lone bush on a 9x9 grid an agent runs `MoveTo` to that bush
- **THEN** every run completes within 7 steps, no run fails, and every final position is within `Reach` of the bush center and outside its tile

### Requirement: Eat
A running `Eat` SHALL count down `EatTicks` steps. On the step it finishes it SHALL re-check that the bush still has at least one berry. If so, the bush SHALL lose one berry, the agent's hunger SHALL drop by `BerryNutrition` to no less than 0, an agent-ate event SHALL be emitted naming the bush and the agent's new hunger, and the action SHALL complete. If the bush is empty, the action SHALL fail with reason `bush is empty`. When several agents finish eating the same bush on one step, they SHALL be resolved in ascending agent id order, so the lower id takes the last berry.

#### Scenario: Successful eat
- **WHEN** an agent with hunger 40 within reach of a bush with 2 berries runs `Eat` and the world steps `EatTicks` times
- **THEN** the bush has 1 berry, the agent has hunger 15, and the last step emits agent-ate then action-completed

#### Scenario: Hunger floors at zero
- **WHEN** an agent with hunger 10 completes `Eat`
- **THEN** its hunger is 0

#### Scenario: Last-berry tie
- **WHEN** `agent-1` and `agent-2` both start `Eat` on a bush with 1 berry on the same step and the world steps `EatTicks` times
- **THEN** `agent-1` completes with the berry, `agent-2` fails with reason `bush is empty`, and the bush has 0 berries

### Requirement: Action outcomes are recorded
Every action SHALL end as exactly one of `Completed`, `Failed` with a reason, or `Interrupted`. The agent SHALL expose its last action's kind, outcome and reason until the next action ends. A failed start SHALL count as a `Failed` last action. Outcomes SHALL be reported as events and results, never as exceptions.

#### Scenario: Result after failure
- **WHEN** an agent's `MoveTo` fails with `blocked by bush-1`
- **THEN** the agent's last action result has kind `MoveTo`, outcome `Failed` and reason `blocked by bush-1`

#### Scenario: Result after interruption
- **WHEN** an agent's `Wait` is interrupted by a hunger alert
- **THEN** the agent's last action result has outcome `Interrupted` and no current action
