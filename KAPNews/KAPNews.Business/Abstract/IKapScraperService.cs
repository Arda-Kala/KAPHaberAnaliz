using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace KAPNews.Business.Abstract
{
    /// <summary>
    /// KAP (Kamuyu Aydınlatma Platformu) sitesinden yeni bildirimleri tespit eden
    /// ve bildirim eklerinin (PDF) linklerini çözen servisin sözleşmesi.
    ///
    /// NOT (Jüriye açıklama): KAP'ın MKK tarafından sağlanan resmi REST API'si
    /// (bkz. MKK_Api dokümanı) sadece test ortamında (apigwdev.mkk.com.tr) çalışır
    /// ve sabit bir aralıkta (1091689-1231017) DONMUŞ TARİHSEL veri döner — canlı/güncel
    /// bildirim akışı için uygun değildir; canlı erişim için MKK'ya ayrıca ticari
    /// başvuru gerekir. Bu nedenle SPK Bülten Analiz projesindeki SpkScraperService
    /// ile AYNI MİMARİ YAKLAŞIM izlenerek, KAP'ın kendi web sitesinin (kap.org.tr)
    /// herkese açık, kimlik doğrulama gerektirmeyen JSON uç noktaları kullanılır.
    /// Bu uç noktalar KAP'ın kendi web arayüzünün de kullandığı, tarayıcı ile
    /// erişilebilen ve KVKK kapsamında zaten kamuya açık olan verilerdir.
    /// </summary>
    public interface IKapScraperService
    {
        /// <summary>
        /// Belirtilen tarih aralığında yayınlanmış bildirimleri KAP'tan çeker.
        /// Varsayılan olarak son 1 gün taranır (periyodik arka plan taraması için).
        /// </summary>
        Task<List<TespitEdilenBildirim>> YeniBildirimleriTespitEtAsync(
            DateTime? baslangic = null,
            DateTime? bitis = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Tek bir bildirimin tam metnini (HTML gövdesi + varsa ek dosya listesi)
        /// KAP'ın detay uç noktasından çeker. "Bir haberi yeniden analiz et" akışında
        /// ve ilk kayıt sırasında kullanılır.
        /// </summary>
        Task<BildirimDetayi?> BildirimDetayiGetirAsync(long disclosureIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bir bildirim ekinin GERÇEK PDF baytlarını KAP'tan indirir. KAP'ın
        /// /tr/api/file/download/{objId} uç noktası PDF'i Java'ya özgü bir
        /// byte[] serileştirme sarmalayıcısı içinde döndürür; bu metot o
        /// sarmalayıcıyı çözüp saf PDF baytlarını döner. disclosureIndex,
        /// KAP'ın gerektirdiği Referer header'ını doğru kurabilmek için gerekir.
        /// Başarısız olursa (WAF, format değişikliği vb.) null döner — çağıran
        /// taraf bu durumda kullanıcıyı bildirim sayfasına yönlendirmelidir.
        /// </summary>
        Task<byte[]?> PdfIndirAsync(string objId, long disclosureIndex, CancellationToken cancellationToken = default);
    }

    /// <summary>KAP bildirim listesinden tespit edilen özet bilgi.</summary>
    public class TespitEdilenBildirim
    {
        public long DisclosureIndex { get; set; }
        public string SirketAdi { get; set; } = string.Empty;
        public string HisseKodu { get; set; } = string.Empty;
        public string Konu { get; set; } = string.Empty;
        public string BildirimSinifi { get; set; } = string.Empty;
        public DateTime YayinTarihi { get; set; }
    }

    /// <summary>Bir bildirimin tam detayı — analiz için Gemini'ye gönderilecek metni içerir.</summary>
    public class BildirimDetayi
    {
        public long DisclosureIndex { get; set; }
        public string SirketAdi { get; set; } = string.Empty;
        public string HisseKodu { get; set; } = string.Empty;
        public string Konu { get; set; } = string.Empty;

        /// <summary>HTML etiketlerinden ayıklanmış, Gemini'ye gönderilmeye hazır düz metin.</summary>
        public string DuzMetin { get; set; } = string.Empty;

        /// <summary>Bildirimin KAP sitesindeki orijinal sayfasının linki.</summary>
        public string KapLinki { get; set; } = string.Empty;

        /// <summary>Bildirime ait PDF ekleri (varsa).</summary>
        public List<BildirimEkiDto> Ekler { get; set; } = new();
    }

    public class BildirimEkiDto
    {
        public string DosyaAdi { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        /// <summary>KAP'ın attachment-detail yanıtındaki ham "objId" değeri.</summary>
        public string? ObjId { get; set; }
    }
}
