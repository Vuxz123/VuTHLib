#nullable enable

namespace _VuTH.Core.Persistent.SaveSystem.Encrypt
{
    /// <summary>
    /// Marks an encryptor whose <see cref="IEncryptor.Encrypt"/> / <see cref="IEncryptor.Decrypt"/> may run on a worker
    /// thread: no Unity API calls and no shared mutable state. <see cref="SaveService"/> then encrypts large saves off
    /// the main thread. All built-in encryptors implement it.
    /// </summary>
    public interface IThreadSafeEncryptor : IEncryptor
    {
    }

    /// <summary>Whether an encryptor may run on a worker thread (see <see cref="IThreadSafeEncryptor"/>).</summary>
    public static class EncryptorThreading
    {
        /// <summary>A composite is thread-safe when all of its encryptors are.</summary>
        public static bool IsThreadSafe(IEncryptor encryptor) => encryptor switch
        {
            CompositeEncryptor composite => composite.AllThreadSafe,
            IThreadSafeEncryptor => true,
            _ => false,
        };
    }
}
