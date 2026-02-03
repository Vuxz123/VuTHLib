namespace _VuTH.Core.Persistant.SaveSystem.Events
{
    /// <summary>
    /// Interface for publishing save/load domain events.
    /// Can be implemented with MessagePipe or other event systems.
    /// </summary>
    public interface ISaveEventPublisher
    {
        void OnSaveSuccess(string key, string dataType);
        void OnSaveFailed(string key, string dataType, string error);
        void OnLoadSuccess(string key, string dataType);
        void OnLoadFailed(string key, string dataType, string error);
    }
}
