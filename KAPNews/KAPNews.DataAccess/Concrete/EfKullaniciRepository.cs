using KAPNews.DataAccess.Abstract;
using KAPNews.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace KAPNews.DataAccess.Concrete
{
    public class EfKullaniciRepository : IKullaniciRepository
    {
        private readonly AppDbContext _context;

        public EfKullaniciRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Kullanici?> KullaniciAdiIleGetirAsync(string kullaniciAdi)
        {
            return await _context.Kullanicilar
                .FirstOrDefaultAsync(k => k.KullaniciAdi == kullaniciAdi);
        }

        public async Task<Kullanici> EkleAsync(Kullanici kullanici)
        {
            _context.Kullanicilar.Add(kullanici);
            await _context.SaveChangesAsync();
            return kullanici;
        }

        public async Task GuncelleAsync(Kullanici kullanici)
        {
            _context.Kullanicilar.Update(kullanici);
            await _context.SaveChangesAsync();
        }

        public async Task<int> KullaniciSayisiAsync()
        {
            return await _context.Kullanicilar.CountAsync();
        }
    }
}
