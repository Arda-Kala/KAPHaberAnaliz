using KAPNews.Entities;

namespace KAPNews.Business.Abstract
{
    // 🌟 v2.3.0 — Günlük Piyasa Skoru hesaplama ve okuma sözleşmesi.
    public interface IGunlukPiyasaSkoruServisi
    {
        /// <summary>
        /// Belirtilen günün (varsayılan: dün) TÜM analiz edilmiş haberlerinden
        /// SPK Bülten mantığındaki gibi bir Net Skor hesaplar ve veritabanına
        /// yazar (upsert). Arka plan servisi tarafından her gece 00:00'da
        /// otomatik çağrılır; admin panelinden manuel tetiklenebilir de.
        /// </summary>
        Task<GunlukPiyasaSkoru> GununSkorunuHesaplaVeKaydetAsync(DateTime gun);

        /// <summary>
        /// Dashboard'da gösterilecek en güncel (en son hesaplanmış) günlük
        /// skor kaydını döner. Hiç hesaplanmış kayıt yoksa null döner —
        /// bu durumda frontend "henüz hesaplanmadı" mesajı gösterebilir.
        /// </summary>
        GunlukPiyasaSkoru? SonGunlukSkoruGetir();
    }
}
