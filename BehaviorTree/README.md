# SimpleBehaviorTree3

A lightweight, modular Behavior Tree framework for Unity and C# projects.

SimpleBehaviorTree3 provides a composable, code-first API for building AI logic using standard behavior tree concepts such as sequences, selectors, decorators, and reactive priority evaluation.

---

## Table of Contents

- [Overview](#overview)
- [Key Features](#key-features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Core Concepts](#core-concepts)
- [Node Types](#node-types)
- [Decorators](#decorators)
- [Execution Model](#execution-model)
- [Debugging](#debugging)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)
- [License](#license)

---

## Overview

SimpleBehaviorTree3 is designed for developers who prefer expressive, strongly typed C# code over visual scripting workflows.  
It supports:

- clear `Enter / Tick / Exit` lifecycle boundaries
- composable task trees
- reactive and non-reactive visibility control
- deterministic random selection via seeded context RNG
- lightweight runtime debugging output

---

## Key Features

- **Code-first authoring** with fluent syntax sugar
- **Reactive trees** via `Priority` and `Ensure`
- **Visibility gating** with `If`, `IfNot`, `While`, `WhileNot`
- **Time control** with `Wait` and `Timeout`
- **Result mapping** via `ForceSuccess`
- **Repetition** via `Repeat`
- **Named nodes** for readable debug paths
- **Runtime-safe execution** through `BtInstance`

---

## Installation

1. Copy `SimpleBehaviorTree3.cs` into your Unity project.
2. Add namespace imports in your behavior script:

```csharp
using SimpleBehaviorTree3;
using static SimpleBehaviorTree3.Bt;
```

3. Build a tree and execute it from a `MonoBehaviour`.

---

## Quick Start

```csharp
using UnityEngine;
using SimpleBehaviorTree3;
using static SimpleBehaviorTree3.Bt;

public class EnemyAI : MonoBehaviour
{
    BtInstance bt;

    void Awake()
    {
        BtNode tree = Priority(
            Ensure(
                Action(Reload).IfNot(HasAmmo),
                Action(Shoot, "Burst", onExit: OnShootExit)
            ).If(EnemyInSight).Name("Combat"),
            Action(Patrol)
        ).Name("MainLoop").Repeat();

        bt = new BtInstance(tree, debugEnabled: true, logLineLimit: 20);
    }

    void FixedUpdate()
    {
        bt.Tick(Time.fixedDeltaTime);
    }

    void TakeHit()
    {
        bt.Abort();
    }

    BtStatus Reload(bool isFirstFrame) => BtStatus.Success;
    BtStatus Shoot(bool isFirstFrame, string fireMode) => BtStatus.Running;
    void OnShootExit(BtEndReason reason) { } // reason: Succeeded / Failed / Aborted
    BtStatus Patrol(bool isFirstFrame) => BtStatus.Running;
    bool HasAmmo(BtCtx c) => false;
    bool EnemyInSight(BtCtx c) => true;
}
```

---

## Core Concepts

### `BtStatus`

- `Success`: node completed successfully
- `Failure`: node cannot complete under current conditions
- `Running`: node is still in progress

### Lifecycle

Each node follows:

1. `Enter(...)` once when activated
2. `Tick(...)` every frame while active
3. `Exit(...)` once on completion or abort

---

## Node Types

### Composites

- `Sequence(...)`  
  Succeeds only if every visible child succeeds.

- `Selector(...)`  
  Succeeds on first successful visible child.

- `RandomSelector(...)`  
  Tries visible children in random order until success or exhaustion.

- `Priority(...)`  
  Rechecks higher-priority children each tick and can preempt lower running work.

- `Ensure(...)`  
  Reactive sequence that rechecks earlier required steps each tick; fails if any required visible step fails; succeeds only when all required visible steps are done.

### Leaf Nodes

- `Action(...)`  
  Callback-based task with first-frame flag and optional `onExit`.

- `Wait(seconds)`  
  Runs for a duration using `ctx.deltaTime`, then succeeds.

---

## Decorators

- `node.If(cond)` / `node.IfNot(cond)`  
  Entry-time visibility gate (non-reactive).

- `node.While(cond)` / `node.WhileNot(cond)`  
  Reactive visibility gate (can abort while running).

- `node.Timeout(seconds)`  
  Fails if child exceeds duration while still running.

- `node.Repeat(count)`  
  Repeats child on success; negative count means infinite.

- `node.ForceSuccess()`  
  Converts finished result to success.

- `node.Name("Label")`  
  Renames node for debug output.

---

## Execution Model

Use `BtInstance` as the root runtime executor.

- `Tick()` executes with current context values
- `Tick(deltaTime)` sets `ctx.deltaTime` before ticking
- `Abort()` stops active subtree with `BtEndReason.Aborted`

Visibility and root behavior:

- If root becomes invisible, active root is aborted.
- Root returns configured `rootInvisibleResult` (default `Success`, cannot be `Running`).

---

## Debugging

When debug is enabled in `BtInstance`:

- `CurrentPath` gives the active path string (single line, joined by ` > `)
- `LogLines` stores recent lifecycle events (`Entered`, `Succeeded`, `Failed`, `Aborted`)

Typical usage:

```csharp
if (bt.DebugEnabled) // put this inside your tick loop
{
    headText.text = bt.CurrentPath; // one-line text above NPC
}
```

---

## Best Practices

- Keep action nodes focused and deterministic.
- Name important nodes to improve path readability.
- Prefer `If` for one-time gating and `While` for reactive interruption.
- Use `Timeout` for fail-safe behavior.
- Use seeded `BtCtx` during testing for reproducible random selection.

---

## Troubleshooting

- **Tree always returns `Success` immediately**  
  Check root visibility and `rootInvisibleResult`.

- **Reactive node aborts unexpectedly**  
  Verify `While` condition and external state updates.

- **Wait/Timeout behave incorrectly**  
  Ensure `Tick(deltaTime)` is used and `deltaTime` is positive.

- **Logs are empty**  
  Confirm `debugEnabled: true` and non-zero `logLineLimit`.

---

## License

Licensed under the MIT License. See `LICENSE` at the repository root.
