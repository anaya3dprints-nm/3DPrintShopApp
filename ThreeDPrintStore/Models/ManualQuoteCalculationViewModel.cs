namespace ThreeDPrintStore.Models
{
    public class ManualQuoteCalculationViewModel
    {
        public int QuoteRequestId { get; set; }
        public string CustomerEmail { get; set; } = string.Empty;
        public string Material { get; set; } = string.Empty;
        
        // Inputs from your slicer
        public double GramsUsed { get; set; }
        public double PrintHours { get; set; }
    }
}