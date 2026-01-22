using Common;
using Common.Log;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TScript
{
    public class TestBoostraperB : VBootstrapManager<TestBoostraperB, ICommonManager> , ICommonManager
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