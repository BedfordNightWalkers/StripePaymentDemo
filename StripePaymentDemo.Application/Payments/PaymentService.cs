using Microsoft.Extensions.Logging;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Application.Payments.Interfaces;
using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Application.Payments;

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStripeCheckoutClient  _stripeCheckoutClient;
    private readonly ILogger<PaymentService> _logger;
    
    public PaymentService(
        IPaymentRepository  paymentRepository,
        IProductRepository  productRepository,
        IStripeCheckoutClient stripeCheckoutClient,
        ILogger<PaymentService> logger)
    {
        _paymentRepository =  paymentRepository;
        _productRepository = productRepository;
        _stripeCheckoutClient = stripeCheckoutClient;
        _logger = logger;
    }
    
    public async Task<CheckoutResult> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        if(request.ProductId == Guid.Empty)
        {
            throw new ArgumentException(
                "A valid product ID is required.",
                nameof(request));
        }
        
        // 1. Retrieve trusted product details from our own system.
        var product = await _productRepository.GetByIdAsync(
            request.ProductId,
            cancellationToken);

        if (product is null)
        {
            throw new ProductNotFoundException(request.ProductId);
        }

        if (!product.IsActive)
        {
            throw new ProductUnavailableException(product.Id);
        }
        
        // 2. Create local payment in Pending status
        var payment = new Payment(
            product.Price,
            product.Currency);
        
        
        // 3. Persist payment before calling stripe
        await _paymentRepository.AddAsync(
            payment,
            cancellationToken);

        CheckoutResult checkout;

        try
        {
            // 4. Request a stripe checkout session
            checkout = await _stripeCheckoutClient.CreateSessionAsync(
                payment,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // The outcome of an external request may be uncertain.
            // Keep the persisted payment for reconciliation.
            throw;
        }
        catch (Exception ex)
        {
            // Do not assume Stripe failed to create a session:
            // a network error can occur after Stripe has processed it.
            _logger.LogError(
                ex,
                "Could not obtain a Checkout Session for payment {PaymentId}.",
                payment.Id);
            
            throw;
        }

        // 5. Store the Stripe Session ID against our payment
        payment.SetCheckoutSession(checkout.SessionId);
        
        await _paymentRepository.UpdateAsync(
            payment,
            cancellationToken);
        
        _logger.LogInformation(
            "Created Stripe Checkout Session for payment {PaymentId}.",
            payment.Id);
        
        // 6. Return session details to the caller
        return checkout;
    }

    public Task<Payment?> GetPaymentAsync(Guid id, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}