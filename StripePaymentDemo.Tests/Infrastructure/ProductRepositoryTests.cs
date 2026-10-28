using Microsoft.EntityFrameworkCore;
using StripePaymentDemo.Domain.Entities;
using StripePaymentDemo.Infrastructure.Persistence;

namespace StripePaymentDemo.Tests.Infrastructure;

public class ProductRepositoryTests
{
    private readonly DbContextOptions<PaymentDbContext> _options =
        new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private PaymentDbContext CreateContext() => new(_options);

    private async Task SeedAsync(params Product[] products)
    {
        await using var context = CreateContext();
        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }

    private static Product CreateProduct(string name, decimal price, bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Price = price,
        Currency = "GBP",
        IsActive = isActive
    };

    [Fact]
    public async Task GetByIdAsync_WhenProductExists_ReturnsProduct()
    {
        var product = CreateProduct("Book", 12.99m);
        await SeedAsync(product, CreateProduct("Other", 5m));

        await using var context = CreateContext();
        var result = await new ProductRepository(context)
            .GetByIdAsync(product.Id, CancellationToken.None);

        result.Should().BeEquivalentTo(product);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsUntrackedEntity()
    {
        var product = CreateProduct("Book", 12.99m);
        await SeedAsync(product);

        await using var context = CreateContext();
        await new ProductRepository(context).GetByIdAsync(product.Id, CancellationToken.None);

        context.ChangeTracker.Entries().Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductMissing_ReturnsNull()
    {
        await SeedAsync(CreateProduct("Book", 12.99m));

        await using var context = CreateContext();
        var result = await new ProductRepository(context)
            .GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllActiveAsync_ReturnsOnlyActiveProductsOrderedByPrice()
    {
        var expensive = CreateProduct("Expensive", 99.99m);
        var cheap = CreateProduct("Cheap", 1.50m);
        var middle = CreateProduct("Middle", 20m);
        var inactive = CreateProduct("Inactive", 0.50m, isActive: false);
        await SeedAsync(expensive, cheap, middle, inactive);

        await using var context = CreateContext();
        var result = await new ProductRepository(context)
            .GetAllActiveAsync(CancellationToken.None);

        result.Select(p => p.Name).Should().Equal("Cheap", "Middle", "Expensive");
    }

    [Fact]
    public async Task GetAllActiveAsync_WhenNoActiveProducts_ReturnsEmptyList()
    {
        await SeedAsync(CreateProduct("Inactive", 10m, isActive: false));

        await using var context = CreateContext();
        var result = await new ProductRepository(context)
            .GetAllActiveAsync(CancellationToken.None);

        result.Should().BeEmpty();
    }
}
