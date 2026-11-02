#nullable enable
using _VuTH.Common;

namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>
    /// Interface combining ISaveService and ICommonManager.
    /// </summary>
    public interface ISaveManager : ISaveService, ICommonManager
    {
    }
}
