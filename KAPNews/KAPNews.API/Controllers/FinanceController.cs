using Microsoft.AspNetCore.Mvc;
using KAPNews.Business.Abstract;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace KAPNews.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinanceController : ControllerBase
    {
        private readonly IYahooFinanceService _yahooService;

        public FinanceController(IYahooFinanceService yahooService)
        {
            _yahooService = yahooService;
        }

        [HttpGet("chart/{symbol}")]
        public async Task<IActionResult> GetChart(
            string symbol,
            [FromQuery] int days = 7, // Kaç günlük veri istendiği (Varsayılan: 7 gün)
            [FromQuery] string interval = "1d")
        {
            try
            {
                // Bitiş tarihi: Şu an (Bugünün en güncel saati)
                var now = DateTimeOffset.UtcNow;
                long period2 = now.ToUnixTimeSeconds();

                // Başlangıç tarihi: İstenen gün kadar gerisi
                long period1 = now.AddDays(-days).ToUnixTimeSeconds();

                var jsonData = await _yahooService.GetChartDataAsync(symbol, period1, period2, interval);
                return Content(jsonData, "application/json");
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(500, new
                {
                    message = "Finansal veri servisi isteği reddetti.",
                    error = ex.Message,
                    symbol = symbol
                });
            }
        }
    }
}