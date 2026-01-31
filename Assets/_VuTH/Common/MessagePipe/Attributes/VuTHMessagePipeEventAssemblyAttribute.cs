using System;

namespace _VuTH.Common.MessagePipe.Attributes
{
    /// <summary>
    /// Assembly-level attribute to mark assemblies that contain MessagePipe events.
    /// Use this attribute instead of modifying the whitelist asset for plugin assemblies.
    ///
    /// Usage: Add this line to any file in your assembly (e.g., AssemblyInfo.cs):
    /// [assembly: VuTHMessagePipeEventAssembly]
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    // ReSharper disable once InconsistentNaming
    public sealed class VuTHMessagePipeEventAssemblyAttribute : Attribute
    {
    }
}
