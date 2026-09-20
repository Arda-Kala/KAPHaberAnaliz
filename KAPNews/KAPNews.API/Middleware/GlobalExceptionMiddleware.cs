using System.Net;
using System.Text.Json;

namespace KAPNews.API.Middleware
{
    /// <summary>
    /// Merkezi (global) hata yakalama middleware'i. Pipeline'ın EN DIŞ katmanıdır
    /// (Program.cs'te en başa eklenir) — kendinden SONRAKİ her şeyde (controller'lar,
    /// authentication, CORS) fırlatılan ve yakalanmayan (unhandled) her exception'ı
    /// tek bir noktadan yakalar.
    ///
    /// SPK Bülten Analiz projesindeki GlobalExceptionMiddleware ile BİREBİR AYNI
    /// mimari role sahiptir — iki proje arasında tutarlı bir hata yönetim deneyimi
    /// sağlamak için kasıtlı olarak aynı desende yazılmıştır.
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HataylaBasEt(context, ex);
            }
        }

        private async Task HataylaBasEt(HttpContext context, Exception ex)
        {
            var (statusCode, seviye, kullaniciMesaji) = ex switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Unauthorized, LogLevel.Warning, "Bu işlem için yetkiniz bulunmuyor."),
                ArgumentException or InvalidOperationException => (HttpStatusCode.BadRequest, LogLevel.Warning, ex.Message),
                KeyNotFoundException => (HttpStatusCode.NotFound, LogLevel.Warning, "İstenen kayıt bulunamadı."),
                HttpRequestException => (HttpStatusCode.BadGateway, LogLevel.Error, "Dış bir servise (KAP sitesi veya Gemini API) ulaşılamadı."),
                TaskCanceledException or TimeoutException => (HttpStatusCode.GatewayTimeout, LogLevel.Error, "İstek zaman aşımına uğradı."),
                _ => (HttpStatusCode.InternalServerError, LogLevel.Error, "Beklenmeyen bir sunucu hatası oluştu.")
            };

            _logger.Log(seviye, ex,
                "İşlenmeyen hata yakalandı. Yol: {RequestMethod} {RequestPath} | Durum Kodu: {StatusCode} | Kullanıcı: {KullaniciAdi}",
                context.Request.Method,
                context.Request.Path,
                (int)statusCode,
                context.User?.Identity?.Name ?? "anonim");

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            var hataYaniti = new HataYanitDto
            {
                Basarili = false,
                Mesaj = kullaniciMesaji,
                HataKodu = ex.GetType().Name,
                IzlemeId = context.TraceIdentifier,
                DetayGeliştirmeModu = _env.IsDevelopment() ? ex.ToString() : null
            };

            var json = JsonSerializer.Serialize(hataYaniti, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }

    public class HataYanitDto
    {
        public bool Basarili { get; set; }
        public string Mesaj { get; set; } = string.Empty;
        public string HataKodu { get; set; } = string.Empty;
        public string IzlemeId { get; set; } = string.Empty;
        public string? DetayGeliştirmeModu { get; set; }
    }

    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        {
            return app.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}
