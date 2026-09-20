using KAPNews.Business.Security;
using KAPNews.DataAccess.Abstract;
using KAPNews.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace KAPNews.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IKullaniciRepository _kullaniciRepository;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IConfiguration configuration, IKullaniciRepository kullaniciRepository, ILogger<AuthController> logger)
        {
            _configuration = configuration;
            _kullaniciRepository = kullaniciRepository;
            _logger = logger;
        }

        // =====================================================================
        // v2.0.0 DÜZELTMESİ: Eskiden burada "admin" / "123456" SABİT KODLANMIŞ
        // (hardcoded) bir kullanıcı adı/şifre kontrolü vardı — bu, kaynak kodunu
        // gören HERKESİN admin paneline giriş yapabileceği kritik bir güvenlik
        // açığıydı. Artık SPK Bülten Analiz projesindeki AuthService ile aynı
        // desende: kullanıcılar veritabanında (Kullanicilar tablosu) saklanır,
        // şifreler PBKDF2 ile tuzlanıp hash'lenir, düz metin şifre hiçbir zaman
        // saklanmaz veya loglanmaz.
        // =====================================================================
        [HttpPost("giris")]
        public async Task<IActionResult> Giris([FromBody] GirisDto model)
        {
            if (string.IsNullOrWhiteSpace(model.KullaniciAdi) || string.IsNullOrWhiteSpace(model.Sifre))
                return BadRequest("Kullanıcı adı ve şifre gereklidir.");

            var kullanici = await _kullaniciRepository.KullaniciAdiIleGetirAsync(model.KullaniciAdi.Trim());

            if (kullanici == null || !kullanici.Aktif || !SifreHashService.Dogrula(model.Sifre, kullanici.SifreHash))
            {
                _logger.LogWarning("Başarısız giriş denemesi. Kullanıcı adı: {KullaniciAdi}", model.KullaniciAdi);
                return Unauthorized("Geçersiz kullanıcı adı veya şifre.");
            }

            kullanici.SonGirisTarihi = DateTime.UtcNow;
            await _kullaniciRepository.GuncelleAsync(kullanici);

            var token = TokenUret(kullanici);
            _logger.LogInformation("Admin girişi başarılı. Kullanıcı: {KullaniciAdi}", kullanici.KullaniciAdi);

            return Ok(new { token, message = "Giriş başarılı" });
        }

        private string TokenUret(Kullanici kullanici)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, kullanici.KullaniciAdi),
                new Claim(ClaimTypes.Role, kullanici.Rol)
            };

            var sureDakika = _configuration.GetValue<int?>("Jwt:SureDakika") ?? 120;

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(sureDakika),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

    public class GirisDto
    {
        public string KullaniciAdi { get; set; } = string.Empty;
        public string Sifre { get; set; } = string.Empty;
    }
}
