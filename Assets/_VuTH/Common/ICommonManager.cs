namespace _VuTH.Common
{
    /// <summary>Common contract of every VuTH manager.</summary>
    public interface ICommonManager
    {
        /// <summary>True between the manager's initialization and its deinitialization (or destruction).</summary>
        bool IsInitialized { get; }
    }

    /// <summary>
    /// A manager that can be switched on and off at runtime (<see cref="VManager{T,TI}"/>). Bootstrap managers are
    /// always on and do not implement it.
    /// </summary>
    public interface IToggleableManager : ICommonManager
    {
        bool IsEnabledSystem { get; }

        /// <summary>Enabling initializes the manager (if needed); disabling deinitializes it.</summary>
        void EnableSystem(bool enable);

        void ToggleSystem();
    }
}
