namespace Common.SharedLib
{
    public interface ICommonManager
    {
        bool IsEnabledSystem { get; }
        void EnableSystem(bool enable);
        void ToggleSystem();
    }
}