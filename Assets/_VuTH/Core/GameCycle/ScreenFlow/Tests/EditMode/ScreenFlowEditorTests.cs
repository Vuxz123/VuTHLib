using System.Linq;
using _VuTH.Core.GameCycle.ScreenFlow.Editor.Graph;
using _VuTH.Core.GameCycle.ScreenFlow.Editor.Validator;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace _VuTH.Core.GameCycle.ScreenFlow.Tests.EditMode
{
    [TestFixture]
    public class ScreenFlowEditorTests
    {
        private GraphBuilder _b;

        [SetUp]
        public void SetUp()
        {
            _b = new GraphBuilder();
            _b.Node("home", _b.Screen("Home"), start: true);
            _b.Node("shop", _b.Screen("Shop"));
            _b.Node("game", _b.Screen("Game"));
        }

        [TearDown]
        public void TearDown() => _b.Dispose();

        private string[] Warnings() => ScreenFlowValidator.Validate(_b.Graph)
            .Where(i => i.Severity == ScreenFlowValidationSeverity.Warning)
            .Select(i => i.Message).ToArray();

        // ---- validator ----

        [Test]
        public void TerminalNode_IsNotReported_UnreachableNodeIs_ByName()
        {
            _b.Edge("home", "game", "play"); // Game has no outgoing transition: a valid end screen

            var warnings = Warnings();

            Assert.IsFalse(warnings.Any(w => w.Contains("'Game'")), string.Join("\n", warnings));
            Assert.IsTrue(warnings.Any(w => w.Contains("'Shop' cannot be reached")), string.Join("\n", warnings));
        }

        [Test]
        public void Pop_FromNodeNeverPushed_IsReported()
        {
            _b.Edge("home", "shop", "shop");                                         // Enter, not Push
            _b.Edge("shop", "home", "back", navigation: ScreenFlowNavigation.Pop);

            Assert.IsTrue(Warnings().Any(w => w.Contains("uses Pop") && w.Contains("never entered with Push")));
        }

        [Test]
        public void Pop_FromPushedNode_IsFine()
        {
            _b.Edge("home", "shop", "shop", navigation: ScreenFlowNavigation.Push);
            _b.Edge("shop", "home", "back", navigation: ScreenFlowNavigation.Pop);
            _b.Edge("home", "game", "play");

            Assert.IsFalse(Warnings().Any(w => w.Contains("uses Pop")), string.Join("\n", Warnings()));
        }

        [Test]
        public void EventNameIssues_AreReported()
        {
            _b.Edge("home", "shop", "open shop");
            _b.Edge("home", "game", "open_shop");
            _b.Edge("shop", "home", " back");

            var warnings = Warnings();

            Assert.IsTrue(warnings.Any(w => w.Contains("map to ScreenFlowEvents.OpenShop")), string.Join("\n", warnings));
            Assert.IsTrue(warnings.Any(w => w.Contains("leading/trailing spaces")), string.Join("\n", warnings));
        }

        // ---- transition inspector proxy ----

        [Test]
        public void TransitionProxy_EditsItsOwnTransition_EvenAfterOthersAreDeleted()
        {
            _b.Edge("home", "shop", "a");
            _b.Edge("home", "game", "b");
            var target = _b.Graph.Transitions[1];
            var proxy = _b.Track(ScreenTransitionSelectionProxy.Create(_b.Graph, target));

            // Delete the first transition: an index-based proxy would now point past the end / at the wrong item.
            using (var so = new SerializedObject(_b.Graph))
            {
                so.FindProperty("transitions").DeleteArrayElementAtIndex(0);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            using (var proxySo = new SerializedObject(proxy))
            {
                proxySo.FindProperty("eventName").stringValue = "renamed";
                proxySo.FindProperty("navigation").intValue = (int)ScreenFlowNavigation.Push;
                proxySo.ApplyModifiedPropertiesWithoutUndo();
            }
            proxy.ApplyToGraph();

            Assert.AreEqual(1, _b.Graph.Transitions.Count);
            Assert.AreEqual("renamed", _b.Graph.Transitions[0].EventName);
            Assert.AreEqual(ScreenFlowNavigation.Push, _b.Graph.Transitions[0].Navigation);
            Assert.IsTrue(proxy.IsValid);
        }

        [Test]
        public void TransitionProxy_BecomesInvalid_WhenItsTransitionIsDeleted()
        {
            _b.Edge("home", "shop", "a");
            var proxy = _b.Track(ScreenTransitionSelectionProxy.Create(_b.Graph, _b.Graph.Transitions[0]));

            using (var so = new SerializedObject(_b.Graph))
            {
                so.FindProperty("transitions").DeleteArrayElementAtIndex(0);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Assert.IsFalse(proxy.IsValid);
        }
    }
}
