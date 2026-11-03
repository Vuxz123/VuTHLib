using System.Reflection;
using System.Text.RegularExpressions;
using _VuTH.Core.GameCycle.ScreenFlow.Condition;
using _VuTH.Core.GameCycle.ScreenFlow.Condition.Logical;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _VuTH.Core.GameCycle.ScreenFlow.Tests.EditMode
{
    [TestFixture]
    public class ScreenFlowResolverTests
    {
        private GraphBuilder _b;

        [SetUp] public void SetUp() => _b = new GraphBuilder();
        [TearDown] public void TearDown() => _b.Dispose();

        [Test]
        public void GetStartNode_ReturnsNodeMarkedAsStart()
        {
            _b.Node("a", _b.Screen("A"));
            var start = _b.Node("b", _b.Screen("B"), start: true);

            Assert.AreSame(start, new ScreenFlowGraphResolver(_b.Graph).GetStartNode());
        }

        [Test]
        public void TryResolve_NullCondition_IsAlwaysTrue()
        {
            var a = _b.Node("a", _b.Screen("A"), start: true);
            var b = _b.Node("b", _b.Screen("B"));
            _b.Edge("a", "b", "go");

            Assert.IsTrue(new ScreenFlowGraphResolver(_b.Graph).TryResolve(a, "go", out var next));
            Assert.AreSame(b, next);
        }

        [Test]
        public void TryResolve_UnknownEvent_ReturnsFalse()
        {
            var a = _b.Node("a", _b.Screen("A"), start: true);
            _b.Node("b", _b.Screen("B"));
            _b.Edge("a", "b", "go");

            Assert.IsFalse(new ScreenFlowGraphResolver(_b.Graph).TryResolve(a, "other", out var next));
            Assert.IsNull(next);
        }

        [Test]
        public void TryResolve_SkipsFalseCondition_AndTakesFirstTrueInGraphOrder()
        {
            var a = _b.Node("a", _b.Screen("A"), start: true);
            _b.Node("b", _b.Screen("B"));
            var c = _b.Node("c", _b.Screen("C"));
            _b.Node("d", _b.Screen("D"));
            _b.Edge("a", "b", "go", _b.Condition(false));
            _b.Edge("a", "c", "go", _b.Condition(true));
            _b.Edge("a", "d", "go", _b.Condition(true));

            LogAssert.Expect(LogType.Warning, new Regex("Multiple conditions evaluated to true"));
            Assert.IsTrue(new ScreenFlowGraphResolver(_b.Graph).TryResolve(a, "go", out var next));
            Assert.AreSame(c, next);
        }

        [Test]
        public void TryResolve_ThrowingCondition_CountsAsFalse()
        {
            var a = _b.Node("a", _b.Screen("A"), start: true);
            _b.Node("b", _b.Screen("B"));
            _b.Edge("a", "b", "go", _b.Condition(true, throws: true));

            LogAssert.Expect(LogType.Warning, new Regex("threw exception"));
            Assert.IsFalse(new ScreenFlowGraphResolver(_b.Graph).TryResolve(a, "go", out _));
        }

        [Test]
        public void TryResolve_TargetNodeMissing_ReturnsFalse()
        {
            var a = _b.Node("a", _b.Screen("A"), start: true);
            _b.Edge("a", "ghost", "go");

            Assert.IsFalse(new ScreenFlowGraphResolver(_b.Graph).TryResolve(a, "go", out _));
        }

        // ---- logical conditions ----

        [Test]
        public void And_Empty_IsTrue_AndRequiresAllChildren()
        {
            var and = _b.Track(ScriptableObject.CreateInstance<AndCondition>());
            Assert.IsTrue(and.Evaluate());

            SetChildren(and, new System.Collections.Generic.List<TransitionCondition> { _b.Condition(true), _b.Condition(false) });
            Assert.IsFalse(and.Evaluate());
        }

        [Test]
        public void Or_Empty_IsFalse_AndNeedsOneChild()
        {
            var or = _b.Track(ScriptableObject.CreateInstance<OrCondition>());
            Assert.IsFalse(or.Evaluate());

            SetChildren(or, new TransitionCondition[] { _b.Condition(false), _b.Condition(true) });
            Assert.IsTrue(or.Evaluate());
        }

        [Test]
        public void NullChildren_AreLoggedAndCountAsFalse_InsteadOfThrowing()
        {
            var and = _b.Track(ScriptableObject.CreateInstance<AndCondition>());
            SetChildren(and, new System.Collections.Generic.List<TransitionCondition> { null });
            var not = _b.Track(ScriptableObject.CreateInstance<NotCondition>());

            LogAssert.Expect(LogType.Error, new Regex("empty \\(null\\) child"));
            Assert.IsFalse(and.Evaluate());

            LogAssert.Expect(LogType.Error, new Regex("no child condition"));
            Assert.IsFalse(not.Evaluate(), "a broken Not must not open a transition");
        }

        private static void SetChildren(TransitionCondition owner, object value)
        {
            owner.GetType().GetField("conditions", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
        }
    }
}
