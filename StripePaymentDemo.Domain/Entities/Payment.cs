using StripePaymentDemo.Domain.Enums;

namespace StripePaymentDemo.Domain.Entities;

public class Payment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    
    public decimal Amount { get; private set; }
    
    public string Currency { get; private set; } = "GBP";
    
    public PaymentStatus  Status { get; private set; } = PaymentStatus.Pending;
    
    public string? StripeCheckoutSessionId { get; private set; }
    
    public string? StripePaymentInternalId  { get; private set; }
    
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private Payment() { } //For EF Core

    public Payment(decimal amount, string currency)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        
        Amount = amount;

        Currency = currency.ToUpperInvariant();
    }

    public void SetCheckoutSession(string sessionId)
    {
        StripeCheckoutSessionId = sessionId;
    }
    
    public void MarkAsPaid(string? paymentIntentId)
    {
        if (Status == PaymentStatus.Paid)
            return;
        
        Status = PaymentStatus.Paid;
        StripePaymentInternalId = paymentIntentId;
    }

    public void MarkAsFailed()
    {
        if (Status != PaymentStatus.Paid)
            Status =  PaymentStatus.Failed;
    }
}