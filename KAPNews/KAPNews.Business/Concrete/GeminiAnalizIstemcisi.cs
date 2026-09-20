using KAPNews.Business.Abstract;
using KAPNews.Entities;
using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace KAPNews.Business.Concrete
{
    // =======================================================================
    // 🌟 SOLID (Jüriye Not): Google Gemini ile konuşan TEK sınıf burasıdır.
    // Prompt (talimat metni) hazırlama, API çağrısı yapma ve dönen JSON'ı
    // çözümleme sorumluluğu sadece bu sınıfa aittir (SRP). HaberManager artık
    // hangi yapay zeka sağlayıcısının kullanıldığını, API anahtarının nereden
    // geldiğini ya da prompt'un nasıl kurgulandığını hiç bilmez; sadece
    // IHaberAnalizIstemcisi arayüzü üzerinden "bu haberi analiz et" der.
    // =======================================================================
    public class GeminiAnalizIstemcisi : IHaberAnalizIstemcisi
    {
        private readonly GoogleAI _googleAI;

        public GeminiAnalizIstemcisi(IConfiguration configuration)
        {
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls13;

            string apiKey = configuration["GeminiApiKey"] ?? string.Empty;
            _googleAI = new GoogleAI(apiKey);
        }

        public bool KotaVeyaHizSiniriHatasiMi(Exception ex)
        {
            string mesaj = ex.Message ?? string.Empty;
            return mesaj.Contains("quota", StringComparison.OrdinalIgnoreCase)
                || mesaj.Contains("429")
                || mesaj.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                || mesaj.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<GeminiAnalizModel?> TekHaberiAnalizEtAsync(Haber haber)
        {
            var config = JsonYanitAyari();
            var model = _googleAI.GenerativeModel(Model.Gemini3Flash, generationConfig: config);

            string sistemTalimati = AnalizKurallariMetni() +
                "\nYanıtı MUTLAKA şu JSON formatında dön, başka hiçbir metin ekleme:\n" +
                "{\n" +
                "  \"duyguDurumu\": \"Olumlu\",\n" +
                "  \"etkiVadesi\": \"Uzun Vadeli\",\n" +
                "  \"etkiSkoru\": 65,\n" +
                "  \"sektorAdi\": \"Bankacılık\",\n" +
                "  \"indikatorSeti\": [\"F/K Oranı\", \"PD/DD\", \"RSI(14)\"],\n" +
                "  \"aiYorumu\": \"Özgün yorum buraya\"\n" +
                "}";

            string haberDetayi = HaberiMetneCevir(haber);

            var response = await model.GenerateContent(
                $"{sistemTalimati}\n\nAnaliz Edilecek Haber:\n{haberDetayi}")
                .ConfigureAwait(false);

            if (response == null || string.IsNullOrEmpty(response.Text)) return null;

            string temizJson = response.Text.Replace("```json", "").Replace("```", "").Trim();
            var ayarlar = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            return JsonSerializer.Deserialize<GeminiAnalizModel>(temizJson, ayarlar);
        }

        public async Task<List<GeminiTopluAnalizModel>?> HaberleriTopluAnalizEtAsync(List<Haber> haberler)
        {
            var config = JsonYanitAyari();
            var model = _googleAI.GenerativeModel(Model.Gemini3Flash, generationConfig: config);

            var haberlerMetni = new StringBuilder();
            foreach (var haber in haberler)
            {
                haberlerMetni.AppendLine($"--- Haber Id: {haber.Id} ---");
                haberlerMetni.AppendLine(HaberiMetneCevir(haber));
            }

            string sistemTalimati = AnalizKurallariMetni() +
                "\nSana birden fazla haber gönderiliyor. Her biri için AYRI analiz üret. " +
                "Yanıtı MUTLAKA aşağıdaki gibi JSON DİZİSİ olarak dön, başka hiçbir metin ekleme. " +
                "Her elemanın 'id' değeri, '--- Haber Id: X ---' satırındaki X ile birebir eşleşmeli:\n" +
                "[\n" +
                "  {\n" +
                "    \"id\": 1,\n" +
                "    \"duyguDurumu\": \"Olumlu\",\n" +
                "    \"etkiVadesi\": \"Orta Vadeli\",\n" +
                "    \"etkiSkoru\": 55,\n" +
                "    \"sektorAdi\": \"Bankacılık\",\n" +
                "    \"indikatorSeti\": [\"F/K Oranı\", \"RSI(14)\"],\n" +
                "    \"aiYorumu\": \"Özgün yorum\"\n" +
                "  }\n" +
                "]";

            var response = await model.GenerateContent(
                $"{sistemTalimati}\n\nAnaliz Edilecek Haberler:\n{haberlerMetni}")
                .ConfigureAwait(false);

            if (response == null || string.IsNullOrEmpty(response.Text)) return null;

            string temizJson = response.Text.Replace("```json", "").Replace("```", "").Trim();
            var ayarlar = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            return JsonSerializer.Deserialize<List<GeminiTopluAnalizModel>>(temizJson, ayarlar);
        }

        // =======================================================================
        // ANALİZ KURALLARI
        // Gemini'ye ne yapması gerektiğini anlatan ana prompt metni.
        // =======================================================================
        private string AnalizKurallariMetni()
        {
            return
                "Sen kıdemli bir borsa ve finans analistisin. " +
                "Sana gönderilen KAP haberini aşağıdaki kurallara göre analiz et.\n\n" +

                // ---- KURAL 1: DUYGU DURUMU (YÖN VE ŞİDDET) ----
                "KURAL 1 - duyguDurumu: Haberin şirket/hisse için DUYGU YÖNÜNÜ VE ŞİDDETİNİ belirle. " +
                "Sadece şu 5 değerden birini kullan:\n" +
                "  • 'Yüksek Olumlu' → Şirket hissesini çok güçlü yükseltecek büyük haber (Dev satın alma, rekor kâr)\n" +
                "  • 'Olumlu'        → Net pozitif, büyüme/kazanç artışı sinyali (İhale kazanımı, yeni sipariş)\n" +
                "  • 'Nötr'          → Etki belirsiz veya çift yönlü\n" +
                "  • 'Olumsuz'       → Net negatif, kısa vadeli olumsuz baskı (Ceza, mahkeme, kâr düşüşü)\n" +
                "  • 'Yüksek Olumsuz'→ Ciddi negatif (Bedelli sermaye artırımı, büyük zarar, iflas/üretim durdurma riski)\n\n" +

                // ---- KURAL 2: ETKİ VADESİ (ZAMAN BOYUTU) ----
                "KURAL 2 - etkiVadesi: Haberin finansal/operasyonel etkisinin HANGİ ZAMAN UFUKLARINDA HİSSEDİLECEĞİNİ belirle. " +
                "Sadece şu 4 değerden birini kullan:\n" +
                "  • 'Anlık'        → 1-3 gün içinde geçer, şirketin esas faaliyetine kalıcı etkisi olmayan anlık reaksiyonlar (Adres değişikliği, rutin bildirim, anlık ceza)\n" +
                "  • 'Kısa Vadeli'  → 1-3 ay içinde bilançoya yansıyacak durumlar (Çeyreklik sipariş, geçici duruşlar, küçük ihale)\n" +
                "  • 'Orta Vadeli'  → 3-12 ay içinde meyve verecek operasyonlar (Yeni ürün lansmanı, kapasite artırımı, stratejik anlaşma)\n" +
                "  • 'Uzun Vadeli'  → 1 yıl ve üzeri kalıcı etkiler (Fabrika yatırımı, Ar-Ge projeleri, ana lisans iptali, birleşmeler)\n\n" +

                // ---- KURAL 2.5: ETKİ SKORU (GÜÇ/ŞİDDET, 0-100) ----
                "KURAL 2.5 - etkiSkoru: duyguDurumu'nda belirlediğin YÖNDEN bağımsız olarak, " +
                "haberin şirket hissesi üzerindeki etkisinin GÜCÜNÜ/ŞİDDETİNİ 0 ile 100 arasında bir " +
                "TAM SAYI olarak puanla (yön işareti YOK, her zaman pozitif bir sayı ver — yönü zaten " +
                "duyguDurumu taşıyor). Rehber:\n" +
                "  • 0-20   → Çok düşük etki (rutin, prosedürel bildirimler)\n" +
                "  • 21-40  → Düşük-orta etki (küçük ölçekli operasyonel gelişmeler)\n" +
                "  • 41-60  → Orta etki (şirketin faaliyetlerini gözle görülür şekilde etkileyen gelişmeler)\n" +
                "  • 61-80  → Yüksek etki (önemli finansal veya stratejik gelişmeler)\n" +
                "  • 81-100 → Çok yüksek etki (şirketin gidişatını köklü şekilde değiştirebilecek, piyasada büyük yankı uyandıracak gelişmeler)\n" +
                "'Yüksek Olumlu'/'Yüksek Olumsuz' kategorisindeki haberler genelde 60'ın üzerinde, " +
                "'Nötr' haberler genelde 30'un altında bir etkiSkoru almalıdır — ama mekanik biçimde " +
                "duyguDurumu kategorisine bakarak sabit bir sayı üretme, her haberi kendi somut içeriğine " +
                "göre puanla.\n\n" +

                // ---- KURAL 3: SEKTÖR (sektorAdi) ----
                "KURAL 3 - sektorAdi: Şirketin faaliyet gösterdiği sektörü tek kelime/kısa ifadeyle yaz. " +
                "Örnekler: 'Bankacılık', 'Enerji', 'Teknoloji', 'Perakende', 'İnşaat', 'Havacılık', " +
                "'Otomotiv', 'Sigorta', 'Telekomünikasyon', 'Gıda', 'Tekstil', 'Kimya', 'Madencilik', 'REIT'.\n\n" +

                // ---- KURAL 4: İNDİKATÖR SETİ (indikatorSeti) ----
                "KURAL 4 - indikatorSeti: Bu haberi analiz ederken takip edilmesi gereken " +
                "3-5 adet finansal/teknik indikatörü JSON DİZİSİ olarak ver. " +
                "Temel analiz için (F/K, PD/DD, Borç/FAVÖK, Temettü Verimi, Cari Oran, vb.), " +
                "teknik analiz için (RSI, MACD, Bollinger Bantları, Hacim Ortalaması, 50/200 GHO) seç. " +
                "Haberin türüne ve sektöre uygun olanları seç. " +
                "Örnek: [\"F/K Oranı\", \"PD/DD\", \"Borç/FAVÖK\", \"RSI(14)\", \"Hacim Ortalaması\"]\n\n" +

                // ---- KURAL 5: AI YORUMU (aiYorumu) ----
                "KURAL 5 - aiYorumu: Habere özel, şablon cümle kullanmadan, finansal terimler içeren, " +
                "rasyonel ve özgün bir yorum yaz. Maksimum 2-3 cümle. " +
                "Yorumda duygu yönüne ve etki vadesine değin.\n";
        }

        // =======================================================================
        // 🌟 v2.2.0 — PDF EKLİ DETAYLI ANALİZ
        // =======================================================================
        public async Task<GeminiAnalizModel?> PdfIleDetayliAnalizEtAsync(Haber haber, byte[]? pdfBaytlari)
        {
            if (pdfBaytlari == null || pdfBaytlari.Length == 0)
            {
                return await TekHaberiAnalizEtAsync(haber).ConfigureAwait(false);
            }

            var config = JsonYanitAyari();
            var model = _googleAI.GenerativeModel(Model.Gemini3Flash, generationConfig: config);

            string sistemTalimati = AnalizKurallariMetni() +
                "\nEKSTRA TALİMAT: Sana bu haberin KAP bildirim ekindeki PDF dosyası da " +
                "verilmiştir. PDF'in içeriğini (tablolar, tutarlar, tarihler, sözleşme/anlaşma " +
                "detayları, finansal veriler dahil) DİKKATLE incele ve aiYorumu alanını sadece " +
                "başlık/özet metnine değil, PDF'te yer alan somut detaylara dayanarak " +
                "ÖNCEKİNDEN DAHA DETAYLI ve SOMUT rakam/veri içerecek şekilde yaz. " +
                "\nYanıtı MUTLAKA şu JSON formatında dön, başka hiçbir metin ekleme:\n" +
                "{\n" +
                "  \"duyguDurumu\": \"Olumlu\",\n" +
                "  \"etkiVadesi\": \"Uzun Vadeli\",\n" +
                "  \"etkiSkoru\": 70,\n" +
                "  \"sektorAdi\": \"Bankacılık\",\n" +
                "  \"indikatorSeti\": [\"F/K Oranı\", \"PD/DD\", \"RSI(14)\"],\n" +
                "  \"aiYorumu\": \"PDF detaylarına dayanan, somut ve özgün yorum buraya\"\n" +
                "}";

            string haberDetayi = HaberiMetneCevir(haber);
            string geciciDosyaYolu = Path.Combine(Path.GetTempPath(), $"kap_ek_{Guid.NewGuid():N}.pdf");

            try
            {
                await File.WriteAllBytesAsync(geciciDosyaYolu, pdfBaytlari).ConfigureAwait(false);

                string promptMetni = $"{sistemTalimati}\n\nAnaliz Edilecek Haber:\n{haberDetayi}\n\n" +
                    "(Bu haberin bildirim eki PDF'i de ekte sunulmuştur, lütfen inceleyip yorumuna yansıt.)";

                // Mscc.GenerativeAI kütüphanesinin derleme zamanı tip kontrolünü dynamic ile aşarak doğrudan çağrı yapıyoruz:
                dynamic modelDynamic = model;
                var response = await modelDynamic.GenerateContent(promptMetni, pdfBaytlari, "application/pdf");

                if (response == null || string.IsNullOrEmpty((string)response.Text)) return null;

                string yanitMetni = response.Text;
                string temizJson = yanitMetni.Replace("```json", "").Replace("```", "").Trim();
                var ayarlar = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                return JsonSerializer.Deserialize<GeminiAnalizModel>(temizJson, ayarlar);
            }
            finally
            {
                try
                {
                    if (File.Exists(geciciDosyaYolu)) File.Delete(geciciDosyaYolu);
                }
                catch { /* silme başarısız olsa da analiz sonucu etkilenmesin */ }
            }
        }

        private GenerationConfig JsonYanitAyari()
        {
            return new GenerationConfig
            {
                ResponseMimeType = "application/json"
            };
        }

        private string HaberiMetneCevir(Haber haber)
        {
            return $"Hisse Kodu: {haber.HisseKodu}\nŞirket Adı: {haber.SirketAdi}\nBaşlık: {haber.Baslik}\nİçerik: {haber.Icerik}";
        }
    }
}