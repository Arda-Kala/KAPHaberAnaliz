// KAPNews.Business.Concrete.OtoAnalizServisi.cs
using KAPNews.DataAccess.Abstract;
using KAPNews.Business.Abstract;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace KAPNews.Business.Concrete
{
    public class OtoAnalizServisi : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<OtoAnalizServisi> _logger;
        private const string CACHE_KEY = "tumHaberlerListesi";
        private const int TOPLU_ISTEK_HABER_ESIGI = 5;
        private static readonly TimeSpan MAKSIMUM_BEKLEME_SURESI = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan KONTROL_ARALIGI = TimeSpan.FromSeconds(60);
        private DateTime _sonTopluIstekZamani = DateTime.Now;

        public OtoAnalizServisi(IServiceScopeFactory scopeFactory, IMemoryCache memoryCache, ILogger<OtoAnalizServisi> logger)
        {
            _scopeFactory = scopeFactory;
            _memoryCache = memoryCache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Scope açarak DI container'dan servisleri güvenle alıyoruz
                    using var scope = _scopeFactory.CreateScope();
                    var haberRepository = scope.ServiceProvider.GetRequiredService<IHaberRepository>();
                    var haberService = scope.ServiceProvider.GetRequiredService<IHaberService>();

                    var bekleyenHaberler = haberRepository.BekleyenAnalizHaberleriniGetir();

                    bool bekleyenHaberVar = bekleyenHaberler.Count > 0;
                    bool esikDoldu = bekleyenHaberler.Count >= TOPLU_ISTEK_HABER_ESIGI;
                    bool sureDoldu = (DateTime.Now - _sonTopluIstekZamani) >= MAKSIMUM_BEKLEME_SURESI;

                    if (bekleyenHaberVar && (esikDoldu || sureDoldu))
                    {
                        var gonderilecekGrup = bekleyenHaberler.Take(TOPLU_ISTEK_HABER_ESIGI).ToList();

                        await haberService.HaberleriTopluYorumlaAsync(gonderilecekGrup);
                        _sonTopluIstekZamani = DateTime.Now;
                        _memoryCache.Remove(CACHE_KEY);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[AI Arka Plan Motoru] Toplu analiz sırasında beklenmeyen hata oluştu.");
                }

                await Task.Delay(KONTROL_ARALIGI, stoppingToken);
            }
        }
    }
}