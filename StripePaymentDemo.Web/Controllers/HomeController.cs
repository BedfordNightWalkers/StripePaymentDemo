using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Web.Models;

namespace StripePaymentDemo.Web.Controllers;

public class HomeController : Controller
{
    private readonly IProductRepository _productRepository;

    public HomeController(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }
    
    
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllActiveAsync(
            cancellationToken);
        
        return View(products);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}