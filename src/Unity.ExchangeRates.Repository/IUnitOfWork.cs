namespace Unity.ExchangeRates.Repository
{
    public interface IUnitOfWork : IDisposable
    {
        IExchangeRateRepository ExchangeRates { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
        Task BeginTransactionAsync(CancellationToken cancellationToken);
        Task CommitAsync(CancellationToken cancellationToken);
        Task RollbackAsync(CancellationToken cancellationToken);
    }
}