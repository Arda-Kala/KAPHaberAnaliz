using System;
using System.Collections.Generic;

namespace KAPNews.Entities
{
    public class Haber
    {
        public int Id { get; set; }

        public string HisseKodu { get; set; }

        public string SirketAdi { get; set; }

        public string Baslik { get; set; }

        public string Icerik { get; set; }

        public DateTime YayinlanmaTarihi { get; set; }

        // --- 1. PARAMETRE: DUYGU DURUMU (YÖN VE ŞİDDET) ---
        // Beklenen Değerler: "Yüksek Olumlu", "Olumlu", "Nötr", "Olumsuz", "Yüksek Olumsuz"
        public string? DuyguDurumu { get; set; }

        // --- 2. PARAMETRE: ETKİ VADESİ (ZAMAN BOYUTU) ---
        // Beklenen Değerler: "Anlık", "Kısa Vadeli", "Orta Vadeli", "Uzun Vadeli"
        public string? EtkiVadesi { get; set; }

        // --- 3. PARAMETRE: ETKİ SKORU (GÜÇ/ŞİDDET, 0-100) ---
        // 🌟 v2.3.0 — SPK Bülten projesindeki mantıkla aynı: Gemini, DuyguDurumu
        // (yön) ile birlikte 0-100 arası SAYISAL bir etki skoru da üretir.
        // Bu, "Yüksek Olumlu" kategorisinin sabit bir yüzdeye eşlenmesi yerine,
        // her haberin kendi somut, AI tarafından değerlendirilmiş gücünü taşır.
        // "Haber Genel Skoru" rozeti ve Piyasa Skoru artık bu alandan hesaplanır.
        public int? EtkiSkoru { get; set; }

        // --- YAPAY ZEKA DEĞERLENDİRMESİ VE EK METRİKLER ---
        public string? AiYorumu { get; set; }

        // Gemini'nin çıkardığı sektör (Bankacılık, Enerji, Teknoloji vb.)
        public string? SektorAdi { get; set; }

        // Gemini'nin önerdiği teknik/temel göstergeler (JSON string olarak tutulur)
        // Örnek: ["P/K Oranı", "Borç/Özsermaye", "Momentum"]
        public string? IndikatorSeti { get; set; }

        // =====================================================================
        // v2.0.0 İLE EKLENEN ALANLAR — KAP web scraping ve zengin haber detayı
        // için gereklidir (bkz. KapScraperService).
        // =====================================================================

        /// <summary>
        /// KAP sitesindeki bildirimin benzersiz kimliği (disclosureIndex).
        /// Aynı bildirimin veritabanına iki kez eklenmesini önlemek için
        /// (tekilleştirme/dedup) kullanılır — Haber.Id ile KARIŞTIRILMAMALI,
        /// o bizim kendi otomatik artan birincil anahtarımızdır.
        /// </summary>
        public string? DisclosureIndex { get; set; }

        /// <summary>
        /// Bildirimin KAP sitesindeki orijinal sayfasının linki
        /// (örn. https://www.kap.org.tr/tr/Bildirim/1234567). Kullanıcının
        /// "site bağlantısına git" isteğini karşılar.
        /// </summary>
        public string? KapLinki { get; set; }

        /// <summary>
        /// Bildirime ait PDF eklerinin listesi. Her bildirimde 0, 1 veya daha
        /// fazla ek olabileceğinden JSON dizisi olarak (List&lt;BildirimEki&gt;
        /// serileştirilmiş hali) tek bir sütunda saklanır — SPK projesindeki
        /// BultenMetinBlogu gibi ayrı bir tabloya çıkarmaya şu an gerek yok
        /// çünkü ek sayısı azdır ve ilişkisel sorgu ihtiyacı yoktur.
        /// </summary>
        public string? EklerJson { get; set; }

        /// <summary>
        /// Bildirimin sınıfı (KAP terminolojisiyle): ODA (Özel Durum Açıklaması),
        /// FR (Finansal Rapor), DG (Diğer), DUY (Düzenleyici Kurum) vb.
        /// Filtreleme ve rozet gösterimi için kullanılır.
        /// </summary>
        public string? BildirimSinifi { get; set; }

        /// <summary>
        /// İşlenme durumu — SPK projesindeki Bulten.Durum alanına karşılık gelir.
        /// Beklenen değerler: "Beklemede", "AnalizEdildi", "Hata".
        /// </summary>
        public string Durum { get; set; } = "Beklemede";

        /// <summary>Bu kayıt sisteme ne zaman eklendi (scraping zamanı).</summary>
        public DateTime KayitTarihi { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Bir bildirime ait tekil bir PDF eki. Haber.EklerJson alanında
    /// JSON dizisi olarak saklanır.
    /// </summary>
    public class BildirimEki
    {
        public string DosyaAdi { get; set; } = string.Empty;

        /// <summary>
        /// Bu PDF'in GERÇEKTEN indirileceği/görüntüleneceği adres. Artık KAP'ın
        /// bildirim sayfasına değil, kendi backend'imizdeki
        /// api/Haberler/pdf-indir/{ObjId} uç noktasına işaret eder — bu uç nokta
        /// KAP'tan Java-wrapped PDF'i çekip gerçek application/pdf olarak
        /// tarayıcıya döner (bkz. KapScraperService.PdfIndirAsync).
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// KAP'ın attachment-detail yanıtındaki ham "objId" değeri. PDF indirme
        /// uç noktasının KAP tarafında dosyayı bulmak için ihtiyaç duyduğu
        /// gerçek anahtardır; Url alanı bunu zaten içerir ama ileride farklı bir
        /// kullanım (örn. önbellekleme) için ayrıca saklanır.
        /// </summary>
        public string? ObjId { get; set; }
    }
}
