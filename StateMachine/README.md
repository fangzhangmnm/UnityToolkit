# SimpleStateMachine

State machine utilities for embodied gameplay logic.

Files:

- `ClassStateMachine.cs`
- `DelegateStateMachine.cs`
- `SimpleStateMachine.cs`

1. `ClassStateMachine<TSelf>`
- Best default for gameplay states.
- OO style (`State.Enter / Tick / Exit`).
- Return `this` to stay, return another state object to transition.

2. `DelegateStateMachine`
- Function style state machine using `StateFunc`.
- Return `null` to stay, return any non-null function to transition/reenter (even same method).

3. `SimpleStateMachine<TState>`
- Lowest-level generic state container.
- You implement `DispatchTick(current, out next)` directly.

## Quick Start (ClassStateMachine)

```csharp
using UnityEngine;

public class NPC : ClassStateMachine<NPC>
{
    public class Idle : State
    {
        public override State Tick(NPC owner)
        {
            if (owner.ShouldMove()) return new Move();
            return this; // stay
        }
    }

    public class Move : State
    {
        public override State Tick(NPC owner)
        {
            if (owner.ShouldStop()) return new Idle();
            return this; // stay
        }
    }

    void FixedUpdate() => TickState(Time.fixedDeltaTime, initialState: new Idle());

    bool ShouldMove() => false;
    bool ShouldStop() => false;
}
```

## Quick Start (DelegateStateMachine)

```csharp
using UnityEngine;

public class NPC : DelegateStateMachine
{
    void FixedUpdate() => TickState(Time.fixedDeltaTime, Idle);

    StateFunc Idle()
    {
        if (ShouldMove()) return Move;
        return null; // stay
    }

    StateFunc Move()
    {
        if (ShouldStop()) return Idle;
        return null; // stay
    }

    bool ShouldMove() => false;
    bool ShouldStop() => false;
}
```

## Quick Start (SimpleStateMachine)

```csharp
using UnityEngine;

public enum NPCState { Idle, Move }

public class NPC : SimpleStateMachine<NPCState>
{
    protected override bool DispatchTick(NPCState current, out NPCState next)
    {
        next = current;
        if (current == NPCState.Idle && ShouldMove()) { next = NPCState.Move; return true; }
        if (current == NPCState.Move && ShouldStop()) { next = NPCState.Idle; return true; }
        return false; // stay
    }

    void FixedUpdate() => TickState(Time.fixedDeltaTime, NPCState.Idle);

    bool ShouldMove() => false;
    bool ShouldStop() => false;
}
```

## Utilities

- `IsFirstFrameInState`: true only on the first frame after a transition (and initial state enter).
- `FramesInState`: number of frames spent in current state.
- `TimeInState`: total seconds spent in current state.
- `RandomEventEverySeconds(interval)`: frame-rate independent random gate with expected interval.
- `OnceEverySeconds(interval)`: deterministic periodic gate based on state time and `DeltaTime`.
