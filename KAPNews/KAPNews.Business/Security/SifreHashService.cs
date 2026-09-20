using System;
using System.Security.Cryptography;

namespace KAPNews.Business.Security
{
    /// <summary>
    /// Şifreleri PBKDF2 (Password-Based Key Derivation Function 2) algoritmasıyla
    /// tuzlayıp (salt) tek yönlü olarak özetleyen (hash) yardımcı sınıf.
    ///
    /// ÖĞRENME NOTU: Şifreler ASLA düz metin veya geri döndürülebilir
    /// (reversible) şifrelemeyle saklanmaz. PBKDF2, kasıtlı olarak YAVAŞ
    /// çalışacak şekilde tasarlanmıştır (yüksek iterasyon sayısı) — bu, bir
    /// saldırganın veritabanı sızsa bile milyonlarca şifreyi hızlıca deneyerek
    /// (brute-force) kırmasını pratik olarak imkansız hale getirir. Her şifre
    /// için rastgele bir "tuz" (salt) üretilir ki aynı şifreyi kullanan iki
    /// kullanıcının hash'i asla aynı çıkmasın (rainbow table saldırılarını önler).
    ///
    /// Bu proje küçük ölçekli/öğrenme amaçlı olduğu için harici bir kütüphane
    /// (BCrypt.Net vb.) yerine .NET'in yerleşik Rfc2898DeriveBytes sınıfı
    /// kullanılmıştır — ek NuGet bağımlılığı gerektirmez ve mekanizma
    /// tamamen görünür/öğretici kalır.
    /// </summary>
    public static class SifreHashService
    {
        private const int TuzBoyutu = 16;      // 128 bit
        private const int HashBoyutu = 32;     // 256 bit
        private const int Iterasyon = 210_000; // OWASP 2023+ önerisi (PBKDF2-SHA256 için)

        /// <summary>Verilen düz metin şifreyi hash'ler. Sonuç veritabanında saklanır.</summary>
        public static string Hashle(string duzMetinSifre)
        {
            if (string.IsNullOrWhiteSpace(duzMetinSifre))
                throw new ArgumentException("Şifre boş olamaz.", nameof(duzMetinSifre));

            var tuz = RandomNumberGenerator.GetBytes(TuzBoyutu);
            var hash = Rfc2898DeriveBytes.Pbkdf2(duzMetinSifre, tuz, Iterasyon, HashAlgorithmName.SHA256, HashBoyutu);

            return $"{Iterasyon}.{Convert.ToBase64String(tuz)}.{Convert.ToBase64String(hash)}";
        }

        /// <summary>Girilen düz metin şifrenin, saklanan hash ile eşleşip eşleşmediğini doğrular.</summary>
        public static bool Dogrula(string duzMetinSifre, string saklananHash)
        {
            if (string.IsNullOrWhiteSpace(duzMetinSifre) || string.IsNullOrWhiteSpace(saklananHash))
                return false;

            var parcalar = saklananHash.Split('.', 3);
            if (parcalar.Length != 3)
                return false;

            if (!int.TryParse(parcalar[0], out var iterasyon))
                return false;

            byte[] tuz, beklenenHash;
            try
            {
                tuz = Convert.FromBase64String(parcalar[1]);
                beklenenHash = Convert.FromBase64String(parcalar[2]);
            }
            catch (FormatException)
            {
                return false;
            }

            var hesaplananHash = Rfc2898DeriveBytes.Pbkdf2(duzMetinSifre, tuz, iterasyon, HashAlgorithmName.SHA256, beklenenHash.Length);

            // Zamanlama saldırılarına (timing attack) karşı sabit süreli karşılaştırma.
            return CryptographicOperations.FixedTimeEquals(hesaplananHash, beklenenHash);
        }

        /// <summary>Kriptografik olarak güvenli, rastgele bir başlangıç şifresi üretir (ilk kurulum için).</summary>
        public static string RastgeleSifreUret(int uzunluk = 14)
        {
            const string karakterHavuzu = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
            var bytes = RandomNumberGenerator.GetBytes(uzunluk);
            var sonuc = new char[uzunluk];
            for (int i = 0; i < uzunluk; i++)
                sonuc[i] = karakterHavuzu[bytes[i] % karakterHavuzu.Length];
            return new string(sonuc);
        }
    }
}
