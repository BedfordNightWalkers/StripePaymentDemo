using StripePaymentDemo.Application.Payments;
using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Application.Interfaces;

public interface IStripeCheckoutClient
{
    Task<CheckoutResult> CreateSessionAsync(
        Payment payment,
        CancellationToken cancellationToken);
}