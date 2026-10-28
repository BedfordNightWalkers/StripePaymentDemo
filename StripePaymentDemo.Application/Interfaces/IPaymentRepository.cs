using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Application.Interfaces;

public interface IPaymentRepository
{
    Task AddAsync(
        Payment payment,
        CancellationToken cancellationToken);
    
    Task<Payment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
    
    Task UpdateAsync(
        Payment payment,
        CancellationToken cancellationToken);

}