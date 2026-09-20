using Microsoft.EntityFrameworkCore;
using KAPNews.Entities;

namespace KAPNews.DataAccess
{
    public class AppDbContext : DbContext
    {
        public AppDbContext()
        {
        }

        // Program.cs içerisinden gönderilen veritabanı ayarlarını (AddDbContext)
        // kabul edebilmek için bu yapıcı metodun bulunması şarttır.
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // C# tarafındaki 'Haber' sınıfını, veritabanında 'Haberler' adında bir tabloya dönüştür diyoruz.
        public DbSet<Haber> Haberler { get; set; }

        // v2.0.0: Admin panel kimlik doğrulaması için kullanıcı tablosu.
        public DbSet<Kullanici> Kullanicilar { get; set; }

        // 🌟 v2.3.0: Her takvim gününe ait, gün kapandıktan sonra (00:00'da)
        // hesaplanıp "dondurulan" Piyasa Skoru kayıtları (bkz. GunlukPiyasaSkoru).
        public DbSet<GunlukPiyasaSkoru> GunlukPiyasaSkorlari { get; set; }

        // Veritabanı bağlantı ayarlarını (Connection String) yaptığımız yer
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Eğer Program.cs üzerinden konfigürasyon verilmeyen durumlar olursa (ör. Migration alırken)
            // fallback olarak buradaki bağlantı dizesini kullanmasını sağlıyoruz.
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("");
            }
        }

        // v2.0.0 DÜZELTMESİ: Eskiden burada, build çıktı klasöründeki bir
        // "haberler.json" dosyasını okuyup ModelBuilder.HasData(...) ile
        // migration'a GÖMEN bir seed mekanizması vardı. Bu yaklaşım kırılgandır:
        // (1) dosya build'e kopyalanmazsa migration sessizce boş veri üretir,
        // (2) HasData sabit Id bekler, bu da gerçek KAP verisiyle çakışabilir,
        // (3) OnModelCreating içinde dosya sistemi I/O yapmak, EF Core'un model
        // önbellekleme (model caching) davranışıyla kötü etkileşir. Artık gerçek
        // veri KapScraperService ile canlı çekildiği için sabit demo verisine
        // ihtiyaç yoktur — model tamamen kod-öncelikli (code-first) kalır.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Haber>(entity =>
            {
                entity.HasIndex(h => h.DisclosureIndex).IsUnique(false);
                entity.HasIndex(h => h.HisseKodu);
                entity.Property(h => h.Baslik).HasMaxLength(500);
                entity.Property(h => h.SirketAdi).HasMaxLength(300);
                entity.Property(h => h.HisseKodu).HasMaxLength(20);
                entity.Property(h => h.DuyguDurumu).HasMaxLength(50);
                entity.Property(h => h.EtkiVadesi).HasMaxLength(50);
                entity.Property(h => h.SektorAdi).HasMaxLength(100);
                entity.Property(h => h.BildirimSinifi).HasMaxLength(50);
                entity.Property(h => h.Durum).HasMaxLength(30);
                entity.Property(h => h.DisclosureIndex).HasMaxLength(50);
            });

            modelBuilder.Entity<Kullanici>(entity =>
            {
                entity.HasIndex(k => k.KullaniciAdi).IsUnique();
                entity.Property(k => k.KullaniciAdi).HasMaxLength(100).IsRequired();
                entity.Property(k => k.Rol).HasMaxLength(30).IsRequired();
            });

            modelBuilder.Entity<GunlukPiyasaSkoru>(entity =>
            {
                // Aynı gün için iki kez kayıt oluşmasını (ör. job iki kez tetiklenirse)
                // engellemek için Tarih alanı benzersiz olmalı.
                entity.HasIndex(g => g.Tarih).IsUnique();
                entity.Property(g => g.Yon).HasMaxLength(20);
            });
        }
    }
}