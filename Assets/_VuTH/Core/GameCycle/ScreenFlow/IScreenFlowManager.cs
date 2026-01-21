using Core.GameCycle.Screen;

namespace Core.GameCycle.ScreenFlow
{
    public interface IScreenFlowManager
    {
        ScreenModel GetStartScreen();
        ScreenModel Resolve(ScreenModel current, string eventName);
    }
}