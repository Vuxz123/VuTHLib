#nullable enable
using System;
using System.Threading;
using _VuTH.Core.Persistant.SaveSystem.Events;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;

namespace _VuTH.Core.Persistant.SaveSystem.Example
{
    /// <summary>
    /// Example DataManager that uses ISaveService.
    /// Demonstrates how to inject and use the save system.
    /// Note: DataManager only knows about ISaveService interface, not backend details.
    /// </summary>
    public class ExampleDataManager
    {
        // Keys for save data - use constants for consistency
        public static readonly string PlayerDataKey = "player_data";
        public static readonly string SettingsDataKey = "settings_data";
        public static readonly string ProgressionDataKey = "progression_data";

        private readonly ISaveService _saveService;
        private readonly IDisposable? _eventSubscription;

        // Cached data for in-memory access
        private PlayerData? _playerData;
        private GameSettingsData? _settingsData;
        private ProgressionData? _progressionData;

        public ExampleDataManager(ISaveService saveService, ISubscriber<SaveEvent>? saveEventSubscriber)
        {
            _saveService = saveService;

            // Subscribe to save events via MessagePipe
            if (saveEventSubscriber != null)
            {
                _eventSubscription = saveEventSubscriber.Subscribe(OnSaveEvent);
            }
        }

        /// <summary>
        /// Loads all game data asynchronously.
        /// </summary>
        public async UniTask LoadAllAsync(CancellationToken cancellationToken = default)
        {
            // Load player data
            _playerData = await _saveService.LoadAsync(
                PlayerDataKey,
                new PlayerData(),
                cancellationToken);

            // Load settings
            _settingsData = await _saveService.LoadAsync(
                SettingsDataKey,
                new GameSettingsData(),
                cancellationToken);

            // Load progression
            _progressionData = await _saveService.LoadAsync(
                ProgressionDataKey,
                new ProgressionData(),
                cancellationToken);

            Debug.Log("[ExampleDataManager] All data loaded successfully.");
        }

        /// <summary>
        /// Saves all game data asynchronously.
        /// </summary>
        public async UniTask SaveAllAsync(CancellationToken cancellationToken = default)
        {
            if (_playerData != null)
            {
                _playerData.lastSaveTime = DateTime.UtcNow;
                await _saveService.SaveAsync(PlayerDataKey, _playerData, cancellationToken);
            }

            if (_settingsData != null)
            {
                await _saveService.SaveAsync(SettingsDataKey, _settingsData, cancellationToken);
            }

            if (_progressionData != null)
            {
                await _saveService.SaveAsync(ProgressionDataKey, _progressionData, cancellationToken);
            }

            Debug.Log("[ExampleDataManager] All data saved successfully.");
        }

        /// <summary>
        /// Saves player data only.
        /// </summary>
        public async UniTask SavePlayerAsync(CancellationToken cancellationToken = default)
        {
            if (_playerData == null)
            {
                Debug.LogWarning("[ExampleDataManager] No player data to save.");
                return;
            }

            _playerData.lastSaveTime = DateTime.UtcNow;
            await _saveService.SaveAsync(PlayerDataKey, _playerData, cancellationToken);
            Debug.Log("[ExampleDataManager] Player data saved.");
        }

        /// <summary>
        /// Gets current player data.
        /// </summary>
        public PlayerData? GetPlayerData() => _playerData;

        /// <summary>
        /// Gets or creates player data (auto-initializes if null).
        /// </summary>
        public PlayerData GetOrCreatePlayerData()
        {
            _playerData ??= new PlayerData();
            return _playerData;
        }

        /// <summary>
        /// Modifies player data and triggers auto-save.
        /// </summary>
        public async UniTask AddExperienceAsync(int amount, CancellationToken cancellationToken = default)
        {
            var data = GetOrCreatePlayerData();
            data.AddExperience(amount);
            await _saveService.SaveAsync(PlayerDataKey, data, cancellationToken);
        }

        /// <summary>
        /// Checks if player data exists.
        /// </summary>
        public async UniTask<bool> HasPlayerDataAsync(CancellationToken cancellationToken = default)
        {
            return await _saveService.ExistsAsync(PlayerDataKey, cancellationToken);
        }

        /// <summary>
        /// Clears all save data (for new game).
        /// </summary>
        public async UniTask ClearAllDataAsync(CancellationToken cancellationToken = default)
        {
            await _saveService.DeleteAsync(PlayerDataKey, cancellationToken);
            await _saveService.DeleteAsync(SettingsDataKey, cancellationToken);
            await _saveService.DeleteAsync(ProgressionDataKey, cancellationToken);

            _playerData = null;
            _settingsData = null;
            _progressionData = null;

            Debug.Log("[ExampleDataManager] All save data cleared.");
        }

        private void OnSaveEvent(SaveEvent eventData)
        {
            Debug.Log($"[ExampleDataManager] Save event: {eventData.EventType} - Key={eventData.Key}");
        }

        public void Dispose()
        {
            _eventSubscription?.Dispose();
        }
    }
}
