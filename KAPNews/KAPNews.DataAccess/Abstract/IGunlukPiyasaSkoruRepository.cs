using KAPNews.Entities;

namespace KAPNews.DataAccess.Abstract;

// 🌟 v2.3.0 — GunlukPiyasaSkoru kayıtları için CRUD sözleşmesi.
public interface IGunlukPiyasaSkoruRepository
{
    // Belirli bir güne (saat bileşeni olmadan) ait skor kaydını getirir, yoksa null döner.
    GunlukPiyasaSkoru? TarihIleGetir(DateTime tarih);

    // En son hesaplanmış (en güncel tarihli) skor kaydını getirir — dashboard'da
    // "Piyasa Skoru" rozetinde gösterilecek olan budur.
    GunlukPiyasaSkoru? SonuncuyuGetir();

    // Varsa günceller, yoksa yeni kayıt oluşturur (upsert) — hesaplama servisinin
    // aynı gün için iki kez tetiklenmesi durumunda hata vermemesi için gereklidir.
    void EkleVeyaGuncelle(GunlukPiyasaSkoru skor);

    // Son N günün skor geçmişini getirir (ör. trend grafiği için ileride kullanılabilir).
    List<GunlukPiyasaSkoru> SonNGunuGetir(int gunSayisi);
}
