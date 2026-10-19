namespace ThreeDPrintStore.Models
{
    public class Quote
    {
        public int Id { get; set; }
        public string? CustomerEmail { get; set; }

        public decimal Materials { get; set; }
        public decimal MachineTime { get; set; }
        public decimal ServiceFee { get; set; }
        public decimal Total { get; set; }
        public string? Status { get; set; } // Pending, Accepted, Declined
        public DateTime CreatedAt { get; set; }

        public DateTime? PrintedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public decimal ShippingCost { get; set; }
        public string? ShippingMethod { get; set; }
    }    
}
