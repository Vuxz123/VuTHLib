using Common.SharedLib;

namespace Core.GameCycle.ScreenFlow
{
    public class ScreenFlowFlag : VBoostrapManager<ScreenFlowFlag, IScreenFlowLag> , IScreenFlowLag
    {
        protected override void InitializeBootstrap()
        {
        }

        protected override void DeinitializeBootstrap()
        {
        }
    }

    public interface IScreenFlowLag : ICommonManager
    {
        
    }
}