using System.Collections.Generic;

namespace KAPNews.Business.Abstract
{
    // =======================================================================
    // GEMİNİ YANIT MODELLERİ (DTO'lar)
    // Not: Bu sınıflar eskiden HaberManager.cs dosyasının içinde duruyordu.
    // IHaberAnalizIstemcisi arayüzü ile onu uygulayan GeminiAnalizIstemcisi
    // sınıfı arasında ortak bir sözleşme (contract) oldukları için Abstract
    // katmanına taşındı.
    // =======================================================================
    public class GeminiAnalizModel
    {
        public string duyguDurumu { get; set; } = "Nötr";
        public string etkiVadesi { get; set; } = "Anlık";

        // 🌟 v2.3.0 — SPK Bülten mantığındaki gibi 0-100 arası sayısal etki
        // gücü. duyguDurumu YÖNÜ, etkiSkoru ise GÜCÜ/ŞİDDETİ temsil eder.
        public int etkiSkoru { get; set; } = 50;

        public string sektorAdi { get; set; } = "";
        public List<string>? indikatorSeti { get; set; }
        public string aiYorumu { get; set; } = "";
    }

    public class GeminiTopluAnalizModel
    {
        public int id { get; set; }
        public string duyguDurumu { get; set; } = "Nötr";
        public string etkiVadesi { get; set; } = "Anlık";
        public int etkiSkoru { get; set; } = 50;
        public string sektorAdi { get; set; } = "";
        public List<string>? indikatorSeti { get; set; }
        public string aiYorumu { get; set; } = "";
    }
}
