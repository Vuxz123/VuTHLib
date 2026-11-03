using System.Collections.Generic;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Identifier;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _VuTH.Core.GameCycle.Screen.Tests.EditMode
{
    /// <summary>Which screens a scene belongs to (drives EventScope.Screen MessagePipe brokers).</summary>
    public class ScreenSceneMembershipTests
    {
        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            ScreenSceneMembership.Loading = null;
            foreach (var o in _created)
            {
                if (o) Object.DestroyImmediate(o);
            }
            _created.Clear();
        }

        private ScreenModel Screen(string id)
        {
            var identifier = ScriptableObject.CreateInstance<ScreenIdentifier>();
            identifier.name = id;
            var model = ScriptableObject.CreateInstance<ScreenModel>();
            model.screenId = identifier;
            _created.Add(identifier);
            _created.Add(model);
            return model;
        }

        [Test]
        public void ScreenBeingLoaded_OwnsTheScene()
        {
            ScreenSceneMembership.Loading = Screen("Arena");

            CollectionAssert.Contains(ScreenSceneMembership.GetScreenIds(default), "Arena");
        }

        [Test]
        public void ScreenMetaDataOnARootObject_OwnsTheScene()
        {
            // ScreenMetaData.OnValidate may complain about the test GameObject's position in the scene.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            var go = new GameObject("ScreenMetaData");
            _created.Add(go);
            go.AddComponent<ScreenMetaData>().model = Screen("Home");

            var ids = ScreenSceneMembership.GetScreenIds(SceneManager.GetActiveScene());

            CollectionAssert.Contains(ids, "Home");
        }

        [Test]
        public void UnknownScene_HasNoScreens()
        {
            CollectionAssert.IsEmpty(ScreenSceneMembership.GetScreenIds(default));
        }
    }
}
