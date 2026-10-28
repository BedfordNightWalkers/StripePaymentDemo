using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Domain.Entities;
using StripePaymentDemo.Web.Controllers;
using StripePaymentDemo.Web.Models;

namespace StripePaymentDemo.Tests.Web;

public class HomeControllerTests
{
    private readonly IProductRepository _productRepository = A.Fake<IProductRepository>();
    private readonly HomeController _sut;

    public HomeControllerTests()
    {
        _sut = new HomeController(_productRepository);
    }

    [Fact]
    public async Task Index_ReturnsViewWithActiveProducts()
    {
        IReadOnlyList<Product> products =
        [
            new() { Id = Guid.NewGuid(), Name = "A", Price = 1m, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "B", Price = 2m, IsActive = true }
        ];
        using var cts = new CancellationTokenSource();
        A.CallTo(() => _productRepository.GetAllActiveAsync(cts.Token)).Returns(products);

        var result = await _sut.Index(cts.Token);

        result.Should().BeOfType<ViewResult>()
            .Which.Model.Should().BeSameAs(products);
    }

    [Fact]
    public void Privacy_ReturnsView()
    {
        var result = _sut.Privacy();

        result.Should().BeOfType<ViewResult>();
    }

    [Fact]
    public void Error_WithoutCurrentActivity_UsesTraceIdentifier()
    {
        var previous = Activity.Current;
        Activity.Current = null;
        try
        {
            _sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { TraceIdentifier = "trace-123" }
            };

            var result = _sut.Error();

            result.Should().BeOfType<ViewResult>()
                .Which.Model.Should().BeOfType<ErrorViewModel>()
                .Which.RequestId.Should().Be("trace-123");
        }
        finally
        {
            Activity.Current = previous;
        }
    }
}
