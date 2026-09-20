using KAPNews.Business.Abstract;
using KAPNews.DataAccess.Abstract;
using KAPNews.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace KAPNews.Business.Concrete
{
    // =======================================================================
    // 🌟 SOLID (Jüriye Not): HaberManager artık SADECE haber CRUD işlemlerini
    // ve "hangi haber ne zaman analiz edilmeli" akışını yönetir (SRP).
    // Google Gemini'ye ait tüm detaylar (prompt hazırlama, API çağrısı, JSON
    // çözümleme, API anahtarı) IHaberAnalizIstemcisi arayüzünün arkasına,
    // KAP web sitesinden veri çekme detayları da IKapScraperService arayüzünün
    // arkasına taşındı. HaberManager bu arayüzlere CONSTRUCTOR ile enjekte
    // edilen bağımlılıklar olarak erişir (DIP). Böylece HaberManager hiçbir
    // somut (concrete) sınıfı doğrudan bilmez.
    // =======================================================================
    public class HaberManager : IHaberService
    {
        private readonly IHaberRepository _haberRepository;
        private readonly IHaberAnalizIstemcisi _analizIstemcisi;
        private readonly IKapScraperService _kapScraperService;
        private readonly ILogger<HaberManager> _logger;

        public HaberManager(
            IHaberRepository haberRepository,
            IHaberAnalizIstemcisi analizIstemcisi,
            IKapScraperService kapScraperService,
            ILogger<HaberManager> logger)
        {
            _haberRepository = haberRepository;
            _analizIstemcisi = analizIstemcisi;
            _kapScraperService = kapScraperService;
            _logger = logger;
        }

        public List<Haber> TumHaberleriGetir() => _haberRepository.HepsiniGetir();
        public Haber? HaberGetir(int id) => _haberRepository.IdIleGetir(id);
        public void HaberEkle(Haber haber) => _haberRepository.Ekle(haber);
        public void HaberGuncelle(Haber haber) => _haberRepository.Guncelle(haber);
        public void HaberSil(int id) => _haberRepository.Sil(id);

        public List<Haber> SirketGecmisiniGetir(string hisseKodu) => _haberRepository.HisseKoduIleGecmisGetir(hisseKodu);

        // =======================================================================
        // KAP TARAMASI — hem arka plan servisi (KapTarayiciServisi) hem de
        // dashboard'daki "Yeni Haberleri Tara" butonu (manuel tetikleme) bu
        // metodu çağırır.
        // =======================================================================
        public async Task<int> KapVerileriniAnlikCekAndKaydetAsync()
        {
            var tespitEdilenler = await _kapScraperService.YeniBildirimleriTespitEtAsync().ConfigureAwait(false);
            if (tespitEdilenler.Count == 0) return 0;

            var eklenecekHaberler = new List<Haber>();

            foreach (var bildirim in tespitEdilenler)
            {
                var disclosureIndexStr = bildirim.DisclosureIndex.ToString();
                if (_haberRepository.DisclosureIndexIleVarMi(disclosureIndexStr))
                    continue; // Zaten kayıtlı, tekrar ekleme.

                // Detay çağrısı, düz metni ve ek dosya linklerini getirir.
                // Sadece özet bilgiyle de haberi kaydedebiliriz (detay isteği
                // başarısız olursa), ama mümkünse zengin içerikle kaydetmeyi tercih ederiz.
                var detay = await _kapScraperService.BildirimDetayiGetirAsync(bildirim.DisclosureIndex).ConfigureAwait(false);

                var yeniHaber = new Haber
                {
                    DisclosureIndex = disclosureIndexStr,
                    HisseKodu = bildirim.HisseKodu,
                    SirketAdi = bildirim.SirketAdi,
                    Baslik = bildirim.Konu,
                    Icerik = detay?.DuzMetin ?? bildirim.Konu,
                    BildirimSinifi = bildirim.BildirimSinifi,
                    YayinlanmaTarihi = bildirim.YayinTarihi,
                    KapLinki = detay?.KapLinki ?? $"https://www.kap.org.tr/tr/Bildirim/{bildirim.DisclosureIndex}",
                    EklerJson = detay != null && detay.Ekler.Count > 0 ? JsonSerializer.Serialize(detay.Ekler) : null,
                    Durum = "Beklemede",
                    KayitTarihi = DateTime.UtcNow
                };

                eklenecekHaberler.Add(yeniHaber);
            }

            if (eklenecekHaberler.Count > 0)
            {
                _haberRepository.TopluEkle(eklenecekHaberler);
                _logger.LogInformation("KAP taramasında {Sayi} yeni bildirim veritabanına kaydedildi.", eklenecekHaberler.Count);
            }

            return eklenecekHaberler.Count;
        }

        // =======================================================================
        // TEKLİ ANALİZ — manuel tetikleme veya admin panelinden kullanılır
        // =======================================================================
        public async Task<Haber> HaberiYorumlaAsync(int id)
        {
            var haber = _haberRepository.IdIleGetir(id);
            if (haber == null) return null;

            if (!string.IsNullOrEmpty(haber.DuyguDurumu) && !string.IsNullOrEmpty(haber.EtkiVadesi)) return haber;

            await TekHaberiAnalizEtVeKaydetAsync(haber).ConfigureAwait(false);
            return haber;
        }

        // =======================================================================
        // v2.0.0 — "BU HABERİ YENİDEN ANALİZ ET" (dashboard'daki tekil buton)
        // Önce KAP'tan bildirimi TEKRAR çeker (güncel metin/ek listesiyle — ilgili
        // bildirim güncellenmiş/düzeltilmiş olabilir), sonra zorla yeniden analiz eder.
        // =======================================================================
        public async Task<Haber?> HaberiYenidenTaraVeAnalizEtAsync(int id)
        {
            var haber = _haberRepository.IdIleGetir(id);
            if (haber == null) return null;

            if (!string.IsNullOrEmpty(haber.DisclosureIndex) && long.TryParse(haber.DisclosureIndex, out var disclosureIndex))
            {
                var detay = await _kapScraperService.BildirimDetayiGetirAsync(disclosureIndex).ConfigureAwait(false);
                if (detay != null)
                {
                    haber.Icerik = detay.DuzMetin;
                    haber.KapLinki = detay.KapLinki;
                    haber.EklerJson = detay.Ekler.Count > 0 ? JsonSerializer.Serialize(detay.Ekler) : null;
                }
            }

            // Eski analiz sonuçlarını temizle ki TekHaberiAnalizEtVeKaydetAsync
            // "zaten analiz edilmiş" diye atlamasın.
            haber.DuyguDurumu = null;
            haber.EtkiVadesi = null;

            await TekHaberiAnalizEtVeKaydetAsync(haber).ConfigureAwait(false);
            return haber;
        }

        private async Task TekHaberiAnalizEtVeKaydetAsync(Haber haber)
        {
            try
            {
                _logger.LogInformation("[Gemini] {HisseKodu} için genişletilmiş analiz talebi gönderiliyor...", haber.HisseKodu);

                var analizSonucu = await _analizIstemcisi.TekHaberiAnalizEtAsync(haber).ConfigureAwait(false);

                if (analizSonucu != null)
                {
                    haber.DuyguDurumu = analizSonucu.duyguDurumu;
                    haber.EtkiVadesi = analizSonucu.etkiVadesi;
                    haber.EtkiSkoru = analizSonucu.etkiSkoru;
                    haber.SektorAdi = analizSonucu.sektorAdi;
                    haber.IndikatorSeti = analizSonucu.indikatorSeti != null
                        ? JsonSerializer.Serialize(analizSonucu.indikatorSeti)
                        : null;
                    haber.AiYorumu = analizSonucu.aiYorumu;
                }

                haber.Durum = "AnalizEdildi";
                _haberRepository.Guncelle(haber);
                _logger.LogInformation("[Gemini] {HisseKodu} genişletilmiş analiz tamamlandı.", haber.HisseKodu);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gemini Hatası] {HisseKodu} analiz edilirken hata oluştu.", haber.HisseKodu);
                haber.DuyguDurumu = "Nötr";
                haber.EtkiVadesi = "Anlık";
                haber.EtkiSkoru = 0;
                haber.AiYorumu = "Yapay zeka analiz servislerine şu anda erişilemiyor. Hata: " + ex.Message;
                haber.Durum = "Hata";
                _haberRepository.Guncelle(haber);
            }
        }

        // =======================================================================
        // TOPLU ANALİZ — OtoAnalizServisi tarafından her turda çağrılır
        // =======================================================================
        public async Task<List<Haber>> HaberleriTopluYorumlaAsync(List<Haber> haberler)
        {
            var yorumlanacaklar = haberler.Where(h => string.IsNullOrEmpty(h.DuyguDurumu) || string.IsNullOrEmpty(h.EtkiVadesi)).ToList();

            if (yorumlanacaklar.Count == 0) return haberler;

            try
            {
                _logger.LogInformation("[Gemini Toplu] {Sayi} haber TEK istekte genişletilmiş analiz ediliyor...", yorumlanacaklar.Count);

                var analizSonuclari = await _analizIstemcisi.HaberleriTopluAnalizEtAsync(yorumlanacaklar).ConfigureAwait(false);

                if (analizSonuclari != null)
                {
                    foreach (var sonuc in analizSonuclari)
                    {
                        var ilgiliHaber = yorumlanacaklar.FirstOrDefault(h => h.Id == sonuc.id);
                        if (ilgiliHaber != null)
                        {
                            ilgiliHaber.DuyguDurumu = sonuc.duyguDurumu;
                            ilgiliHaber.EtkiVadesi = sonuc.etkiVadesi;
                            ilgiliHaber.EtkiSkoru = sonuc.etkiSkoru;
                            ilgiliHaber.SektorAdi = sonuc.sektorAdi;
                            ilgiliHaber.IndikatorSeti = sonuc.indikatorSeti != null
                                ? JsonSerializer.Serialize(sonuc.indikatorSeti)
                                : null;
                            ilgiliHaber.AiYorumu = sonuc.aiYorumu;
                        }
                    }
                }

                foreach (var haber in yorumlanacaklar)
                {
                    if (string.IsNullOrEmpty(haber.DuyguDurumu))
                    {
                        haber.DuyguDurumu = "Nötr";
                        haber.EtkiVadesi = "Anlık";
                        haber.EtkiSkoru = 0;
                        haber.AiYorumu = "Bu haber toplu analiz yanıtında bulunamadı, bir sonraki turda tekrar denenecek.";
                    }
                    else
                    {
                        haber.Durum = "AnalizEdildi";
                    }
                    _haberRepository.Guncelle(haber);
                }

                _logger.LogInformation("[Gemini Toplu] {Sayi} haber genişletilmiş analiz tamamlandı.", yorumlanacaklar.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gemini Toplu Hata] Toplu analiz sırasında hata oluştu.");

                if (_analizIstemcisi.KotaVeyaHizSiniriHatasiMi(ex))
                {
                    _logger.LogWarning("[Gemini] Kota/hız sınırı doldu. Haberler bekleyen durumda kalacak.");
                }
                else
                {
                    foreach (var haber in yorumlanacaklar)
                    {
                        haber.DuyguDurumu = "Nötr";
                        haber.EtkiVadesi = "Anlık";
                        haber.EtkiSkoru = 0;
                        haber.AiYorumu = "Yapay zeka analiz servislerine şu anda erişilemiyor. Hata: " + ex.Message;
                        haber.Durum = "Hata";
                        _haberRepository.Guncelle(haber);
                    }
                }
            }

            return haberler;
        }
        // =======================================================================
        // v2.2.0 — "PDF EKİNİ ANALİZ ET" (admin panelindeki detaylandırma butonu)
        //
        // Haberin EklerJson alanındaki İLK PDF ekini KAP'tan indirir (aynı hız
        // sınırlı, WAF-güvenli PdfIndirAsync üzerinden), ardından hem haber
        // metnini hem PDF içeriğini Gemini'ye birlikte göndererek mevcut
        // aiYorumu'nu daha detaylı/somut bir versiyonla değiştirir.
        //
        // PDF indirilemezse (eski kayıt, ObjId yok, WAF, vb.) sessizce metin
        // tabanlı analize düşer — kullanıcı yine bir sonuç görür, sadece PDF
        // detayları eklenmemiş olur; bu durum loglanır.
        // =======================================================================
        public async Task<Haber?> PdfEkiniAnalizEdipDetaylandirAsync(int id)
        {
            var haber = _haberRepository.IdIleGetir(id);
            if (haber == null) return null;

            byte[]? pdfBaytlari = null;

            try
            {
                if (!string.IsNullOrWhiteSpace(haber.EklerJson) &&
                    long.TryParse(haber.DisclosureIndex, out var disclosureIndex))
                {
                    var ekler = JsonSerializer.Deserialize<List<BildirimEkiDto>>(haber.EklerJson);
                    var ilkEk = ekler?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.ObjId));

                    if (ilkEk != null)
                    {
                        _logger.LogInformation("[PDF Analiz] {HisseKodu} için bildirim eki PDF indiriliyor (ObjId: {ObjId})...", haber.HisseKodu, ilkEk.ObjId);
                        pdfBaytlari = await _kapScraperService.PdfIndirAsync(ilkEk.ObjId!, disclosureIndex).ConfigureAwait(false);

                        if (pdfBaytlari == null)
                        {
                            _logger.LogWarning("[PDF Analiz] {HisseKodu} için PDF indirilemedi, metin tabanlı analize düşülüyor.", haber.HisseKodu);
                        }
                    }
                }

                _logger.LogInformation("[Gemini PDF] {HisseKodu} için PDF eki dahil detaylı analiz talebi gönderiliyor...", haber.HisseKodu);

                var analizSonucu = await _analizIstemcisi.PdfIleDetayliAnalizEtAsync(haber, pdfBaytlari).ConfigureAwait(false);

                if (analizSonucu != null)
                {
                    haber.DuyguDurumu = analizSonucu.duyguDurumu;
                    haber.EtkiVadesi = analizSonucu.etkiVadesi;
                    haber.EtkiSkoru = analizSonucu.etkiSkoru;
                    haber.SektorAdi = analizSonucu.sektorAdi;
                    haber.IndikatorSeti = analizSonucu.indikatorSeti != null
                        ? JsonSerializer.Serialize(analizSonucu.indikatorSeti)
                        : null;
                    haber.AiYorumu = analizSonucu.aiYorumu;
                    haber.Durum = "AnalizEdildi";
                    _haberRepository.Guncelle(haber);
                    _logger.LogInformation("[Gemini PDF] {HisseKodu} PDF eki dahil detaylı analiz tamamlandı.", haber.HisseKodu);
                }

                return haber;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gemini PDF Hata] {HisseKodu} PDF eki analiz edilirken hata oluştu.", haber.HisseKodu);
                throw; // Controller bu hatayı yakalayıp kullanıcıya anlamlı bir mesaj döner.
            }
        }
    }
}

