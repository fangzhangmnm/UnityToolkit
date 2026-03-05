public abstract class DelegateStateMachine : SimpleStateMachine<DelegateStateMachine.StateFunc>
{
    public delegate StateFunc StateFunc();
    protected sealed override bool DispatchTick(StateFunc current, out StateFunc next)
    {
        next = current();
        return next != null; // equal ref means reenter same state!
    }
}
