using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Application.Payments.Interfaces;

public interface IPaymentService
{
    Task<CheckoutResult> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken);
    
    Task<Payment?> GetPaymentAsync(
        Guid id,
        CancellationToken cancellationToken);
}