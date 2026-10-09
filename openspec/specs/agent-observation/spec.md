# Agent Observation Specification

## Purpose

Defines what a living agent is told about the world when it is asked for a decision, in the tile-based terms brains are allowed to see, so that any brain, scripted or LLM, can be written against one stable picture of the world.

## Requirements

### Requirement: Observation contents
The engine SHALL produce, on request and for any living agent, an observation holding: the current tick; the agent itself (id, name, tile position, hunger, current action kind or none); the visible bushes (id, tile position, berries, distance in whole tiles); the visible other agents (id, name, tile position, distance in whole tiles, current action kind or none); the recent events; the outcome and reason of the agent's previous action, or none if it has not had one; and the world width and height in tiles. Requesting an observation SHALL NOT change the world. Requesting one for an id that is not a living agent SHALL be reported as an error to the caller.

#### Scenario: Fresh agent
- **WHEN** an agent at the center of tile (4, 4) with hunger 12 and no action is observed on tick 7 in a 9x9 world
- **THEN** the observation has tick 7, self at (4, 4) with hunger 12 and no current action, no last action, width 9 and height 9

#### Scenario: Last action is reported
- **WHEN** an agent whose `MoveTo` just failed with reason `blocked by bush-3` is observed
- **THEN** the observation's last action has outcome `Failed` and reason `blocked by bush-3`

#### Scenario: Observing is read-only
- **WHEN** an agent is observed twice with no step in between
- **THEN** both observations are equal and the world's tick, agents and bushes are unchanged

#### Scenario: Dead agent
- **WHEN** an observation is requested for an agent that has died
- **THEN** the request fails with an error naming the id

### Requirement: Tile units only
Positions in an observation SHALL be tile coordinates, never sub-tile units. Distances SHALL be the Euclidean distance in units divided by the tile size, rounded down, so that an entity within one tile's reach may show distance 0 or 1 and an entity at distance 0 is always within reach.

#### Scenario: Sub-tile position rounds to its tile
- **WHEN** an agent at sub-tile position (700, 300) is observed
- **THEN** its tile position is (2, 1)

#### Scenario: Distance in whole tiles
- **WHEN** an agent at the center of tile (0, 0) observes a bush at tile (3, 4)
- **THEN** the bush's distance is 5

#### Scenario: Adjacent diagonal bush shows distance 1
- **WHEN** an agent at the center of tile (0, 0) observes a bush at tile (1, 1)
- **THEN** the bush's distance is 1

### Requirement: Perception filtering
An observation SHALL list exactly the bushes and the other agents whose exact position is within `PerceptionRadius` tiles (Euclidean, inclusive) of the observing agent's exact position, in ascending id order. The observing agent SHALL NOT appear in its own agent list. Bushes with zero berries SHALL be listed like any other bush.

#### Scenario: Inside and outside the radius
- **WHEN** with `PerceptionRadius` 5 an agent at the center of tile (0, 0) observes bushes at tiles (5, 0), (3, 4) and (6, 0)
- **THEN** the observation lists the bushes at (5, 0) and (3, 4) in id order and not the one at (6, 0)

#### Scenario: Self is excluded
- **WHEN** two agents stand on the same tile and one is observed
- **THEN** its agent list holds only the other agent, at distance 0

#### Scenario: Empty bush is visible
- **WHEN** an agent observes a bush with 0 berries two tiles away
- **THEN** the bush is listed with berries 0

### Requirement: Recent events are not yet reported
The recent events list SHALL exist in every observation and SHALL be empty until per-agent event history is introduced, so that brains written now compile and behave unchanged when it is filled.

#### Scenario: Always empty for now
- **WHEN** an agent that moved and ate during the previous steps is observed
- **THEN** its recent events list is empty

### Requirement: Observations are plain data
An observation SHALL be an immutable value made of ids, names, integers, action kinds, action results and events, so that it can be serialized to JSON with a fixed property order starting with the tick and compared for equality.

#### Scenario: Serializes
- **WHEN** an observation is serialized to JSON
- **THEN** the serialization succeeds and the first property is the tick
