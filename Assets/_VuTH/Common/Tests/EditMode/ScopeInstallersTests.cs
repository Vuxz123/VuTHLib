using System.Collections.Generic;
using System.Linq;
using _VuTH.Common.DI;
using NUnit.Framework;
using VContainer;

namespace _VuTH.Common.Tests.EditMode
{
    public class ScopeInstallersTests
    {
        private const string KeyA = "Test.A";
        private const string KeyB = "Test.B";

        [TearDown]
        public void TearDown()
        {
            ScopeInstallers.UnregisterRoot(KeyA);
            ScopeInstallers.UnregisterRoot(KeyB);
            ScopeInstallers.UnregisterScene(KeyA);
        }

        [Test]
        public void InstallRoot_RunsInstallersInKeyOrder()
        {
            var calls = new List<string>();
            ScopeInstallers.RegisterRoot(KeyB, _ => calls.Add(KeyB));
            ScopeInstallers.RegisterRoot(KeyA, _ => calls.Add(KeyA));

            ScopeInstallers.InstallRoot(new ContainerBuilder());

            var ours = calls.Where(c => c == KeyA || c == KeyB).ToList();
            CollectionAssert.AreEqual(new[] { KeyA, KeyB }, ours);
        }

        [Test]
        public void RegisteringTheSameKeyTwice_ReplacesInsteadOfDuplicating()
        {
            var first = 0;
            var second = 0;
            ScopeInstallers.RegisterScene(KeyA, (_, _) => first++);
            ScopeInstallers.RegisterScene(KeyA, (_, _) => second++); // e.g. Enter Play Mode without domain reload

            ScopeInstallers.InstallScene(new ContainerBuilder(), default);

            Assert.AreEqual(0, first, "the replaced installer must not run");
            Assert.AreEqual(1, second, "the latest installer runs exactly once");
        }

        [Test]
        public void EmptyKeyOrNullInstaller_Throw()
        {
            Assert.Throws<System.ArgumentException>(() => ScopeInstallers.RegisterRoot("", _ => { }));
            Assert.Throws<System.ArgumentNullException>(() => ScopeInstallers.RegisterRoot(KeyA, null));
        }

        [Test]
        public void CommonDI_NoLongerDependsOnCommonMessagePipe()
        {
            var references = typeof(ScopeInstallers).Assembly.GetReferencedAssemblies().Select(a => a.Name);

            CollectionAssert.DoesNotContain(references, "VuTH.Common.MessagePipe",
                "DI must stay independent; MessagePipe plugs itself in through ScopeInstallers");
        }
    }
}
