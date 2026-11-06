using System;
using _VuTH.Core.Persistent.SaveSystem;
using UnityEngine;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Example DTO for PlayerProfile - contains only raw data (no logic, no R3).
    /// This is what gets serialized to JSON. The Unity attributes constrain the generated fields; progress values refuse
    /// out-of-range changes ([RejectOutOfRange]) instead of clamping them, so a logic error (spending more gold than
    /// owned) is reported instead of silently leaving 0.
    /// </summary>
    [Serializable]
    public class PlayerProfileDTO
    {
        public string PlayerName = "Player";
        [Min(1), RejectOutOfRange] public int Level = 1;
        [Min(0), RejectOutOfRange] public long Gold = 0;
        [Min(0), RejectOutOfRange] public long Exp = 0;
        [Min(0), RejectOutOfRange] public int HighScore = 0;
    }

    /// <summary>
    /// Example Package for PlayerProfile. Its fields (<c>PlayerName</c>, <c>Level</c>, <c>Gold</c>, <c>Exp</c>,
    /// <c>HighScore</c>) and the payload mapping are generated from <see cref="PlayerProfileDTO"/>
    /// ([GeneratePersistentFields]): adding a value only takes a DTO member. <c>[Min]</c> + <c>[RejectOutOfRange]</c> make
    /// ranged fields that refuse to go negative: check <c>Gold.TrySetValue(...)</c> before granting a purchase.
    /// </summary>
    [GeneratePersistentFields]
    public partial class PlayerProfilePackage : PersistencePackage<PlayerProfileDTO>
    {
        /// <summary>Progress: each save slot has its own profile (the default slot keeps the existing save).</summary>
        public override bool IsSlotScoped => true;

        public PlayerProfilePackage()
            : base("player_profile", SaveStrategy.Immediate)
        {
            // Immediate: every change is saved right away (concurrent changes are coalesced into one more save).
        }
        
        // Example gameplay method - no need to manually save!
        public void AddGold(long amount)
        {
            Gold.Value += amount; // saved by the Immediate strategy
        }
        
        // Example gameplay method
        public void AddExp(long amount)
        {
            // One change: exp and level are saved together, never a new exp with the old level.
            using (BatchChanges())
            {
                Exp.Value += amount;

                // Level up logic
                var expNeeded = Level.Value * 100;
                if (Exp.Value >= expNeeded)
                {
                    Level.Value++;
                    Exp.Value -= expNeeded;
                }
            }
        }

        // The PersistentFields register themselves and are disposed with the package: no Dispose override needed.
    }
}
