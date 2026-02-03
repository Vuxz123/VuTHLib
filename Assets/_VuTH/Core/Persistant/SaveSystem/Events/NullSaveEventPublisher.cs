#nullable enable

namespace _VuTH.Core.Persistant.SaveSystem.Events
{
    /// <summary>
    /// Null pattern implementation - does nothing.
    /// </summary>
    public class NullSaveEventPublisher : ISaveEventPublisher
    {
        public void OnSaveSuccess(string key, string dataType) { }
        public void OnSaveFailed(string key, string dataType, string error) { }
        public void OnLoadSuccess(string key, string dataType) { }
        public void OnLoadFailed(string key, string dataType, string error) { }
    }
}
