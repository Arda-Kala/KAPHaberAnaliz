// KAPNews.Business.Concrete.KapTarayiciServisi.cs
using KAPNews.Business.Abstract;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace KAPNews.Business.Concrete
{
    // =========================================================================
    // 🌟 v2.0.0 — MKK API tabanlı KapVeriCekmeServisi'nin yerini alır.
    //
    // Bu servis, IKapScraperService (web scraping) üzerinden KAP'ın herkese açık
    // JSON uç noktalarını periyodik olarak tarar ve yeni bildirimleri veritabanına
    // ekler. Gerçek tarama/ayrıştırma mantığı KapScraperService'e, "hangi haberin
    // zaten kayıtlı olduğu" kontrolü ise IHaberRepository'ye devredilmiştir — bu
    // sınıfın TEK sorumluluğu "belirli aralıklarla tetikleme" yapmaktır (SRP).
    //
    // Dashboard'daki "Yeni Haberleri Tara" butonu da AYNI IHaberService metodunu
    // (KapVerileriniAnlikCekAndKaydetAsync) çağırır — böylece otomatik tarama ile
    // manuel tarama arasında davranış farkı olmaz, tek bir doğru kaynak (single
    // source of truth) korunur.
    // =========================================================================
    public class KapTarayiciServisi : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<KapTarayiciServisi> _logger;
        private const string CACHE_KEY = "tumHaberlerListesi";

        // KAP verileri her birkaç dakikada bir güncellenir; WAF'ı yormamak ve
        // gereksiz yük bindirmemek için 3 dakikalık bir tarama aralığı seçildi
        // (bkz. KAP_ENDPOINT_NOTES.md — "2 req/s" önerisi bu aralıkla uyumludur).
        private static readonly TimeSpan TaramaAraligi = TimeSpan.FromMinutes(3);

        public KapTarayiciServisi(
            IServiceScopeFactory scopeFactory,
            IMemoryCache memoryCache,
            ILogger<KapTarayiciServisi> logger)
        {
            _scopeFactory = scopeFactory;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Uygulama her ayağa kalktığında hemen bir tarama yap, sonra periyodik devam et.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var haberService = scope.ServiceProvider.GetRequiredService<IHaberService>();

                    var eklenenSayi = await haberService.KapVerileriniAnlikCekAndKaydetAsync().ConfigureAwait(false);

                    if (eklenenSayi > 0)
                    {
                        _memoryCache.Remove(CACHE_KEY);
                        _logger.LogInformation("[KAP Tarayıcı] {Sayi} yeni bildirim eklendi.", eklenenSayi);
                    }
                }
                catch (Exception ex)
                {
                    // Arka plan servisi ASLA burada patlayıp tamamen durmamalı —
                    // bir turdaki hata (örn. KAP WAF'ı geçici olarak isteği reddetti)
                    // sonraki turu etkilememeli.
                    _logger.LogError(ex, "[KAP Tarayıcı] Tarama sırasında beklenmeyen hata oluştu.");
                }

                await Task.Delay(TaramaAraligi, stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
