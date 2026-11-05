#nullable enable
using System;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Storage keys of slot-scoped packages (see <see cref="IPersistencePackage.IsSlotScoped"/>).
    /// The default slot (null) uses the package's own key, so saves made before slots existed stay in it.
    /// </summary>
    public static class SaveSlots
    {
        /// <summary>Separates the slot from the package key.</summary>
        public const string Separator = "/";

        /// <summary>The storage key of <paramref name="baseKey"/> in <paramref name="slot"/>.</summary>
        public static string GetKey(string? slot, string baseKey)
        {
            if (string.IsNullOrEmpty(baseKey)) throw new ArgumentException("The key cannot be empty.", nameof(baseKey));
            return string.IsNullOrEmpty(slot) ? baseKey : "slot-" + slot + Separator + baseKey;
        }
    }

    /// <summary>Lets the manager release the waiters of a package that will never load (no save service).</summary>
    internal interface IPackageLoadFailure
    {
        void FailLoading(Exception error);
    }
}
