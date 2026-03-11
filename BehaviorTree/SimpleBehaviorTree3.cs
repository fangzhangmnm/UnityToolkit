using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SimpleBehaviorTree3
{
    public enum BtStatus { Success, Failure, Running }
    public enum BtEndReason { Succeeded, Failed, Aborted }

    public interface IBtRuntime { } // only valid between Enter and Exit, destroyed after Exit

    public class BtCtx
    {
        public readonly Random rng;
        public float deltaTime;
        internal BtTrace trace;

        public BtCtx() : this(new Random()) { }
        public BtCtx(int seed) : this(new Random(seed)) { }
        public BtCtx(Random rng)
            => this.rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    public abstract class BtNode
    {
        public readonly string name;

        protected BtNode(string name)
            => this.name = name ?? throw new ArgumentNullException(nameof(name));

        protected static string ResolveName(string name, string defaultName)
            => string.IsNullOrWhiteSpace(name) ? defaultName : name;

        internal static string ResolveDelegateName(Delegate fn, string name, string defaultName)
        {
            if (!string.IsNullOrWhiteSpace(name))
                return name;
            if (fn == null || LooksCompilerGenerated(fn.Method))
                return defaultName;
            return fn.Method.Name;
        }

        static bool LooksCompilerGenerated(MethodInfo method)
        {
            if (method == null) return true;
            if (method.IsDefined(typeof(CompilerGeneratedAttribute), false)) return true;
            if (method.DeclaringType?.IsDefined(typeof(CompilerGeneratedAttribute), false) == true) return true;
            return method.Name.Contains("<") || method.Name.StartsWith("g__", StringComparison.Ordinal);
        }

        // rule: a node is a (composited or concrete) task can be revisited multiple times but have a clear start and end
        public abstract IBtRuntime Enter(BtCtx ctx);
        // the owner guarantees to call Tick only between Enter() and Exit()
        public abstract BtStatus Tick(IBtRuntime rt, BtCtx ctx);
        // the owner guarantees to call Exit() if Tick() returns non Running
        public abstract void Exit(IBtRuntime rt, BtCtx ctx, BtEndReason reason);

        // Invisible nodes are treated like missing children by compositors.
        public virtual bool IsVisible(BtCtx ctx)
            => true;

        // Active-aware visibility is used internally by BtHandle for reactive visibility semantics.
        internal virtual bool IsVisible(BtCtx ctx, bool isActive)
            => IsVisible(ctx);

        internal virtual bool ShouldLogLifecycle(IBtRuntime rt)
            => true;

        internal virtual void AppendDebugPath(IBtRuntime rt, List<string> segments, ref string prefix, ref string postfix, ref string rename)
        {
            var label = rename ?? name;
            if (!string.IsNullOrWhiteSpace(label))
                segments.Add(FormatDebugSegment(label, postfix, null, prefix));
            prefix = string.Empty;
            postfix = string.Empty;
            rename = null;
        }

        internal static string FormatDebugSegment(string label, string postfix = "", int? childIndex = null, string prefix = "")
        {
            if (string.IsNullOrWhiteSpace(label))
                return string.Empty;

            var segment = string.Concat(prefix, label, postfix);
            if (childIndex.HasValue)
                segment += $"[{childIndex.Value}]";
            return segment;
        }
    }

    internal sealed class BtTrace
    {
        readonly struct Frame
        {
            internal readonly BtNode node;
            internal readonly IBtRuntime rt;

            internal Frame(BtNode node, IBtRuntime rt)
            {
                this.node = node;
                this.rt = rt;
            }
        }

        readonly List<Frame> stack = new List<Frame>();
        readonly List<string> logLines = new List<string>();
        readonly int logLineLimit;

        internal BtTrace(int logLineLimit, bool enabled)
        {
            this.logLineLimit = Math.Max(1, logLineLimit);
            this.enabled = enabled;
        }

        internal bool enabled;
        internal IReadOnlyList<string> LogLines => logLines;
        internal string CurrentPath => BuildCurrentPath();

        internal void OnEnter(BtNode node, IBtRuntime rt)
        {
            stack.Add(new Frame(node, rt));
            if (enabled && node.ShouldLogLifecycle(rt))
            {
                var currentPath = BuildCurrentPath();
                if (!string.IsNullOrWhiteSpace(currentPath))
                    AddLog($"{currentPath}: Entered");
            }
        }

        internal void OnExit(BtNode node, IBtRuntime rt, BtEndReason reason)
        {
            if (enabled && node.ShouldLogLifecycle(rt))
            {
                var currentPath = BuildCurrentPath();
                if (!string.IsNullOrWhiteSpace(currentPath))
                    AddLog($"{currentPath}: {FormatEndReason(reason)}");
            }

            if (stack.Count > 0)
                stack.RemoveAt(stack.Count - 1);
        }

        string BuildCurrentPath()
        {
            if (stack.Count == 0) return string.Empty;

            var segments = new List<string>(stack.Count);
            var prefix = string.Empty;
            var postfix = string.Empty;
            string rename = null;

            for (int i = 0; i < stack.Count; i++)
                stack[i].node.AppendDebugPath(stack[i].rt, segments, ref prefix, ref postfix, ref rename);

            return string.Join(" > ", segments);
        }

        void AddLog(string line)
        {
            if (logLines.Count >= logLineLimit)
                logLines.RemoveAt(0);
            logLines.Add(line);
        }

        static string FormatEndReason(BtEndReason reason)
            => reason switch
            {
                BtEndReason.Succeeded => "Succeeded",
                BtEndReason.Failed => "Failed",
                BtEndReason.Aborted => "Aborted",
                _ => reason.ToString()
            };
    }

    // BtHandle is the minimal internal lifecycle runner for one subtree definition.
    internal sealed class BtHandle
    {
        readonly BtNode node;
        IBtRuntime rt;

        internal BtHandle(BtNode node)
        {
            this.node = node ?? throw new ArgumentNullException(nameof(node));
        }

        internal BtStatus Tick(BtCtx ctx)
        {
            if (rt == null)
            {
                rt = node.Enter(ctx);
                ctx.trace?.OnEnter(node, rt);
            }
            var status = node.Tick(rt, ctx);
            if (status != BtStatus.Running)
            {
                var reason = status == BtStatus.Success ? BtEndReason.Succeeded : BtEndReason.Failed;
                node.Exit(rt, ctx, reason);
                ctx.trace?.OnExit(node, rt, reason);
                rt = null;
            }
            return status;
        }

        internal void Abort(BtCtx ctx)
        {
            if (rt == null) return;
            node.Exit(rt, ctx, BtEndReason.Aborted);
            ctx.trace?.OnExit(node, rt, BtEndReason.Aborted);
            rt = null;
        }

        internal bool IsActive => rt != null;
        internal bool IsVisible(BtCtx ctx)
            => node.IsVisible(ctx, IsActive);
    }

    // BtInstance is the public root executor and root-level policy container for one subtree definition.
    public sealed class BtInstance
    {
        readonly BtNode node;
        readonly BtHandle handle;
        readonly BtCtx ctx;
        readonly BtStatus rootInvisibleResult;

        public BtInstance(BtNode node, BtStatus rootInvisibleResult = BtStatus.Success, bool debugEnabled = false, int logLineLimit = 5)
            : this(node, new BtCtx(), rootInvisibleResult, debugEnabled, logLineLimit) { }

        public BtInstance(BtNode node, BtCtx ctx, BtStatus rootInvisibleResult = BtStatus.Success, bool debugEnabled = false, int logLineLimit = 5)
        {
            this.node = node ?? throw new ArgumentNullException(nameof(node));
            this.ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            if (rootInvisibleResult == BtStatus.Running)
                throw new ArgumentException("rootInvisibleResult cannot be Running", nameof(rootInvisibleResult));
            this.rootInvisibleResult = rootInvisibleResult;
            this.ctx.trace = new BtTrace(logLineLimit, debugEnabled);
            handle = new BtHandle(node);
        }

        public BtInstance(BtNode node, int seed, BtStatus rootInvisibleResult = BtStatus.Success, bool debugEnabled = false, int logLineLimit = 5)
            : this(node, new BtCtx(seed), rootInvisibleResult, debugEnabled, logLineLimit) { }

        public BtStatus Tick()
        {
            if (!handle.IsVisible(ctx))
            {
                handle.Abort(ctx);
                return rootInvisibleResult;
            }
            return handle.Tick(ctx);
        }

        public BtStatus Tick(float deltaTime)
        {
            ctx.deltaTime = deltaTime;
            return Tick();
        }

        public void Abort()
            => handle.Abort(ctx);

        public bool IsActive => handle.IsActive;
        public bool IsVisible => handle.IsVisible(ctx);
        public BtCtx Ctx => ctx;
        public bool DebugEnabled
        {
            get => ctx.trace != null && ctx.trace.enabled;
            set
            {
                if (ctx.trace != null)
                    ctx.trace.enabled = value;
            }
        }
        public string CurrentPath => DebugEnabled ? ctx.trace.CurrentPath : string.Empty;
        public IReadOnlyList<string> LogLines => DebugEnabled ? ctx.trace.LogLines : Array.Empty<string>();
    }

    internal sealed class CompositeRuntime : IBtRuntime
    {
        internal int activeIndex = -1;
        internal BtHandle activeChild = null;
    }

    // Runs one child at a time over a list of children.
    public abstract class CompositeNode : BtNode
    {
        protected readonly BtNode[] children;

        protected CompositeNode(string name, params BtNode[] children) : base(name)
            => this.children = children ?? Array.Empty<BtNode>();

        private protected void BindChild(CompositeRuntime rt)
        {
            if (rt.activeIndex < 0 || rt.activeIndex >= children.Length)
                throw new InvalidOperationException("Composite activeIndex is out of range");
            rt.activeChild = new BtHandle(children[rt.activeIndex]);
        }

        private protected static bool IsChildVisible(BtNode child, BtCtx ctx)
            => child.IsVisible(ctx);

        private protected static bool IsChildVisible(BtHandle childHandle, BtCtx ctx)
            => childHandle.IsVisible(ctx);

        private protected void AbortActiveChild(CompositeRuntime rt, BtCtx ctx)
        {
            if (rt.activeChild != null)
                rt.activeChild.Abort(ctx);
            rt.activeChild = null;
        }

        private protected void AbortChild(CompositeRuntime rt, BtCtx ctx)
        {
            AbortActiveChild(rt, ctx);
            rt.activeIndex = -1;
        }

        private protected void EnsureChild(CompositeRuntime rt, int index, BtCtx ctx)
        {
            if (rt.activeIndex == index && rt.activeChild != null)
                return;

            AbortChild(rt, ctx);
            rt.activeIndex = index;
            BindChild(rt);
        }

        public override void Exit(IBtRuntime _rt, BtCtx ctx, BtEndReason reason)
        {
            var rt = (CompositeRuntime)_rt;
            if (reason == BtEndReason.Aborted && rt.activeChild != null)
                rt.activeChild.Abort(ctx);
        }

        internal override bool ShouldLogLifecycle(IBtRuntime rt)
            => false;

        internal override void AppendDebugPath(IBtRuntime _rt, List<string> segments, ref string prefix, ref string postfix, ref string rename)
        {
            var rt = (CompositeRuntime)_rt;
            var label = rename ?? name;
            if (!string.IsNullOrWhiteSpace(label))
                segments.Add(FormatDebugSegment(label, postfix, rt.activeIndex >= 0 ? rt.activeIndex : null, prefix));
            prefix = string.Empty;
            postfix = string.Empty;
            rename = null;
        }
    }

    // Succeeds only if every child succeeds, and stops at the first failure.
    public sealed class SequenceNode : CompositeNode
    {
        public SequenceNode(params BtNode[] children) : this(null, children) { }
        public SequenceNode(string name, params BtNode[] children) : base(ResolveName(name, "Sequence"), children) { }

        public override IBtRuntime Enter(BtCtx ctx)
        {
            var rt = new CompositeRuntime();
            rt.activeIndex = 0;
            return rt;
        }

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (CompositeRuntime)_rt;
            while (rt.activeIndex >= 0 && rt.activeIndex < children.Length)
            {
                if (rt.activeChild != null && !IsChildVisible(rt.activeChild, ctx))
                {
                    AbortActiveChild(rt, ctx);
                    rt.activeIndex++;
                    continue;
                }
                if (rt.activeChild == null)
                {
                    if (!IsChildVisible(children[rt.activeIndex], ctx))
                    {
                        rt.activeIndex++;
                        continue;
                    }
                    BindChild(rt);
                }

                var status = rt.activeChild.Tick(ctx);
                if (status == BtStatus.Running) return BtStatus.Running;
                if (status == BtStatus.Failure) return BtStatus.Failure;
                AbortActiveChild(rt, ctx);
                rt.activeIndex++;
            }
            return BtStatus.Success;
        }
    }

    // Succeeds on the first successful child, and fails only if every child fails.
    public sealed class SelectorNode : CompositeNode
    {
        public SelectorNode(params BtNode[] children) : this(null, children) { }
        public SelectorNode(string name, params BtNode[] children) : base(ResolveName(name, "Selector"), children) { }

        public override IBtRuntime Enter(BtCtx ctx)
        {
            var rt = new CompositeRuntime();
            rt.activeIndex = 0;
            return rt;
        }

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (CompositeRuntime)_rt;
            while (rt.activeIndex >= 0 && rt.activeIndex < children.Length)
            {
                if (rt.activeChild != null && !IsChildVisible(rt.activeChild, ctx))
                {
                    AbortActiveChild(rt, ctx);
                    rt.activeIndex++;
                    continue;
                }
                if (rt.activeChild == null)
                {
                    if (!IsChildVisible(children[rt.activeIndex], ctx))
                    {
                        rt.activeIndex++;
                        continue;
                    }
                    BindChild(rt);
                }

                var status = rt.activeChild.Tick(ctx);
                if (status == BtStatus.Running) return BtStatus.Running;
                if (status == BtStatus.Success) return BtStatus.Success;
                AbortActiveChild(rt, ctx);
                rt.activeIndex++;
            }
            return BtStatus.Failure;
        }
    }

    internal sealed class RandomSelectorRuntime : IBtRuntime
    {
        internal readonly List<int> remaining = new List<int>();
        internal int activeIndex = -1;
        internal BtHandle activeChild = null;
    }

    // Tries visible children in a random order until one succeeds or all candidates fail.
    public sealed class RandomSelectorNode : CompositeNode
    {
        public RandomSelectorNode(params BtNode[] children) : this(null, children) { }
        public RandomSelectorNode(string name, params BtNode[] children) : base(ResolveName(name, "RandomSelector"), children) { }

        public override IBtRuntime Enter(BtCtx ctx)
        {
            var rt = new RandomSelectorRuntime();
            for (int i = 0; i < children.Length; i++)
                rt.remaining.Add(i);
            return rt;
        }

        public override void Exit(IBtRuntime _rt, BtCtx ctx, BtEndReason reason)
        {
            var rt = (RandomSelectorRuntime)_rt;
            if (reason == BtEndReason.Aborted && rt.activeChild != null)
                rt.activeChild.Abort(ctx);
        }

        internal override void AppendDebugPath(IBtRuntime _rt, List<string> segments, ref string prefix, ref string postfix, ref string rename)
        {
            var rt = (RandomSelectorRuntime)_rt;
            var label = rename ?? name;
            if (!string.IsNullOrWhiteSpace(label))
                segments.Add(FormatDebugSegment(label, postfix, rt.activeIndex >= 0 ? rt.activeIndex : null, prefix));
            prefix = string.Empty;
            postfix = string.Empty;
            rename = null;
        }

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (RandomSelectorRuntime)_rt;
            while (true)
            {
                if (rt.activeChild != null)
                {
                    if (!IsChildVisible(rt.activeChild, ctx))
                    {
                        rt.activeChild.Abort(ctx);
                        rt.activeChild = null;
                        rt.activeIndex = -1;
                        continue;
                    }

                    var status = rt.activeChild.Tick(ctx);
                    if (status == BtStatus.Running) return BtStatus.Running;

                    rt.activeChild = null;
                    rt.activeIndex = -1;
                    if (status == BtStatus.Success) return BtStatus.Success;
                    continue;
                }

                var visibleCount = 0;
                for (int i = 0; i < rt.remaining.Count; i++)
                {
                    if (IsChildVisible(children[rt.remaining[i]], ctx))
                        visibleCount++;
                }
                if (visibleCount == 0) return BtStatus.Failure;

                var pick = ctx.rng.Next(visibleCount);
                for (int i = 0; i < rt.remaining.Count; i++)
                {
                    var index = rt.remaining[i];
                    if (!IsChildVisible(children[index], ctx))
                        continue;
                    if (pick > 0)
                    {
                        pick--;
                        continue;
                    }

                    rt.remaining.RemoveAt(i);
                    rt.activeIndex = index;
                    rt.activeChild = new BtHandle(children[index]);
                    break;
                }
            }
        }
    }

    // Rechecks earlier children every tick, skips invisible options, and runs the first visible child that does not fail.
    public class ReactiveSelectorNode : CompositeNode
    {
        public ReactiveSelectorNode(params BtNode[] children) : this(null, children) { }
        public ReactiveSelectorNode(string name, params BtNode[] children) : base(ResolveName(name, "Priority"), children) { }

        public override IBtRuntime Enter(BtCtx ctx)
            => new CompositeRuntime();

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (CompositeRuntime)_rt;
            for (int i = 0; i < children.Length; i++)
            {
                bool visible = rt.activeIndex == i && rt.activeChild != null
                    ? IsChildVisible(rt.activeChild, ctx)
                    : IsChildVisible(children[i], ctx);
                if (!visible)
                {
                    if (rt.activeIndex == i)
                        AbortChild(rt, ctx);
                    continue;
                }

                EnsureChild(rt, i, ctx);
                var status = rt.activeChild.Tick(ctx);
                if (status == BtStatus.Running) return BtStatus.Running;

                AbortChild(rt, ctx);
                if (status == BtStatus.Success) return BtStatus.Success;
            }
            return BtStatus.Failure;
        }
    }

    // Rechecks earlier children every tick, skips invisible steps, and keeps the first visible unfinished step as current.
    public class ReactiveSequencerNode : CompositeNode
    {
        public ReactiveSequencerNode(params BtNode[] children) : this(null, children) { }
        public ReactiveSequencerNode(string name, params BtNode[] children) : base(ResolveName(name, "Ensure"), children) { }

        public override IBtRuntime Enter(BtCtx ctx)
            => new CompositeRuntime();

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (CompositeRuntime)_rt;
            for (int i = 0; i < children.Length; i++)
            {
                bool visible = rt.activeIndex == i && rt.activeChild != null
                    ? IsChildVisible(rt.activeChild, ctx)
                    : IsChildVisible(children[i], ctx);
                if (!visible)
                {
                    if (rt.activeIndex == i)
                        AbortChild(rt, ctx);
                    continue;
                }

                EnsureChild(rt, i, ctx);
                var status = rt.activeChild.Tick(ctx);
                if (status == BtStatus.Running) return BtStatus.Running;

                AbortChild(rt, ctx);
                if (status == BtStatus.Failure) return BtStatus.Failure;
            }
            return BtStatus.Success;
        }
    }

    internal class DecoratorRuntime : IBtRuntime
    {
        internal BtHandle child = null;

        internal DecoratorRuntime(BtNode child)
            => this.child = new BtHandle(child);
    }

    // Wraps one child and changes when or how that child runs or reports results.
    public abstract class DecoratorNode : BtNode
    {
        protected readonly BtNode child;

        protected DecoratorNode(BtNode child, string name) : base(name)
            => this.child = child ?? throw new ArgumentNullException(nameof(child));

        public override IBtRuntime Enter(BtCtx ctx)
            => new DecoratorRuntime(child);

        public override void Exit(IBtRuntime _rt, BtCtx ctx, BtEndReason reason)
        {
            var rt = (DecoratorRuntime)_rt;
            if (reason == BtEndReason.Aborted)
                rt.child.Abort(ctx);
        }

        public override bool IsVisible(BtCtx ctx)
            => child.IsVisible(ctx);

        internal override bool IsVisible(BtCtx ctx, bool isActive)
            => child.IsVisible(ctx, isActive);
    }

    // Controls entry visibility and active interruption policy for one child.
    public class VisibilityNode : DecoratorNode
    {
        readonly Func<BtCtx, bool> cond;
        readonly bool invert;
        readonly bool interruptSelfOnVisible;
        readonly bool interruptSelfOnInvisible;

        public VisibilityNode(BtNode child, Func<BtCtx, bool> cond, bool invert, bool interruptSelfOnVisible, bool interruptSelfOnInvisible, string name = null)
            : base(child, ResolveName(name, "Visibility"))
        {
            this.cond = cond;
            this.invert = invert;
            this.interruptSelfOnVisible = interruptSelfOnVisible;
            this.interruptSelfOnInvisible = interruptSelfOnInvisible;
        }

        bool ConditionMatches(BtCtx ctx) => cond == null || cond(ctx);

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (DecoratorRuntime)_rt;
            var condRaw = ConditionMatches(ctx);
            var visibleNow = invert ? !condRaw : condRaw;
            if (rt.child.IsActive && interruptSelfOnVisible && visibleNow)
                rt.child.Abort(ctx);
            if (rt.child.IsActive && interruptSelfOnInvisible && !visibleNow)
                rt.child.Abort(ctx);
            return rt.child.Tick(ctx);
        }

        public override bool IsVisible(BtCtx ctx)
            => IsVisible(ctx, false);

        internal override bool IsVisible(BtCtx ctx, bool isActive)
        {
            var condRaw = ConditionMatches(ctx);
            var visibleNow = invert ? !condRaw : condRaw;
            if (!isActive)
                return visibleNow && child.IsVisible(ctx, false);

            if (!visibleNow && interruptSelfOnInvisible)
                return false;
            return child.IsVisible(ctx, true);
        }

        internal override bool ShouldLogLifecycle(IBtRuntime rt)
            => false;

        internal override void AppendDebugPath(IBtRuntime rt, List<string> segments, ref string prefix, ref string postfix, ref string rename)
            => prefix += (invert ? "!" : string.Empty) + name + "? ";
    }

    internal sealed class RepeatRuntime : IBtRuntime
    {
        internal int completedRuns = 0;
        internal BtHandle child = null;

        internal RepeatRuntime(BtNode child)
            => this.child = new BtHandle(child);
    }

    // Re-runs the child until it has succeeded enough times, or stops on failure.
    public sealed class RepeatNode : BtNode
    {
        readonly BtNode child;
        readonly int repeat;

        public RepeatNode(BtNode child, int repeat = -1, string name = null) : base(ResolveName(name, "Repeat"))
        {
            this.child = child ?? throw new ArgumentNullException(nameof(child));
            this.repeat = repeat;
        }

        public override IBtRuntime Enter(BtCtx ctx)
            => new RepeatRuntime(child);

        public override bool IsVisible(BtCtx ctx)
            => child.IsVisible(ctx);

        public override void Exit(IBtRuntime _rt, BtCtx ctx, BtEndReason reason)
        {
            var rt = (RepeatRuntime)_rt;
            if (reason == BtEndReason.Aborted)
                rt.child.Abort(ctx);
        }

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (RepeatRuntime)_rt;
            if (repeat == 0) return BtStatus.Success;

            var status = rt.child.Tick(ctx);
            if (status == BtStatus.Running) return BtStatus.Running;
            if (status == BtStatus.Failure) return BtStatus.Failure;

            rt.completedRuns++;
            if (repeat >= 0 && rt.completedRuns >= repeat)
                return BtStatus.Success;

            // Delay the next cycle to the next frame to avoid same-frame infinite loops.
            return BtStatus.Running;
        }

        internal override bool ShouldLogLifecycle(IBtRuntime rt)
            => false;

        internal override void AppendDebugPath(IBtRuntime _rt, List<string> segments, ref string prefix, ref string postfix, ref string rename)
        {
            var rt = (RepeatRuntime)_rt;
            var current = rt.completedRuns + (rt.child.IsActive ? 1 : 0);
            var target = repeat < 0 ? "∞" : repeat.ToString();
            postfix += $"({current}/{target})";
        }
    }

    // Reports success whenever the child finishes, regardless of how it ended.
    public sealed class ForceSuccessNode : DecoratorNode
    {
        public ForceSuccessNode(BtNode child, string name = null) : base(child, ResolveName(name, "ForceSuccess")) { }

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (DecoratorRuntime)_rt;
            var status = rt.child.Tick(ctx);
            if (status == BtStatus.Running) return BtStatus.Running;
            return BtStatus.Success;
        }

        internal override bool ShouldLogLifecycle(IBtRuntime rt)
            => false;

        internal override void AppendDebugPath(IBtRuntime rt, List<string> segments, ref string prefix, ref string postfix, ref string rename)
        {
        }
    }

    internal sealed class TimeoutRuntime : IBtRuntime
    {
        internal readonly BtHandle child;
        internal float elapsed;

        internal TimeoutRuntime(BtNode child)
            => this.child = new BtHandle(child);
    }

    // Fails and aborts the child if it stays running longer than the timeout duration.
    public sealed class TimeoutNode : DecoratorNode
    {
        readonly float timeout;

        public TimeoutNode(BtNode child, float timeout, string name = null) : base(child, ResolveName(name, "Timeout"))
            => this.timeout = Math.Max(0f, timeout);

        public override IBtRuntime Enter(BtCtx ctx)
            => new TimeoutRuntime(child);

        public override void Exit(IBtRuntime _rt, BtCtx ctx, BtEndReason reason)
        {
            var rt = (TimeoutRuntime)_rt;
            if (reason == BtEndReason.Aborted)
                rt.child.Abort(ctx);
        }

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            if (timeout <= 0f) return BtStatus.Failure;

            var rt = (TimeoutRuntime)_rt;
            var status = rt.child.Tick(ctx);
            if (status != BtStatus.Running)
                return status;

            rt.elapsed += Math.Max(0f, ctx.deltaTime);
            if (rt.elapsed >= timeout)
            {
                rt.child.Abort(ctx);
                return BtStatus.Failure;
            }
            return BtStatus.Running;
        }

        internal override bool ShouldLogLifecycle(IBtRuntime rt)
            => false;

        internal override void AppendDebugPath(IBtRuntime rt, List<string> segments, ref string prefix, ref string postfix, ref string rename)
        {
        }
    }

    internal sealed class WaitRuntime : IBtRuntime
    {
        internal float elapsed;
    }

    // Waits for the configured duration, then succeeds.
    public sealed class WaitNode : BtNode
    {
        readonly float duration;

        public WaitNode(float duration, string name = null) : base(ResolveName(name, "Wait"))
            => this.duration = Math.Max(0f, duration);

        public override IBtRuntime Enter(BtCtx ctx)
            => new WaitRuntime();

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            if (duration <= 0f) return BtStatus.Success;

            var rt = (WaitRuntime)_rt;
            rt.elapsed += Math.Max(0f, ctx.deltaTime);
            return rt.elapsed >= duration ? BtStatus.Success : BtStatus.Running;
        }

        public override void Exit(IBtRuntime _rt, BtCtx ctx, BtEndReason reason)
        {
        }
    }

    // Only changes the displayed name of the child, without changing its behavior.
    public sealed class RenameNode : DecoratorNode
    {
        public RenameNode(BtNode child, string name) : base(child, ResolveName(name, child?.name ?? "Node")) { }

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (DecoratorRuntime)_rt;
            return rt.child.Tick(ctx);
        }

        internal override bool ShouldLogLifecycle(IBtRuntime rt)
            => false;

        internal override void AppendDebugPath(IBtRuntime rt, List<string> segments, ref string prefix, ref string postfix, ref string rename)
            => rename = name;
    }

    internal sealed class ActionRuntime : IBtRuntime
    {
        internal float elapsedStateTime;
    }

    // Calls one callback every tick, with elapsed time in this state and an optional final callback on exit.
    public sealed class ActionNode : BtNode
    {
        readonly Func<float, BtStatus> tick;
        readonly global::System.Action<BtEndReason> onExit;

        public ActionNode(Func<float, BtStatus> tick, global::System.Action<BtEndReason> onExit = null, string name = null)
            : base(ResolveDelegateName(tick, name, "Action"))
        {
            this.tick = tick ?? throw new ArgumentNullException(nameof(tick));
            this.onExit = onExit;
        }

        public override IBtRuntime Enter(BtCtx ctx)
            => new ActionRuntime();

        public override BtStatus Tick(IBtRuntime _rt, BtCtx ctx)
        {
            var rt = (ActionRuntime)_rt;
            var status = tick(rt.elapsedStateTime);
            if (status == BtStatus.Running)
                rt.elapsedStateTime += Math.Max(0f, ctx.deltaTime);
            return status;
        }

        public override void Exit(IBtRuntime _rt, BtCtx ctx, BtEndReason reason)
            => onExit?.Invoke(reason);
    }

    public static class Bt
    {
        /// <summary>Runs children until one fails or all succeed.</summary>
        public static BtNode Sequence(params BtNode[] children)
            => new SequenceNode(children);

        /// <summary>Runs children until one fails or all succeed.</summary>
        public static BtNode Sequence(string name, params BtNode[] children)
            => new SequenceNode(name, children);

        /// <summary>Runs children until one succeeds or all fail.</summary>
        public static BtNode Selector(params BtNode[] children)
            => new SelectorNode(children);

        /// <summary>Runs children until one succeeds or all fail.</summary>
        public static BtNode Selector(string name, params BtNode[] children)
            => new SelectorNode(name, children);

        /// <summary>Tries visible children in a random order until one succeeds or all candidates fail.</summary>
        public static BtNode RandomSelector(params BtNode[] children)
            => new RandomSelectorNode(children);

        /// <summary>Tries visible children in a random order until one succeeds or all candidates fail.</summary>
        public static BtNode RandomSelector(string name, params BtNode[] children)
            => new RandomSelectorNode(name, children);

        /// <summary>Rechecks earlier options every tick and lets higher-priority work preempt lower running work.</summary>
        public static BtNode Priority(params BtNode[] children)
            => new ReactiveSelectorNode(children);

        /// <summary>Rechecks earlier options every tick and lets higher-priority work preempt lower running work.</summary>
        public static BtNode Priority(string name, params BtNode[] children)
            => new ReactiveSelectorNode(name, children);

        /// <summary>Rechecks earlier required steps every tick and treats invisible steps as missing.</summary>
        public static BtNode Ensure(params BtNode[] children)
            => new ReactiveSequencerNode(children);

        /// <summary>Rechecks earlier required steps every tick and treats invisible steps as missing.</summary>
        public static BtNode Ensure(string name, params BtNode[] children)
            => new ReactiveSequencerNode(name, children);

        /// <summary>Runs one callback every tick with elapsed state time in seconds; onExit runs on success, failure, or abort.</summary>
        public static BtNode Action(Func<float, BtStatus> tick, global::System.Action<BtEndReason> onExit = null, string name = null)
            => new ActionNode(tick, onExit, name);

        /// <summary>Runs one callback every tick with elapsed state time in seconds; onExit runs on success, failure, or abort.</summary>
        public static BtNode Action<T1>(Func<float, T1, BtStatus> tick, T1 p1, global::System.Action<BtEndReason> onExit = null, string name = null)
            => new ActionNode(elapsedStateTime => tick(elapsedStateTime, p1), onExit, BtNode.ResolveDelegateName(tick, name, "Action"));

        /// <summary>Runs one callback every tick with elapsed state time in seconds; onExit runs on success, failure, or abort.</summary>
        public static BtNode Action<T1, T2>(Func<float, T1, T2, BtStatus> tick, T1 p1, T2 p2, global::System.Action<BtEndReason> onExit = null, string name = null)
            => new ActionNode(elapsedStateTime => tick(elapsedStateTime, p1, p2), onExit, BtNode.ResolveDelegateName(tick, name, "Action"));

        /// <summary>Runs one callback every tick with elapsed state time in seconds; onExit runs on success, failure, or abort.</summary>
        public static BtNode Action<T1, T2, T3>(Func<float, T1, T2, T3, BtStatus> tick, T1 p1, T2 p2, T3 p3, global::System.Action<BtEndReason> onExit = null, string name = null)
            => new ActionNode(elapsedStateTime => tick(elapsedStateTime, p1, p2, p3), onExit, BtNode.ResolveDelegateName(tick, name, "Action"));

        /// <summary>Waits for the configured duration, then succeeds.</summary>
        public static BtNode Wait(float duration, string name = null)
            => new WaitNode(duration, name);
    }

    public static class BtNodeExt
    {
        static BtNode WithVisibilityNode(BtNode child, Func<BtCtx, bool> cond, bool invert, bool interruptSelfOnVisible, bool interruptSelfOnInvisible, Delegate condDelegate, string name, string defaultName)
            => new VisibilityNode(child, cond, invert, interruptSelfOnVisible, interruptSelfOnInvisible, BtNode.ResolveDelegateName(condDelegate, name, defaultName));

        /// <summary>Shows the child when the condition is true; entry gate only, no active self-interruption.</summary>
        public static BtNode When(this BtNode child, Func<bool> cond, string name = null)
            => WithVisibilityNode(child, _ => cond(), false, false, false, cond, name, "When");

        /// <summary>Shows the child when the condition is true; entry gate only, no active self-interruption.</summary>
        public static BtNode When(this BtNode child, Func<BtCtx, bool> cond, string name = null)
            => WithVisibilityNode(child, cond, false, false, false, cond, name, "When");

        /// <summary>Shows the child when the condition is true; entry gate only, no active self-interruption.</summary>
        public static BtNode When<T1>(this BtNode child, Func<T1, bool> cond, T1 p1, string name = null)
            => WithVisibilityNode(child, _ => cond(p1), false, false, false, cond, name, "When");

        /// <summary>Shows the child when the condition is true; entry gate only, no active self-interruption.</summary>
        public static BtNode When<T1, T2>(this BtNode child, Func<T1, T2, bool> cond, T1 p1, T2 p2, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2), false, false, false, cond, name, "When");

        /// <summary>Shows the child when the condition is true; entry gate only, no active self-interruption.</summary>
        public static BtNode When<T1, T2, T3>(this BtNode child, Func<T1, T2, T3, bool> cond, T1 p1, T2 p2, T3 p3, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2, p3), false, false, false, cond, name, "When");

        /// <summary>Shows the child only while the condition is true; aborts self when it turns invisible while active.</summary>
        public static BtNode While(this BtNode child, Func<bool> cond, string name = null)
            => WithVisibilityNode(child, _ => cond(), false, false, true, cond, name, "While");

        /// <summary>Shows the child only while the condition is true; aborts self when it turns invisible while active.</summary>
        public static BtNode While(this BtNode child, Func<BtCtx, bool> cond, string name = null)
            => WithVisibilityNode(child, cond, false, false, true, cond, name, "While");

        /// <summary>Shows the child only while the condition is true; aborts self when it turns invisible while active.</summary>
        public static BtNode While<T1>(this BtNode child, Func<T1, bool> cond, T1 p1, string name = null)
            => WithVisibilityNode(child, _ => cond(p1), false, false, true, cond, name, "While");

        /// <summary>Shows the child only while the condition is true; aborts self when it turns invisible while active.</summary>
        public static BtNode While<T1, T2>(this BtNode child, Func<T1, T2, bool> cond, T1 p1, T2 p2, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2), false, false, true, cond, name, "While");

        /// <summary>Shows the child only while the condition is true; aborts self when it turns invisible while active.</summary>
        public static BtNode While<T1, T2, T3>(this BtNode child, Func<T1, T2, T3, bool> cond, T1 p1, T2 p2, T3 p3, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2, p3), false, false, true, cond, name, "While");

        /// <summary>Shows the child when the condition is true; aborts self and re-enters when condition is true again while active.</summary>
        public static BtNode Whenever(this BtNode child, Func<bool> cond, string name = null)
            => WithVisibilityNode(child, _ => cond(), false, true, false, cond, name, "Whenever");

        /// <summary>Shows the child when the condition is true; aborts self and re-enters when condition is true again while active.</summary>
        public static BtNode Whenever(this BtNode child, Func<BtCtx, bool> cond, string name = null)
            => WithVisibilityNode(child, cond, false, true, false, cond, name, "Whenever");

        /// <summary>Shows the child when the condition is true; aborts self and re-enters when condition is true again while active.</summary>
        public static BtNode Whenever<T1>(this BtNode child, Func<T1, bool> cond, T1 p1, string name = null)
            => WithVisibilityNode(child, _ => cond(p1), false, true, false, cond, name, "Whenever");

        /// <summary>Shows the child when the condition is true; aborts self and re-enters when condition is true again while active.</summary>
        public static BtNode Whenever<T1, T2>(this BtNode child, Func<T1, T2, bool> cond, T1 p1, T2 p2, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2), false, true, false, cond, name, "Whenever");

        /// <summary>Shows the child when the condition is true; aborts self and re-enters when condition is true again while active.</summary>
        public static BtNode Whenever<T1, T2, T3>(this BtNode child, Func<T1, T2, T3, bool> cond, T1 p1, T2 p2, T3 p3, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2, p3), false, true, false, cond, name, "Whenever");

        /// <summary>Equivalent to <see cref="When(BtNode, Func{bool}, string)"/> with negated condition.</summary>
        public static BtNode WhenNot(this BtNode child, Func<bool> cond, string name = null)
            => WithVisibilityNode(child, _ => cond(), true, false, false, cond, name, "WhenNot");

        /// <summary>Equivalent to <see cref="When(BtNode, Func{BtCtx, bool}, string)"/> with negated condition.</summary>
        public static BtNode WhenNot(this BtNode child, Func<BtCtx, bool> cond, string name = null)
            => WithVisibilityNode(child, cond, true, false, false, cond, name, "WhenNot");

        /// <summary>Negated-condition variant of <c>When</c> with one bound parameter.</summary>
        public static BtNode WhenNot<T1>(this BtNode child, Func<T1, bool> cond, T1 p1, string name = null)
            => WithVisibilityNode(child, _ => cond(p1), true, false, false, cond, name, "WhenNot");

        /// <summary>Negated-condition variant of <c>When</c> with two bound parameters.</summary>
        public static BtNode WhenNot<T1, T2>(this BtNode child, Func<T1, T2, bool> cond, T1 p1, T2 p2, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2), true, false, false, cond, name, "WhenNot");

        /// <summary>Negated-condition variant of <c>When</c> with three bound parameters.</summary>
        public static BtNode WhenNot<T1, T2, T3>(this BtNode child, Func<T1, T2, T3, bool> cond, T1 p1, T2 p2, T3 p3, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2, p3), true, false, false, cond, name, "WhenNot");

        /// <summary>Equivalent to <see cref="While(BtNode, Func{bool}, string)"/> with negated condition.</summary>
        public static BtNode WhileNot(this BtNode child, Func<bool> cond, string name = null)
            => WithVisibilityNode(child, _ => cond(), true, false, true, cond, name, "WhileNot");

        /// <summary>Equivalent to <see cref="While(BtNode, Func{BtCtx, bool}, string)"/> with negated condition.</summary>
        public static BtNode WhileNot(this BtNode child, Func<BtCtx, bool> cond, string name = null)
            => WithVisibilityNode(child, cond, true, false, true, cond, name, "WhileNot");

        /// <summary>Negated-condition variant of <c>While</c> with one bound parameter.</summary>
        public static BtNode WhileNot<T1>(this BtNode child, Func<T1, bool> cond, T1 p1, string name = null)
            => WithVisibilityNode(child, _ => cond(p1), true, false, true, cond, name, "WhileNot");

        /// <summary>Negated-condition variant of <c>While</c> with two bound parameters.</summary>
        public static BtNode WhileNot<T1, T2>(this BtNode child, Func<T1, T2, bool> cond, T1 p1, T2 p2, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2), true, false, true, cond, name, "WhileNot");

        /// <summary>Negated-condition variant of <c>While</c> with three bound parameters.</summary>
        public static BtNode WhileNot<T1, T2, T3>(this BtNode child, Func<T1, T2, T3, bool> cond, T1 p1, T2 p2, T3 p3, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2, p3), true, false, true, cond, name, "WhileNot");

        /// <summary>Equivalent to <see cref="Whenever(BtNode, Func{bool}, string)"/> with negated condition.</summary>
        public static BtNode WheneverNot(this BtNode child, Func<bool> cond, string name = null)
            => WithVisibilityNode(child, _ => cond(), true, true, false, cond, name, "WheneverNot");

        /// <summary>Equivalent to <see cref="Whenever(BtNode, Func{BtCtx, bool}, string)"/> with negated condition.</summary>
        public static BtNode WheneverNot(this BtNode child, Func<BtCtx, bool> cond, string name = null)
            => WithVisibilityNode(child, cond, true, true, false, cond, name, "WheneverNot");

        /// <summary>Negated-condition variant of <c>Whenever</c> with one bound parameter.</summary>
        public static BtNode WheneverNot<T1>(this BtNode child, Func<T1, bool> cond, T1 p1, string name = null)
            => WithVisibilityNode(child, _ => cond(p1), true, true, false, cond, name, "WheneverNot");

        /// <summary>Negated-condition variant of <c>Whenever</c> with two bound parameters.</summary>
        public static BtNode WheneverNot<T1, T2>(this BtNode child, Func<T1, T2, bool> cond, T1 p1, T2 p2, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2), true, true, false, cond, name, "WheneverNot");

        /// <summary>Negated-condition variant of <c>Whenever</c> with three bound parameters.</summary>
        public static BtNode WheneverNot<T1, T2, T3>(this BtNode child, Func<T1, T2, T3, bool> cond, T1 p1, T2 p2, T3 p3, string name = null)
            => WithVisibilityNode(child, _ => cond(p1, p2, p3), true, true, false, cond, name, "WheneverNot");

        /// <summary>Re-runs the child until it has succeeded enough times; negative counts repeat forever.</summary>
        public static BtNode Repeat(this BtNode child, int repeat = -1, string name = null)
            => new RepeatNode(child, repeat, name);

        /// <summary>Turns any finished child result into success.</summary>
        public static BtNode ForceSuccess(this BtNode child, string name = null)
            => new ForceSuccessNode(child, name);

        /// <summary>Fails and aborts the running child if it exceeds the timeout duration.</summary>
        public static BtNode Timeout(this BtNode child, float timeout, string name = null)
            => new TimeoutNode(child, timeout, name);

        /// <summary>Only changes the displayed name of the node.</summary>
        public static BtNode Name(this BtNode child, string name)
            => new RenameNode(child, name);
    }
}
