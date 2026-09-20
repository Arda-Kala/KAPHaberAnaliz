// KAPNews.Business.Concrete.GunlukPiyasaSkoruHesaplamaServisi.cs
using KAPNews.Business.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KAPNews.Business.Concrete
{
    // =========================================================================
    // 🌟 v2.3.0 — Her gece TAM 00:00'da (yerel saat), bir önceki günün Piyasa
    // Skorunu hesaplayıp veritabanına yazan arka plan servisi.
    //
    // NEDEN GECE YARISI: O günün KAP bildirimleri artık kesinleşmiş (yeni haber
    // gelmeyecek) demektir — hesaplama "kapanmış" bir veri kümesi üzerinden
    // yapılır, bu da SPK Bülten projesindeki "bülten bazlı" (dönemi kapanmış)
    // skorlama mantığıyla birebir örtüşür. Anlık hesaplama yerine bu yaklaşım
    // tercih edilmiştir çünkü gün içinde sürekli değişen bir skor kullanıcı
    // için kafa karıştırıcı ve güvenilmez olur.
    //
    // Uygulama her başladığında da (deploy, restart) EKSİK GÜNLERİ TAMAMLAMAK
    // için dünün skorunu hemen bir kez hesaplar — böylece sunucu gece yarısı
    // kapalıyken bir gün "unutulmuş" olmaz.
    // =========================================================================
    public class GunlukPiyasaSkoruHesaplamaServisi : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GunlukPiyasaSkoruHesaplamaServisi> _logger;

        public GunlukPiyasaSkoruHesaplamaServisi(
            IServiceScopeFactory scopeFactory,
            ILogger<GunlukPiyasaSkoruHesaplamaServisi> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Başlangıçta: dünün skorunu hemen hesapla (eksik gün kalmasın).
            await GununSkorunuHesaplaAsync(DateTime.Now.Date.AddDays(-1), stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                var simdi = DateTime.Now;
                var birSonrakiGeceYarisi = simdi.Date.AddDays(1); // bugünün 00:00'ı geçtiyse yarının 00:00'ı
                var beklemeSuresi = birSonrakiGeceYarisi - simdi;

                try
                {
                    await Task.Delay(beklemeSuresi, stoppingToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    break; // Uygulama kapanıyor.
                }

                // Gece yarısı oldu: biten günün (bugüne kadar olan, artık "dün" olan) skorunu hesapla.
                await GununSkorunuHesaplaAsync(DateTime.Now.Date.AddDays(-1), stoppingToken).ConfigureAwait(false);
            }
        }

        private async Task GununSkorunuHesaplaAsync(DateTime gun, CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var skoruServisi = scope.ServiceProvider.GetRequiredService<IGunlukPiyasaSkoruServisi>();
                await skoruServisi.GununSkorunuHesaplaVeKaydetAsync(gun).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Arka plan servisi ASLA burada patlayıp tamamen durmamalı.
                _logger.LogError(ex, "[Piyasa Skoru Servisi] {Tarih:yyyy-MM-dd} için skor hesaplanırken hata oluştu.", gun);
            }
        }
    }
}
