namespace _VuTH.Common.DI
{
    /// <summary>
    /// Optional for <see cref="IBootstrapVContainerConfigurator"/>s: lets <see cref="RootScopeContainer"/> skip
    /// instances that must not register (e.g. a duplicate singleton that is about to be destroyed).
    /// </summary>
    public interface IRootScopeRegistrationFilter
    {
        bool ShouldConfigureRootScope { get; }
    }

    /// <summary>
    /// Optional for <see cref="IBootstrapVContainerConfigurator"/>s: position in the root scope configuration
    /// (ascending, default 0). Registration order matters in VContainer (build callbacks run in that order, and a
    /// single-resolve of a type registered twice returns the last one), so it is made explicit instead of depending on
    /// <c>FindObjectsByType</c> ordering.
    /// </summary>
    public interface IRootScopeConfigurationOrder
    {
        int RootScopeConfigurationOrder { get; }
    }
}
