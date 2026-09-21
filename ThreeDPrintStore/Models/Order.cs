//Imports the DataAnnotations library, which lets you use attributes like [Required]
using System.ComponentModel.DataAnnotations;

namespace ThreeDPrintStore.Models //declares namespace
{
    public class Order //this model represents an order placed in my store
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full name is required")] //adds validation: the field must be filled in, if not the error message is displayed
        public string CustomerName { get; set; } = string.Empty; //stores the customer's full name. Default value us an empty string

        [Required(ErrorMessage = "Email is required")] //email must be provided
        [EmailAddress(ErrorMessage = "Invalid email address")] //additional validation
        public string CustomerEmail { get; set; } = string.Empty; //stores the customer's email address

        [Required(ErrorMessage = "Street address is required")] //required field
        public string StreetAddress { get; set; } = string.Empty; //stores the order's street address
 
        [Required(ErrorMessage = "City is required")] //required field
        public string City { get; set; } = string.Empty; //stores cutomer city

        [Required(ErrorMessage = "ZIP Code is required")] //required field
        public string PostalCode { get; set; } = string.Empty; //stores the postal code

        // Financial Breakdown Totals
        public decimal Subtotal { get; set; } //the subtotal cost of all cart items before shipping
        public decimal ShippingFee { get; set; } //cost of shipping added after subtotal
        public decimal GrandTotal => Subtotal + ShippingFee; //a computed property (read-only) that returns: Subtotal + Shippingfee

        //Stores the date the order was created
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        
    }
}
