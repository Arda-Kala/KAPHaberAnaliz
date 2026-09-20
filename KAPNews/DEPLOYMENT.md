# Dağıtım (Deployment) Rehberi — KAP Haberleri Analiz Platformu

Bu doküman, projeyi bir sunucuya/hosting'e kurarken izlenecek adımları ve
zorunlu ortam değişkenlerini özetler. GitHub Actions / CI kullanılmıyorsa
tüm adımlar manuel veya kendi script'lerinle yapılabilir.

## 1) Zorunlu Ortam Değişkenleri

Uygulama appsettings.json içinde bu değerleri **boş** bırakır; production'da
mutlaka ortam değişkeni (veya `dotnet user-secrets`, sadece geliştirmede)
ile doldurulmalıdır. `__` (çift alt çizgi) iç içe appsettings anahtarlarını temsil eder.

| Ortam Değişkeni                        | Açıklama                                                              | Örnek / Üretim komutu |
|------------------------------------------|-------------------------------------------------------------------------|------------------------|
| `ConnectionStrings__DefaultConnection`   | SQL Server bağlantı dizesi                                             | `Server=...;Database=KapHaberleri;User Id=...;Password=...;TrustServerCertificate=True` |
| `Jwt__Key`                               | JWT imzalama anahtarı, **en az 32 karakter**                           | `openssl rand -base64 48` |
| `GeminiApiKey`                           | Gemini API anahtarı                                                    | Google AI Studio'dan alınır |
| `Cors__AllowedOrigins__0`                | Dashboard'un yayınlandığı origin (birden fazlaysa `__1`, `__2` ekle)    | `https://dashboard.senin-domainin.com` |
| `ASPNETCORE_ENVIRONMENT`                 | `Production` olarak ayarlanmalı                                        | `Production` |

> `Jwt__Key` veya `ConnectionStrings__DefaultConnection` boş kalırsa uygulama
> **başlamayı reddeder** — bu bir hata değil, güvenlik önlemidir.

## 2) Veritabanı

- İlk açılışta `db.Database.Migrate()` otomatik çalışır, migration'ları
  kendin elle uygulamana gerek yok.
- **Tek instance ile başlat.** Aynı anda birden fazla replika/instance ile
  başlatırsan migration'lar çakışabilir. İlk kurulum ve migration'lar
  tamamlandıktan sonra ölçeklendirebilirsin.
- İlk çalıştırmada hiç kullanıcı yoksa otomatik bir `admin` kullanıcısı ve
  rastgele güçlü bir şifre oluşturulur; şifre **sadece konsol/log çıktısına**
  bir kereliğine yazdırılır:
  ```
  docker logs kap-haberleri-analiz
  ```
  komutuyla görebilirsin. Bu şifreyi hemen not al ve admin panelinden değiştir.

## 3) Docker ile Dağıtım (önerilen, en basit yol)

```bash
# Repo kökünde (KAPNews.sln'in bulunduğu klasörde):
docker build -t kap-haberleri-analiz -f KAPNews.API/Dockerfile .

docker run -d -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True" \
  -e Jwt__Key="$(openssl rand -base64 48)" \
  -e GeminiApiKey="senin-api-anahtarin" \
  -e Cors__AllowedOrigins__0="https://dashboard-adresin.com" \
  -e ASPNETCORE_ENVIRONMENT="Production" \
  --name kap-haberleri-analiz kap-haberleri-analiz

docker logs kap-haberleri-analiz   # ilk admin şifresini görmek için
```

## 4) Docker Kullanmadan Dağıtım (Linux + systemd örneği)

```bash
dotnet publish KAPNews.API/KAPNews.API.csproj -c Release -o /var/www/kap-haberleri-analiz

# /etc/kap-haberleri-analiz.env
#   ConnectionStrings__DefaultConnection=...
#   Jwt__Key=...
#   GeminiApiKey=...
#   Cors__AllowedOrigins__0=https://dashboard-adresin.com
#   ASPNETCORE_ENVIRONMENT=Production
#   ASPNETCORE_URLS=http://localhost:5081
```

