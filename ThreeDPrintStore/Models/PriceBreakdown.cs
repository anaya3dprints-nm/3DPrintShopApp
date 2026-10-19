namespace ThreeDPrintStore.Models
{
    public class PriceBreakdown
    {
        public decimal Materials { get; set; }
        public decimal MachineTime { get; set; }
        public decimal ServiceFee { get; set; }
        public decimal Total { get; set; }
    }
}
