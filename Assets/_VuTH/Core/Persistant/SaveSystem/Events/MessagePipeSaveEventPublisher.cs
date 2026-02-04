#nullable enable
using _VuTH.Common.MessagePipe.Attributes;
using MessagePipe;

namespace _VuTH.Core.Persistant.SaveSystem.Events
{
    /// <summary>
    /// Save event published via MessagePipe.
    /// Contains information about save/load operation results.
    /// Marked with MessagePipeEventAttribute for automatic registration.
    /// </summary>
    [MessagePipeEvent]
    public class SaveEvent
    {
        /// <summary>
        /// The key of the data being saved/loaded.
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// The type of data being saved/loaded.
        /// </summary>
        public string DataType { get; set; } = string.Empty;

        /// <summary>
        /// The type of event that occurred.
        /// </summary>
        public SaveEventType EventType { get; set; }

        /// <summary>
        /// Error message if the operation failed.
        /// </summary>
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Types of save events.
    /// </summary>
    public enum SaveEventType
    {
        /// <summary>
        /// Save operation completed successfully.
        /// </summary>
        SaveSuccess,

        /// <summary>
        /// Save operation failed.
        /// </summary>
        SaveFailed,

        /// <summary>
        /// Load operation completed successfully.
        /// </summary>
        LoadSuccess,

        /// <summary>
        /// Load operation failed.
        /// </summary>
        LoadFailed
    }

    /// <summary>
    /// MessagePipe implementation for save events.
    /// Publishes SaveEvent when save/load operations complete.
    /// </summary>
    public class MessagePipeSaveEventPublisher : ISaveEventPublisher
    {
        private IPublisher<SaveEvent>? _eventPublisher;

        public MessagePipeSaveEventPublisher() : this(null)
        {
        }

        public MessagePipeSaveEventPublisher(IPublisher<SaveEvent>? eventPublisher)
        {
            _eventPublisher = eventPublisher;
        }
        
        internal void SetEventPublisher(IPublisher<SaveEvent> eventPublisher)
        {
            // This method can be used to set the event publisher if not provided in constructor
            _eventPublisher ??= eventPublisher;
        }

        public void OnSaveSuccess(string key, string dataType)
        {
            _eventPublisher!.Publish(new SaveEvent
            {
                Key = key,
                DataType = dataType,
                EventType = SaveEventType.SaveSuccess
            });
        }

        public void OnSaveFailed(string key, string dataType, string error)
        {
            _eventPublisher!.Publish(new SaveEvent
            {
                Key = key,
                DataType = dataType,
                EventType = SaveEventType.SaveFailed,
                Error = error
            });
        }

        public void OnLoadSuccess(string key, string dataType)
        {
            _eventPublisher!.Publish(new SaveEvent
            {
                Key = key,
                DataType = dataType,
                EventType = SaveEventType.LoadSuccess
            });
        }

        public void OnLoadFailed(string key, string dataType, string error)
        {
            _eventPublisher!.Publish(new SaveEvent
            {
                Key = key,
                DataType = dataType,
                EventType = SaveEventType.LoadFailed,
                Error = error
            });
        }
    }

}
