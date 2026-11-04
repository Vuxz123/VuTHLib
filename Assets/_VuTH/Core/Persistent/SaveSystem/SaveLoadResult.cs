namespace _VuTH.Core.Persistent.SaveSystem
{
    /// <summary>Outcome of <see cref="ISaveService.TryLoadAsync{T}"/>.</summary>
    public enum SaveLoadStatus
    {
        /// <summary>The data was read, decoded and (if needed) migrated.</summary>
        Loaded,

        /// <summary>Nothing is stored under the key.</summary>
        NotFound,

        /// <summary>
        /// The stored data could not be decoded (decryption or deserialization failed). A raw copy was written to
        /// <c>key + </c><see cref="SaveService.CorruptedBackupSuffix"/> first, so overwriting the key is safe.
        /// </summary>
        Corrupted,

        /// <summary>The storage could not be read (I/O error). The stored data is untouched: do not overwrite it.</summary>
        ReadFailed,

        /// <summary>
        /// The data was written by a newer schema version (e.g. the game was downgraded). Reading it would drop the
        /// fields this version does not know: do not overwrite it.
        /// </summary>
        UnsupportedVersion,

        /// <summary>A migrator is missing or failed. The stored data is untouched: do not overwrite it.</summary>
        MigrationFailed,
    }

    /// <summary>Result of <see cref="ISaveService.TryLoadAsync{T}"/>.</summary>
    public readonly struct SaveLoadResult<T>
    {
        public SaveLoadResult(SaveLoadStatus status, T data, string error, bool canOverwrite)
        {
            Status = status;
            Data = data;
            Error = error;
            CanOverwrite = canOverwrite;
        }

        public SaveLoadStatus Status { get; }

        /// <summary>The loaded data. Only meaningful when <see cref="IsLoaded"/>.</summary>
        public T Data { get; }

        /// <summary>Why the load did not succeed, or null.</summary>
        public string Error { get; }

        public bool IsLoaded => Status == SaveLoadStatus.Loaded;

        /// <summary>
        /// Whether saving to this key would not destroy anything recoverable: true when the data loaded, nothing was
        /// stored, or the corrupted data was backed up.
        /// </summary>
        public bool CanOverwrite { get; }

        public static SaveLoadResult<T> Success(T data) => new(SaveLoadStatus.Loaded, data, null, true);
        public static SaveLoadResult<T> NotFound() => new(SaveLoadStatus.NotFound, default, null, true);

        public static SaveLoadResult<T> Failure(SaveLoadStatus status, string error, bool canOverwrite = false) =>
            new(status, default, error, canOverwrite);

        public override string ToString() => Error == null ? Status.ToString() : $"{Status}: {Error}";
    }
}
