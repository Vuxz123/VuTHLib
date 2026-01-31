namespace _VuTH.Core.GameCycle.Screen.Events.Global
{
    public interface IGlobalScreenEventRegistration
    {
        void RegisterListener(IScreenEventListener listener);
        void UnregisterListener(IScreenEventListener listener);
    }
}