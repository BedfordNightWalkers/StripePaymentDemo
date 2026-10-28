namespace StripePaymentDemo.Application.Payments;

public sealed record CheckoutResult(
    string SessionId,
    string CheckoutUrl);