using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace PickupBot.Encryption
{
    public static class EncryptionProvider
    {
        /// <summary>  
        /// AES encrypt
        /// </summary>  
        /// <param name="data">Raw data</param>  
        /// <param name="key">Key, requires 32 bits</param>  
        /// <param name="vector">IV,requires 16 bits</param>  
        /// <returns>Encrypted string</returns>  
        // ReSharper disable once InconsistentNaming
        public static string AESEncrypt(string data, string key, string vector)
        {
            var plainBytes = Encoding.UTF8.GetBytes(data);

            var encryptBytes = AESEncrypt(plainBytes, key, vector);
            return encryptBytes == null ? null : Convert.ToBase64String(encryptBytes);
        }

        /// <summary>
        /// AES encrypt
        /// </summary>
        /// <param name="data">Raw data</param>  
        /// <param name="key">Key, requires 32 bits</param>  
        /// <param name="vector">IV,requires 16 bits</param>  
        /// <returns>Encrypted byte array</returns>  
        // ReSharper disable once InconsistentNaming
        public static byte[] AESEncrypt(byte[] data, string key, string vector)
        {
            var plainBytes = data;
            var bKey = new byte[32];
            Array.Copy(Encoding.UTF8.GetBytes(key.PadRight(bKey.Length)), bKey, bKey.Length);
            var bVector = new byte[16];
            Array.Copy(Encoding.UTF8.GetBytes(vector.PadRight(bVector.Length)), bVector, bVector.Length);

            using var aes = Aes.Create();
            if (aes == null) return null;
            byte[] encryptData; // encrypted data
            try
            {
                using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms,
                    aes.CreateEncryptor(bKey, bVector),
                    CryptoStreamMode.Write);
                cs.Write(plainBytes, 0, plainBytes.Length);
                cs.FlushFinalBlock();

                encryptData = ms.ToArray();
            }
            catch (Exception)
            {
                encryptData = null;
            }
            return encryptData;
        }

        /// <summary>  
        ///  AES decrypt
        /// </summary>  
        /// <param name="data">Encrypted data</param>  
        /// <param name="key">Key, requires 32 bits</param>  
        /// <param name="vector">IV,requires 16 bits</param>  
        /// <returns>Decrypted string</returns>  
        // ReSharper disable once InconsistentNaming
        public static string AESDecrypt(string data, string key, string vector)
        {
            var encryptedBytes = Convert.FromBase64String(data);

            var decryptBytes = AESDecrypt(encryptedBytes, key, vector);

            if (decryptBytes == null)
            {
                return null;
            }
            return Encoding.UTF8.GetString(decryptBytes);
        }
        
        /// <summary>  
        ///  AES decrypt
        /// </summary>  
        /// <param name="data">Encrypted data</param>  
        /// <param name="key">Key, requires 32 bits</param>  
        /// <param name="vector">IV,requires 16 bits</param>  
        /// <returns>Decrypted byte array</returns>
        // ReSharper disable once InconsistentNaming
        public static byte[] AESDecrypt(byte[] data, string key, string vector)
        {
            var encryptedBytes = data;
            var bKey = new byte[32];
            Array.Copy(Encoding.UTF8.GetBytes(key.PadRight(bKey.Length)), bKey, bKey.Length);
            var bVector = new byte[16];
            Array.Copy(Encoding.UTF8.GetBytes(vector.PadRight(bVector.Length)), bVector, bVector.Length);

            using var aes = Aes.Create();
            if (aes == null) return null;

            byte[] decryptedData; // decrypted data
            try
            {
                using var ms = new MemoryStream(encryptedBytes);
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(bKey, bVector), CryptoStreamMode.Read);
                using var tempMemory = new MemoryStream();
                var buffer = new byte[1024];
                var readBytes = 0;
                while ((readBytes = cs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    tempMemory.Write(buffer, 0, readBytes);
                }

                decryptedData = tempMemory.ToArray();
            }
            catch(Exception)
            {
                decryptedData = null;
            }

            return decryptedData;
        }
    }
}
