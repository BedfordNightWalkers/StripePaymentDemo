namespace StripePaymentDemo.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; set; }
    
    public string Name { get; set; }  = string.Empty;
    
    public decimal Price { get; set; }

    public string Currency { get; set; } = "GBP";
    
    public bool IsActive { get; set; }
}