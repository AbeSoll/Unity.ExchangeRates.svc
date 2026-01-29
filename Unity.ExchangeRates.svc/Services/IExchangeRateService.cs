namespace Unity.ExchangeRates.svc.Services
{
    public interface IExchangeRateService
    {
        // This defines the contract for getting rates from BNM
        Task<object> GetRatesFromBnmAsync();
    }
}