## 5) Reverse Proxy (Nginx/Caddy) Arkasında Çalıştırma

Uygulama `UseForwardedHeaders` middleware'i içerir — bir ters proxy'nin
arkasında (TLS'i proxy sonlandırıyor) sorunsuz çalışır.

```nginx
location / {
    proxy_pass         http://127.0.0.1:8080;
    proxy_set_header    X-Forwarded-For $remote_addr;
    proxy_set_header    X-Forwarded-Proto $scheme;
    proxy_set_header    Host $host;
}
```

## 6) Loglama ve Hata İzleme (Serilog)

- **Konsol**: `docker logs` / `journalctl` ile canlı izlenir.
- **Dosya**: `logs/kap-haberleri-analiz-YYYYMMDD.log`, günlük döner, 30 gün saklanır.
- **Veritabanı**: `Error` ve üzeri kayıtlar SQL Server'daki `Logs` tablosuna da yazılır
  (tablo ilk çalıştırmada otomatik oluşturulur).
- Her isteğe özgü `IzlemeId` hata yanıtlarına eklenir — kullanıcı "hata aldım"
  dediğinde bu ID ile log dosyasında/DB'de saniyeler içinde arama yapabilirsin.
- `GlobalExceptionMiddleware` tüm işlenmeyen hataları tek noktadan yakalar;
  stack trace SADECE Development ortamında istemciye döner.

## 7) KAP Web Scraping Hakkında Önemli Not

Bu proje, MKK'nın resmi test API'si yerine (bkz. proje içindeki
`IKapScraperService` açıklaması) kap.org.tr'nin herkese açık JSON uç
noktalarını kullanır. Bu noktalar:

- Kimlik doğrulama gerektirmez ama bir **WAF (Web Application Firewall)**
  ile korunur — istekler `User-Agent` ve `Referer` header'ları olmadan
  reddedilebilir (kod içinde zaten ayarlı).
- Site yapısı KAP tarafından haber verilmeden değişebilir. Tarama sırasında
  sürekli hata alırsan önce `KapScraperService` içindeki JSON alan adlarının
  (`disclosureIndex`, `kapTitle` vb.) hâlâ geçerli olup olmadığını kontrol et.
- Aşırı sık istek atmak IP'nin geçici olarak engellenmesine yol açabilir;
  arka plan taraması varsayılan olarak 3 dakikada bir çalışacak şekilde
  ayarlanmıştır (`KapTarayiciServisi.TaramaAraligi`) — bunu düşürmeden önce
  dikkatli ol.

## 8) Dağıtım Öncesi Son Kontrol Listesi

- [ ] `Jwt__Key`, `ConnectionStrings__DefaultConnection`, `GeminiApiKey` ortam değişkenleri ayarlandı
- [ ] `ASPNETCORE_ENVIRONMENT=Production` ayarlandı
- [ ] `Cors__AllowedOrigins__0` gerçek Dashboard adresine ayarlandı
- [ ] SQL Server erişilebilir ve migration'lar sorunsuz uygulandı (ilk açılış loglarından kontrol et)
- [ ] İlk admin şifresi loglardan alınıp not edildi, giriş yapıldıktan sonra değiştirildi
- [ ] Domain için gerçek bir TLS sertifikası var
- [ ] `/health` uç noktası dışarıdan erişilebilir ve 200 dönüyor
- [ ] "Yeni Haberleri Tara" butonu admin girişiyle test edildi, KAP'tan gerçek veri geldi
- [ ] Arka plan tarayıcı servisi (KapTarayiciServisi) loglarda çalıştığı görülüyor
- [ ] `logs/` klasörü uygulamanın yazma izni olan bir yerde ve `Logs` tablosu SQL Server'da otomatik oluşmuş
