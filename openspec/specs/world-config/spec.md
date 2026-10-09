# World Config Specification

## Purpose

Defines the simulation configuration: which values exist, their defaults from the PoC plan, and how the runner loads them from a JSON file so that an engine run is fully described by one config plus one seed.

## Requirements

### Requirement: Configuration values and defaults
The system SHALL expose a single immutable configuration value carrying exactly these settings with these defaults: `Width` 32, `Height` 32, `Seed` 42, `AgentCount` 6, `BushCount` 8, `HungerTicksPerPoint` 2, `BerryNutrition` 25, `EatTicks` 5, `BushMaxBerries` 5, `BushRegrowTicks` 40, `PerceptionRadius` 5, and `HungerAlertThresholds` which defaults to the pair 50 and 80 when not given. The engine SHALL receive the configuration as this immutable value and SHALL NOT read configuration from any other source.

#### Scenario: Default configuration
- **WHEN** a configuration is created without specifying any value
- **THEN** every setting equals the default listed above

#### Scenario: Partial override
- **WHEN** a configuration is created specifying only `BushCount` as 3
- **THEN** `BushCount` is 3 and every other setting keeps its default

### Requirement: Runner loads configuration from JSON
The runner SHALL accept an optional path to a JSON configuration file as its first command-line argument. When no path is given the runner SHALL use the default configuration. Property names SHALL be matched case-insensitively. Properties absent from the file SHALL take their default values. A property name that matches no setting SHALL cause loading to fail with a message naming the property, rather than being ignored.

#### Scenario: No config file given
- **WHEN** the runner starts with no arguments
- **THEN** it runs with the default configuration

#### Scenario: Sparse config file
- **WHEN** the runner is given a file containing `{"bushCount": 3}`
- **THEN** the loaded configuration has `BushCount` 3 and all other settings at their defaults

#### Scenario: Unknown property
- **WHEN** the runner is given a file containing `{"BushCont": 3}`
- **THEN** loading fails and the error names `BushCont`

#### Scenario: Thresholds override
- **WHEN** the runner is given a file containing `{"HungerAlertThresholds": [40, 70, 90]}`
- **THEN** the loaded configuration's thresholds are exactly 40, 70, 90 in that order
