#nullable enable
using System;
using System.Collections.Generic;

namespace _VuTH.Core.Persistant.SaveSystem.Example
{
    /// <summary>
    /// Sample player data model.
    /// Note: Data model does NOT call save directly - it should be plain POCO.
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public string playerName = "New Player";
        public int level = 1;
        public int experience = 0;
        public float gold = 100f;
        public DateTime lastSaveTime;
        public List<string> inventory = new();

        public void AddExperience(int amount)
        {
            experience += amount;
            if (experience >= GetLevelThreshold(level + 1))
            {
                level++;
                experience = 0;
            }
        }

        private int GetLevelThreshold(int targetLevel)
        {
            return targetLevel * 100;
        }
    }

    [Serializable]
    public class GameSettingsData
    {
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.8f;
        public bool vsync = true;
        public int qualityLevel = 2;
        public string language = "en";
    }

    [Serializable]
    public class ProgressionData
    {
        public List<string> unlockedAchievements = new();
        public int highestScore = 0;
        public int totalPlayTime = 0;
        public List<string> completedLevels = new();
    }
}
