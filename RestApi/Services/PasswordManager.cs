using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace RestApi.Services
{
    public class PasswordManager
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;
        private string _secretKey = "";

        public PasswordManager(IConfiguration configuration)
        {
            try
            {
                var configured = configuration?.GetValue<string>("PasswordEncryption:_secretKey");
                if (!string.IsNullOrEmpty(configured))
                {
                    _secretKey = configured;
                }
            }
            catch
            {
                // Swallow and continue with default key if configuration not available
            }

            using (var deriveBytes = new Rfc2898DeriveBytes(
                _secretKey,
                new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 },
                10000, // Önerilen minimum yineleme sayısı
                HashAlgorithmName.SHA256 // Daha güvenli bir karma algoritması
            ))
            {
                _key = deriveBytes.GetBytes(32); // 256-bit key
                _iv = deriveBytes.GetBytes(16);  // 128-bit IV
            }
        }


    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentNullException(nameof(plainText));

        byte[] encryptedBytes;

        using (var aesAlg = Aes.Create())
        {
            aesAlg.Key = _key;

            // Rastgele IV üret
            aesAlg.GenerateIV();
            byte[] iv = aesAlg.IV;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, iv);

            using (var msEncrypt = new MemoryStream())
            {
                // İlk olarak IV'yi belleğe yaz
                msEncrypt.Write(iv, 0, iv.Length);

                using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                using (var swEncrypt = new StreamWriter(csEncrypt))
                {
                    swEncrypt.Write(plainText);
                }

                encryptedBytes = msEncrypt.ToArray();
            }
        }

        return Convert.ToBase64String(encryptedBytes);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentNullException(nameof(cipherText));

        byte[] cipherBytes = Convert.FromBase64String(cipherText);

        using (var aesAlg = Aes.Create())
        {
            aesAlg.Key = _key;

            // IV'yi ilk 16 bayttan al
            byte[] iv = new byte[16];
            Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
            aesAlg.IV = iv;

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using (var msDecrypt = new MemoryStream(cipherBytes, 16, cipherBytes.Length - 16))
            using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
            using (var srDecrypt = new StreamReader(csDecrypt))
            {
                return srDecrypt.ReadToEnd();
            }
        }
    }



    public string GenerateSecureKey(int keySizeInBytes = 32) // 32 byte = 256 bit
    {
        byte[] key = new byte[keySizeInBytes];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(key);
        }
        return Convert.ToBase64String(key);
    }

}
}