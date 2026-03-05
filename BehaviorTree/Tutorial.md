# Tutorial for Behavior Tree in GameDev

Behavior Trees describe complex AI behavior as small, modular tasks. 

Each task is a piece of code that runs repeatedly, similar to `MonoBehaviour.Update`. A behavior tree chooses which task to run based on whether the current task reports `Success`, `Failure`, or still `Running`, or interruption rules.

A task always has a clear boundary marked by `Enter` and `Exit`. A new task runs only after the current one has finished.


To start, just paste the script into your Unity3D project, and import the package and syntax sugar at the beginning of your behavior script.

```csharp
using SimpleBehaviorTree3;
using static SimpleBehaviorTree3.Bt;
```

Behavior Trees describe complex AI behavior as small, modular tasks. 

Each task is a piece of code that runs repeatedly, similar to `MonoBehaviour.Update`. A behavior tree chooses which task to run based on whether the current task reports `Success`, `Failure`, or still `Running`, or interruption rules.

A task always has a clear boundary marked by `Enter` and `Exit`. A new task runs only after the current one has finished.
 
Describe a task using C# Action:

```csharp
BtStatus TickAttack(bool isFirstFrame, string attackName)
{
    if(isFirstFrame) anim.Play(attackName);
    if(CheckHitboxOverlap()) return BtStatus.Success;
    if(!anim.IsPlaying(attackName)) return BtStatus.Failure;
    return BtStatus.Running;
}
void Start()
{
    var Attack = Action(TickAttack, "NormalAttack", onExit: (reason) => anim.Stop("NormalAttack"));
    //...
}
```

Tasks can be composed into more complex tasks

A `Sequence` describes a Standard Operating Procedure (SOP), which is a list of tasks you must perform in order. If any subtask fails, the task fails.

A `Selector` describes a list of choices that can individually achieve the same goal. If any subtask succeeds, the task succeeds.

```csharp
var SaveWork = Sequence(
    GitCommit,
    GitPush,
    CloseComputer
);
// A Selector succeeds if any subtask succeeds, describing a list of choices from best to fallbacks
var FindFood = Selector(
    FindFoodInFridge,
    CookFood,
    GoToSupermarket
);
```

In Reactive Behavior Tree, a task may be interrupted externally, when a higher-priority task kicks in.

The difference between `Priority` and `Ensure` is that `Priority` reports success if any task succeeds, `Ensure` reports only after all tasks are cleared. Make sure to write condition nodes. 

```csharp
var StrictlyWLBProgrammer = Priority(
    Eat.If(IsHungry),
    Sleep.If(IsTired),
    Develop.WhileNot(IsTaskDone),
    Idle,
);
var BabyHandler = Ensure(
    HandleChoking.If(IsChoking),
    Comfort.If(IsCrying),
    Feed.If(IsHungry),
    TeachCalculus
);
```

The difference between `If` and `While` nodes is whether the task should be interrupted when the condition no longer holds.
```csharp
var Sleep = Wait(28800);
Sleep.If(IsTired); // sleep for 8 hours
Sleep.While(IsTired); // sleep for up to 8 hours
```

---

There is an old saying: *more is different*. You don’t always want a tactical squad game with super smart AI flanking, or a dystopian institution management sim (think of RimWorld, Lobotomy Corporation). But it still helps to think of each game AI agent not as “the thing that must be clever”, but as a basic building block the level designer uses to make interesting situations.

In a platformer, a block is purely passive and a spike just reactively hurts you on contact. In a single-player FPS, an enemy can be as dumb as `if player in cone → shoot`. The fun comes from placement and protocol: this guard watches this choke door, these archers cover that bridge, or a gatekeeper pulls a lever and seals the back exit, splitting the player party. Soldiers are dangerous not because they are smart, but because they know how this level works.

When writing this library, I focus on making it a Domain-Specific Language (DSL) that encourages the user to think like:

- a policymaker writing rules and Standard Operating Procedure in a docx document
- a kid describing a new Hide and Seek ruleset variant in human language
- a king or general assigning rules, roles, responsibilities, and permissions
- an anxious risk control officer defensively thinking through failure cases and fallback options
- a senior system architect who solves race conditions not by examining every logic path, but by proposing a reservation policy

And free the user from getting tangled in logic puzzles and API docs inside an IDE.

---

Here are best practices, showing intended responsibility and non-responsibility of Behavior Tree:

**Don't use it for engine level glue code**
If you can write programs, then a visual scripting layer just wastes time and overcomplicates things.
Just write C# inline functions.
Don't treat Unity API calls as atomic actions. The correct granularity is when each action can be clearly described as a verb in human language.

**Don't use it for animators and character controllers**
Finite State Machine is the right tool for modeling combo, attack cancel, action set, etc.

**Use it to describe world knowledge and game system knowledge**
The superpower of Behavior Tree is describing dumb bureaucratic Standard Operating Procedure (SOP). 
It assigns characters roles and responsibilities, tells them how to operate props, and teaches prerequisites and fallbacks. 
It tells them what to do when player comes, when alarm rings, when health is low. It tells them where are the covers, turrets, gates. It tells them you must turn off the breaker before replacing the fuse.

**Use it for patterns and performative actions**
Think of military tactic, cinematic moments and daily rituals, attack patterns in Soulslike bosses.
Behavior Tree is intended to encapsulate multi-step asynchronous coroutines into a single verb, with grammars like priority and fallback as first-class citizens.

**Don't use it for problem solving and improvisation**
Goal-Oriented Action Planning (GOAP) is the right tool for navigating complex world-model state space. 
Write multiple Behavior Trees as skills. Make each of them simple and dumb, with bounded scope, well-defined prerequisites, and mechanical, easily predictable consequences. Run pathfinding with GOAP using whatever cost function you like.
Don't try to make your AI clever with an overambitious uber behavior tree.

**Don't use it for decision making and "personality"**
Behavior Tree is awkward when dealing with tasks with conflicting goals (trolley problem lol).
If you want a "vibe" of personality, fuzzy logic, or decision making under "moral dilemmas", use whatever sounds fancy to you, or just a d100 random table for better authorial control -- advanced ML components like ChatGPT are just another random number generator, only less transparent and harder to fine-tune.

**Use it for Game Master Layer**
Think of Hide and Seek, and its modern SCP variants. Behavior Tree is good for describing the main loop of the gameplay, like a Match, a Racing Game, etc. 
Writing winning/losing conditions, rewards, punishments, taboos, timeouts, phases. 
Global rule makes the game fun.
