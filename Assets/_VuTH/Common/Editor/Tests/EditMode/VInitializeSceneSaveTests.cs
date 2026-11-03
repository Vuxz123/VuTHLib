
using _VuTH.Common.Init;
using _VuTH.Common.Tests.PlayMode;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace _VuTH.Common.Editor.Tests.EditMode
{
    /// <summary>
    /// Saving a scene must collect its IVInitializable components on a VInitializeInvokeSite — in that same save — and
    /// the list must survive a reload. (It used to be stored with [SerializeReference], which cannot hold components, so
    /// it was lost on every save; and it ran after the save, so the scene stayed dirty.)
    /// </summary>
    public class VInitializeSceneSaveTests
    {
        private const string ScenePath = "Assets/__VInitializeSceneSaveTest.unity";
        private UnityEngine.SceneManagement.Scene _scene;

        [SetUp]
        public void SetUp()
        {
            // Single: the test runner keeps an unsaved untitled scene, which forbids adding scenes additively.
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            // The last loaded scene cannot be closed: replace it with a fresh empty one, then delete the test asset.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(ScenePath);
        }

        private static VInitializeInvokeSite FindSite(UnityEngine.SceneManagement.Scene scene)
        {
            scene.TryGetVInitializeInvokeSite(out var site);
            return site;
        }

        [Test]
        public void FirstSave_CollectsComponents_SavedInThatSave_AndSurvivesReload()
        {
            var go = new GameObject("Initializable");
            EditorSceneManager.MoveGameObjectToScene(go, _scene);
            var component = go.AddComponent<TestInitializableBehaviour>();

            Assert.IsTrue(EditorSceneManager.SaveScene(_scene, ScenePath));

            var site = FindSite(_scene);
            Assert.IsNotNull(site, "invoke site created on the first save of a new scene");
            CollectionAssert.AreEqual(new[] { component }, site.SceneInitializables);
            Assert.IsFalse(_scene.isDirty, "the collected list must be part of this save, not left for the next one");

            var reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _scene = reopened;

            var reloadedSite = FindSite(reopened);
            Assert.IsNotNull(reloadedSite);
            Assert.AreEqual(1, reloadedSite.SceneInitializables.Count);
            Assert.IsInstanceOf<TestInitializableBehaviour>(reloadedSite.SceneInitializables[0], "not lost on reload");
        }

        [Test]
        public void SavingAgainWithoutChanges_DoesNotDirtyTheScene()
        {
            var go = new GameObject("Initializable");
            EditorSceneManager.MoveGameObjectToScene(go, _scene);
            go.AddComponent<TestInitializableBehaviour>();
            EditorSceneManager.SaveScene(_scene, ScenePath);

            EditorSceneManager.SaveScene(_scene, ScenePath);

            Assert.IsFalse(_scene.isDirty);
        }

        [Test]
        public void AssignInitializables_ReportsChangesOnly_AndIgnoresNonComponents()
        {
            var go = new GameObject("Site");
            EditorSceneManager.MoveGameObjectToScene(go, _scene);
            var site = go.AddComponent<VInitializeInvokeSite>();
            var a = go.AddComponent<TestInitializableBehaviour>();

            Assert.IsTrue(site.AssignInitializables(new IVInitializable[] { a, null, a }));
            Assert.IsFalse(site.AssignInitializables(new IVInitializable[] { a }), "same list: no change");
            CollectionAssert.AreEqual(new[] { a }, site.SceneInitializables, "nulls and duplicates dropped");
        }
    }
}
