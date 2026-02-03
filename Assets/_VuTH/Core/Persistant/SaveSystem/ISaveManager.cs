#nullable enable
using _VuTH.Common;

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// Interface combining ISaveService and ICommonManager.
    /// </summary>
    public interface ISaveManager : ISaveService, ICommonManager
    {
    }
}
