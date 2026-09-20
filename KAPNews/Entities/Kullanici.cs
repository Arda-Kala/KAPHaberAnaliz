using System;

namespace KAPNews.Entities
{
    /// <summary>
    /// Admin paneline giriş yapabilecek kullanıcı. SPK Bülten Analiz projesindeki
    /// Kullanici entity'siyle aynı desendedir — iki proje arasında tutarlı bir
    /// kimlik doğrulama modeli sağlamak için kasıtlı olarak aynı yapıdadır.
    /// </summary>
    public class Kullanici
    {
        public int Id { get; set; }
        public string KullaniciAdi { get; set; } = string.Empty;

        /// <summary>
        /// PBKDF2 ile tuzlanmış şifre özeti — ASLA düz metin şifre saklanmaz.
        /// Format: "{iterasyonSayisi}.{tuzBase64}.{hashBase64}" (bkz. SifreHashService).
        /// </summary>
        public string SifreHash { get; set; } = string.Empty;

        public string Rol { get; set; } = "Admin";
        public DateTime OlusturmaTarihi { get; set; } = DateTime.UtcNow;
        public DateTime? SonGirisTarihi { get; set; }
        public bool Aktif { get; set; } = true;
    }
}
