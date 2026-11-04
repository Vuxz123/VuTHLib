#nullable enable
using _VuTH.Common;

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// The save service exposed by <see cref="SaveServiceManager"/>, including the synchronous path used on app pause / quit.
    /// </summary>
    public interface ISaveManager : ISaveService, ISyncSaveService, ICommonManager
    {
    }
}
