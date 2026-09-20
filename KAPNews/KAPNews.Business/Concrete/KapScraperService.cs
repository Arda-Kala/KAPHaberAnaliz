using KAPNews.Business.Abstract;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace KAPNews.Business.Concrete
{
    // =========================================================================
    // 🌟 SOLID (Jüriye Not): KAP web sitesiyle "konuşan" TEK sınıf burasıdır.
    // HTTP isteği kurma, JSON çözümleme ve HTML gövdesinden düz metin çıkarma
    // sorumluluğu sadece bu sınıfa aittir (SRP). KapVeriCekmeServisi (arka plan
    // işi) ve HaberlerController (manuel "yeniden analiz et" butonu) bu sınıfı
    // IKapScraperService arayüzü üzerinden kullanır; hangi URL'nin, hangi HTTP
    // metodunun kullanıldığını hiç bilmezler (DIP).
    //
    // SPK Bülten Analiz projesindeki SpkScraperService ile birebir aynı mimari
    // rolü oynar: "dış dünyadan veri çekme" sorumluluğunu tek bir yere hapseder.
    // =========================================================================
    public class KapScraperService : IKapScraperService
    {
        private const string BaseUrl = "https://www.kap.org.tr";
        private readonly HttpClient _httpClient;
        private readonly ILogger<KapScraperService> _logger;

        // =====================================================================
        // 🌟 HIZ SINIRLAMA (RATE LIMITING) — KAP'ın WAF'ı kısa sürede çok sayıda
        // istek atan istemcileri geçici olarak engelliyor (429 / bağlantı reddi).
        // Bu sınıftan KAP'a giden HER istek (liste taraması + N adet bildirim
        // detayı) buradaki tek, statik semafordan ve minimum bekleme süresinden
        // geçer. "static" olması kritik: uygulama içinde KapScraperService birden
        // fazla kez örneklense bile (örn. istek başına HttpClient enjeksiyonu),
        // KAP'a giden toplam istek hızı tek bir merkezden kontrol edilir.
        //
        // 2 istek/saniye KAP için güvenli kabul edilen üst sınırdır (bkz. sınıf
        // başındaki not); burada daha temkinli davranıp ardışık istekler arasına
        // en az 700ms koyuyoruz ve aynı anda sadece 1 isteğin gitmesine izin
        // veriyoruz (eşzamanlılık = 1) — yani istekler KUYRUKLANIR, paralel
        // ateşlenmez. Bu, "yeni haberleri tara" sırasında 20-30 bildirimin
        // detayının aynı anda çekilip WAF'ı tetiklemesini engeller.
        // =====================================================================
        private static readonly SemaphoreSlim _hizSinirlamaKilidi = new(1, 1);
        private static readonly TimeSpan MinimumIstekAraligi = TimeSpan.FromMilliseconds(700);
        private static DateTime _sonIstekZamaniUtc = DateTime.MinValue;

        public KapScraperService(HttpClient httpClient, ILogger<KapScraperService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // KAP'ın kendi web sitesinin JSON API'sini kullanıyoruz; bu API'ler
            // yalnızca kap.org.tr'nin kendi ön yüzünden gelen isteklere yanıt
            // verecek şekilde bir WAF (Web Application Firewall) ile korunuyor.
            // Referer ve User-Agent header'ları olmadan istekler reddedilir.
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) KAPHaberleriAnalizBotu/1.0 (+https://kap.org.tr)");
        }

        /// <summary>
        /// KAP'a giden TÜM HTTP isteklerinin geçmesi gereken tek kapı. İstekleri
        /// sıraya koyar, ardışık iki istek arasında en az <see cref="MinimumIstekAraligi"/>
        /// kadar bekler ve 429 (Too Many Requests) yanıtı gelirse KAP'ın verdiği
        /// Retry-After süresi kadar (yoksa artan bir süre) bekleyip otomatik olarak
        /// yeniden dener. Böylece "fazla istek yüzünden engellenme" sorunu tek bir
        /// merkezi noktada, tüm çağıranlar için (liste taraması + detay çekme)
        /// çözülmüş olur.
        /// </summary>
        private async Task<HttpResponseMessage> HizSinirliGonderAsync(
            Func<HttpRequestMessage> istekFactory,
            CancellationToken cancellationToken)
        {
            const int maksimumDeneme = 3;
            TimeSpan? bekleyecegimizSure = null;

            for (var deneme = 0; deneme <= maksimumDeneme; deneme++)
            {
                if (bekleyecegimizSure.HasValue)
                {
                    // 429 sonrası bekleme, kilit DIŞINDA yapılır ki bu süre zarfında
                    // kuyruktaki başka bir işlem (varsa) engellenmiş kalmasın; bir
                    // sonraki turda kilit yeniden alınıp tek istek olarak denenir.
                    await Task.Delay(bekleyecegimizSure.Value, cancellationToken).ConfigureAwait(false);
                    bekleyecegimizSure = null;
                }

                await _hizSinirlamaKilidi.WaitAsync(cancellationToken).ConfigureAwait(false);
                HttpResponseMessage yanit;
                try
                {
                    var gecenSure = DateTime.UtcNow - _sonIstekZamaniUtc;
                    if (gecenSure < MinimumIstekAraligi)
                    {
                        await Task.Delay(MinimumIstekAraligi - gecenSure, cancellationToken).ConfigureAwait(false);
                    }

                    using var istek = istekFactory();
                    yanit = await _httpClient.SendAsync(istek, cancellationToken).ConfigureAwait(false);
                    _sonIstekZamaniUtc = DateTime.UtcNow;
                }
                finally
                {
                    _hizSinirlamaKilidi.Release();
                }

                if (yanit.StatusCode != System.Net.HttpStatusCode.TooManyRequests || deneme == maksimumDeneme)
                {
                    return yanit;
                }

                // KAP Retry-After header'ı gönderdiyse ona uyulur; göndermediyse
                // deneme sayısına göre artan bir bekleme (2sn, 4sn, 8sn) uygulanır.
                bekleyecegimizSure = yanit.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(Math.Pow(2, deneme + 1));

                _logger.LogWarning(
                    "[KAP Hız Sınırı] 429 alındı, {Bekleme}sn beklenip yeniden denenecek (deneme {Deneme}/{Maks}).",
                    bekleyecegimizSure.Value.TotalSeconds, deneme + 1, maksimumDeneme);

                yanit.Dispose();
            }

            // Buraya teorik olarak hiç düşülmez (döngü her zaman return veya devam eder),
            // ama derleyiciyi memnun etmek için son bir deneme sonucu döndürülür.
            using var sonIstek = istekFactory();
            return await _httpClient.SendAsync(sonIstek, cancellationToken).ConfigureAwait(false);
        }

        public async Task<List<TespitEdilenBildirim>> YeniBildirimleriTespitEtAsync(
            DateTime? baslangic = null,
            DateTime? bitis = null,
            CancellationToken cancellationToken = default)
        {
            var sonuc = new List<TespitEdilenBildirim>();

            // Varsayılan: son 24 saat (periyodik tarama). Manuel/backfill için
            // parametre ile daha geniş bir aralık verilebilir.
            var bitisTarihi = bitis ?? DateTime.UtcNow;
            var baslangicTarihi = baslangic ?? bitisTarihi.AddDays(-1);

            try
            {
                HttpRequestMessage IstekOlustur()
                {
                    var istek = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/tr/api/disclosure/members/byCriteria");
                    istek.Headers.Referrer = new Uri($"{BaseUrl}/tr/bildirim-sorgu");
                    istek.Content = JsonContent.Create(new
                    {
                        fromDate = baslangicTarihi.ToString("yyyy-MM-dd"),
                        toDate = bitisTarihi.ToString("yyyy-MM-dd"),
                        mkkMemberOidList = Array.Empty<string>(),
                        subjectList = Array.Empty<string>()
                    });
                    return istek;
                }

                using var yanit = await HizSinirliGonderAsync(IstekOlustur, cancellationToken).ConfigureAwait(false);

                if (!yanit.IsSuccessStatusCode)
                {
                    _logger.LogWarning("KAP bildirim listesi alınamadı. HTTP {StatusCode}", (int)yanit.StatusCode);
                    return sonuc;
                }

                var json = await yanit.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var kayitlar = JsonSerializer.Deserialize<List<KapDisclosureListItem>>(json, JsonAyarlari);

                if (kayitlar == null) return sonuc;

                foreach (var kayit in kayitlar)
                {
                    if (string.IsNullOrWhiteSpace(kayit.RelatedStocks) && string.IsNullOrWhiteSpace(kayit.StockCodes))
                        continue; // Şirket/hisse ile ilişkilendirilemeyen genel duyuruları atla (fon eşik bildirimleri vb.)

                    var hisseKodu = (kayit.RelatedStocks ?? kayit.StockCodes ?? string.Empty).Split(',').FirstOrDefault()?.Trim() ?? string.Empty;

                    sonuc.Add(new TespitEdilenBildirim
                    {
                        DisclosureIndex = kayit.DisclosureIndex,
                        SirketAdi = kayit.KapTitle ?? string.Empty,
                        HisseKodu = hisseKodu,
                        Konu = kayit.Subject ?? kayit.Summary ?? string.Empty,
                        BildirimSinifi = kayit.DisclosureClass ?? string.Empty,
                        YayinTarihi = TarihiAyristir(kayit.PublishDate)
                    });
                }

                _logger.LogInformation("KAP taramasında {Sayi} bildirim tespit edildi ({Baslangic} - {Bitis}).",
                    sonuc.Count, baslangicTarihi.ToString("yyyy-MM-dd"), bitisTarihi.ToString("yyyy-MM-dd"));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "KAP bildirim listesi çekilirken ağ hatası oluştu.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "KAP bildirim listesi JSON yanıtı çözümlenemedi — site yapısı değişmiş olabilir.");
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("KAP bildirim listesi isteği zaman aşımına uğradı veya iptal edildi.");
            }

            return sonuc;
        }

        public async Task<BildirimDetayi?> BildirimDetayiGetirAsync(long disclosureIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                HttpRequestMessage IstekOlustur()
                {
                    var istek = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/tr/api/notification/attachment-detail/{disclosureIndex}");
                    istek.Headers.Referrer = new Uri($"{BaseUrl}/tr/Bildirim/{disclosureIndex}");
                    return istek;
                }

                using var yanit = await HizSinirliGonderAsync(IstekOlustur, cancellationToken).ConfigureAwait(false);

                if (!yanit.IsSuccessStatusCode)
                {
                    _logger.LogWarning("KAP bildirim detayı alınamadı. DisclosureIndex: {Index}, HTTP {StatusCode}",
                        disclosureIndex, (int)yanit.StatusCode);
                    return null;
                }

                var json = await yanit.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var kayitlar = JsonSerializer.Deserialize<List<KapDetailResponseItem>>(json, JsonAyarlari);
                var kayit = kayitlar?.FirstOrDefault();

                if (kayit?.Disclosure?.DisclosureBasic == null) return null;

                var temel = kayit.Disclosure.DisclosureBasic;
                var duzMetin = HtmlGovdesindenDuzMetneCevir(kayit.DisclosureBody);

                var detay = new BildirimDetayi
                {
                    DisclosureIndex = disclosureIndex,
                    SirketAdi = temel.CompanyTitle ?? string.Empty,
                    HisseKodu = (temel.RelatedStocks ?? temel.StockCode ?? string.Empty).Split(',').FirstOrDefault()?.Trim() ?? string.Empty,
                    Konu = temel.Summary ?? temel.Title ?? string.Empty,
                    DuzMetin = duzMetin,
                    KapLinki = $"{BaseUrl}/tr/Bildirim/{disclosureIndex}",
                    // 🌟 DÜZELTME (v2.1.0): Artık kullanıcıyı bildirim sayfasına DEĞİL,
                    // kendi backend'imizdeki PDF proxy uç noktasına yönlendiriyoruz.
                    // KapAttachment.ObjId doluysa GERÇEK PDF servis edilir (bkz.
                    // HaberlerController.PdfIndir + KapScraperService.PdfIndirAsync);
                    // ObjId boşsa (KAP formatı değiştiyse) yine de kullanıcı için
                    // en azından çalışan bir yere gitsin diye bildirim sayfasına düşülür.
                    Ekler = (kayit.Attachments ?? new List<KapAttachment>())
                        .Select(ek => new BildirimEkiDto
                        {
                            DosyaAdi = ek.FileName ?? "ek.pdf",
                            ObjId = ek.ObjId,
                            Url = !string.IsNullOrWhiteSpace(ek.ObjId)
                                ? $"/api/Haberler/pdf-indir/{ek.ObjId}?disclosureIndex={disclosureIndex}"
                                : $"{BaseUrl}/tr/Bildirim/{disclosureIndex}"
                        })
                        .ToList()
                };

                return detay;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "KAP bildirim detayı çekilirken ağ hatası oluştu. DisclosureIndex: {Index}", disclosureIndex);
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "KAP bildirim detayı JSON yanıtı çözümlenemedi. DisclosureIndex: {Index}", disclosureIndex);
                return null;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("KAP bildirim detayı isteği zaman aşımına uğradı. DisclosureIndex: {Index}", disclosureIndex);
                return null;
            }
        }

        // =====================================================================
        // 🌟 v2.1.0 — PDF EKİ İNDİRME (Java-wrapped byte[] çözümü)
        //
        // KAP'ın /tr/api/file/download/{objId} uç noktası PDF'i çıplak bayt
        // olarak değil, Java'nın kendi nesne serileştirme formatıyla (ObjectOutputStream
        // ile yazılmış bir byte[]) sarmalayıp döndürür. Format sabit ve basittir:
        //
        //   Offset 0  : AC ED 00 05           -> Java serialization magic + version
        //   Offset 4  : 75                    -> TC_ARRAY tipi işareti
        //   Offset 5  : 72 00 02 5B 42 ...     -> "[B" (byte dizisi) için sınıf tanımı
        //   ...       : classDesc alanları, serialVersionUID vb. (sabit uzunlukta değil
        //               ama her zaman 0x78 0x70 (TC_ENDBLOCKDATA + TC_NULL) ile biter)
        //   +0        : 4 bayt big-endian uzunluk (asıl PDF'in bayt sayısı)
        //   +4..      : GERÇEK PDF baytları (%PDF-1.x ile başlar)
        //
        // Bu yüzden 0x78 0x70 imzasını arayıp ondan SONRAKİ 4 baytı uzunluk olarak
        // okuyoruz, sonrasındaki o kadar baytı da gerçek PDF içeriği olarak alıyoruz.
        // Ayrıştırma başarısız olursa (KAP formatı değiştirdiyse) null döneriz ki
        // çağıran taraf kullanıcıyı güvenli bir şekilde bildirim sayfasına yönlendirsin.
        // =====================================================================
        public async Task<byte[]?> PdfIndirAsync(string objId, long disclosureIndex, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(objId)) return null;

            try
            {
                HttpRequestMessage IstekOlustur()
                {
                    var istek = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/tr/api/file/download/{objId}");
                    istek.Headers.Referrer = new Uri($"{BaseUrl}/tr/Bildirim/{disclosureIndex}");
                    return istek;
                }

                using var yanit = await HizSinirliGonderAsync(IstekOlustur, cancellationToken).ConfigureAwait(false);

                if (!yanit.IsSuccessStatusCode)
                {
                    _logger.LogWarning("KAP PDF indirme başarısız. ObjId: {ObjId}, HTTP {StatusCode}", objId, (int)yanit.StatusCode);
                    return null;
                }

                var hamBaytlar = await yanit.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

                // Bazı durumlarda KAP hâlâ ham/çıplak PDF döndürebilir (ör. ileride
                // format değişirse) — bu durumu da tolere edelim, gereksiz yere
                // Java sarmalayıcı arayıp başarısız olmayalım.
                if (hamBaytlar.Length >= 4 &&
                    hamBaytlar[0] == (byte)'%' && hamBaytlar[1] == (byte)'P' &&
                    hamBaytlar[2] == (byte)'D' && hamBaytlar[3] == (byte)'F')
                {
                    return hamBaytlar;
                }

                return JavaByteDizisindenPdfCikar(hamBaytlar);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "KAP PDF indirilirken ağ hatası oluştu. ObjId: {ObjId}", objId);
                return null;
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("KAP PDF indirme isteği zaman aşımına uğradı. ObjId: {ObjId}", objId);
                return null;
            }
        }

        /// <summary>
        /// KAP'ın Java ObjectOutputStream ile sarmalanmış byte[] yanıtından gerçek
        /// PDF baytlarını çıkarır. Sarmalayıcının class-descriptor kısmı, "[B" sınıf
        /// adının ardından TC_ENDBLOCKDATA(0x78) + TC_NULL(0x79 DEĞİL, 0x70) ikilisiyle
        /// biter; hemen ardından 4 baytlık big-endian dizi uzunluğu, sonra da dizinin
        /// kendisi (bizim durumumuzda PDF baytları) gelir.
        /// </summary>
        private byte[]? JavaByteDizisindenPdfCikar(byte[] ham)
        {
            try
            {
                // TC_ENDBLOCKDATA (0x78) + TC_NULL (0x70) imzasını en az offset 10'dan
                // itibaren ara (class descriptor'ın erken kısmında yanlışlıkla
                // eşleşmemesi için — gerçek imza gözlemlere göre offset ~21'de).
                var imzaIndex = -1;
                for (var i = 10; i < ham.Length - 5; i++)
                {
                    if (ham[i] == 0x78 && ham[i + 1] == 0x70)
                    {
                        imzaIndex = i;
                        break;
                    }
                }

                if (imzaIndex < 0)
                {
                    _logger.LogWarning("KAP PDF sarmalayıcısında beklenen Java imzası (0x78 0x70) bulunamadı — format değişmiş olabilir.");
                    return null;
                }

                var uzunlukOffset = imzaIndex + 2;
                if (uzunlukOffset + 4 > ham.Length)
                {
                    _logger.LogWarning("KAP PDF sarmalayıcısı beklenenden kısa — uzunluk alanı okunamadı.");
                    return null;
                }

                // Big-endian 4 bayt uzunluk (Java int, network byte order).
                var dizininUzunlugu =
                    (ham[uzunlukOffset] << 24) |
                    (ham[uzunlukOffset + 1] << 16) |
                    (ham[uzunlukOffset + 2] << 8) |
                    ham[uzunlukOffset + 3];

                var pdfBaslangic = uzunlukOffset + 4;
                if (dizininUzunlugu <= 0 || pdfBaslangic + dizininUzunlugu > ham.Length)
                {
                    _logger.LogWarning("KAP PDF sarmalayıcısından okunan uzunluk ({Uzunluk}) geçersiz veya veri sınırlarını aşıyor.", dizininUzunlugu);
                    return null;
                }

                var pdfBaytlari = new byte[dizininUzunlugu];
                Array.Copy(ham, pdfBaslangic, pdfBaytlari, 0, dizininUzunlugu);

                // Son bir doğrulama: gerçekten "%PDF" ile başlıyor mu?
                if (pdfBaytlari.Length < 4 || pdfBaytlari[0] != (byte)'%' || pdfBaytlari[1] != (byte)'P'
                    || pdfBaytlari[2] != (byte)'D' || pdfBaytlari[3] != (byte)'F')
                {
                    _logger.LogWarning("Java sarmalayıcıdan çıkarılan veri geçerli bir PDF başlığıyla başlamıyor.");
                    return null;
                }

                return pdfBaytlari;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "KAP PDF Java sarmalayıcısı çözümlenirken beklenmeyen hata oluştu.");
                return null;
            }
        }

        // =====================================================================
        // YARDIMCI METOTLAR
        // =====================================================================

        private static readonly JsonSerializerOptions JsonAyarlari = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static DateTime TarihiAyristir(string? kapTarihFormati)
        {
            // KAP iki farklı tarih formatı kullanabiliyor: "26.05.2026 09:10:35" (liste)
            // veya "2026.05.26 09:10:35" (detay). İkisini de destekliyoruz.
            if (string.IsNullOrWhiteSpace(kapTarihFormati)) return DateTime.UtcNow;

            string[] formatlar = { "dd.MM.yyyy HH:mm:ss", "yyyy.MM.dd HH:mm:ss", "dd.MM.yyyy", "yyyy.MM.dd" };
            foreach (var format in formatlar)
            {
                if (DateTime.TryParseExact(kapTarihFormati, format,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var sonuc))
                {
                    return sonuc;
                }
            }

            return DateTime.TryParse(kapTarihFormati, out var genelSonuc) ? genelSonuc : DateTime.UtcNow;
        }

        /// <summary>
        /// KAP'ın döndürdüğü HTML tablo/gövde parçalarından, Gemini'ye gönderilmeye
        /// uygun düz metin çıkarır. Tam bir HTML parser (HtmlAgilityPack) kullanmak
        /// yerine basit regex tabanlı temizlik yeterlidir çünkü amaç sadece anlam
        /// taşıyan metni ayıklamaktır, DOM yapısını korumak değildir.
        /// </summary>
        private static string HtmlGovdesindenDuzMetneCevir(List<string>? disclosureBody)
        {
            if (disclosureBody == null || disclosureBody.Count == 0) return string.Empty;

            var birlesikHtml = string.Join("\n", disclosureBody);

            // Etiketler arasına boşluk koyarak kelimelerin birbirine yapışmasını önle.
            var etiketsiz = Regex.Replace(birlesikHtml, "<[^>]+>", " ");
            // HTML entity'lerini çöz (temel olanlar).
            etiketsiz = etiketsiz
                .Replace("&nbsp;", " ")
                .Replace("&amp;", "&")
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"");
            // Çoklu boşlukları teke indir.
            etiketsiz = Regex.Replace(etiketsiz, @"\s+", " ").Trim();

            return etiketsiz;
        }

        // =====================================================================
        // KAP JSON YANIT MODELLERİ (sadece bu sınıf içinde kullanılan, dış dünyaya
        // sızdırılmayan iç DTO'lar — bu yüzden internal/private sınıflar olarak
        // tutulur; HaberlerController veya başka bir katman bunları asla görmez).
        // =====================================================================

        private class KapDisclosureListItem
        {
            [JsonPropertyName("publishDate")] public string? PublishDate { get; set; }
            [JsonPropertyName("kapTitle")] public string? KapTitle { get; set; }
            [JsonPropertyName("disclosureClass")] public string? DisclosureClass { get; set; }
            [JsonPropertyName("subject")] public string? Subject { get; set; }
            [JsonPropertyName("summary")] public string? Summary { get; set; }
            [JsonPropertyName("relatedStocks")] public string? RelatedStocks { get; set; }
            [JsonPropertyName("stockCodes")] public string? StockCodes { get; set; }
            [JsonPropertyName("disclosureIndex")] public long DisclosureIndex { get; set; }
        }

        private class KapDetailResponseItem
        {
            [JsonPropertyName("disclosure")] public KapDisclosureWrapper? Disclosure { get; set; }
            [JsonPropertyName("disclosureBody")] public List<string>? DisclosureBody { get; set; }
            [JsonPropertyName("attachments")] public List<KapAttachment>? Attachments { get; set; }
        }

        private class KapDisclosureWrapper
        {
            [JsonPropertyName("disclosureBasic")] public KapDisclosureBasic? DisclosureBasic { get; set; }
        }

        private class KapDisclosureBasic
        {
            [JsonPropertyName("title")] public string? Title { get; set; }
            [JsonPropertyName("companyTitle")] public string? CompanyTitle { get; set; }
            [JsonPropertyName("stockCode")] public string? StockCode { get; set; }
            [JsonPropertyName("relatedStocks")] public string? RelatedStocks { get; set; }
            [JsonPropertyName("summary")] public string? Summary { get; set; }
        }

        private class KapAttachment
        {
            [JsonPropertyName("objId")] public string? ObjId { get; set; }
            [JsonPropertyName("fileName")] public string? FileName { get; set; }
        }
    }
}
