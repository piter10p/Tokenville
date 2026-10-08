# Spec Delta

## Purpose

Defines the fixed policy a scripted agent follows from an observation, so that six scripted agents can run the Phase 1 acceptance scenario deterministically with no LLM involved.

## ADDED Requirements

### Requirement: Brain contract
A brain SHALL take an observation and return a decision asynchronously, and a scripted brain SHALL decide from the observation alone: it SHALL NOT read the world, the clock or any shared random source. Each scripted brain SHALL own one random source seeded at construction, so two scripted brains built from the same seed and given the same observations SHALL return the same decisions.

#### Scenario: Same seed, same decisions
- **WHEN** two scripted brains are built from seed 7 and each is given the same sequence of 100 observations
- **THEN** they return identical decision sequences

#### Scenario: Different seeds diverge
- **WHEN** two scripted brains are built from seeds 7 and 8 and each is given the same wander-inducing observation 20 times
- **THEN** at least one of the 20 decision pairs differs

### Requirement: Eat when hungry
When the agent's hunger is at least 30 and at least one visible bush has berries, the brain SHALL pick the nearest such bush (smallest whole-tile distance, lowest id on a tie). If its distance is at most 1 and the agent's last action did not fail as out of reach, the brain SHALL decide `Eat` on it; otherwise it SHALL decide `MoveTo` on it. Bushes with no berries SHALL be ignored for this rule.

#### Scenario: Eat in reach
- **WHEN** hunger is 30 and `bush-2` with 3 berries is at distance 0
- **THEN** the decision is `Eat` targeting `bush-2`

#### Scenario: Eat at distance 1
- **WHEN** hunger is 30 and `bush-2` with 3 berries is at distance 1
- **THEN** the decision is `Eat` targeting `bush-2`

#### Scenario: Walk after an out-of-reach failure
- **WHEN** hunger is 30, `bush-2` with berries is at distance 1 and the last action failed with `bush-2 is out of reach`
- **THEN** the decision is `MoveTo` targeting `bush-2`

#### Scenario: Walk to food
- **WHEN** hunger is 45 and the only bush with berries, `bush-1`, is at distance 3
- **THEN** the decision is `MoveTo` targeting `bush-1`

#### Scenario: Nearest wins, ties by id
- **WHEN** hunger is 60 and bushes `bush-3` (distance 2), `bush-1` (distance 1) and `bush-2` (distance 1) all have berries
- **THEN** the decision targets `bush-1`

#### Scenario: Empty bush is skipped
- **WHEN** hunger is 60, `bush-1` at distance 0 has 0 berries and `bush-2` at distance 4 has 1 berry
- **THEN** the decision is `MoveTo` targeting `bush-2`

### Requirement: Wander otherwise
When hunger is below 30, or no visible bush has berries, the brain SHALL decide `MoveTo` on a tile drawn from its random source with x drawn before y, both uniform over the world's width and height, redrawing while the tile holds a visible bush. The brain SHALL NOT `Wait` in this case.

#### Scenario: Not hungry
- **WHEN** hunger is 29 and a bush with berries is at distance 0 in a 9x9 world
- **THEN** the decision is `MoveTo` with coordinates inside 0..8 on both axes and not on a visible bush tile

#### Scenario: Hungry but blind
- **WHEN** hunger is 70 and no bush is visible
- **THEN** the decision is `MoveTo` with in-bounds coordinates

#### Scenario: Visible bushes are avoided
- **WHEN** a 2x2 world has visible bushes on three of its four tiles and the brain wanders
- **THEN** the decision targets the fourth tile

### Requirement: Sidestep after a blocked move
When the observation's last action failed with a reason beginning `blocked`, the brain SHALL, before any other rule, decide `MoveTo` on one of the agent's four edge-adjacent tiles that is in bounds and not a visible bush tile, chosen uniformly from its random source. If no such tile exists the brain SHALL decide `Wait(1)`.

#### Scenario: Blocked while hungry
- **WHEN** hunger is 80, the last action is `MoveTo` failed with `blocked by bush-4`, and the agent stands at (3, 3) with no visible bushes adjacent
- **THEN** the decision is `MoveTo` to one of (3, 4), (3, 2), (4, 3), (2, 3)

#### Scenario: Blocked neighbours are excluded
- **WHEN** the last action failed with `blocked by bush-1`, the agent stands at (0, 0) and a visible bush occupies (1, 0)
- **THEN** the decision is `MoveTo` to (0, 1)

#### Scenario: Boxed in
- **WHEN** the last action failed with `blocked by bush-1`, the agent stands at (0, 0) and visible bushes occupy (1, 0) and (0, 1)
- **THEN** the decision is `Wait` for 1 tick

### Requirement: Phase 1 acceptance
Six scripted agents on the default configuration, each driven in lockstep through observation and decision, SHALL all be alive after 2,000 steps with 8 bushes, and at least one SHALL die before step 2,000 with 3 bushes. Two such runs from the same configuration and brain seeds SHALL produce equal event lists on every step.

#### Scenario: Survival with 8 bushes
- **WHEN** a default world runs 2,000 steps with one scripted brain per agent seeded from the world seed and the agent number
- **THEN** six agents are alive and no agent ever ends a step inside a bush tile

#### Scenario: Starvation with 3 bushes
- **WHEN** the same run uses `BushCount` 3
- **THEN** fewer than six agents are alive at step 2,000

#### Scenario: Deterministic run
- **WHEN** two default worlds are each run 500 steps with scripted brains seeded the same way
- **THEN** every step's event list is equal between the two runs
