using Microsoft.EntityFrameworkCore;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Infrastructure.Persistence;

public sealed class ProductRepository : IProductRepository
{
    private readonly PaymentDbContext _context;

    public ProductRepository(PaymentDbContext context)
    {
        _context = context;
    }
    
    public async Task<Product?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                p => p.Id == id,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllActiveAsync(CancellationToken cancellationToken)
    {
        return await _context.Products
        .AsNoTracking()
        .Where(p => p.IsActive)
        .OrderBy(p => p.Price)
        .ToListAsync(cancellationToken);
    }
}