using _VuTH.Common.Log;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _VuTH.Common.DI
{
    public class SceneScopeContainer : LifetimeScope
    {
        [Header("Configurators")]
        [SerializeReference] private MonoBehaviour[] configurators;

        protected override void Configure(IContainerBuilder builder)
        {
            // Kiểm tra xem Parent đã được gán chưa
            if (Parent)
            {
                this.Log($"{gameObject.name} linked to Parent: {Parent.name}");
            }
            else
            {
                this.LogWarning($"{gameObject.name} has NO Parent! Check ScreenManager EnqueueParent logic.");
            }
            
            base.Configure(builder);
            
            foreach (var configurator in configurators)
            {
                if (configurator is IVContainerConfigurator vContainerConfigurator)
                {
                    vContainerConfigurator.Configure(builder);
                }
            }
        }
    }
}