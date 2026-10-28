using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Application.Payments;
using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Infrastructure.Payments;

public sealed class StripeCheckoutClient : IStripeCheckoutClient
{
    private readonly SessionService _sessionService;
    private readonly StripeOptions _options;
    
    public StripeCheckoutClient(
        IOptions<StripeOptions> options)
    {
        _options = options.Value;
        
        if(string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException(
                "Stripe:SecretKey is not configured");
        }

        var stripeClient = new StripeClient(_options.SecretKey);
        
        _sessionService = new SessionService(stripeClient);
    }
    
    public async Task<CheckoutResult> CreateSessionAsync(
        Payment payment,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payment);
        
        //This learning application supports GBP only
        if (!string.Equals(
                payment.Currency,
                "GBP",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "This application currently supports GBP only.");
        }

        if (payment.Amount <= 0 ||
            payment.Amount != decimal.Round(payment.Amount, 2))
        {
            throw new ArgumentException(
                "Payment must be positive and have no more than " +
                "two decimal places.",
                nameof(payment));
        }
        
        var amountInPence = decimal.ToInt64(
            payment.Amount * 100m);
        
        var requestOptions = new RequestOptions
        {
            //Retries for this same local payment reuse the same key.
            IdempotencyKey = $"checkout-session-{payment.Id}"
        };

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "payment",

            SuccessUrl = _options.Successurl
                         + "?session_id={CHECKOUT_SESSION_ID}",

            CancelUrl = _options.Cancelurl,
            ClientReferenceId = payment.Id.ToString(),

            Metadata = new Dictionary<string, string>
            {
                ["paymentId"] = payment.Id.ToString()
            },

            LineItems = new List<SessionLineItemOptions>
            {
                new()
                {
                    Quantity = 1,

                    PriceData = new SessionLineItemPriceDataOptions()
                    {
                        Currency = "gbp",

                        UnitAmount = amountInPence,

                        ProductData =
                            new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = "Test product"
                            }
                    }
                }
            }
        };

        var session = await _sessionService.CreateAsync(
            sessionOptions,
            requestOptions,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(session.Url))
        {
            throw new InvalidOperationException(
                "Stripe did not return a checkout URL.");
        }

        return new CheckoutResult(
            session.Id,
            session.Url);
    }
}