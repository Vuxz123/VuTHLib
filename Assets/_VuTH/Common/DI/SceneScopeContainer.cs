using _VuTH.Common.Log;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _VuTH.Common.DI
{
    /// <summary>
    /// LifetimeScope of a screen scene. Its parent is the Core <see cref="RootScopeContainer"/>, whether the scene was
    /// loaded by ScreenManager (which enqueues the root as parent) or any other way (SceneManager.LoadScene, opened in
    /// the editor next to Core...). Applies the configurators referenced in <see cref="configurators"/>.
    /// </summary>
    public class SceneScopeContainer : LifetimeScope
    {
        [Header("Configurators")]
        [Tooltip("MonoBehaviours implementing IVContainerConfigurator (same scene).")]
        // Plain object references: [SerializeReference] does not apply to UnityEngine.Object fields (the data was
        // already stored as normal references, so switching keeps existing scenes intact).
        [SerializeField] private MonoBehaviour[] configurators = System.Array.Empty<MonoBehaviour>();

        /// <summary>
        /// Default parent = the built root scope. Only used when no explicit <c>parentReference</c> object is set;
        /// ScreenManager enqueues this same root, so both paths agree.
        /// </summary>
        protected override LifetimeScope FindParent()
        {
            var root = Find<RootScopeContainer>();
            return root != null && root != this && root.Container != null ? root : null;
        }

        protected override void Configure(IContainerBuilder builder)
        {
            if (Parent)
            {
                this.Log($"{gameObject.name} linked to Parent: {Parent.name}");
            }
            else
            {
                this.LogWarning($"{gameObject.name} has NO parent scope: the Core scene (RootScopeContainer) is not loaded. " +
                                "Core services will not be injectable in this scene.");
            }

            base.Configure(builder);

            // Module installers (e.g. MessagePipe scene-scoped brokers), then the configurators.
            ScopeInstallers.InstallScene(builder);

            if (configurators == null) return;

            foreach (var configurator in configurators)
            {
                if (configurator is IVContainerConfigurator vContainerConfigurator)
                {
                    vContainerConfigurator.Configure(builder);
                }
                else if (configurator != null)
                {
                    this.LogWarning($"{configurator.GetType().Name} on '{configurator.name}' does not implement " +
                                    $"{nameof(IVContainerConfigurator)} and was ignored.");
                }
            }
        }
    }
}
