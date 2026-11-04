using System.Collections.Generic;
using _VuTH.Core.Persistent.SaveSystem.Backend;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using _VuTH.Core.Persistent.SaveSystem.Migrate;
using _VuTH.Core.Persistent.SaveSystem.Serialize;
using UnityEngine;

namespace _VuTH.Core.Persistent.SaveSystem.Profile
{
    /// <summary>
    /// Save System configuration: adapter instances (SerializeReference), the schema version and its migrators.
    /// Loaded from Resources at <see cref="ResourcePath"/> by <see cref="SaveServiceManager"/>.
    /// The defaults are the same in the Editor and in builds, so what is tested in the Editor is what ships.
    /// </summary>
    [CreateAssetMenu(fileName = "SaveServiceAdapterProfile", menuName = "VuTH/Save System/Adapter Profile")]
    public class SaveServiceAdapterProfile : ScriptableObject
    {
        /// <summary>Resources path of the profile.</summary>
        public const string ResourcePath = "SaveServiceAdapterProfile";

        [Header("Adapters (SerializeReference instances)")]
        [Tooltip("IEncryptor implementation instance.")]
        [SerializeReference] private IEncryptor encryptor;
        [Tooltip("ISerializer implementation instance.")]
        [SerializeReference] private ISerializer serializer;
        [Tooltip("ISaveBackend implementation instance. PlayerPrefs suits small saves only; prefer JsonFile for larger data.")]
        [SerializeReference] private ISaveBackend backend;

        [Header("Versioning")]
        [Tooltip("Schema version written with every save. Bump it when the saved data format changes, and add a " +
                 "migrator from the previous version (a DefaultSaveMigrator if the format did not change).")]
        [SerializeField, Min(0)] private int schemaVersion = SaveService.DefaultSchemaVersion;
        [Tooltip("Migrators, one per schema version step.")]
        [SerializeReference] private List<ISaveMigrator> migrators = new();

        public IEncryptor Encryptor => encryptor;
        public ISerializer Serializer => serializer;
        public ISaveBackend Backend => backend;
        public int SchemaVersion => schemaVersion;
        public IReadOnlyList<ISaveMigrator> Migrators => migrators;

        public void SetEncryptor(IEncryptor value) => encryptor = value;
        public void SetSerializer(ISerializer value) => serializer = value;
        public void SetBackend(ISaveBackend value) => backend = value;
        public void SetSchemaVersion(int value) => schemaVersion = Mathf.Max(0, value);
        public void SetMigrators(List<ISaveMigrator> value) => migrators = value ?? new List<ISaveMigrator>();

        public static IEncryptor CreateDefaultEncryptor() => new XorEncryptor();
        public static ISerializer CreateDefaultSerializer() => new NewtonsoftJsonSerializer();

        /// <summary>PlayerPrefs: the backend shipped builds have always used, so existing saves stay readable.</summary>
        public static ISaveBackend CreateDefaultBackend() => new PlayerPrefsSaveBackend();

        public void ResetToDefaults()
        {
            encryptor = CreateDefaultEncryptor();
            serializer = CreateDefaultSerializer();
            backend = CreateDefaultBackend();
            schemaVersion = SaveService.DefaultSchemaVersion;
            migrators = new List<ISaveMigrator>();
        }

        private void OnEnable()
        {
            // Only fill what is missing: the other selections are kept.
            encryptor ??= CreateDefaultEncryptor();
            serializer ??= CreateDefaultSerializer();
            backend ??= CreateDefaultBackend();
            migrators ??= new List<ISaveMigrator>();
        }
    }
}
