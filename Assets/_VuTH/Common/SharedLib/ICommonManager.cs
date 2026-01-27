namespace _VuTH.Common
{
    public interface ICommonManager
    {
        bool IsEnabledSystem { get; }
        void EnableSystem(bool enable);
        void ToggleSystem();
    }
}