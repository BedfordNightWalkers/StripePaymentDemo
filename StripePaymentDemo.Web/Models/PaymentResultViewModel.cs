namespace StripePaymentDemo.Web.Models;

public sealed class PaymentResultViewModel
{
    public Guid? PaymentId { get; init; }

    public string ProductName { get; set; } = string.Empty;
    
    public string Message { get; init; } = string.Empty;
    
    public string Status { get; init; } = "Pending";
}