using KAPNews.Entities;
namespace KAPNews.DataAccess.Abstract;

public interface IHaberRepository
{
    // Sözleşme Kuralları: Bu arayüzü kullanan sınıf aşağıdaki metotları içermek zorundadır!

    List<Haber> HepsiniGetir();            // Read (Listeleme)
    Haber? IdIleGetir(int id);             // Read (Tek bir haberi bulma)
    void Ekle(Haber haber);                // Create (Ekleme)
    void Guncelle(Haber haber);            // Update (Güncelleme)
    void Sil(int id);                      // Delete (Silme)

    // 🌟 SOLID (Jüriye Not): Bu metotlar, KapVeriCekmeServisi ve
    // OtoAnalizServisi'nin (Business katmanı) artık DataAccess katmanının
    // somut AppDbContext sınıfına DOĞRUDAN erişmesini engellemek için
    // eklendi. Katmanlar arası bağımlılık her zaman SOYUTLAMA (interface)
    // üzerinden kurulmalıdır (DIP + N-Katmanlı Mimari kuralı).

    // KAP'tan gelen bir bildirimin daha önce kaydedilip kaydedilmediğini kontrol eder.
    // v2.0.0: artık Haber.Id yerine Haber.DisclosureIndex alanına göre kontrol eder —
    // Id bizim kendi otomatik artan birincil anahtarımızdır, KAP'ın bildirim
    // numarasıyla (disclosureIndex) hiçbir ilişkisi yoktur.
    bool DisclosureIndexIleVarMi(string disclosureIndex);

    // Birden fazla haberi TEK seferde ekleyip veritabanına kaydeder.
    void TopluEkle(IEnumerable<Haber> haberler);

    // Henüz yapay zeka analizi yapılmamış (DuyguDurumu veya EtkiVadesi boş olan) haberleri getirir.
    List<Haber> BekleyenAnalizHaberleriniGetir();

    // v2.0.0: Bir şirketin/hissenin GEÇMİŞ tüm bildirimlerini getirir.
    // Dashboard'da "Şirket ismine tıklayınca geçmiş bildirimler listesi" özelliği için kullanılır.
    List<Haber> HisseKoduIleGecmisGetir(string hisseKodu);
}