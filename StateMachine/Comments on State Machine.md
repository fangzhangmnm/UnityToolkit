# Some comments on state machine

## Dont Fear of Uber Classes
State machines are inherently non-modular.

Each state has references to almost all other states.

And many states need the same service layer.

Successful games like Zelda, and many commercial Unity3D packages, do feature an uber core class for FSM.

**The modular feeling in the inspector is more on the config layer**

It's a good idea to use `ScriptableObject`s to configure your move and attack.

**Partial Class is your Friend**
In C#, the **partial class** syntax is your friend. It lets you separate a very big class into different files.

For example, you can separate different states under `CharacterController.Locomotion.cs` and `CharacterController.Attack.cs`, but make them live in a unified `CharacterController` class.

## A State is Actually a Million Substates

In the formal sense, a state machine doesn’t just live on your `enum`.

A "state" is the entire configuration of the system the transition function cares about at a given moment: `enum state`, `int HP`, `int stamina`, `int combo step`, `enum current weapon`, `enum current attack`, `uint flags`, etc.

Two frames that are both in Locomotion, but with different HP or a different weapon equipped, are technically different states. If you look at it that way, each enum value is more like a family of states, not a single point.

**Use `if` to write states with similar logic in batch**
You can think of each enum value as naming an abstract state class.

The actual states are all the different configurations (HP, weapon, flags…) that fall under that abstract class.

The state update function is like a base class method that runs across an entire family of implicit subclasses.

One `TickLocomotion` can handle running, swimming, dashing, crouch-walking, and so on. Use `if` to branch into different paths based on boolean flags, a secondary enum, and any other conditions.

This is how Zelda handles the combinatorial explosion of weapons, movement modes, and aiming status.
