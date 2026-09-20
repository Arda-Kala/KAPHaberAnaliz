// KAPNews.API.Controllers.HaberlerController.cs
using KAPNews.Entities;
using KAPNews.Business.Abstract;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
namespace KAPNews.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HaberlerController : ControllerBase
{
    private readonly IHaberService _haberService;
    private readonly IKapScraperService _kapScraperService;
    private readonly IGunlukPiyasaSkoruServisi _piyasaSkoruServisi;
    private readonly IMemoryCache _memoryCache; // 2. Bellek yöneticisi tanımı
    private readonly ILogger<HaberlerController> _logger;

    // 🌟 DÜZELTME: Cache key'ini tek bir yerde sabit olarak tutuyoruz.
    // Böylece her metotta aynı anahtarı kullanıp cache'i doğru temizleyebiliyoruz.
    private const string CACHE_KEY = "tumHaberlerListesi";

    public HaberlerController(
        IHaberService haberService,
        IKapScraperService kapScraperService,
        IGunlukPiyasaSkoruServisi piyasaSkoruServisi,
        IMemoryCache memoryCache,
        ILogger<HaberlerController> logger)
    {
        _haberService = haberService;
        _kapScraperService = kapScraperService;
        _piyasaSkoruServisi = piyasaSkoruServisi;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    // 1. GET: api/haberler (Önbellek destekli listeleme)
    [HttpGet]
    public IActionResult HepsiniGetir()
    {
        if (!_memoryCache.TryGetValue(CACHE_KEY, out List<Haber>? cachedHaberler))
        {
            cachedHaberler = _haberService.TumHaberleriGetir();
            var cacheSeçenekleri = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            _memoryCache.Set(CACHE_KEY, cachedHaberler, cacheSeçenekleri);
        }
        return Ok(cachedHaberler);
    }

    // 2. GET: api/haberler/5 (ID'ye göre tek bir haber getirir)
    [HttpGet("{id}")]
    public IActionResult IdIleGetir(int id)
    {
        var haber = _haberService.HaberGetir(id);
        if (haber == null) return NotFound("Haber bulunamadı.");
        return Ok(haber);
    }

    // 3. POST: api/haberler (Yeni haber ekler)
    [Authorize(Roles = "Admin")]
    [HttpPost]
    public IActionResult Ekle([FromBody] Haber haber)
    {
        _haberService.HaberEkle(haber);

        // 🌟 DÜZELTME: Veri değiştiği için eski önbelleği (cache) siliyoruz.
        // Silmezsek, kullanıcı 5 dakika boyunca yeni haberi ekranda göremez!
        _memoryCache.Remove(CACHE_KEY);

        return StatusCode(201, haber); // HTTP 201 Created
    }

    // 4. PUT: api/haberler (Haberi günceller)
    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public IActionResult Guncelle(int id, [FromBody] Haber haber)
    {
        if (id != haber.Id) return BadRequest();
        _haberService.HaberGuncelle(haber);

        // 🌟 DÜZELTME: Yapay zeka yorumu tam olarak bu metotla veritabanına yazılıyor.
        // Cache'i temizlemezsek, ekrandaki "Gemini ile konuşuyor, lütfen bekleyin..."
        // yazısı hiç değişmez; çünkü GET isteği hep eski (boş) veriyi döndürür.
        _memoryCache.Remove(CACHE_KEY);

        return NoContent();
    }

    // 5. DELETE: api/haberler/5 (Haber siler)
    // Güvenlik Koruması: Sadece Secret-Admin-Key gönderenler silebilir.
    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public IActionResult Sil(int id)
    {
       
        _haberService.HaberSil(id);
        _memoryCache.Remove(CACHE_KEY);
        return Ok("Haber başarıyla silindi.");
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("tetikle-kap")]
    public async Task<IActionResult> AnlikKapTetikle()
    {
        var eklenenSayi = await _haberService.KapVerileriniAnlikCekAndKaydetAsync();
        _memoryCache.Remove(CACHE_KEY); // Cache'i temizliyoruz
        return Ok(new { message = "KAP senkronizasyonu başarıyla çalıştırıldı.", eklenenHaberSayisi = eklenenSayi });
    }

    // 🚀 SPRINT 2 YILDIZI: api/haberler/yorumla/5 (Yapay zekayı tetikler)
    [HttpPost("yorumla/{id}")]
    public async Task<IActionResult> Yorumla(int id)
    {
        try
        {
            var analizEdilmisHaber = await _haberService.HaberiYorumlaAsync(id);

            // 🌟 DÜZELTME: Bu uç nokta üzerinden manuel analiz yapıldığında da
            // eski önbelleği siliyoruz ki sonuç ekranda hemen görünsün.
            _memoryCache.Remove(CACHE_KEY);

            return Ok(analizEdilmisHaber);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message); // Bir hata olursa fırlat (HTTP 400)
        }
    }

