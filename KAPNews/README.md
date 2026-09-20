<div align="center">

# 📊 KAP Haberleri Analiz Paneli

**Kamuyu Aydınlatma Platformu bildirimlerini yapay zeka ile analiz eden, gerçek zamanlı piyasa takip uygulaması**

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![License](https://img.shields.io/badge/lisans-kişisel-gray?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-blue?style=flat-square)

</div>

---

## 🖥️ Ekran Görüntüleri

<img width="1348" height="625" alt="adminpaneli" src="https://github.com/user-attachments/assets/67a49223-6254-4eb3-ace8-14278b6cbd19" />
<img width="1350" height="632" alt="İlgili göstergeler" src="https://github.com/user-attachments/assets/86deab54-1e8c-44ef-b7c0-75fb135423d3" />
<img width="1346" height="628" alt="anasayfa" src="https://github.com/user-attachments/assets/bb2b622e-d7d2-408d-b908-d960a6073f17" />
 

---

## ✨ Özellikler

- 🔍 **Otomatik KAP Taraması** — Arka planda düzenli aralıklarla yeni bildirimleri çeker
- 🤖 **Gemini AI Analizi** — Her bildirimi toplu olarak analiz eder; duygu durumu, etki vadesi, sektör ve etki skoru üretir
- 📈 **Günlük Piyasa Skoru** — Tüm bildirimlere göre genel piyasa eğilimi hesaplar
- 📉 **Hisse Fiyat Grafiği** — Yahoo Finance entegrasyonu ile anlık fiyat grafiği
- 🛡️ **Admin Paneli** — Görünüm ayarları, işlem tetikleme, canlı log izleme
- 🔐 **JWT Kimlik Doğrulama** — Güvenli admin girişi, otomatik token yönetimi
- 🌙 **Dark / Light Mode** — Sistem temasına uyumlu
- 📱 **Tam Responsive** — Masaüstü ve mobil uyumlu arayüz

---

## 🛠️ Teknoloji Yığını

| Katman | Teknoloji |
|---|---|
| Backend | ASP.NET Core 8 Web API |
| ORM | Entity Framework Core 8 |
| Veritabanı | Microsoft SQL Server |
| Yapay Zeka | Google Gemini API |
| Loglama | Serilog (Console + File) |
| Kimlik Doğrulama | JWT Bearer Token |
| Frontend | Vanilla JS, Tailwind CSS |
| Hisse Verisi | Yahoo Finance |

---

## ⚙️ Kurulum

### Gereksinimler

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [SQL Server Express](https://www.microsoft.com/tr-tr/sql-server/sql-server-downloads) (ücretsiz)
- [Google Gemini API Anahtarı](https://aistudio.google.com/app/apikey) (ücretsiz)

---

### 1. Repoyu klonla

```bash
git clone https://github.com/KULLANICI_ADIN/KAPHaberleriAnaliz.git
cd KAPHaberleriAnaliz/KAPNews
```

---

### 2. EF Core CLI yükle

```bash
dotnet tool install --global dotnet-ef
```

---

### 3. Gizli değerleri ayarla

Bu proje gizli değerleri `appsettings.json` yerine güvenli bir şekilde **User Secrets** ile yönetir.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=(local);Database=KAPNewsDb;Trusted_Connection=True;TrustServerCertificate=True;" \
  --project KAPNews.API

dotnet user-secrets set "GeminiApiKey" "GEMINI_API_ANAHTARIN" \
  --project KAPNews.API

dotnet user-secrets set "Jwt:Key" "EnAz32KarakterGucluBirSifreGir2026!" \
  --project KAPNews.API
```

> 💡 Gemini API anahtarı almak için → [aistudio.google.com/app/apikey](https://aistudio.google.com/app/apikey)

---

### 4. Veritabanını oluştur

```bash
dotnet ef database update \
  --project KAPNews.DataAccess \
  --startup-project KAPNews.API
```

---

### 5. Çalıştır

```bash
dotnet run --project KAPNews.API
```

| Adres | Açıklama |
|---|---|
| `http://localhost:5121` | Ana uygulama |
| `http://localhost:5121/swagger` | API dokümantasyonu |

---

### 6. İlk admin girişi

Uygulama ilk kez başlatıldığında konsola şu çıktı gelir:

```
══════════════════════════════════════════
İLK KURULUM: 'admin' kullanıcısı oluşturuldu.
Kullanıcı Adı : admin
Şifre         : Xk9#mP2qR7...
Bu şifreyi hemen not alın!
══════════════════════════════════════════
```

> ⚠️ Şifre **bir daha gösterilmez.** Kaybedersen veritabanındaki `Kullanicilar` tablosundan admin kaydını silip uygulamayı yeniden başlat.

---

## 📁 Proje Yapısı

```
KAPNews/
├── KAPNews.API/              # Web API, Controller'lar, Program.cs
│   └── wwwroot/              # Frontend (index.html, app.js)
├── KAPNews.Business/         # İş mantığı, Background servisler
├── KAPNews.DataAccess/       # DbContext, Repository, Migration'lar
├── KAPNews.Core/             # Arayüzler
└── KAPNews.Entities/         # Veri modelleri
```

---

## 🔒 Güvenlik

- `appsettings.json` içinde **hiçbir gizli değer yoktur**
- Tüm sırlar geliştirmede **User Secrets**, production'da **Environment Variables** ile sağlanır
- `secrets.json`, `logs/`, `bin/`, `obj/` git'e gitmez

Güvenlik açığı bildirimi için [SECURITY.md](.github/SECURITY.md) dosyasına bakın.

---

## 📄 Lisans

Bu proje kişisel / eğitim amaçlıdır.
