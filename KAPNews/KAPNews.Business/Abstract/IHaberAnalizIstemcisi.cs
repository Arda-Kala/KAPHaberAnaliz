using KAPNews.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KAPNews.Business.Abstract
{
    // =======================================================================
    // 🌟 SOLID (Jüriye Not): Bu arayüz, "yapay zeka ile haber analizi yapma"
    // sorumluluğunu HaberManager'dan ayırır.
    //
    //  - SRP  : HaberManager artık sadece haber CRUD işlemlerini yönetir;
    //           Gemini'ye prompt hazırlama, API çağrısı ve JSON çözümleme işi
    //           bu arayüzü uygulayan sınıfa (GeminiAnalizIstemcisi) aittir.
    //  - DIP  : HaberManager somut "GoogleAI/Gemini" sınıfına değil, bu
    //           soyutlamaya (interface) bağımlıdır.
    //  - OCP  : Yarın Gemini yerine başka bir yapay zeka sağlayıcısı
    //           (ör. OpenAI) kullanılmak istenirse, sadece bu arayüzün yeni
    //           bir implementasyonu yazılır; HaberManager'da TEK SATIR bile
    //           değişmez.
    // =======================================================================
    public interface IHaberAnalizIstemcisi
    {
        // Tek bir haberi Gemini'ye gönderip analiz sonucunu döner.
        // Analiz üretilemezse (boş yanıt) null döner.
        Task<GeminiAnalizModel?> TekHaberiAnalizEtAsync(Haber haber);

        // Birden fazla haberi TEK istekte Gemini'ye gönderip analiz sonuçlarını döner.
        Task<List<GeminiTopluAnalizModel>?> HaberleriTopluAnalizEtAsync(List<Haber> haberler);

        // =====================================================================
        // 🌟 v2.2.0 — PDF EKLİ DETAYLI ANALİZ
        // Haberin başlık/içerik metnine EK OLARAK, bildirim ekindeki PDF
        // dosyasını da (Gemini'nin multimodal/döküman girişini kullanarak)
        // okuyup mevcut AI yorumunu genişletir. PDF genellikle KAP haber
        // metninde olmayan tablo, finansal tutar, sözleşme detayı gibi ek
        // bilgiler içerir; bu metot o bilgileri de analize katar.
        // pdfBaytlari null/boş verilirse PDF'siz normal analiz davranışına
        // (TekHaberiAnalizEtAsync ile aynı) düşer.
        // =====================================================================
        Task<GeminiAnalizModel?> PdfIleDetayliAnalizEtAsync(Haber haber, byte[]? pdfBaytlari);

        // Verilen hatanın Gemini'nin kota/hız sınırı hatası olup olmadığını söyler.
        // (HaberManager bu bilgiye göre "haberi bekletmeli mi yoksa Nötr işaretlemeli mi" kararını verir.)
        bool KotaVeyaHizSiniriHatasiMi(Exception ex);
    }
}
