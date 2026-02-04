using System.Collections.Generic;
using _VuTH.Core.Persistant.SaveSystem.Backend;
using _VuTH.Core.Persistant.SaveSystem.Encrypt;
using _VuTH.Core.Persistant.SaveSystem.Migrate;
using _VuTH.Core.Persistant.SaveSystem.Serialize;
using UnityEngine;
using UnityEngine.Scripting;

namespace _VuTH.Core.Persistant.SaveSystem.Profile
{
    /// <summary>
    /// Profile asset storing selected adapter instances for the Save System using SerializeReference.
    /// Stored in Resources so it can be loaded at runtime without editor dependencies.
    /// </summary>
    [CreateAssetMenu(fileName = "SaveServiceAdapterProfile", menuName = "VuTH/Save System/Adapter Profile")]
    public class SaveServiceAdapterProfile : ScriptableObject
    {
        [Header("Adapters (SerializeReference instances)")]
        [Tooltip("IEncryptor implementation instance.")]
        [SerializeReference] private IEncryptor encryptor;
        [Tooltip("ISerializer implementation instance.")]
        [SerializeReference] private ISerializer serializer;
        [Tooltip("ISaveBackend implementation instance.")]
        [SerializeReference] private ISaveBackend backend;
        [Tooltip("Ordered list of ISaveMigrator implementation instances.")]
        [SerializeReference] private List<ISaveMigrator> migrators = new List<ISaveMigrator>();

        public IEncryptor Encryptor => encryptor;
        public ISerializer Serializer => serializer;
        public ISaveBackend Backend => backend;
        public IReadOnlyList<ISaveMigrator> Migrators => migrators;

        public void SetEncryptor(IEncryptor value) => encryptor = value;
        public void SetSerializer(ISerializer value) => serializer = value;
        public void SetBackend(ISaveBackend value) => backend = value;
        public void SetMigrators(List<ISaveMigrator> value) => migrators = value;

        public void ResetToDefaults()
        {
            encryptor = new XorEncryptor();
            serializer = new NewtonsoftJsonSerializer();
#if UNITY_EDITOR
            backend = new JsonFileSaveBackend();
#else
            backend = new PlayerPrefsSaveBackend();
#endif
            migrators = new List<ISaveMigrator> { new DefaultSaveMigrator(0, 1, Serializer) };
        }

        private void OnEnable()
        {
            if (encryptor == null || serializer == null || backend == null)
            {
                ResetToDefaults();
            }
        }
    }
}
