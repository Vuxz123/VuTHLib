using UnityEngine;
using _VuTH.Core.Persistant.DataPackage;
using _VuTH.Core.Persistant.SaveSystem;
using JetBrains.Annotations;
using R3;
using UnityEngine.Serialization;

namespace TScript
{
    /// <summary>
    /// Demo script showing how to use the DataPackage system.
    /// This demonstrates the 3-step workflow from the architecture spec.
    /// </summary>
    public class DataPackageDemo : MonoBehaviour
    {
        // Reference to PlayerProfilePackage (from DataPackage module)
        [CanBeNull] private PlayerProfilePackage _playerProfile;
        
        // Reference to DataPersistenceManager
        [CanBeNull] private IDataPersistenceManager _persistenceManager;
        
        private void Start()
        {
            SetupPersistence();
            DemonstrateUsage();
        }
        
        private void SetupPersistence()
        {
            // Step 1: Create the package instance
            _playerProfile = new PlayerProfilePackage();
            
            // Step 2: Register with DataPersistenceManager
            if (DataPersistenceManager.HasInstance)
            {
                _persistenceManager = DataPersistenceManager.Instance;
                _persistenceManager.RegisterPackage(_playerProfile);
            }
            else
            {
                // Fallback: initialize directly
                _playerProfile.Initialize(SaveServiceManager.Instance);
                _playerProfile.Load();
            }
        }
        
        private void DemonstrateUsage()
        {
            if (_playerProfile == null) return;
            
            // ===== GAMEPLAY USAGE =====
            
            // Example 1: Add gold - automatically saves after 3 seconds debounce
            Debug.Log("[Demo] Adding 100 gold...");
            _playerProfile.AddGold(100);
            
            // Example 2: Add experience - triggers level up logic and auto-save
            Debug.Log("[Demo] Adding 150 exp...");
            _playerProfile.AddExp(150);
            
            // Example 3: Direct field modification
            _playerProfile.PlayerName.Value = "Hero123";
            
            // ===== UI BINDING USAGE =====
            
            // Subscribe to gold changes - UI updates automatically!
            // IMPORTANT: Always add .AddTo(this) to prevent memory leaks!
            _playerProfile.Gold.Observable
                .Subscribe(OnGoldChanged)
                .AddTo(this); // AddTo must be called!
            
            // Subscribe to level changes
            _playerProfile.Level.Observable
                .Subscribe(OnLevelChanged)
                .AddTo(this);
        }
        
        private void OnGoldChanged(long gold)
        {
            // Update UI - this is called automatically when gold changes!
            Debug.Log($"[Demo] UI Updated: Gold = {gold}");
            // uiGoldText.text = gold.ToString();
        }
        
        private void OnLevelChanged(int level)
        {
            // Update UI - this is called automatically when level changes!
            Debug.Log($"[Demo] UI Updated: Level = {level}");
            // uiLevelText.text = $"Level {level}";
        }
        
        private void OnDestroy()
        {
            // Clean up - save any dirty data
            _playerProfile?.SaveNow();
            
            // Unregister from manager
            _persistenceManager?.UnregisterPackage(_playerProfile!);
            
            // Dispose to clean up R3 subscriptions
            _playerProfile?.Dispose();
        }
    }
    
    // ===== EXAMPLE: Creating a Custom Package =====
    
    /// <summary>
    /// Example of creating a custom package following the workflow.
    /// </summary>
    public class GameSettingsPackage : PersistencePackage<GameSettingsDTO>
    {
        // Define persistent fields
        public PersistentField<bool> SoundEnabled { get; }
        public PersistentField<bool> MusicEnabled { get; }
        public PersistentField<float> SoundVolume { get; }
        public PersistentField<float> MusicVolume { get; }
        public PersistentField<int> QualityLevel { get; }
        
        public GameSettingsPackage() 
            : base("game_settings", SaveStrategy.OnAppClose)  // Save only when app closes
        {
            // Initialize fields with defaults
            SoundEnabled = new PersistentField<bool>(this, true);
            MusicEnabled = new PersistentField<bool>(this, true);
            SoundVolume = new PersistentField<float>(this, 0.7f);
            MusicVolume = new PersistentField<float>(this, 0.5f);
            QualityLevel = new PersistentField<int>(this, 2);
        }
        
        // Step 2 (continued): ExtractPayload - map fields to DTO
        public override GameSettingsDTO ExtractPayload()
        {
            return new GameSettingsDTO
            {
                soundEnabled = SoundEnabled.Value,
                musicEnabled = MusicEnabled.Value,
                soundVolume = SoundVolume.Value,
                musicVolume = MusicVolume.Value,
                qualityLevel = QualityLevel.Value
            };
        }
        
        // Step 2 (continued): InjectPayload - map DTO to fields
        public override void InjectPayload(GameSettingsDTO data)
        {
            LoadWithoutNotify(() =>
            {
                SoundEnabled.SetValueWithoutNotify(data.soundEnabled);
                MusicEnabled.SetValueWithoutNotify(data.musicEnabled);
                SoundVolume.SetValueWithoutNotify(data.soundVolume);
                MusicVolume.SetValueWithoutNotify(data.musicVolume);
                QualityLevel.SetValueWithoutNotify(data.qualityLevel);
            });
        }
    }
    
    /// <summary>
    /// DTO for GameSettings - only raw data, no logic!
    /// </summary>
    [System.Serializable]
    public class GameSettingsDTO
    {
        public bool soundEnabled = true;
        public bool musicEnabled = true;
        public float soundVolume = 0.7f;
        public float musicVolume = 0.5f;
        public int qualityLevel = 2;
    }
}
