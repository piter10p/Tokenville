# Tokenville

Headless, deterministic, tick-based simulation of LLM-driven agents who must eat from berry bushes to survive. .NET 10, C#.

The full spec (world rules, engine–brain contract, phases, engineering rules) is in
[docs/Tokenville — PoC Implementation Plan.md](docs/Tokenville%20—%20PoC%20Implementation%20Plan.md). Treat it as binding.

- Solution: `src/Tokenville.slnx`
- Determinism in `Tokenville.Core` is the top priority: seeded RNG only, ascending id iteration, integer math, no clock, no I/O, no async.
- One phase task per PR, tests in the same PR. Ambiguity → simplest deterministic option, stated in the PR description.
