using System.Collections.Generic;
using System.Text.RegularExpressions;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Identifier;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace _VuTH.Core.GameCycle.Screen.Tests.EditMode
{
    /// <summary>
    /// ScreenModelContainer: misconfigured lists are reported instead of crashing the bootstrap, and the library no
    /// longer requires any particular (Bootstrap / Home / GamePlay) screen.
    /// </summary>
    [TestFixture]
    public class ScreenModelContainerTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created)
            {
                if (o) Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        private ScreenIdentifier Id(string name)
        {
            var id = ScriptableObject.CreateInstance<ScreenIdentifier>();
            id.name = name;
            _created.Add(id);
            return id;
        }

        private ScreenModel Screen(string name, ScreenIdentifier id)
        {
            var s = ScriptableObject.CreateInstance<ScreenModel>();
            s.name = name;
            s.screenId = id;
            _created.Add(s);
            return s;
        }

        [Test]
        public void GameSpecificScreensOnly_SetsUpWithoutAnyErrorOrWarning()
        {
            // Any unexpected Error/Warning log fails this test (no ignoreFailingMessages).
            var shop = Screen("Shop", Id("Shop"));
            var battle = Screen("Battle", Id("Battle"));
            var container = new ScreenModelContainer();

            container.SetupContainer(new[] { shop, battle });

            Assert.AreEqual(2, container.Count);
            Assert.IsNull(container.BaselineScreen, "no baseline configured and no built-in Bootstrap screen");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ExplicitBaseline_IsUsed()
        {
            var splash = Screen("Splash", Id("Splash"));
            var container = new ScreenModelContainer { baselineScreen = splash };

            container.SetupContainer(new[] { splash, Screen("Menu", Id("Menu")) });

            Assert.AreSame(splash, container.BaselineScreen);
        }

        [Test]
        public void BaselineNotInScreenList_IsKept_ButWarns()
        {
            var splash = Screen("Splash", Id("Splash"));
            var container = new ScreenModelContainer { baselineScreen = splash };

            LogAssert.Expect(LogType.Warning, new Regex("không nằm trong danh sách"));
            container.SetupContainer(new[] { Screen("Menu", Id("Menu")) });

            Assert.AreSame(splash, container.BaselineScreen);
        }

        [Test]
        public void NoExplicitBaseline_FallsBackToBuiltInBootstrapId()
        {
            var bootstrapId = DefaultScreenIds.Bootstrap;
            Assume.That(bootstrapId != null, "built-in Bootstrap ScreenIdentifier asset not found in Resources");

            var bootstrap = Screen("MyBootstrap", bootstrapId);
            var container = new ScreenModelContainer();

            container.SetupContainer(new[] { Screen("Menu", Id("Menu")), bootstrap });

            Assert.AreSame(bootstrap, container.BaselineScreen);
        }

        [Test]
        public void NullEntries_AndScreensWithoutId_AreSkipped()
        {
            LogAssert.ignoreFailingMessages = true; // the skipped entries are reported as errors

            var shopId = Id("Shop");
            var shop = Screen("Shop", shopId);
            var container = new ScreenModelContainer();

            Assert.DoesNotThrow(() => container.SetupContainer(new[] { null, Screen("NoId", null), shop }));
            Assert.AreSame(shop, container.GetScreenById(shopId));
            Assert.AreEqual(1, container.Count);
        }

        [Test]
        public void DuplicateIds_KeepTheFirstScreen()
        {
            LogAssert.ignoreFailingMessages = true; // the duplicate is reported as an error

            var id = Id("Shop");
            var first = Screen("ShopA", id);
            var second = Screen("ShopB", id);
            var container = new ScreenModelContainer();

            Assert.DoesNotThrow(() => container.SetupContainer(new[] { first, second }));
            Assert.AreSame(first, container.GetScreenById(id));
        }

        [Test]
        public void NullScreenArray_AndNullLookupId_DoNotThrow()
        {
            LogAssert.ignoreFailingMessages = true; // GetScreenById(null) reports an error

            var container = new ScreenModelContainer();

            Assert.DoesNotThrow(() => container.SetupContainer(null));
            Assert.IsNull(container.GetScreenById(null));
            Assert.IsFalse(container.TryGetScreen(null, out _));
        }
    }
}
