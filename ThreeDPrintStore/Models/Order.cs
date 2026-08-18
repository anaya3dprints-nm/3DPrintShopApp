using System.ComponentModel.DataAnnotations;

namespace ThreeDPrintStore.Models
{
    public class Order
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full name is required")]
        public string CustomerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string CustomerEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Street address is required")]
        public string StreetAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "ZIP Code is required")]
        public string PostalCode { get; set; } = string.Empty;

        // Financial Breakdown Totals
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal GrandTotal => Subtotal + ShippingFee;

        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    }
}
