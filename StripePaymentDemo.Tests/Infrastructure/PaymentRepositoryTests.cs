using Microsoft.EntityFrameworkCore;
using StripePaymentDemo.Domain.Entities;
using StripePaymentDemo.Domain.Enums;
using StripePaymentDemo.Infrastructure.Persistence;

namespace StripePaymentDemo.Tests.Infrastructure;

public class PaymentRepositoryTests
{
    private readonly DbContextOptions<PaymentDbContext> _options =
        new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private PaymentDbContext CreateContext() => new(_options);

    [Fact]
    public async Task AddAsync_PersistsPayment()
    {
        var payment = new Payment(42.10m, "GBP");

        await using (var context = CreateContext())
        {
            await new PaymentRepository(context).AddAsync(payment, CancellationToken.None);
        }

        await using var verify = CreateContext();
        var stored = await verify.Payments.SingleAsync();
        stored.Id.Should().Be(payment.Id);
        stored.Amount.Should().Be(42.10m);
        stored.Currency.Should().Be("GBP");
        stored.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task GetByIdAsync_WhenPaymentExists_ReturnsPayment()
    {
        var payment = new Payment(10m, "GBP");
        await using (var context = CreateContext())
        {
            context.Payments.Add(payment);
            await context.SaveChangesAsync();
        }

        await using var readContext = CreateContext();
        var result = await new PaymentRepository(readContext)
            .GetByIdAsync(payment.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(payment.Id);
        result.Amount.Should().Be(10m);
    }

    [Fact]
    public async Task GetByIdAsync_WhenPaymentMissing_ReturnsNull()
    {
        await using var context = CreateContext();

        var result = await new PaymentRepository(context)
            .GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_PersistsChanges()
    {
        var payment = new Payment(10m, "GBP");
        await using (var context = CreateContext())
        {
            await new PaymentRepository(context).AddAsync(payment, CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            var repository = new PaymentRepository(context);
            var loaded = await repository.GetByIdAsync(payment.Id, CancellationToken.None);
            loaded!.SetCheckoutSession("cs_test_123");
            loaded.MarkAsPaid("pi_123");

            await repository.UpdateAsync(loaded, CancellationToken.None);
        }

        await using var verify = CreateContext();
        var stored = await verify.Payments.SingleAsync(p => p.Id == payment.Id);
        stored.StripeCheckoutSessionId.Should().Be("cs_test_123");
        stored.StripePaymentInternalId.Should().Be("pi_123");
        stored.Status.Should().Be(PaymentStatus.Paid);
    }

    [Fact]
    public async Task UpdateAsync_WithDetachedPayment_PersistsChanges()
    {
        var payment = new Payment(10m, "GBP");
        await using (var context = CreateContext())
        {
            await new PaymentRepository(context).AddAsync(payment, CancellationToken.None);
        }

        payment.SetCheckoutSession("cs_test_456");
        await using (var context = CreateContext())
        {
            await new PaymentRepository(context).UpdateAsync(payment, CancellationToken.None);
        }

        await using var verify = CreateContext();
        var stored = await verify.Payments.SingleAsync(p => p.Id == payment.Id);
        stored.StripeCheckoutSessionId.Should().Be("cs_test_456");
    }
}
