using Microsoft.EntityFrameworkCore;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Domain.Entities;

namespace StripePaymentDemo.Infrastructure.Persistence;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _context;

    public PaymentRepository(PaymentDbContext context)
    {
        _context = context;
    }
    
    public async Task AddAsync(Payment payment, CancellationToken cancellationToken)
    {
        await _context.Payments.AddAsync(
            payment,
            cancellationToken);
        
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Payment?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await _context.Payments
            .SingleOrDefaultAsync(
                p => p.Id == id,
                cancellationToken);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken cancellationToken)
    {
        _context.Payments.Update(payment);
        
        await _context.SaveChangesAsync(cancellationToken);
    }
}