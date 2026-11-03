using System;

namespace _VuTH.Common.MessagePipe.Attributes
{
    /// <summary>
    /// No longer needed: events are discovered at runtime in every assembly that references VuTH.Common.MessagePipe.
    /// Kept so existing <c>[assembly: VuTHMessagePipeEventAssembly]</c> lines still compile.
    /// </summary>
    [Obsolete("Not needed anymore: [MessagePipeEvent] types are discovered automatically. Remove this attribute.")]
    [AttributeUsage(AttributeTargets.Assembly)]
    // ReSharper disable once InconsistentNaming
    public sealed class VuTHMessagePipeEventAssemblyAttribute : Attribute
    {
    }
}
