using NUnit.Framework;

namespace _VuTH.Common.Tests.EditMode
{
    [TestFixture]
    public class LegacyTypeResolverTests
    {
        [TestCase(
            "_VuTH.Core.Persistant.SaveSystem.Encrypt.AesEncryptor, VuTH.Persistant.SaveSystem, Version=0.0.0.0",
            "_VuTH.Core.Persistent.SaveSystem.Encrypt.AesEncryptor, VuTH.Persistent.SaveSystem, Version=0.0.0.0")]
        [TestCase(
            "_VuTH.Core.Persistant.SaveSystem.SaveAdapterTypeScanner, VuTH..Persistant.SaveSystem.Editor",
            "_VuTH.Core.Persistent.SaveSystem.SaveAdapterTypeScanner, VuTH.Persistent.SaveSystem.Editor")]
        [TestCase(
            "_VuTH.Core.Booststrap.BoostrapManagerCentral, VuTH.Bootstrap",
            "_VuTH.Core.Bootstrap.BootstrapManagerCentral, VuTH.Bootstrap")]
        [TestCase(
            "_VuTH.Core.GameCycle.Screen.Core.A.ScreenModelContainer, VuTH.Screen",
            "_VuTH.Core.GameCycle.Screen.Core.ScreenModelContainer, VuTH.Screen")]
        [TestCase(
            "VuTH.Common.MessagePipe.MessagePipeHelper, VuTH.Common.MessagePipe",
            "_VuTH.Common.MessagePipe.MessagePipeHelper, VuTH.Common.MessagePipe")]
        public void Migrate_RewritesOldNames(string oldName, string expected)
        {
            Assert.AreEqual(expected, LegacyTypeResolver.Migrate(oldName));
        }

        [TestCase("_VuTH.Core.Persistent.SaveSystem.Encrypt.AesEncryptor, VuTH.Persistent.SaveSystem")]
        [TestCase("_VuTH.Common.MessagePipe.Core.EventScopeLookup, VuTH.Common.MessagePipe")]
        [TestCase("System.String")]
        public void Migrate_LeavesCurrentNamesUntouched(string name)
        {
            Assert.AreEqual(name, LegacyTypeResolver.Migrate(name));
        }

        [Test]
        public void Resolve_CurrentAssemblyQualifiedName_ReturnsType()
        {
            var type = typeof(UnityNullExtensions);
            Assert.AreSame(type, LegacyTypeResolver.Resolve(type.AssemblyQualifiedName));
        }

        [Test]
        public void Resolve_PreRenameName_FindsRenamedType()
        {
            // No compile-time reference to the SaveSystem assembly is needed: resolution is by name.
            var type = LegacyTypeResolver.Resolve(
                "_VuTH.Core.Persistant.SaveSystem.Encrypt.NoOpEncryptor, VuTH.Persistant.SaveSystem");

            Assert.IsNotNull(type);
            Assert.AreEqual("_VuTH.Core.Persistent.SaveSystem.Encrypt.NoOpEncryptor", type.FullName);
            Assert.AreEqual("VuTH.Persistent.SaveSystem", type.Assembly.GetName().Name);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Does.Not.Exist.Anywhere, Nope")]
        public void Resolve_Unknown_ReturnsNull(string name)
        {
            Assert.IsNull(LegacyTypeResolver.Resolve(name));
        }
    }
}
