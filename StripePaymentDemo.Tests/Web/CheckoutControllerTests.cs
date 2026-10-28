using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Application.Payments;
using StripePaymentDemo.Application.Payments.Interfaces;
using StripePaymentDemo.Domain.Entities;
using StripePaymentDemo.Web.Controllers;
using StripePaymentDemo.Web.Models;

namespace StripePaymentDemo.Tests.Web;

public class CheckoutControllerTests
{
    private readonly IPaymentService _paymentService = A.Fake<IPaymentService>();
    private readonly IProductRepository _productRepository = A.Fake<IProductRepository>();
    private readonly ILogger<CheckoutController> _logger = A.Fake<ILogger<CheckoutController>>();
    private readonly CheckoutController _sut;

    public CheckoutControllerTests()
    {
        _sut = new CheckoutController(_paymentService, _productRepository, _logger);
    }

    private Product ArrangeProduct(bool isActive = true)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test product",
            Price = 15.75m,
            Currency = "GBP",
            IsActive = isActive
        };

        A.CallTo(() => _productRepository.GetByIdAsync(product.Id, A<CancellationToken>._))
            .Returns(product);

        return product;
    }

    private static void AssertCheckoutView(IActionResult result, Product product)
    {
        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("Index");
        view.Model.Should().BeEquivalentTo(new CheckoutViewModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Price = product.Price,
            Currency = product.Currency
        });
    }

    private void AssertLogged(LogLevel level) =>
        A.CallTo(_logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log)
                           && call.GetArgument<LogLevel>(0) == level)
            .MustHaveHappenedOnceExactly();

    // Index

    [Fact]
    public async Task Index_WithEmptyProductId_ReturnsBadRequest()
    {
        var result = await _sut.Index(Guid.Empty, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        A.CallTo(_productRepository).MustNotHaveHappened();
    }

    [Fact]
    public async Task Index_WhenProductMissing_ReturnsNotFound()
    {
        var productId = Guid.NewGuid();
        A.CallTo(() => _productRepository.GetByIdAsync(productId, A<CancellationToken>._))
            .Returns((Product?)null);

        var result = await _sut.Index(productId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Index_WithInactiveProduct_ReturnsNotFound()
    {
        var product = ArrangeProduct(isActive: false);

        var result = await _sut.Index(product.Id, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Index_WithActiveProduct_ReturnsViewWithProductDetails()
    {
        var product = ArrangeProduct();

        var result = await _sut.Index(product.Id, CancellationToken.None);

        result.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeEquivalentTo(new CheckoutViewModel
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Price = product.Price,
                Currency = product.Currency
            });
    }

    // CreateSession

    [Fact]
    public async Task CreateSession_WithInvalidModelState_ReloadsCheckoutViewWithoutCreatingSession()
    {
        var product = ArrangeProduct();
        _sut.ModelState.AddModelError(nameof(CheckoutViewModel.ProductId), "Required");

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = product.Id },
            CancellationToken.None);

        AssertCheckoutView(result, product);
        A.CallTo(_paymentService).MustNotHaveHappened();
    }

    [Fact]
    public async Task CreateSession_WhenCheckoutCreated_RedirectsToStripe()
    {
        var productId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        A.CallTo(() => _paymentService.CreateCheckoutAsync(
                A<CreateCheckoutRequest>.That.Matches(r => r.ProductId == productId),
                cts.Token))
            .Returns(new CheckoutResult("cs_test_123", "https://checkout.stripe.com/pay/cs_test_123"));

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = productId },
            cts.Token);

        result.Should().BeOfType<RedirectResult>()
            .Which.Url.Should().Be("https://checkout.stripe.com/pay/cs_test_123");
    }

    [Fact]
    public async Task CreateSession_WhenProductNotFound_ReturnsNotFound()
    {
        var productId = Guid.NewGuid();
        A.CallTo(() => _paymentService.CreateCheckoutAsync(A<CreateCheckoutRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new ProductNotFoundException(productId));

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = productId },
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateSession_WhenKeyNotFound_TreatsAsUnexpectedError()
    {
        var product = ArrangeProduct();
        A.CallTo(() => _paymentService.CreateCheckoutAsync(A<CreateCheckoutRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new KeyNotFoundException());

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = product.Id },
            CancellationToken.None);

        AssertCheckoutView(result, product);
        AssertLogged(LogLevel.Error);
    }

    [Fact]
    public async Task CreateSession_WhenProductUnavailable_LogsWarningAndReloadsViewWithError()
    {
        var product = ArrangeProduct();
        A.CallTo(() => _paymentService.CreateCheckoutAsync(A<CreateCheckoutRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new ProductUnavailableException(product.Id));

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = product.Id },
            CancellationToken.None);

        AssertCheckoutView(result, product);
        _sut.ModelState[string.Empty]!.Errors
            .Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("This product cannot be purchased currently");
        AssertLogged(LogLevel.Warning);
    }

    [Fact]
    public async Task CreateSession_WhenProductUnavailableAndProductNowInactive_ReturnsNotFound()
    {
        var product = ArrangeProduct(isActive: false);
        A.CallTo(() => _paymentService.CreateCheckoutAsync(A<CreateCheckoutRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new ProductUnavailableException(product.Id));

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = product.Id },
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateSession_WhenInvalidOperation_TreatsAsUnexpectedError()
    {
        // e.g. an EF Core failure or Stripe returning no checkout URL.
        var product = ArrangeProduct();
        A.CallTo(() => _paymentService.CreateCheckoutAsync(A<CreateCheckoutRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new InvalidOperationException("Stripe did not return a checkout URL."));

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = product.Id },
            CancellationToken.None);

        AssertCheckoutView(result, product);
        _sut.ModelState[string.Empty]!.Errors
            .Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("We couldn't start checkout. Please try again.");
        AssertLogged(LogLevel.Error);
    }

    [Fact]
    public async Task CreateSession_WithInvalidModelStateAndMissingProduct_ReturnsNotFound()
    {
        var productId = Guid.NewGuid();
        A.CallTo(() => _productRepository.GetByIdAsync(productId, A<CancellationToken>._))
            .Returns((Product?)null);
        _sut.ModelState.AddModelError(nameof(CheckoutViewModel.ProductId), "Required");

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = productId },
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task CreateSession_WhenUnexpectedError_LogsErrorAndReloadsViewWithError()
    {
        var product = ArrangeProduct();
        A.CallTo(() => _paymentService.CreateCheckoutAsync(A<CreateCheckoutRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new HttpRequestException("Stripe unavailable"));

        var result = await _sut.CreateSession(
            new CheckoutViewModel { ProductId = product.Id },
            CancellationToken.None);

        AssertCheckoutView(result, product);
        _sut.ModelState[string.Empty]!.Errors
            .Should().ContainSingle()
            .Which.ErrorMessage.Should().Be("We couldn't start checkout. Please try again.");
        AssertLogged(LogLevel.Error);
    }

    [Fact]
    public async Task CreateSession_WhenRequestCancelled_Rethrows()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        A.CallTo(() => _paymentService.CreateCheckoutAsync(A<CreateCheckoutRequest>._, A<CancellationToken>._))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var act = () => _sut.CreateSession(
            new CheckoutViewModel { ProductId = Guid.NewGuid() },
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        A.CallTo(_productRepository).MustNotHaveHappened();
    }

    // Success / Cancel

    [Fact]
    public void Success_ReturnsPendingResultView()
    {
        var result = _sut.Success();

        var model = result.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeOfType<PaymentResultViewModel>().Subject;
        model.Status.Should().Be("Pending");
        model.Message.Should().Contain("does not itself confirm");
    }

    [Fact]
    public void Cancel_ReturnsCancelledResultView()
    {
        var result = _sut.Cancel();

        var model = result.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeOfType<PaymentResultViewModel>().Subject;
        model.Status.Should().Be("Cancelled");
        model.Message.Should().StartWith("Checkout was cancelled.");
    }
}
