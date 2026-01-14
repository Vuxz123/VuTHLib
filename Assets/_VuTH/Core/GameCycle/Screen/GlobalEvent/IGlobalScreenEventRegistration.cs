namespace Core.GameCycle.Screen.GlobalEvent
{
    public interface IGlobalScreenEventRegistration
    {
        void RegisterListener(IScreenEventListener listener);
        void UnregisterListener(IScreenEventListener listener);
    }
}