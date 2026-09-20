# =============================================================================
# KAP Haberleri Analiz Platformu — Deployment Dockerfile
# =============================================================================
# Kullanım (repo kökünde, yani KAPNews.sln'in bulunduğu klasörde):
#   docker build -t kap-haberleri-analiz -f KAPNews.API/Dockerfile .
#   docker run -d -p 8080:8080 \
#     -e ConnectionStrings__DefaultConnection="Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True" \
#     -e Jwt__Key="<openssl rand -base64 48 ile üretilen değer>" \
#     -e GeminiApiKey="<Gemini API anahtarın>" \
#     -e Cors__AllowedOrigins__0="https://dashboard-adresin.com" \
#     -e ASPNETCORE_ENVIRONMENT="Production" \
#     --name kap-haberleri-analiz kap-haberleri-analiz
#
# İlk açılışta oluşturulan admin şifresini görmek için:
#   docker logs kap-haberleri-analiz
# =============================================================================

# --- 1) Derleme (build) aşaması ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce sadece .csproj dosyalarını kopyala → katman (layer) cache'i sayesinde
# kaynak kod değiştiğinde 'dotnet restore' tekrar tekrar çalışmaz.
COPY KAPNews.sln .
COPY Entities/KAPNews.Entities.csproj Entities/
COPY KAPNews.API/KAPNews.API.csproj KAPNews.API/
COPY KAPNews.Business/KAPNews.Business.csproj KAPNews.Business/
COPY KAPNews.DataAccess/KAPNews.DataAccess.csproj KAPNews.DataAccess/
RUN dotnet restore KAPNews.sln

# Şimdi geri kalan tüm kaynak kodu kopyala ve yayına hazırla (publish).
COPY . .
RUN dotnet publish KAPNews.API/KAPNews.API.csproj \
    -c Release -o /app/publish --no-restore

# --- 2) Çalıştırma (runtime) aşaması — küçük, sadece runtime içeren imaj ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Kestrel'in tüm ağ arayüzlerinde 8080 portunu dinlemesi için.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "KAPNews.API.dll"]
