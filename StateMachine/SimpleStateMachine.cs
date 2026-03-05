using UnityEngine;

public abstract class SimpleStateMachine<TState> : MonoBehaviour
{
    // behavior interface
    protected abstract bool DispatchTick(TState current, out TState next);
    // internaal logic
    protected void TickState(float deltaTime, TState initialState)
    {
        DeltaTime = deltaTime;
        if (!_isInitialized)
        {
            _current = initialState;
            _isInitialized = true;
        }
        var prev = _current;
#if UNITY_EDITOR
        DebugStateInfo = $"{CurrentStateName}";
#endif
        if (DispatchTick(_current, out var next))
        {
            // todo make frame and time update next frame
            _current = next;
            FramesInState = 0;
            TimeInState = 0f;
        }
        else
        {
            FramesInState++;
            TimeInState += deltaTime;
        }
    }
    private bool _isInitialized = false;
    private TState _current = default;
    // utility
    public string CurrentStateName => _isInitialized ? _current.ToString() : "null";
#if UNITY_EDITOR
    public string DebugStateInfo;
#endif
    // time utility
    protected float DeltaTime { get; private set; }
    public int FramesInState { get; private set; }
    public float TimeInState { get; private set; }
    public bool IsFirstFrameInState => FramesInState == 0;
    protected bool RandomEventEverySeconds(float interval) => UnityEngine.Random.Range(0f, 1f) < (DeltaTime / interval);
    protected bool OnceEverySeconds(float interval) => (FramesInState % Mathf.CeilToInt(interval / DeltaTime)) == 0;
    protected TState CurrentState => _current;
}
