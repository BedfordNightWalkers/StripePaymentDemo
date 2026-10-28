using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Application.Payments;
using StripePaymentDemo.Application.Payments.Interfaces;
using StripePaymentDemo.Infrastructure.Payments;
using StripePaymentDemo.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Database 
builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("PaymentDb")));

// Repositories
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IProductRepository,ProductRepository>();


// Application service
builder.Services.AddScoped<IPaymentService, PaymentService>();

builder.Services.Configure<StripeOptions>(
    builder.Configuration.GetSection(
        StripeOptions.SectionName));


builder.Services.AddScoped<IStripeCheckoutClient, StripeCheckoutClient>();


// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();