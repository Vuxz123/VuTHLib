using System.Collections.Generic;
using _VuTH.Core.GameCycle.ScreenFlow.Editor.Codegen;
using NUnit.Framework;

namespace _VuTH.Core.GameCycle.ScreenFlow.Tests.EditMode
{
    [TestFixture]
    public class ScreenFlowEventsGeneratorTests
    {
        [TestCase("BootstrapCompleted", "BootstrapCompleted")]
        [TestCase("bootstrap completed", "BootstrapCompleted")]
        [TestCase("open_shop-now.v2", "OpenShopNowV2")]
        [TestCase("2player", "_2player")]
        [TestCase("!!!", "_")]
        public void ToIdentifier_ProducesValidPascalCaseIdentifier(string eventName, string expected)
        {
            Assert.AreEqual(expected, ScreenFlowEventsGenerator.ToIdentifier(eventName));
        }

        [Test]
        public void CollectEvents_IsDistinctSorted_AndDescribesUsages()
        {
            using var b = new GraphBuilder();
            b.Node("home", b.Screen("Home"), start: true);
            b.Node("shop", b.Screen("Shop"));
            b.Edge("home", "shop", "shop", navigation: ScreenFlowNavigation.Push);
            b.Edge("shop", "home", "back");
            b.Edge("home", "home", "back");
            b.Edge("home", "shop", "  ");

            var events = ScreenFlowEventsGenerator.CollectEvents(b.Graph);

            CollectionAssert.AreEqual(new[] { "back", "shop" }, events.Keys);
            CollectionAssert.AreEqual(new[] { "Shop → Home", "Home → Home" }, events["back"]);
            CollectionAssert.AreEqual(new[] { "Home → Shop (Push)" }, events["shop"]);
        }

        [Test]
        public void BuildSource_EmitsConstants_WithUniqueIdentifiers()
        {
            var events = new SortedDictionary<string, List<string>>(System.StringComparer.Ordinal)
            {
                ["open shop"] = new() { "Home → Shop" },
                ["open_shop"] = new() { "Menu → Shop" }, // same identifier → suffixed
                ["all"] = new(),                          // would clash with the generated 'All' array
            };

            var source = ScreenFlowEventsGenerator.BuildSource(events);

            StringAssert.Contains("namespace Core.Generated", source);
            StringAssert.Contains("public const string OpenShop = \"open shop\";", source);
            StringAssert.Contains("public const string OpenShop2 = \"open_shop\";", source);
            StringAssert.Contains("public const string All2 = \"all\";", source);
            StringAssert.Contains("public static readonly string[] All = { \"all\", \"open shop\", \"open_shop\" };", source);
        }
    }
}
