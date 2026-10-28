using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
    
    Task<IReadOnlyList<Product>> GetAllActiveAsync(
        CancellationToken cancellationToken);
}