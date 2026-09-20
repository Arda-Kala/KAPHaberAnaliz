
// KAPNews.DataAccess.Concrete.EfHaberRepository.cs
using KAPNews.Entities;
using KAPNews.DataAccess.Abstract;
using System.Collections.Generic;
using System.Linq;

namespace KAPNews.DataAccess.Concrete
{
    public class EfHaberRepository : IHaberRepository


    {
        // Bağımlılık (Dependency) dışarıdan enjekte ediliyor.
        private readonly AppDbContext _context;

        public EfHaberRepository(AppDbContext context)
        {
            _context = context;
        }

        public List<Haber> HepsiniGetir()
        {
            return _context.Haberler.ToList();
        }

        public Haber? IdIleGetir(int id)
        {
            return _context.Haberler.Find(id);
        }

        public void Ekle(Haber haber)
        {
            _context.Haberler.Add(haber);
            _context.SaveChanges();
        }

        public void Guncelle(Haber haber)
        {
            _context.Haberler.Update(haber);
            _context.SaveChanges();
        }

        public void Sil(int id)
        {
            var silinecekHaber = _context.Haberler.Find(id);
            if (silinecekHaber != null)
            {
                _context.Haberler.Remove(silinecekHaber);
                _context.SaveChanges();
            }
        }

        public bool DisclosureIndexIleVarMi(string disclosureIndex)
        {
            // v2.0.0 DÜZELTMESİ: Eskiden burada yanlışlıkla Haber.Id (bizim kendi
            // otomatik artan birincil anahtarımız) ile KAP'ın disclosureIndex'i
            // karşılaştırılıyordu — bu, aynı bildirimin defalarca yeniden eklenmesine
            // yol açan sinsi bir tekrar (duplicate) hatasıydı. Artık doğru alan
            // (Haber.DisclosureIndex) ile karşılaştırılıyor.
            return _context.Haberler.Any(h => h.DisclosureIndex == disclosureIndex);
        }

        public void TopluEkle(IEnumerable<Haber> haberler)
        {
            _context.Haberler.AddRange(haberler);
            _context.SaveChanges();
        }

        public List<Haber> BekleyenAnalizHaberleriniGetir()
        {
            return _context.Haberler
                .Where(h => string.IsNullOrEmpty(h.DuyguDurumu) || string.IsNullOrEmpty(h.EtkiVadesi))
                .ToList();
        }

        public List<Haber> HisseKoduIleGecmisGetir(string hisseKodu)
        {
            return _context.Haberler
                .Where(h => h.HisseKodu == hisseKodu)
                .OrderByDescending(h => h.YayinlanmaTarihi)
                .ToList();
        }
    }
}