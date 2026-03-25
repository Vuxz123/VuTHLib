using System;
using _VuTH.Core.Persistant.SaveSystem;

namespace _VuTH.Core.Persistant.DataPackage
{
    /// <summary>
    /// Example DTO for PlayerProfile - contains only raw data (no logic, no R3).
    /// This is what gets serialized to JSON.
    /// </summary>
    [Serializable]
    public class PlayerProfileDTO
    {
        public string PlayerName = "Player";
        public int Level = 1;
        public long Gold = 0;
        public long Exp = 0;
        public int HighScore = 0;
    }
    
    /// <summary>
    /// Example Package for PlayerProfile - manages reactive fields and save logic.
    /// Following the 3-step workflow from the architecture spec.
    /// </summary>
    public class PlayerProfilePackage : PersistencePackage<PlayerProfileDTO>
    {
        // Step 2: Define PersistentFields - these auto-subscribe to UI changes
        public PersistentField<string> PlayerName { get; }
        public PersistentField<int> Level { get; }
        public PersistentField<long> Gold { get; }
        public PersistentField<long> Exp { get; }
        public PersistentField<int> HighScore { get; }
        
        public PlayerProfilePackage() 
            : base("player_profile", SaveStrategy.Debounced)
        {
            // Initialize fields and register them
            PlayerName = new PersistentField<string>(this, "Player");
            Level = new PersistentField<int>(this, 1);
            Gold = new PersistentField<long>(this, 0);
            Exp = new PersistentField<long>(this, 0);
            HighScore = new PersistentField<int>(this, 0);
        }
        
        // Step 2 (continued): Implement ExtractPayload - map fields to DTO
        public override PlayerProfileDTO ExtractPayload()
        {
            return new PlayerProfileDTO
            {
                PlayerName = PlayerName.Value,
                Level = Level.Value,
                Gold = Gold.Value,
                Exp = Exp.Value,
                HighScore = HighScore.Value
            };
        }
        
        // Step 2 (continued): Implement InjectPayload - map DTO to fields (no auto-save)
        public override void InjectPayload(PlayerProfileDTO data)
        {
            LoadWithoutNotify(() =>
            {
                PlayerName.SetValueWithoutNotify(data.PlayerName);
                Level.SetValueWithoutNotify(data.Level);
                Gold.SetValueWithoutNotify(data.Gold);
                Exp.SetValueWithoutNotify(data.Exp);
                HighScore.SetValueWithoutNotify(data.HighScore);
            });
        }
        
        // Example gameplay method - no need to manually save!
        public void AddGold(long amount)
        {
            Gold.Value += amount;
            // Debounced strategy will auto-save after 3 seconds without changes
        }
        
        // Example gameplay method
        public void AddExp(long amount)
        {
            Exp.Value += amount;
            
            // Level up logic
            var expNeeded = Level.Value * 100;
            if (Exp.Value >= expNeeded)
            {
                Level.Value++;
                Exp.Value -= expNeeded;
            }
            // Debounced will auto-save
        }
        
        public override void Dispose()
        {
            SaveLifecycleHook.UnregisterPackage(this);
            base.Dispose();
        }
    }
}
