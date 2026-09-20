using KAPNews.Entities;
using System.Threading.Tasks;

namespace KAPNews.DataAccess.Abstract
{
    public interface IKullaniciRepository
    {
        Task<Kullanici?> KullaniciAdiIleGetirAsync(string kullaniciAdi);
        Task<Kullanici> EkleAsync(Kullanici kullanici);
        Task GuncelleAsync(Kullanici kullanici);
        Task<int> KullaniciSayisiAsync();
    }
}
