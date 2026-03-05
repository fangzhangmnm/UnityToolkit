# ImpactFeel

`ImpactFeel` is a small data-driven impact/effect system for fire-and-forget gameplay feedback.

It is built around three concepts:

- `ImpactController`: runtime component that plays named impacts.
- `ImpactSet`: ScriptableObject map from impact name to effect list.
- `Effect`: serializable effect unit (`AudioImpact`, `SpawnImpact`, `AnimationImpact`, etc).

## Quick Start

1. Add `ImpactController` to your object.
2. Assign target arrays (`AudioSource`, `Transform`, `Animator`, `Renderer`) if needed.
3. Create an `ImpactSet` asset and define impact names/effects.
4. Assign the `ImpactSet` to the controller.
5. Trigger by name:

```csharp
GetComponent<ImpactFeel.ImpactController>().Play("Hit");
```

## Notes

- `Play(...)` returns max effect duration in seconds.
- `GlobalImpactController` is for global effects (camera/time).
- `TimePause` requires global controller context by design.
- Custom effects can be added by inheriting `Effect` and implementing `Play(...)`.
