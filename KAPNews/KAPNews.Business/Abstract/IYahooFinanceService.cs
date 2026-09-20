using System.Threading.Tasks;

namespace KAPNews.Business.Abstract
{
    public interface IYahooFinanceService
    {
        // period1 ve period2 (Unix Timestamp) parametreleri eklendi
        Task<string> GetChartDataAsync(string symbol, long period1, long period2, string interval);
    }
}
