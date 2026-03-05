public abstract class ClassStateMachine<TSelf>
    : SimpleStateMachine<ClassStateMachine<TSelf>.State>
    where TSelf : ClassStateMachine<TSelf>
{
    public abstract class State
    {
        public abstract State Tick(TSelf owner); // return this for stay, return new for transition
        public virtual void Enter(TSelf owner) { }
        public virtual void Exit(TSelf owner) { }
    }
    protected sealed override bool DispatchTick(State current, out State next)
    {
        if (IsFirstFrameInState)
            current.Enter((TSelf)this);
        next = current.Tick((TSelf)this);
        var changed = next != null && !ReferenceEquals(next, current);
        if (changed)
            current.Exit((TSelf)this);
        return changed;
    }
}