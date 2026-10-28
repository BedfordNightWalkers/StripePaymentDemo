using Microsoft.AspNetCore.Mvc;
using StripePaymentDemo.Application.Interfaces;
using StripePaymentDemo.Application.Payments;
using StripePaymentDemo.Application.Payments.Interfaces;
using StripePaymentDemo.Web.Models;

namespace StripePaymentDemo.Web.Controllers;

public class CheckoutController : Controller
{
    private readonly IPaymentService  _paymentService;
    private readonly IProductRepository _productRepository;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        IPaymentService paymentService,
        IProductRepository productRepository,
        ILogger<CheckoutController> logger)
    {
        _paymentService = paymentService;
        _productRepository = productRepository;
        _logger =  logger;
    }
    
    // GET: /Checkout?productId=<guid>
    [HttpGet]
    public async Task<ActionResult> Index(
        Guid productId,
        CancellationToken cancellationToken)
    {
        if(productId == Guid.Empty)
        {
            return BadRequest("A valid Product ID is required.");
        }

        var product = await _productRepository.GetByIdAsync(
            productId,
            cancellationToken);

        if (product is null || !product.IsActive)
        {
            return NotFound("Product not found or is unavailable.");
        }
        
        var model = new CheckoutViewModel
        {
            ProductId = productId,
            ProductName = product.Name,
            Currency = product.Currency,
            Price = product.Price
        };

        return View(model);
    }

    // POST : /Checkout/CreateSession
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSession(
        CheckoutViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return await ReloadCheckoutViewAsync(
                model.ProductId,
                cancellationToken);
        }

        try
        {
            var result = await _paymentService.CreateCheckoutAsync(
                new CreateCheckoutRequest(model.ProductId),
                cancellationToken);

            // Redirect to Stripe's hosted checkout page
            return Redirect(result.CheckoutUrl);
        }
        catch (ProductNotFoundException)
        {
            return NotFound("Product not found or is unavailable.");
        }
        catch (ProductUnavailableException ex)
        {
            _logger.LogWarning(
                ex,
                "Checkout could not be started for product {ProductId}.",
                model.ProductId);

            ModelState.AddModelError(
                string.Empty,
                "This product cannot be purchased currently");


            return await ReloadCheckoutViewAsync(
                model.ProductId,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch(Exception ex)
        {
            _logger.LogError(
                ex,
                "An error occurred while starting checkout for product {ProductId}",
                model.ProductId);
            
            ModelState.AddModelError(
                string.Empty,
                "We couldn't start checkout. Please try again.");

            return await ReloadCheckoutViewAsync(
                model.ProductId,
                cancellationToken);
        }
    }
    
    // GET: /Checkout/Success
    public IActionResult Success()
    {
        var model = new PaymentResultViewModel
        {
            Status = "Pending",
            Message = "Thank you. We are checking your payment status. " + 
                "This page does not itself confirm that payment succeeded. "
        };

        return View(model);
    }
    
    // GET: /Checkout/Cancel
    public IActionResult Cancel()
    {
        var model = new PaymentResultViewModel
        {
            Status = "Cancelled",
            Message = "Checkout was cancelled. No successful payment " +
            "has been confirmed by this page."
        };

        return View(model);
    }

    private async Task<IActionResult> ReloadCheckoutViewAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(
            productId,
            cancellationToken);

        if (product is null || !product.IsActive)
        {
            return NotFound("Product not found or is unavailable.");
        }

        var model = new CheckoutViewModel
        {
            ProductId = product.Id,
            ProductName = product.Name,
            Price = product.Price,
            Currency = product.Currency
        };

        return View("Index", model);
    }
}