using System;
using System.Collections.Generic;
using System.Reflection;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Events.Local;
using _VuTH.Core.GameCycle.Screen.Identifier;
using _VuTH.Core.GameCycle.Screen.Transition;
using _VuTH.Core.GameCycle.ScreenFlow.Condition;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _VuTH.Core.GameCycle.ScreenFlow.Tests.EditMode
{
    /// <summary>Builds ScreenFlowGraph / ScreenModel assets in memory and destroys them afterwards.</summary>
    internal sealed class GraphBuilder : IDisposable
    {
        private readonly List<Object> _created = new();

        public ScreenFlowGraph Graph { get; }

        public GraphBuilder()
        {
            Graph = Track(ScriptableObject.CreateInstance<ScreenFlowGraph>());
        }

        public T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        public ScreenModel Screen(string name)
        {
            var screen = Track(ScriptableObject.CreateInstance<ScreenModel>());
            screen.name = name;
            return screen;
        }

        public ScreenFlowNode Node(string guid, ScreenModel screen, bool start = false)
        {
            var node = new ScreenFlowNode(guid, screen, Vector2.zero);
            List<ScreenFlowNode>("nodes").Add(node);
            if (start) Graph.SetStartNode(guid);
            return node;
        }

        public void Edge(string from, string to, string eventName, TransitionCondition condition = null,
            ScreenFlowNavigation navigation = ScreenFlowNavigation.Enter,
            ScreenBelowVisibility screenBelow = ScreenBelowVisibility.ScreenDefault)
        {
            List<ScreenFlowTransition>("transitions")
                .Add(new ScreenFlowTransition(from, to, eventName, condition, navigation, screenBelow));
        }

        public TestCondition Condition(bool result, bool throws = false)
        {
            var c = Track(ScriptableObject.CreateInstance<TestCondition>());
            c.Result = result;
            c.Throws = throws;
            return c;
        }

        private List<T> List<T>(string field)
        {
            return (List<T>)typeof(ScreenFlowGraph)
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(Graph);
        }

        public void Dispose()
        {
            foreach (var o in _created)
            {
                if (o) Object.DestroyImmediate(o);
            }
        }
    }

    internal sealed class TestCondition : TransitionCondition
    {
        public bool Result;
        public bool Throws;
        public int EvaluateCount;

        public override bool Evaluate()
        {
            EvaluateCount++;
            if (Throws) throw new InvalidOperationException("boom");
            return Result;
        }
    }

    /// <summary>
    /// Minimal IScreenManager: records Enter calls. With <see cref="HoldTransitions"/> each Enter stays in flight
    /// until <see cref="CompleteTransition"/> / <see cref="FailTransition"/>.
    /// </summary>
    internal sealed class FakeScreenManager : IScreenManager
    {
        public readonly List<ScreenModel> Entered = new();

        /// <summary>Every navigation call, e.g. "Enter:Shop", "Push:Shop:hide=True", "Pop", "PopOverride".</summary>
        public readonly List<string> Ops = new();
        public bool HoldTransitions;
        public Exception FailNextWith;

        private UniTaskCompletionSource _pending;
        private ScreenModel _pendingTarget;
        private TransitionContext _pendingContext;

        public bool IsTransitioning => _pending != null;
        public ScreenModel Current { get; private set; }
        public ScreenModel Previous { get; private set; }
        public event Action<TransitionCompletedEventArgs> OnTransitionCompleted;

        public UniTask Enter(ScreenModel screen, TransitionContext context)
        {
            Entered.Add(screen);
            Ops.Add("Enter:" + (screen ? screen.name : "null"));

            if (FailNextWith != null)
            {
                var e = FailNextWith;
                FailNextWith = null;
                return UniTask.FromException(e);
            }

            if (!HoldTransitions)
            {
                Finish(screen, context);
                return UniTask.CompletedTask;
            }

            _pending = new UniTaskCompletionSource();
            _pendingTarget = screen;
            _pendingContext = context;
            return _pending.Task;
        }

        public void CompleteTransition()
        {
            var source = _pending;
            _pending = null;
            Finish(_pendingTarget, _pendingContext); // completion event first, like ScreenManager
            source.TrySetResult();
        }

        public void FailTransition(Exception e)
        {
            var source = _pending;
            _pending = null;
            source.TrySetException(e);
        }

        private void Finish(ScreenModel screen, TransitionContext context)
        {
            Previous = Current;
            Current = screen;
            OnTransitionCompleted?.Invoke(new TransitionCompletedEventArgs(TransitionKind.Enter, Previous, screen, context));
        }

        public bool HasSubscribers => OnTransitionCompleted != null;

        // ---- unused members ----
        public ILocalScreenEventRegistration LocalEventRegistration => null;
        public bool CanPop { get; set; }
        public bool IsOverriding { get; set; }
        public bool IsEnabledSystem => true;
        public void EnableSystem(bool enable) { }
        public void ToggleSystem() { }
        public UniTask Enter(ScreenModel screen) => Enter(screen, TransitionContext.Default);
        public UniTask Enter(ScreenIdentifier screenID) => UniTask.CompletedTask;
        public UniTask Enter(ScreenIdentifier screenID, TransitionContext context) => UniTask.CompletedTask;
        public UniTask Push(ScreenModel screen) => UniTask.CompletedTask;
        public UniTask Push(ScreenModel screen, TransitionContext context) => Push(screen, screen.hideScreenBelow, context);
        public UniTask Push(ScreenModel screen, bool hideScreenBelow, TransitionContext context) => Record($"Push:{screen.name}:hide={hideScreenBelow}");
        public UniTask Pop() => Pop(TransitionContext.Default);
        public UniTask Pop(TransitionContext context) => Record("Pop");
        public UniTask PushOverride(ScreenModel screen) => PushOverride(screen, TransitionContext.Default);
        public UniTask PushOverride(ScreenModel screen, TransitionContext context) => Record("PushOverride:" + screen.name);
        public UniTask PopOverride() => PopOverride(TransitionContext.Default);
        public UniTask PopOverride(TransitionContext context) => Record("PopOverride");

        private UniTask Record(string op)
        {
            Ops.Add(op);
            return UniTask.CompletedTask;
        }
    }
}
