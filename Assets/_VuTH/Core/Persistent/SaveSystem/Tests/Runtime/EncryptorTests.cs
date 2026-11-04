using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using _VuTH.Core.Persistent.SaveSystem.Encrypt;
using NUnit.Framework;

namespace _VuTH.Core.Persistent.SaveSystem.Tests.Runtime
{
    /// <summary>
    /// Encryptors throw on data they cannot decode (instead of returning it unchanged, which hid corruption), and AES
    /// uses a random IV with tamper detection while still reading the previous format.
    /// </summary>
    [TestFixture]
    public class EncryptorTests
    {
        private const string Plain = "{\"schemaVersion\":1,\"payload\":\"{\\\"value\\\":42}\"}";

        private static IEncryptor[] RoundTripEncryptors() => new IEncryptor[]
        {
            new XorEncryptor(), new Base64Encryptor(), new Rot13Encryptor(), new NoOpEncryptor(),
            new DefaultAesEncryptor(), new DefaultUnityObfuscationEncryptor(),
            new CompositeEncryptor(new XorEncryptor(), new DefaultAesEncryptor()),
        };

        [TestCaseSource(nameof(RoundTripEncryptors))]
        public void Encrypt_Decrypt_RoundTrips(IEncryptor encryptor)
        {
            Assert.AreEqual(Plain, encryptor.Decrypt(encryptor.Encrypt(Plain)));
        }

        private static IEncryptor[] DecodingEncryptors() => new IEncryptor[]
        {
            new XorEncryptor(), new Base64Encryptor(), new DefaultAesEncryptor(), new DefaultUnityObfuscationEncryptor(),
        };

        [TestCaseSource(nameof(DecodingEncryptors))]
        public void Decrypt_InvalidData_Throws(IEncryptor encryptor)
        {
            Assert.That(() => encryptor.Decrypt("not base64 at all!"), Throws.InstanceOf<Exception>());
        }

        [Test]
        public void Aes_SameInput_EncryptsDifferently()
        {
            var aes = new DefaultAesEncryptor();
            Assert.AreNotEqual(aes.Encrypt(Plain), aes.Encrypt(Plain), "random IV per save");
        }

        [Test]
        public void Aes_ModifiedData_IsRejected()
        {
            var aes = new DefaultAesEncryptor();
            var encrypted = aes.Encrypt(Plain);
            var prefixLength = encrypted.IndexOf(':') + 1;
            var bytes = Convert.FromBase64String(encrypted.Substring(prefixLength));
            bytes[20] ^= 0x01; // flip one ciphertext bit
            var tampered = encrypted.Substring(0, prefixLength) + Convert.ToBase64String(bytes);

            Assert.Throws<CryptographicException>(() => aes.Decrypt(tampered));
        }

        [Test]
        public void Aes_OtherKey_IsRejected()
        {
            var encrypted = new AesEncryptor("password-a", "salt-1234").Encrypt(Plain);
            Assert.Throws<CryptographicException>(() => new AesEncryptor("password-b", "salt-1234").Decrypt(encrypted));
        }

        [Test]
        public void Aes_ReadsThePreviousFormat()
        {
            // Previous format: AES-CBC, key and IV = first 48 PBKDF2 bytes, Base64, no prefix, no tag.
            const string password = "pw";
            const string salt = "salt-1234";
            string legacy;
            using (var derive = new Rfc2898DeriveBytes(password, Encoding.UTF8.GetBytes(salt), 10000, HashAlgorithmName.SHA256))
            using (var aes = Aes.Create())
            {
                aes.Key = derive.GetBytes(32);
                aes.IV = derive.GetBytes(16);
                using var ms = new MemoryStream();
                using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                using (var sw = new StreamWriter(cs))
                {
                    sw.Write(Plain);
                }

                legacy = Convert.ToBase64String(ms.ToArray());
            }

            Assert.AreEqual(Plain, new AesEncryptor(password, salt).Decrypt(legacy));
        }
    }
}
