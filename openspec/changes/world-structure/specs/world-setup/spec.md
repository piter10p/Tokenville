# Spec Delta

## Purpose

Defines how entities are identified and located on the grid, and how a freshly created world places its agents and bushes from the seed so that the same config always yields the same starting layout.

## ADDED Requirements

### Requirement: Entity identity
Every entity SHALL have an id made of an entity kind (agent or bush) and a positive number. Ids SHALL render as `agent-<n>` and `bush-<n>`. Ids SHALL parse back from that rendering, and any other text SHALL be rejected without throwing. Ids SHALL order by kind first and then by number, so that `agent-2` precedes `agent-10`.

#### Scenario: Rendering
- **WHEN** the id for agent number 1 is rendered as text
- **THEN** the text is `agent-1`

#### Scenario: Round trip
- **WHEN** the text `bush-12` is parsed
- **THEN** the result is the id for bush number 12, and rendering it yields `bush-12` again

#### Scenario: Rejected text
- **WHEN** the text `tree-1`, `agent-`, `agent-x` or `agent-0` is parsed
- **THEN** parsing reports failure and does not throw

#### Scenario: Numeric ordering
- **WHEN** ids `agent-10`, `agent-2` and `agent-1` are sorted ascending
- **THEN** the order is `agent-1`, `agent-2`, `agent-10`

### Requirement: Positions and distance
A position SHALL be an integer pair `(x, y)` with `(0, 0)` the top-left tile. The distance between two positions SHALL be the Manhattan distance, `|x1 - x2| + |y1 - y2|`, computed with integer arithmetic.

#### Scenario: Distance
- **WHEN** the distance from `(1, 2)` to `(4, 6)` is computed
- **THEN** the result is 7

#### Scenario: Distance is symmetric and zero at self
- **WHEN** the distance from `(4, 6)` to `(1, 2)` and from `(3, 3)` to `(3, 3)` are computed
- **THEN** the results are 7 and 0

### Requirement: Entities expose id and position
Every agent and every bush SHALL expose its id and its current position through one common read-only view, so callers can locate any entity without knowing its kind.

#### Scenario: Common view
- **WHEN** a caller holds an agent and a bush through the common entity view
- **THEN** it can read each one's id and position without casting to the concrete kind

### Requirement: Seeded initial placement
Creating a world from a configuration SHALL place exactly `BushCount` bushes and then exactly `AgentCount` agents using only a random generator seeded with `Seed`. Bush ids SHALL be `bush-1` through `bush-<BushCount>` and agent ids `agent-1` through `agent-<AgentCount>`, numbered in placement order. No two bushes SHALL share a tile. No agent SHALL start on a bush tile. Agents MAY share a tile with other agents. Every position SHALL be within `0 <= x < Width` and `0 <= y < Height`.

#### Scenario: Counts and ids
- **WHEN** a world is created with `AgentCount` 6 and `BushCount` 8
- **THEN** it holds agents `agent-1` through `agent-6` and bushes `bush-1` through `bush-8`

#### Scenario: Bushes never overlap
- **WHEN** a world is created with any seed on a 4x4 grid with `BushCount` 16
- **THEN** every tile holds exactly one bush

#### Scenario: Agents avoid bushes
- **WHEN** a world is created on a 4x4 grid with `BushCount` 15 and `AgentCount` 6
- **THEN** every agent is on the single bush-free tile

#### Scenario: Same seed, same layout
- **WHEN** two worlds are created from identical configurations
- **THEN** every agent and bush has the same position in both

#### Scenario: Different seed, different layout
- **WHEN** two worlds are created from the default configuration with seeds 1 and 2
- **THEN** at least one entity's position differs between them

### Requirement: Initial entity state
Each bush SHALL start with `BushMaxBerries` berries. Each agent SHALL start with hunger 0 and no current action. Each agent SHALL have a name taken from a fixed list indexed by its number, wrapping around when `AgentCount` exceeds the list length. The world's tick SHALL start at 0.

#### Scenario: Fresh world
- **WHEN** a world is created with the default configuration
- **THEN** every bush has 5 berries, every agent has hunger 0, no agent has a current action, and the tick is 0

#### Scenario: Names are stable
- **WHEN** two worlds are created with different seeds
- **THEN** `agent-1` has the same name in both

### Requirement: Ascending iteration
The world SHALL expose its agents and its bushes as sequences ordered by ascending id.

#### Scenario: Iteration order
- **WHEN** the agents of a world with 12 agents are enumerated
- **THEN** they appear as `agent-1`, `agent-2`, ..., `agent-12` in that order

### Requirement: Unplaceable configuration is rejected
Creating a world SHALL fail with a descriptive error when `Width` or `Height` is less than 1, when `AgentCount` or `BushCount` is negative, when `BushCount` exceeds `Width * Height`, or when `AgentCount` is positive and `BushCount` equals `Width * Height`.

#### Scenario: Too many bushes
- **WHEN** a world is created on a 3x3 grid with `BushCount` 10
- **THEN** creation fails with an error naming `BushCount`

#### Scenario: No room for agents
- **WHEN** a world is created on a 2x2 grid with `BushCount` 4 and `AgentCount` 1
- **THEN** creation fails with an error naming `AgentCount`
