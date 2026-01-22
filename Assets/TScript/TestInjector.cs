#if VCONTAINER
using Common.DI;
using Core.GameCycle.Camera;
using Core.GameCycle.ScreenFlow;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TScript
{
    public class TestInjector : MonoBehaviour, IVContainerConfigurator
    {
        private IScreenFlowManager _screenFlowManager;
        private ICameraManager _cameraManager;

        [Inject]
        public void Construct(IScreenFlowManager screenFlowManager, ICameraManager cameraManager)
        {
            _screenFlowManager = screenFlowManager;
            _cameraManager = cameraManager;
            
            Debug.Log("Injected successfully!");
        }
        
        public void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponent(this);
        }
    }
}
#endif