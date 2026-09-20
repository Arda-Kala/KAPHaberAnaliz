# 📊 KAP Haberleri Analiz Paneli

KAP (Kamuyu Aydınlatma Platformu) bildirimlerini otomatik takip eden, Google Gemini AI ile analiz eden, modern bir web arayüzü sunan **ASP.NET Core 8** projesi.

---

## 🚀 Özellikler

- **Otomatik KAP Taraması** — Arka planda düzenli aralıklarla yeni bildirimleri çeker
- **Gemini AI Analizi** — Her bildirimi toplu olarak Gemini'ye gönderir; duygu durumu, etki vadesi, sektör ve etki skoru üretir
- **Gerçek Zamanlı Piyasa Skoru** — Günlük bülten analizine göre genel piyasa skoru hesaplar
- **Hisse Fiyat Grafiği** — Yahoo Finance entegrasyonu ile hisse bazlı fiyat grafiği
- **Admin Paneli** — Görünüm ayarları, işlem tetikleme, log izleme
- **JWT Kimlik Doğrulama** — Güvenli admin girişi
- **Responsive Arayüz** — Mobil uyumlu, dark/light mode destekli

---

## 🛠️ Teknolojiler

| Katman | Teknoloji |
|---|---|
| Backend | ASP.NET Core 8, Entity Framework Core |
| Veritabanı | Microsoft SQL Server |
| AI | Google Gemini API |
| Loglama | Serilog (Console + File + MSSqlServer) |
| Auth | JWT Bearer Token |
| Frontend | Vanilla JS, Tailwind CSS, Chart.js |

---

## ⚙️ Kurulum

### Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (Express ücretsiz: [indir](https://www.microsoft.com/tr-tr/sql-server/sql-server-downloads))
- [Google Gemini API Anahtarı](https://aistudio.google.com/app/apikey) (ücretsiz)
- EF Core CLI: `dotnet tool install --global dotnet-ef`

---

### 1. Projeyi klonla

```bash
git clone https://github.com/KULLANICI_ADIN/KAPHaberleriAnaliz.git
cd KAPHaberleriAnaliz
```

---

### 2. User Secrets ile gizli değerleri ayarla

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=(local);Database=KAPNewsDb;Trusted_Connection=True;TrustServerCertificate=True;" \
  --project KAPNews.API

dotnet user-secrets set "GeminiApiKey" "GEMINI_API_ANAHTARIN" \
  --project KAPNews.API

dotnet user-secrets set "Jwt:Key" "EnAz32KarakterGucluBirSifreGir!2026" \
  --project KAPNews.API
```

> ⚠️ Bu değerler git'e **gitmez** — `dotnet user-secrets` Windows'ta `%APPDATA%\Microsoft\UserSecrets\` altında saklar.

---

### 3. Veritabanını oluştur

```bash
dotnet ef database update \
  --project KAPNews.DataAccess \
  --startup-project KAPNews.API
```

Başarılı olursa SQL Server'da `KAPNewsDb` ve tüm tablolar otomatik oluşur.

---

### 4. Uygulamayı başlat

```bash
dotnet run --project KAPNews.API
```

| Adres | Açıklama |
|---|---|
| `http://localhost:5121` | Ana uygulama arayüzü |
| `http://localhost:5121/swagger` | API dokümantasyonu |

---

### 5. İlk admin girişi

Uygulama **ilk kez** başlatıldığında konsola şu çıktı gelir:

```
══════════════════════════════════════════
İLK KURULUM: 'admin' kullanıcısı otomatik oluşturuldu.
Kullanıcı Adı: admin
Şifre        : Xk9#mP2qR7...
Bu şifreyi şimdi not alın!
══════════════════════════════════════════
```

> ⚠️ Bu şifre **bir daha gösterilmez**. Unutursan SSMS'de `Kullanicilar` tablosundaki admin kaydını silip uygulamayı yeniden başlat.

---

## 📁 Proje Yapısı

```
KAPNews/
├── KAPNews.API/              # Web API katmanı, Controllers, Program.cs
│   ├── wwwroot/              # Frontend (index.html, app.js)
│   ├── appsettings.json      # Genel ayarlar (gizli değerler boş)
│   └── appsettings.Development.json  # Local geliştirme ayarları
├── KAPNews.Business/         # İş mantığı, servisler, background job
├── KAPNews.DataAccess/       # DbContext, Repository, Migrations
├── KAPNews.Core/             # Arayüzler, Entity sınıfları
└── KAPNews.Entities/         # Veri modelleri
```

---

## 🔒 Güvenlik Notları

- `appsettings.json` dosyasında **hiçbir gizli değer yoktur** — tümü boş bırakılmıştır
- Gizli değerler geliştirme ortamında **User Secrets**, production'da **Environment Variables** ile sağlanır
- `secrets.json`, `*.env`, `logs/` git'e gitmez (`.gitignore` ile korunur)
- JWT Key boş bırakılırsa uygulama başlamayı reddeder

---


---

## 📄 Lisans

Bu proje kişisel/eğitim amaçlıdır.