    // v2.0.0: api/haberler/yeniden-tara/5 — "Bu haberi yeniden analiz et" butonu.
    // Haberi KAP'tan tekrar çeker (güncel metin/ek listesiyle) ve zorla yeniden
    // Gemini analizinden geçirir; Yorumla'dan farkı, zaten analiz edilmiş bir
    // haberi de tekrar işlemesidir.
    [Authorize(Roles = "Admin")]
    [HttpPost("yeniden-tara/{id}")]
    public async Task<IActionResult> YenidenTara(int id)
    {
        try
        {
            var haber = await _haberService.HaberiYenidenTaraVeAnalizEtAsync(id);
            if (haber == null) return NotFound("Haber bulunamadı.");

            _memoryCache.Remove(CACHE_KEY);
            return Ok(haber);
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    // v2.0.0: api/haberler/sirket-gecmisi/THYAO — şirket ismine tıklanınca
    // açılan "geçmiş bildirimler" listesi için kullanılır.
    [HttpGet("sirket-gecmisi/{hisseKodu}")]
    public IActionResult SirketGecmisi(string hisseKodu)
    {
        var gecmis = _haberService.SirketGecmisiniGetir(hisseKodu);
        return Ok(gecmis);
    }


    [HttpGet("ai-metrikleri")]
    public IActionResult GetAiMetrics()
    {
        var tumHaberler = _haberService.TumHaberleriGetir();

        var toplamAnalizEdilen = tumHaberler.Count(h => !string.IsNullOrEmpty(h.DuyguDurumu));
        var bekleyenHaberler = tumHaberler.Count(h => string.IsNullOrEmpty(h.DuyguDurumu));
        var basarisizHatalilar = tumHaberler.Count(h => h.AiYorumu != null && h.AiYorumu.Contains("Hata"));

        // Batch Processing sayesinde sağlanan tasarruf (Her 5 haber 1 istek sayıldı)
        var tasarrufEdilenIstekSayisi = toplamAnalizEdilen > 0 ? (toplamAnalizEdilen - Math.Ceiling(toplamAnalizEdilen / 5.0)) : 0;

        return Ok(new
        {
            toplamAnalizEdilen,
            bekleyenHaberler,
            basarisizHatalilar,
            tahminiGeminiIstekSayisi = Math.Ceiling(toplamAnalizEdilen / 5.0),
            tasarrufEdilenIstekSayisi
        });
    }
    // 🚀 TOPLU AI ANALİZİ TETİKLEME: api/haberler/ai-analiz-tetikle
    [Authorize(Roles = "Admin")]
    [HttpPost("ai-analiz-tetikle")]
    public async Task<IActionResult> AiAnalizTetikle()
    {
        try
        {
            // 1. Önbelleğe (Cache) takılmamak için doğrudan veritabanından tüm haberleri alıyoruz
            var tumHaberler = _haberService.TumHaberleriGetir();

            // 2. DuyguDurumu boş veya null olan (analiz edilmemiş) haberleri süzüyoruz
            var bekleyenHaberler = tumHaberler
                .Where(h => string.IsNullOrEmpty(h.DuyguDurumu) || h.DuyguDurumu == "Analiz Bekleniyor")
                .ToList();

            if (!bekleyenHaberler.Any())
            {
                return Ok(new { message = "Analiz bekleyen yeni bir haber bulunamadı.", analizEdilenSayi = 0 });
            }

            int analizEdilenSayi = 0;

            // 3. Her bir bekleyen haber için zaten var olan HaberiYorumlaAsync metodunu çağırıyoruz
            foreach (var haber in bekleyenHaberler)
            {
                try
                {
                    await _haberService.HaberiYorumlaAsync(haber.Id);
                    analizEdilenSayi++;
                }
                catch (Exception ex)
                {
                    // Tek bir haberde hata oluşursa diğer haberlerin analizini kesmemek için hatayı loglayıp devam ediyoruz
                    _logger.LogError(ex, "Haber ID {HaberId} analiz edilirken hata oluştu.", haber.Id);
                }
            }

            // 4. Veriler güncellendiği için listeleme önbelleğini (cache) temizliyoruz
            _memoryCache.Remove(CACHE_KEY);

            return Ok(new
            {
                message = $"{analizEdilenSayi} adet haber başarıyla yapay zeka tarafından analiz edildi.",
                analizEdilenSayi
            });
        }
        catch (Exception ex)
        {
            return BadRequest($"Toplu AI Analizi sırasında hata oluştu: {ex.Message}");
        }
    }

    // =====================================================================
    // 🌟 v2.1.0 — PDF EKİ PROXY: api/haberler/pdf-indir/{objId}
    //
    // Frontend'deki "Bildirim Ekleri" linkleri artık KAP bildirim sayfasına
    // DEĞİL, buraya işaret eder. Bu uç nokta KAP'ın Java-wrapped byte[]
    // formatındaki PDF'ini indirir (bkz. KapScraperService.PdfIndirAsync),
    // sarmalayıcıyı çözer ve tarayıcıya GERÇEK bir application/pdf yanıtı
    // olarak sunar — böylece kullanıcı linke tıklayınca doğrudan PDF açılır,
    // bildirim sayfasına yönlendirilmez.
    //
    // disclosureIndex query parametresi zorunludur çünkü KAP, bu isteğin
    // ilgili bildirim sayfasından geldiğini gösteren bir Referer header'ı
    // bekler (WAF kontrolü); onsuz istek reddedilebilir.
    //
    // Admin/kullanıcı ayrımı yok — herkes bildirim eklerini görüntüleyebilir,
    // tıpkı KAP'ın kendi sitesinde olduğu gibi (bu zaten kamuya açık veridir).
    // =====================================================================
    [HttpGet("pdf-indir/{objId}")]
    public async Task<IActionResult> PdfIndir(string objId, [FromQuery] long disclosureIndex)
    {
        if (string.IsNullOrWhiteSpace(objId) || disclosureIndex <= 0)
        {
            return BadRequest("Geçersiz PDF isteği: objId ve disclosureIndex gereklidir.");
        }

        try
        {
            var pdfBaytlari = await _kapScraperService.PdfIndirAsync(objId, disclosureIndex);

            if (pdfBaytlari == null || pdfBaytlari.Length == 0)
            {
                _logger.LogWarning("PDF indirilemedi, kullanıcı bildirim sayfasına yönlendiriliyor. ObjId: {ObjId}", objId);

                // PDF çekilemezse (WAF, format değişikliği vb.) kullanıcıyı boş
                // ekranda bırakmak yerine KAP'ın resmi bildirim sayfasına
                // yönlendiriyoruz — en azından ilgili bildirimi görebilsin.
                return Redirect($"https://www.kap.org.tr/tr/Bildirim/{disclosureIndex}");
            }

            // "inline" content-disposition: tarayıcı sekmesinde doğrudan PDF
            // görüntüleyici açılır, dosya indirme penceresi çıkmaz.
            Response.Headers.Append("Content-Disposition", "inline; filename=\"bildirim.pdf\"");
            return File(pdfBaytlari, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF indirme sırasında beklenmeyen hata oluştu. ObjId: {ObjId}", objId);
            return Redirect($"https://www.kap.org.tr/tr/Bildirim/{disclosureIndex}");
        }
    }

    // =====================================================================
    // 🌟 v2.2.0 — PDF EKİNİ ANALİZ EDİP YORUMU DETAYLANDIR
    // api/haberler/pdf-analiz-detaylandir/5
    //
    // Admin panelinde, haber kartı içinde "PDF Ekini Analiz Et" butonuna
    // basılınca çağrılır. Haberin bildirim eki PDF'ini KAP'tan indirip
    // Gemini'ye (metin + PDF) birlikte göndererek mevcut aiYorumu'nu daha
    // somut/detaylı bir versiyonla değiştirir.
    // =====================================================================
    [Authorize(Roles = "Admin")]
    [HttpPost("pdf-analiz-detaylandir/{id}")]
    public async Task<IActionResult> PdfAnaliziDetaylandir(int id)
    {
        try
        {
            var haber = await _haberService.PdfEkiniAnalizEdipDetaylandirAsync(id);
            if (haber == null) return NotFound("Haber bulunamadı.");

            _memoryCache.Remove(CACHE_KEY);
            return Ok(haber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF eki analiz edilirken hata oluştu. Haber ID: {Id}", id);
            return BadRequest($"PDF eki analiz edilirken hata oluştu: {ex.Message}");
        }
    }

    // =====================================================================
    // 🌟 v2.3.0 — GÜNLÜK PİYASA SKORU
    // api/haberler/piyasa-skoru
    //
    // Dashboard'daki "Genel Özet" panelinde gösterilen Piyasa Skoru artık
    // anlık hesaplanmaz; her gece 00:00'da hesaplanıp veritabanına yazılan
    // (bkz. GunlukPiyasaSkoruHesaplamaServisi) EN SON kapanmış günün skorunu
    // döner. Hiç hesaplanmış kayıt yoksa (uygulama ilk kez ayağa kalkıyorsa)
    // null alanlarla birlikte 200 döner — frontend "henüz hesaplanmadı" der.
    // =====================================================================
    [HttpGet("piyasa-skoru")]
    public IActionResult PiyasaSkoruGetir()
    {
        var skor = _piyasaSkoruServisi.SonGunlukSkoruGetir();
        if (skor == null)
        {
            return Ok(new { hesaplandiMi = false });
        }

        return Ok(new
        {
            hesaplandiMi = true,
            tarih = skor.Tarih,
            netSkor = skor.NetSkor,
            yon = skor.Yon,
            toplamHaberSayisi = skor.ToplamHaberSayisi,
            olumluSayisi = skor.OlumluSayisi,
            olumsuzSayisi = skor.OlumsuzSayisi,
            notrSayisi = skor.NotrSayisi
        });
    }
}
