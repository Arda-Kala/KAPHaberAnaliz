using KAPNews.DataAccess.Abstract;
using KAPNews.Entities;
using Microsoft.EntityFrameworkCore;

namespace KAPNews.DataAccess.Concrete
{
    public class EfGunlukPiyasaSkoruRepository : IGunlukPiyasaSkoruRepository
    {
        private readonly AppDbContext _context;

        public EfGunlukPiyasaSkoruRepository(AppDbContext context)
        {
            _context = context;
        }

        public GunlukPiyasaSkoru? TarihIleGetir(DateTime tarih)
        {
            var gun = tarih.Date;
            return _context.GunlukPiyasaSkorlari.FirstOrDefault(g => g.Tarih == gun);
        }

        public GunlukPiyasaSkoru? SonuncuyuGetir()
        {
            return _context.GunlukPiyasaSkorlari
                .OrderByDescending(g => g.Tarih)
                .FirstOrDefault();
        }

        public void EkleVeyaGuncelle(GunlukPiyasaSkoru skor)
        {
            var mevcut = TarihIleGetir(skor.Tarih);
            if (mevcut == null)
            {
                _context.GunlukPiyasaSkorlari.Add(skor);
            }
            else
            {
                mevcut.NetSkor = skor.NetSkor;
                mevcut.Yon = skor.Yon;
                mevcut.ToplamHaberSayisi = skor.ToplamHaberSayisi;
                mevcut.OlumluSayisi = skor.OlumluSayisi;
                mevcut.OlumsuzSayisi = skor.OlumsuzSayisi;
                mevcut.NotrSayisi = skor.NotrSayisi;
                mevcut.HesaplanmaZamani = DateTime.UtcNow;
                _context.GunlukPiyasaSkorlari.Update(mevcut);
            }
            _context.SaveChanges();
        }

        public List<GunlukPiyasaSkoru> SonNGunuGetir(int gunSayisi)
        {
            return _context.GunlukPiyasaSkorlari
                .OrderByDescending(g => g.Tarih)
                .Take(gunSayisi)
                .OrderBy(g => g.Tarih)
                .ToList();
        }
    }
}
