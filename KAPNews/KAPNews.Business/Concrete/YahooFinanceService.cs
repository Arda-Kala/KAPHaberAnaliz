using System;
using System.Net.Http;
using System.Threading.Tasks;
using KAPNews.Business.Abstract;

namespace KAPNews.Business.Concrete
{
    public class YahooFinanceService : IYahooFinanceService
    {
        private readonly HttpClient _httpClient;

        public YahooFinanceService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        }

        public async Task<string> GetChartDataAsync(string symbol, long period1, long period2, string interval)
        {
            // range yerine period1 ve period2 kullanıldı, includePrePost=true yapıldı
            var endpoint = $"v8/finance/chart/{symbol}?period1={period1}&period2={period2}&interval={interval}&includePrePost=true";

            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Yahoo API Hatası: {(int)response.StatusCode} - {response.ReasonPhrase}. Detay: {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }
    }
}