using _VuTH.Common;
using _VuTH.Common.Log;
using UnityEngine;
using VContainer;

namespace TScript
{
    public class TestBoostraper : VBootstrapManager<TestBoostraper, ICommonManager> , ICommonManager
    {
        [SerializeField] private int testInt;
        
        protected override void InitializeBootstrap()
        {
            this.Log($"TestBootstrapper initialized with testInt: {testInt}");
        }

        protected override void DeinitializeBootstrap()
        {
            this.Log("TestBootstrapper deinitialized");
        }

#if VCONTAINER
        public override void ConfigureRootScope(IContainerBuilder builder)
        {
            builder.RegisterInstance(this).Keyed(testInt);
        }
#endif
    }
}