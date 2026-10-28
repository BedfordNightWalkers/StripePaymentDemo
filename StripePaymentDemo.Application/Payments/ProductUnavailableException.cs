namespace StripePaymentDemo.Application.Payments;

public sealed class ProductUnavailableException : Exception
{
    public ProductUnavailableException(Guid productId)
        : base($"Product '{productId}' is not available for purchase.")
    {
        ProductId = productId;
    }

    public Guid ProductId { get; }
}
