using KAPNews.Business.Abstract;
using KAPNews.DataAccess.Abstract;
using KAPNews.Entities;
using Microsoft.Extensions.Logging;

namespace KAPNews.Business.Concrete
{
    // =======================================================================
    // 🌟 v2.3.0 — GÜNLÜK PİYASA SKORU HESAPLAMA
    //
    // SPK Bülten projesindeki "Bülten Genel Skoru" mantığının KAP projesindeki
    // karşılığı. Orijinal mantık:
    //
    //   Her haber, YÖNÜNE göre işaretli bir katkı verir:
    //     Olumlu   → +EtkiSkoru
    //     Olumsuz  → -EtkiSkoru
    //     Nötr     →  0
    //   Bu katkıların ortalaması "Net Skor" olur (-100..+100 aralığında).
    //   |Net Skor| < 15 ise gün "Nötr" kabul edilir (eşik/ESIK mantığı).
    //   Net Skor'un işareti YÖNÜ, mutlak değeri GÜCÜ gösterir.
    //
    // NEDEN GÜNLÜK (ANLIK DEĞİL): Gün içinde sürekli yeni bildirim geldiği için
    // "o anki tüm haberlerin ortalaması" sürekli dalgalanan, tutarsız bir metrik
    // olur. Bu yüzden hesaplama SADECE kapanmış (dünkü veya daha eski) bir gün
    // için yapılır ve sonuç veritabanında "dondurulur" — GunlukPiyasaSkoruServisi
    // (BackgroundService) bunu her gece 00:00'da tetikler.
    // =======================================================================
    public class GunlukPiyasaSkoruManager : IGunlukPiyasaSkoruServisi
    {
        private readonly IHaberRepository _haberRepository;
        private readonly IGunlukPiyasaSkoruRepository _skoruRepository;
        private readonly ILogger<GunlukPiyasaSkoruManager> _logger;

        // SPK Bülten projesindeki ile birebir aynı eşik: |netSkor| bu değerin
        // altındaysa gün "Nötr" olarak sınıflandırılır.
        private const double NotrEsigi = 15.0;

        public GunlukPiyasaSkoruManager(
            IHaberRepository haberRepository,
            IGunlukPiyasaSkoruRepository skoruRepository,
            ILogger<GunlukPiyasaSkoruManager> logger)
        {
            _haberRepository = haberRepository;
            _skoruRepository = skoruRepository;
            _logger = logger;
        }

        public Task<GunlukPiyasaSkoru> GununSkorunuHesaplaVeKaydetAsync(DateTime gun)
        {
            var gunBaslangici = gun.Date;
            var gunSonu = gunBaslangici.AddDays(1);

            // O güne ait, YAPAY ZEKA TARAFINDAN ANALİZ EDİLMİŞ (DuyguDurumu dolu)
            // haberler alınır. Henüz analiz edilmemiş haberler hesaplamaya dahil
            // edilmez — aksi halde "Nötr" olmayan ama sadece bekleyen haberler
            // yanlışlıkla Nötr gibi sayılır ve skor yanıltıcı olur.
            var gununHaberleri = _haberRepository.HepsiniGetir()
                .Where(h => h.YayinlanmaTarihi >= gunBaslangici && h.YayinlanmaTarihi < gunSonu)
                .Where(h => !string.IsNullOrEmpty(h.DuyguDurumu) && h.EtkiSkoru.HasValue)
                .ToList();

            var kayit = new GunlukPiyasaSkoru
            {
                Tarih = gunBaslangici,
                ToplamHaberSayisi = gununHaberleri.Count
            };

            if (gununHaberleri.Count == 0)
            {
                kayit.NetSkor = 0;
                kayit.Yon = "Nötr";
                _logger.LogInformation("[Piyasa Skoru] {Tarih:yyyy-MM-dd} için analiz edilmiş haber bulunamadı, skor 0 olarak kaydedildi.", gunBaslangici);
            }
            else
            {
                double netToplam = 0;
                foreach (var haber in gununHaberleri)
                {
                    var katsayi = YonKatsayisi(haber.DuyguDurumu!);
                    netToplam += katsayi * (haber.EtkiSkoru ?? 0);

                    if (katsayi > 0) kayit.OlumluSayisi++;
                    else if (katsayi < 0) kayit.OlumsuzSayisi++;
                    else kayit.NotrSayisi++;
                }

                var netSkor = netToplam / gununHaberleri.Count; // -100..+100

                kayit.NetSkor = netSkor;
                kayit.Yon = netSkor >= NotrEsigi ? "Pozitif"
                    : netSkor <= -NotrEsigi ? "Olumsuz"
                    : "Nötr";

                _logger.LogInformation(
                    "[Piyasa Skoru] {Tarih:yyyy-MM-dd} hesaplandı: NetSkor={NetSkor:F1}, Yön={Yon}, Toplam={Toplam} haber.",
                    gunBaslangici, netSkor, kayit.Yon, gununHaberleri.Count);
            }

            _skoruRepository.EkleVeyaGuncelle(kayit);
            return Task.FromResult(kayit);
        }

        public GunlukPiyasaSkoru? SonGunlukSkoruGetir() => _skoruRepository.SonuncuyuGetir();

        // "Olumlu"/"Yüksek Olumlu" → +1, "Olumsuz"/"Yüksek Olumsuz" → -1, "Nötr"/"Etkisiz" → 0.
        private static int YonKatsayisi(string duyguDurumu) => duyguDurumu switch
        {
            "Olumlu" or "Yüksek Olumlu" => 1,
            "Olumsuz" or "Yüksek Olumsuz" => -1,
            _ => 0
        };
    }
}
