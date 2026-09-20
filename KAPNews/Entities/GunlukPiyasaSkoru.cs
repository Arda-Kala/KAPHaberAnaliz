using System;

namespace KAPNews.Entities
{
    /// <summary>
    /// 🌟 v2.3.0 — Bir takvim gününe ait, o gün KAPANMIŞ (yani gün bitmiş, artık
    /// yeni haber gelmeyecek) tüm haberlerden hesaplanan özet Piyasa Skoru.
    ///
    /// NEDEN GÜNLÜK VE ANLIK DEĞİL: Gün içinde sürekli yeni bildirim geldiği için
    /// "o anki tüm haberlerin ortalaması" sürekli dalgalanan, tutarsız bir metrik
    /// olur — kullanıcı sayfayı yenilediğinde farklı bir skor görür. SPK Bülten
    /// projesi de bültenler (kapanmış dönemler) bazında skorluyordu, anlık değil.
    /// Bu yüzden hesaplama, ilgili günün SAAT 00:00'ı geçtikten sonra, bir arka
    /// plan görevi (bkz. GunlukPiyasaSkoruHesaplamaServisi) tarafından yapılır ve
    /// sonuç burada "dondurulmuş" olarak saklanır — tekrar tekrar hesaplanmaz.
    /// </summary>
    public class GunlukPiyasaSkoru
    {
        public int Id { get; set; }

        /// <summary>Skorun ait olduğu takvim günü (saat bileşeni olmadan, örn. 2026-08-10).</summary>
        public DateTime Tarih { get; set; }

        /// <summary>
        /// SPK Bülten projesindeki "Net Skor" mantığıyla hesaplanır:
        /// her haber, yönüne göre işaretli katkı verir (Pozitif → +EtkiSkoru,
        /// Olumsuz → -EtkiSkoru, Nötr → 0), bu değerlerin ortalaması alınır
        /// (-100..+100 aralığında). İşareti YÖNÜ, mutlak değeri GÜCÜ gösterir.
        /// </summary>
        public double NetSkor { get; set; }

        /// <summary>"Pozitif", "Olumsuz" veya "Nötr" — NetSkor'un |15| eşiğine göre sınıflandırılmış hali.</summary>
        public string Yon { get; set; } = "Nötr";

        /// <summary>O gün analiz edilmiş toplam haber sayısı (hesaplamaya dahil edilen).</summary>
        public int ToplamHaberSayisi { get; set; }

        public int OlumluSayisi { get; set; }
        public int OlumsuzSayisi { get; set; }
        public int NotrSayisi { get; set; }

        /// <summary>Bu kayıt ne zaman hesaplandı (denetim/debug amaçlı).</summary>
        public DateTime HesaplanmaZamani { get; set; } = DateTime.UtcNow;
    }
}
