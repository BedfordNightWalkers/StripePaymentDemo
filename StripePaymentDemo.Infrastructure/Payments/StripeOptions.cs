namespace StripePaymentDemo.Infrastructure.Payments;

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";
    
    public string SecretKey { get; init; } = string.Empty;
    
    public string Successurl { get; init; } = string.Empty;
    
    public string Cancelurl { get; init; } = string.Empty;
}