# Spec Delta

## Purpose

Defines the decisions a scripted, model-free brain makes for one agent from the state of the world, so that a full simulation runs deterministically before any LLM is involved.

## ADDED Requirements

### Requirement: Hungry agents go for the nearest berries
When the agent's hunger is 30 or more and at least one bush holds at least one berry, the brain SHALL pick the bush with berries nearest to the agent by Euclidean distance, breaking ties in favor of the lower bush id. If that bush is within reach, the decision SHALL be `Eat` on that bush; otherwise it SHALL be `MoveTo` targeting that bush's id. Bushes with no berries SHALL be ignored. Until perception filtering exists, every bush in the world SHALL be considered.

#### Scenario: Hungry and in reach
- **WHEN** an agent with hunger 30 stands at the center of tile `(0, 0)` and `bush-1` with 2 berries is at `(1, 0)`
- **THEN** the decision is `Eat` with target `bush-1`

#### Scenario: Hungry and out of reach
- **WHEN** an agent with hunger 60 stands at the center of tile `(0, 0)` and `bush-1` with 2 berries is at `(5, 0)`
- **THEN** the decision is `MoveTo` with target `bush-1` and no coordinates

#### Scenario: Empty bushes are ignored
- **WHEN** an agent with hunger 60 at `(0, 0)` has `bush-1` with 0 berries at `(1, 0)` and `bush-2` with 1 berry at `(6, 0)`
- **THEN** the decision is `MoveTo` with target `bush-2`

#### Scenario: Ties go to the lower id
- **WHEN** an agent with hunger 60 at `(4, 0)` has `bush-1` with 1 berry at `(0, 0)` and `bush-2` with 1 berry at `(8, 0)`
- **THEN** the decision targets `bush-1`

#### Scenario: Not hungry enough beside food
- **WHEN** an agent with hunger 29 stands within reach of a bush with berries
- **THEN** the decision is not `Eat`

### Requirement: Agents wander when not hungry
When the agent's hunger is below 30, or when it is 30 or more but no bush holds a berry, the brain SHALL choose `MoveTo` to tile coordinates drawn from its own random generator such that the tile is inside the world and holds no bush.

#### Scenario: Wander target is valid
- **WHEN** an agent with hunger 0 on a 3x3 world with one bush at `(1, 1)` is asked for 200 decisions
- **THEN** every decision is `MoveTo` with coordinates, each inside `[0, 2]` on both axes, and none is `(1, 1)`

#### Scenario: No berries anywhere
- **WHEN** an agent with hunger 90 is on a world whose only bush has 0 berries
- **THEN** the decision is `MoveTo` with coordinates of a bush-free tile

### Requirement: Blocked moves are answered with a sidestep
When the agent's last action failed with a reason beginning `blocked by`, the brain SHALL, regardless of hunger, choose `MoveTo` to the first of the tiles directly below, above, right of and left of the agent's tile, in that order, that is inside the world and holds no bush. If none of the four qualifies, the decision SHALL be `Wait` for 1 tick.

#### Scenario: Step below is free
- **WHEN** an agent at tile `(3, 3)` whose last action failed with `blocked by bush-1` has no bush at `(3, 4)`
- **THEN** the decision is `MoveTo` with coordinates `(3, 4)`

#### Scenario: Step below is taken
- **WHEN** an agent at tile `(3, 3)` whose last action failed with `blocked by bush-1` has a bush at `(3, 4)` and none at `(3, 2)`
- **THEN** the decision is `MoveTo` with coordinates `(3, 2)`

#### Scenario: Boxed in
- **WHEN** an agent at tile `(1, 1)` of a 3x3 world whose last action failed with `blocked by bush-1` has bushes at `(1, 2)`, `(1, 0)`, `(2, 1)` and `(0, 1)`
- **THEN** the decision is `Wait` for 1 tick

### Requirement: Decisions are deterministic per agent
A brain SHALL draw randomness only from its own generator, seeded from the world seed and the agent's number by a fixed formula. Two brains built for the same seed and the same agent SHALL produce identical decision sequences on identical worlds. Every decision SHALL carry a short reason naming the rule that produced it.

#### Scenario: Same seed, same decisions
- **WHEN** two brains are built for `agent-1` with seed 42 and each is asked for 100 wander decisions on identical worlds
- **THEN** the two decision sequences are equal

#### Scenario: Reason is set
- **WHEN** any decision is produced
- **THEN** its reason is a non-empty string
