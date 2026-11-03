using _VuTH.Common.Init;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Common.Tests.PlayMode
{
    /// <summary>
    /// Scene component for VInitialize tests. Lives in a runtime test assembly: MonoBehaviours from Editor assemblies
    /// cannot be attached to GameObjects.
    /// </summary>
    public sealed class TestInitializableBehaviour : MonoBehaviour, IVInitializable
    {
        public int InitializeCount;

        public UniTask VInitialize()
        {
            InitializeCount++;
            return UniTask.CompletedTask;
        }
    }
}
