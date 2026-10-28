using Microsoft.Extensions.Logging;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Application.Payments;
using StripePaymentDemo.Domain.Entities;
using StripePaymentDemo.Domain.Enums;

namespace StripePaymentDemo.Tests.Application;

public class PaymentServiceTests
{
    private readonly IPaymentRepository _paymentRepository = A.Fake<IPaymentRepository>();
    private readonly IProductRepository _productRepository = A.Fake<IProductRepository>();
    private readonly IStripeCheckoutClient _stripeCheckoutClient = A.Fake<IStripeCheckoutClient>();
    private readonly ILogger<PaymentService> _logger = A.Fake<ILogger<PaymentService>>();
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _sut = new PaymentService(
            _paymentRepository,
            _productRepository,
            _stripeCheckoutClient,
            _logger);
    }

    private static Product CreateProduct(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Test product",
        Price = 25.50m,
        Currency = "gbp",
        IsActive = isActive
    };

    private Product ArrangeProduct(bool isActive = true)
    {
        var product = CreateProduct(isActive);

        A.CallTo(() => _productRepository.GetByIdAsync(product.Id, A<CancellationToken>._))
            .Returns(product);

        return product;
    }

    private void AssertLogged(LogLevel level) =>
        A.CallTo(_logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log)
                           && call.GetArgument<LogLevel>(0) == level)
            .MustHaveHappenedOnceExactly();

    private void AssertNotLogged(LogLevel level) =>
        A.CallTo(_logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log)
                           && call.GetArgument<LogLevel>(0) == level)
            .MustNotHaveHappened();

    [Fact]
    public async Task CreateCheckoutAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        var act = () => _sut.CreateCheckoutAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateCheckoutAsync_WithEmptyProductId_ThrowsArgumentException()
    {
        var act = () => _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(Guid.Empty),
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("request");

        A.CallTo(_productRepository).MustNotHaveHappened();
        A.CallTo(_paymentRepository).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenProductNotFound_ThrowsProductNotFoundException()
    {
        var productId = Guid.NewGuid();
        A.CallTo(() => _productRepository.GetByIdAsync(productId, A<CancellationToken>._))
            .Returns((Product?)null);

        var act = () => _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(productId),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ProductNotFoundException>())
            .Which.ProductId.Should().Be(productId);

        A.CallTo(_paymentRepository).MustNotHaveHappened();
        A.CallTo(_stripeCheckoutClient).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenProductInactive_ThrowsProductUnavailableException()
    {
        var product = ArrangeProduct(isActive: false);

        var act = () => _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(product.Id),
            CancellationToken.None);

        (await act.Should().ThrowAsync<ProductUnavailableException>())
            .Which.ProductId.Should().Be(product.Id);

        A.CallTo(_paymentRepository).MustNotHaveHappened();
        A.CallTo(_stripeCheckoutClient).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateCheckoutAsync_WithActiveProduct_ReturnsCheckoutFromStripe()
    {
        var product = ArrangeProduct();
        var expected = new CheckoutResult("cs_test_123", "https://checkout.stripe.com/pay/cs_test_123");
        A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(A<Payment>._, A<CancellationToken>._))
            .Returns(expected);

        var result = await _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(product.Id),
            CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WithActiveProduct_PersistsPendingPaymentFromProductDetails()
    {
        var product = ArrangeProduct();
        Payment? added = null;
        PaymentStatus? statusWhenAdded = null;
        A.CallTo(() => _paymentRepository.AddAsync(A<Payment>._, A<CancellationToken>._))
            .Invokes((Payment p, CancellationToken _) =>
            {
                added = p;
                statusWhenAdded = p.Status;
            });
        A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(A<Payment>._, A<CancellationToken>._))
            .Returns(new CheckoutResult("cs_test_123", "https://checkout.stripe.com/x"));

        await _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(product.Id),
            CancellationToken.None);

        added.Should().NotBeNull();
        added!.Amount.Should().Be(product.Price);
        added.Currency.Should().Be("GBP");
        statusWhenAdded.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WithActiveProduct_AddsPaymentThenCallsStripeThenUpdatesWithSessionId()
    {
        var product = ArrangeProduct();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(A<Payment>._, A<CancellationToken>._))
            .Returns(new CheckoutResult("cs_test_123", "https://checkout.stripe.com/x"));

        await _sut.CreateCheckoutAsync(new CreateCheckoutRequest(product.Id), token);

        A.CallTo(() => _paymentRepository.AddAsync(A<Payment>._, token))
            .MustHaveHappenedOnceExactly()
            .Then(A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(A<Payment>._, token))
                .MustHaveHappenedOnceExactly())
            .Then(A.CallTo(() => _paymentRepository.UpdateAsync(
                    A<Payment>.That.Matches(p => p.StripeCheckoutSessionId == "cs_test_123"),
                    token))
                .MustHaveHappenedOnceExactly());

        AssertLogged(LogLevel.Information);
    }

    [Fact]
    public async Task CreateCheckoutAsync_PassesSamePaymentToStripeAndUpdate()
    {
        var product = ArrangeProduct();
        Payment? added = null;
        A.CallTo(() => _paymentRepository.AddAsync(A<Payment>._, A<CancellationToken>._))
            .Invokes((Payment p, CancellationToken _) => added = p);
        A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(A<Payment>._, A<CancellationToken>._))
            .Returns(new CheckoutResult("cs_test_123", "https://checkout.stripe.com/x"));

        await _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(product.Id),
            CancellationToken.None);

        A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(
                A<Payment>.That.IsSameAs(added!), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _paymentRepository.UpdateAsync(
                A<Payment>.That.IsSameAs(added!), A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenStripeFails_LogsErrorRethrowsAndDoesNotUpdate()
    {
        var product = ArrangeProduct();
        var failure = new HttpRequestException("Network failure");
        A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(A<Payment>._, A<CancellationToken>._))
            .ThrowsAsync(failure);

        var act = () => _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(product.Id),
            CancellationToken.None);

        (await act.Should().ThrowAsync<HttpRequestException>())
            .Which.Should().BeSameAs(failure);

        A.CallTo(() => _paymentRepository.AddAsync(A<Payment>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _paymentRepository.UpdateAsync(A<Payment>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        AssertLogged(LogLevel.Error);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenCancelledDuringStripeCall_RethrowsWithoutLoggingError()
    {
        var product = ArrangeProduct();
        using var cts = new CancellationTokenSource();
        A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(A<Payment>._, A<CancellationToken>._))
            .ReturnsLazily(() =>
            {
                cts.Cancel();
                return Task.FromCanceled<CheckoutResult>(cts.Token);
            });

        var act = () => _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(product.Id),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        A.CallTo(() => _paymentRepository.AddAsync(A<Payment>._, A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
        A.CallTo(() => _paymentRepository.UpdateAsync(A<Payment>._, A<CancellationToken>._))
            .MustNotHaveHappened();
        AssertNotLogged(LogLevel.Error);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenStripeThrowsCancellationWithoutTokenCancelled_LogsError()
    {
        var product = ArrangeProduct();
        A.CallTo(() => _stripeCheckoutClient.CreateSessionAsync(A<Payment>._, A<CancellationToken>._))
            .ThrowsAsync(new TaskCanceledException("HTTP timeout"));

        var act = () => _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(product.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>();

        AssertLogged(LogLevel.Error);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WhenPersistingPaymentFails_DoesNotCallStripe()
    {
        var product = ArrangeProduct();
        A.CallTo(() => _paymentRepository.AddAsync(A<Payment>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var act = () => _sut.CreateCheckoutAsync(
            new CreateCheckoutRequest(product.Id),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Database unavailable");

        A.CallTo(_stripeCheckoutClient).MustNotHaveHappened();
    }
}
