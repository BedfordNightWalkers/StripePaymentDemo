using Microsoft.Extensions.Options;
using StripePaymentDemo.Domain.Entities;
using StripePaymentDemo.Infrastructure.Payments;

namespace StripePaymentDemo.Tests.Infrastructure;

// These tests cover validation that happens before any call to Stripe,
// so no network access or real API key is needed.
public class StripeCheckoutClientTests
{
    private static IOptions<StripeOptions> CreateOptions(string secretKey = "sk_test_fake")
    {
        var options = A.Fake<IOptions<StripeOptions>>();
        A.CallTo(() => options.Value).Returns(new StripeOptions
        {
            SecretKey = secretKey,
            Successurl = "https://localhost/Checkout/Success",
            Cancelurl = "https://localhost/Checkout/Cancel"
        });
        return options;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithMissingSecretKey_ThrowsInvalidOperationException(string secretKey)
    {
        var act = () => new StripeCheckoutClient(CreateOptions(secretKey));

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Stripe:SecretKey is not configured");
    }

    [Fact]
    public void Constructor_WithSecretKey_DoesNotThrow()
    {
        var act = () => new StripeCheckoutClient(CreateOptions());

        act.Should().NotThrow();
    }

    [Fact]
    public async Task CreateSessionAsync_WithNullPayment_ThrowsArgumentNullException()
    {
        var sut = new StripeCheckoutClient(CreateOptions());

        var act = () => sut.CreateSessionAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("eur")]
    public async Task CreateSessionAsync_WithNonGbpCurrency_ThrowsNotSupportedException(string currency)
    {
        var sut = new StripeCheckoutClient(CreateOptions());

        var act = () => sut.CreateSessionAsync(new Payment(10m, currency), CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*GBP only*");
    }

    [Theory]
    [InlineData(10.001)]
    [InlineData(0.005)]
    public async Task CreateSessionAsync_WithMoreThanTwoDecimalPlaces_ThrowsArgumentException(decimal amount)
    {
        var sut = new StripeCheckoutClient(CreateOptions());

        var act = () => sut.CreateSessionAsync(new Payment(amount, "GBP"), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("payment");
    }
}
