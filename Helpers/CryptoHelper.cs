using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

public static class CryptoHelper
{
	// Αυτό πρέπει να έχει το ίδιο μέγεθος με το κλειδί που χρησιμοποίησες στην κρυπτογράφηση (πχ 16 bytes για AES-128)
	private static readonly byte[] Key = Encoding.UTF8.GetBytes("HermesSuperSecretKey_1234567890!"); // πχ δικό σου κλειδί
	private static readonly byte[] IV = Encoding.UTF8.GetBytes("1234567890123456");  // πχ δικό σου IV

	public static string Decrypt(string encryptedBase64)
	{
		var cipherTextBytes = Convert.FromBase64String(encryptedBase64);

		using (var aes = Aes.Create())
		{
			aes.Key = Key;
			aes.IV = IV;

			using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
			using (var ms = new MemoryStream(cipherTextBytes))
			using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
			using (var sr = new StreamReader(cs))
			{
				return sr.ReadToEnd();
			}
		}
	}
}
