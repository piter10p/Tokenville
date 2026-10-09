# Tick Pipeline Specification

## Purpose

Defines what one step of the world does and in which order, so that the same decisions on the same world always produce the same state and the same events, and so brains are asked for decisions at exactly the right moments.

## Requirements

### Requirement: Phase order
A step SHALL run these phases in this order, each iterating agents in ascending id order: (1) start actions from pending decisions, (2) progress running actions, (3) grow hunger and remove dead agents, (4) regrow bushes, (5) emit hunger alerts and interrupt actions, (6) increment the tick. The tick number carried by every event of a step SHALL be the tick value before the increment. The world SHALL change only inside a step.

#### Scenario: Events of one step carry its tick
- **WHEN** a fresh world steps twice with a `Wait(5)` running
- **THEN** the first step's events all carry tick 0, the second step's carry tick 1, and the world's tick is 2

#### Scenario: Order within a step
- **WHEN** on one step `agent-1` starts and moves, `agent-2` dies, a bush regrows and `agent-3` crosses a hunger threshold
- **THEN** the step's events appear in the order action-started, agent-moved, agent-died, bush-regrew, hunger-threshold-crossed

### Requirement: Hunger growth and death
Hunger SHALL rise by 1 on every step whose tick satisfies `(tick + 1) mod HungerTicksPerPoint = 0`, so the first point lands at the end of the `HungerTicksPerPoint`-th step. An agent whose hunger reaches 100 SHALL be removed from the world during that step and an agent-died event SHALL be emitted carrying its last position. A dead agent's running action SHALL end silently with no action event, and a decision pending for it SHALL be dropped. A step SHALL still run when no agents are alive.

#### Scenario: Default pacing
- **WHEN** a default world steps 200 times with no decisions submitted
- **THEN** every agent has hunger 99 after 198 steps and all agents are dead after 200 steps, each with an agent-died event at tick 199

#### Scenario: Death ends the action silently
- **WHEN** an agent at hunger 99 is running `MoveTo` on a step where hunger rises
- **THEN** the step emits agent-died for it and no action-interrupted, action-failed or action-completed event

#### Scenario: Empty world keeps ticking
- **WHEN** all agents are dead and the world steps
- **THEN** the tick increments and bushes keep regrowing

### Requirement: Bush regrowth
A bush below `BushMaxBerries` SHALL count every step on which it is below max, including the step on which it dropped below max, and on the step that brings the count to `BushRegrowTicks` SHALL gain one berry, reset the count and emit a bush-regrew event carrying its new berry count. A full bush SHALL NOT count, so a berry taken from a full bush reappears exactly `BushRegrowTicks` steps after the pick.

#### Scenario: Regrow after a pick
- **WHEN** a full bush loses a berry on step `t` by an eat completing
- **THEN** it has max minus one berries after steps `t` through `t + 38` and is full again after step `t + 39`, with one bush-regrew event at tick `t + 39`

#### Scenario: Full bush stays full
- **WHEN** a full bush exists for 100 steps
- **THEN** no bush-regrew event is emitted and the berry count never exceeds `BushMaxBerries`

### Requirement: Hunger alerts and interrupts
After hunger grows, each living agent whose hunger rose from below to at or above a configured alert threshold (`HungerAlertThresholds`, default 50 and 80) SHALL get a hunger-threshold-crossed event carrying that threshold. If such an agent has a current action other than `Eat`, the action SHALL end as `Interrupted` with an action-interrupted event immediately after the alert, and the agent SHALL have no current action. An `Eat` in progress SHALL continue and the agent SHALL NOT be asked for a decision until it ends. A threshold crossed downward by eating SHALL NOT alert, and climbing back over it later SHALL alert again.

#### Scenario: Wait is interrupted at 50
- **WHEN** an agent with hunger 49 running `Wait(50)` steps on a hunger tick
- **THEN** the step emits hunger-threshold-crossed with threshold 50 then action-interrupted, and the agent awaits a decision

#### Scenario: Eat is not interrupted
- **WHEN** an agent with hunger 79 running `Eat` with 3 ticks left steps on a hunger tick
- **THEN** hunger-threshold-crossed with 80 is emitted, the eat keeps running, and the agent is not awaiting a decision

#### Scenario: Idle agent is alerted
- **WHEN** an idle agent crosses 80
- **THEN** hunger-threshold-crossed is emitted and no action-interrupted event follows

#### Scenario: Re-crossing alerts again
- **WHEN** an agent crosses 50, eats back to 30, and later climbs past 50 again
- **THEN** two hunger-threshold-crossed events with threshold 50 exist in total

### Requirement: Agents to ask after a step
After a step, the agents awaiting a decision SHALL be exactly the living agents with no current action: those idle before the step, those whose action completed, failed or was interrupted during it, and those whose decision failed validation.

#### Scenario: Mixed outcomes
- **WHEN** on one step `agent-1` completes `Wait(1)`, `agent-2` keeps running `Wait(5)`, `agent-3` submits an invalid decision and `agent-4` was idle
- **THEN** the agents awaiting a decision are `agent-1`, `agent-3`, `agent-4`
