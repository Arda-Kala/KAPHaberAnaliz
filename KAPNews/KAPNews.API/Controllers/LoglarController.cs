using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KAPNews.API.Controllers
{
    /// <summary>
    /// Admin panelinin "son sistem logları" görünümü için kullanılır.
    ///
    /// v2.0.0 DÜZELTMESİ: Eskiden burada DI'a hiç kaydedilmemiş bir ILogService
    /// enjekte ediliyordu — bu uç nokta çağrıldığı an "no service registered"
    /// hatasıyla çökerdi (asla test edilmemiş, ölü kod). Artık gerçek log
    /// kaynağı olan Serilog dosya sink'inden (logs/kap-haberleri-analiz-*.log)
    /// son N satırı okuyarak çalışır — ayrı bir bellek-içi log deposu tutmaya
    /// gerek kalmaz, TEK bir doğru kaynak (Serilog) korunur.
    /// </summary>
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class LoglarController : ControllerBase
    {
        private readonly ILogger<LoglarController> _logger;

        public LoglarController(ILogger<LoglarController> logger)
        {
            _logger = logger;
        }

        /// <summary>Bugünün log dosyasından son <paramref name="satirSayisi"/> satırı döner.</summary>
        [HttpGet]
        public IActionResult Getir([FromQuery] int satirSayisi = 100)
        {
            try
            {
                var logKlasoru = Path.Combine(AppContext.BaseDirectory, "logs");
                if (!Directory.Exists(logKlasoru))
                    return Ok(Array.Empty<string>());

                var bugunkuDosya = Directory.GetFiles(logKlasoru, "kap-haberleri-analiz-*.log")
                    .OrderByDescending(f => System.IO.File.GetLastWriteTimeUtc(f))
                    .FirstOrDefault();

                if (bugunkuDosya == null)
                    return Ok(Array.Empty<string>());

                // Dosya Serilog tarafından sürekli açık/yazılıyor olabileceğinden,
                // paylaşımlı okuma modu (FileShare.ReadWrite) ile açıyoruz.
                using var stream = new FileStream(bugunkuDosya, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var satirlar = new List<string>();
                string? satir;
                while ((satir = reader.ReadLine()) != null)
                    satirlar.Add(satir);

                var sonSatirlar = satirlar.Skip(Math.Max(0, satirlar.Count - satirSayisi)).ToList();
                return Ok(sonSatirlar);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Log dosyası okunurken hata oluştu.");
                return StatusCode(500, "Log dosyası okunamadı.");
            }
        }
    }
}
