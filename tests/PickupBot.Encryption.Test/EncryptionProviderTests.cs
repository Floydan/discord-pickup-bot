using System;
using System.Text;
using NUnit.Framework;

namespace PickupBot.Encryption.Test
{
    public class Tests
    {
        private string _key;
        private string _iv;
        [SetUp]
        public void Setup()
        {
            _key = GetRandomStr(32);
            _iv = GetRandomStr(16);
        }

        [Test]
        public void Test1()
        {
            const string original = "test string for encryption and decryption test";

            var encrypted = EncryptionProvider.AESEncrypt(original, _key, _iv);

            Assert.NotNull(encrypted);
            Assert.IsNotEmpty(encrypted);

            var decrypted = EncryptionProvider.AESDecrypt(encrypted, _key, _iv);

            Assert.NotNull(decrypted);
            Assert.IsNotEmpty(decrypted);

            Assert.AreEqual(original, decrypted);
        }

        private static string GetRandomStr(int length)
        {
            var arrChar = new[]{
                'a','b','d','c','e','f','g','h','i','j','k','l','m','n','p','r','q','s','t','u','v','w','z','y','x',
                '0','1','2','3','4','5','6','7','8','9',
                'A','B','C','D','E','F','G','H','I','J','K','L','M','N','Q','P','R','T','S','V','U','W','X','Y','Z',
                '!', '@', '*', '.', ';', '#', '$', '%', '&'
            };

            var num = new StringBuilder();

            var rnd = new Random(DateTime.Now.Millisecond);
            for (var i = 0; i < length; i++)
            {
                num.Append(arrChar[rnd.Next(0, arrChar.Length)].ToString());
            }

            return num.ToString();
        }
    }
}