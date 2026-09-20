
using KAPNews.Entities;

namespace KAPNews.Business.Abstract;

public interface IHaberService
{
    // Arayüz arayüzü (UI/Dashboard) besleyecek olan servis kuralları
    List<Haber> TumHaberleriGetir();
    Haber? HaberGetir(int id);
    void HaberEkle(Haber haber);
    void HaberGuncelle(Haber haber);
    void HaberSil(int id);

    // v2.0.0: KAP web sitesinden yeni bildirimleri tarar, henüz kayıtlı olmayanları
    // ekler. Hem arka plan servisi (KapTarayiciServisi) hem de dashboard'daki
    // "Yeni Haberleri Tara" butonu (manuel tetikleme) tarafından kullanılır.
    Task<int> KapVerileriniAnlikCekAndKaydetAsync();

    // İşte bu projenin yıldızı: Seçilen haberi Gemini API'sine gönderip analiz edecek metot
    Task<Haber> HaberiYorumlaAsync(int id);

    // v2.0.0: Bir haberi KAP'tan YENİDEN çeker (güncel metin/ek listesiyle) ve
    // ardından Gemini ile yeniden analiz eder. Dashboard'daki "bu haberi yeniden
    // analiz et" özelliği için kullanılır — HaberiYorumlaAsync'ten farkı, zaten
    // analiz edilmiş bir haberi de zorla yeniden analiz etmesidir.
    Task<Haber?> HaberiYenidenTaraVeAnalizEtAsync(int id);

    // 🌟 API anahtarını korumak için eklendi: Tek tek haber göndermek yerine,
    // birden fazla haberi TEK bir Gemini isteğinde birlikte analiz eder.
    // Böylece 5 haber için 5 istek yerine sadece 1 istek atılmış olur.
    Task<List<Haber>> HaberleriTopluYorumlaAsync(List<Haber> haberler);

    // v2.0.0: Bir şirketin/hissenin geçmişteki TÜM bildirimlerini getirir.
    // Dashboard'da şirket ismine tıklanınca açılan geçmiş listesi için kullanılır.
    List<Haber> SirketGecmisiniGetir(string hisseKodu);

    // =====================================================================
    // 🌟 v2.2.0 — PDF EKİNİ ANALİZ EDEREK YORUMU DETAYLANDIR
    // Admin panelindeki "PDF Ekini Analiz Et" butonu tarafından kullanılır.
    // Haberin bildirim ekindeki PDF'i KAP'tan indirir, Gemini'ye (metin +
    // PDF) birlikte gönderir ve dönen daha detaylı yorumla haberi günceller.
    // PDF indirilemezse (WAF, eski kayıt, vb.) normal metin-tabanlı analize
    // düşer; kullanıcı yine de bir sonuç alır.
    // =====================================================================
    Task<Haber?> PdfEkiniAnalizEdipDetaylandirAsync(int id);
}
//Not: Task<...> ve Async kelimelerini gördün. Bunlar C# dilinde Asenkron (Eşzamanız) programlama demektir. Yapay zeka API'sine istek attığımızda cevabın gelmesi 1-2 saniye sürebilir.
//Bu süreçte tüm sistemin donup kalmaması, arka planda işini rahatça çözmesi için Async yapısını kullanırız.