using StripePaymentDemo.Domain.Entities;
using StripePaymentDemo.Domain.Enums;

namespace StripePaymentDemo.Tests.Domain;

public class PaymentTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Constructor_WithNonPositiveAmount_Throws(decimal amount)
    {
        var act = () => new Payment(amount, "GBP");

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("amount");
    }

    [Fact]
    public void Constructor_WithValidValues_InitialisesPendingPayment()
    {
        var before = DateTimeOffset.UtcNow;

        var payment = new Payment(19.99m, "gbp");

        payment.Id.Should().NotBeEmpty();
        payment.Amount.Should().Be(19.99m);
        payment.Currency.Should().Be("GBP");
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.StripeCheckoutSessionId.Should().BeNull();
        payment.StripePaymentInternalId.Should().BeNull();
        payment.CreatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public void Constructor_GeneratesUniqueIds()
    {
        var first = new Payment(10m, "GBP");
        var second = new Payment(10m, "GBP");

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void SetCheckoutSession_StoresSessionId()
    {
        var payment = new Payment(10m, "GBP");

        payment.SetCheckoutSession("cs_test_123");

        payment.StripeCheckoutSessionId.Should().Be("cs_test_123");
    }

    [Fact]
    public void MarkAsPaid_FromPending_SetsPaidStatusAndPaymentIntent()
    {
        var payment = new Payment(10m, "GBP");

        payment.MarkAsPaid("pi_123");

        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.StripePaymentInternalId.Should().Be("pi_123");
    }

    [Fact]
    public void MarkAsPaid_WhenAlreadyPaid_DoesNotOverwritePaymentIntent()
    {
        var payment = new Payment(10m, "GBP");
        payment.MarkAsPaid("pi_first");

        payment.MarkAsPaid("pi_second");

        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.StripePaymentInternalId.Should().Be("pi_first");
    }

    [Fact]
    public void MarkAsPaid_FromFailed_SetsPaidStatus()
    {
        var payment = new Payment(10m, "GBP");
        payment.MarkAsFailed();

        payment.MarkAsPaid("pi_123");

        payment.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public void MarkAsFailed_FromPending_SetsFailedStatus()
    {
        var payment = new Payment(10m, "GBP");

        payment.MarkAsFailed();

        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void MarkAsFailed_WhenAlreadyPaid_KeepsPaidStatus()
    {
        var payment = new Payment(10m, "GBP");
        payment.MarkAsPaid("pi_123");

        payment.MarkAsFailed();

        payment.Status.Should().Be(PaymentStatus.Paid);
        payment.StripePaymentInternalId.Should().Be("pi_123");
    }
}
