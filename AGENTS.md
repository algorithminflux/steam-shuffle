# AGENTS.md

## Code style

- All `if` statements must use curly braces, even for single-line bodies. No braceless `if (x) return;` style.

## Class member order

- Order: private fields/properties, then public properties, then constructor(s), then public methods, then private methods.

## Workflow

- After any code modification, run `dotnet format` then `dotnet test` before considering the change done.
