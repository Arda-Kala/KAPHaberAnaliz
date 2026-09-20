// KAPNews.API.Program.cs
using KAPNews.API.Middleware;
using KAPNews.Business.Abstract;
using KAPNews.Business.Concrete;
using KAPNews.DataAccess;
using KAPNews.DataAccess.Abstract;
using KAPNews.DataAccess.Concrete;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System;
using System.Text;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("KAP Haberleri Analiz API başlatılıyor...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfig) =>
    {
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProcessId()
            .Enrich.WithThreadId();
    });

    builder.Services.AddControllers();

    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // 🌟 SOLID: Dependency Injection Kayıtları
    builder.Services.AddScoped<IHaberRepository, EfHaberRepository>();
    builder.Services.AddScoped<IKullaniciRepository, EfKullaniciRepository>();
    builder.Services.AddScoped<IHaberAnalizIstemcisi, GeminiAnalizIstemcisi>();
    builder.Services.AddScoped<IHaberService, HaberManager>();

    // 🌟 v2.3.0: Günlük Piyasa Skoru hesaplama servisi.
    builder.Services.AddScoped<IGunlukPiyasaSkoruRepository, EfGunlukPiyasaSkoruRepository>();
    builder.Services.AddScoped<IGunlukPiyasaSkoruServisi, GunlukPiyasaSkoruManager>();

    // v2.0.0: KAP web sitesini tarayan servis (MKK API'nin yerini alır).
    builder.Services.AddHttpClient<IKapScraperService, KapScraperService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddHttpClient<IYahooFinanceService, YahooFinanceService>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    builder.Services.AddMemoryCache();

    // CORS — Sadece appsettings.json / ortam değişkeni ile açıkça tanımlanan
    // origin'lere izin verilir. AllowAnyOrigin KULLANILMAZ.
    var izinliOriginler = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? Array.Empty<string>();

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            if (izinliOriginler.Length > 0)
            {
                policy.WithOrigins(izinliOriginler)
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                policy.WithOrigins(Array.Empty<string>());
            }
        });
    });

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var jwtKey = builder.Configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey))
            {
                throw new InvalidOperationException(
                    "Jwt:Key ayarı boş — uygulama güvenli şekilde başlatılamaz. " +
                    "appsettings.json'a değer yazmak yerine 'dotnet user-secrets' " +
                    "(geliştirme) veya Jwt__Key ortam değişkeni (üretim) kullanın.");
            }

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
            };
        });

    builder.Services.AddHostedService<OtoAnalizServisi>();
    builder.Services.AddHostedService<KapTarayiciServisi>();
    builder.Services.AddHostedService<GunlukPiyasaSkoruHesaplamaServisi>();

    var app = builder.Build();

    app.UseGlobalExceptionHandling();
    app.UseSerilogRequestLogging();

    app.UseForwardedHeaders(new Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
    });

    if (izinliOriginler.Length == 0)
    {
        app.Logger.LogWarning(
            "'Cors:AllowedOrigins' tanımlanmamış — API, wwwroot dışından (farklı bir " +
            "origin'den) hiçbir isteğe izin vermeyecek.");
    }

    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseCors();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();

    app.MapGet("/health", () => Results.Ok(new { durum = "sağlıklı", zaman = DateTime.UtcNow }))
        .AllowAnonymous();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        // =====================================================================
        // İLK KURULUM: Sistemde hiç kullanıcı yoksa, rastgele güçlü bir şifreyle
        // otomatik bir "admin" kullanıcısı oluştur ve şifreyi SADECE BİR KEZ
        // konsola/log dosyasına yazdır. Böylece "admin/123456" gibi herkesin
        // bildiği sabit bir varsayılan şifre riski oluşmaz (bkz. AuthController
        // güvenlik notu). SPK Bülten Analiz projesindeki AuthService ile aynı
        // ilk-kurulum güvenliği deseni.
        // =====================================================================
        var kullaniciRepository = scope.ServiceProvider.GetRequiredService<IKullaniciRepository>();
        if (await kullaniciRepository.KullaniciSayisiAsync() == 0)
        {
            var rastgeleSifre = KAPNews.Business.Security.SifreHashService.RastgeleSifreUret();
            var adminKullanici = new KAPNews.Entities.Kullanici
            {
                KullaniciAdi = "admin",
                SifreHash = KAPNews.Business.Security.SifreHashService.Hashle(rastgeleSifre),
                Rol = "Admin",
                Aktif = true
            };
            await kullaniciRepository.EkleAsync(adminKullanici);

            Log.Warning(
                "═══════════════════════════════════════════════════════════════\n" +
                "İLK KURULUM: 'admin' kullanıcısı otomatik oluşturuldu.\n" +
                "Kullanıcı Adı: admin\n" +
                "Şifre        : {RastgeleSifre}\n" +
                "Bu şifreyi şimdi not alın — bir daha hiçbir yerde gösterilmeyecek.\n" +
                "═══════════════════════════════════════════════════════════════",
                rastgeleSifre);
        }
    }

    app.Run();
}
catch (Exception ex) when (ex is not Microsoft.Extensions.Hosting.HostAbortedException)
{
    Log.Fatal(ex, "Uygulama beklenmeyen bir şekilde başlatılamadı / çöktü.");
}
finally
{
    Log.CloseAndFlush();
}
