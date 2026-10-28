using System.ComponentModel.DataAnnotations;

namespace StripePaymentDemo.Web.Models;

public sealed class CheckoutViewModel
{
    [Required]
    public Guid ProductId { get; set;  }

    public string ProductName { get; set; } = string.Empty;
    
    public decimal Price { get; set; }

    public string Currency { get; set; } = "GBP";

}