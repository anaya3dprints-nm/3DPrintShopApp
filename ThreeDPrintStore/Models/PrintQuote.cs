using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ThreeDPrintStore.Models
{
	
	public class PrintQuote
	{
		[Key]
		public int Id { get; set; }
		
		[ForeignKey(nameof(PrintRequest))]
		public int PrintRequestId { get; set; }
		public PrintRequest PrintRequest { get; set; }
		
		//slicer inputs
		[Column(TypeName = "decimal(18,2)")]
		public decimal FilamentGrams { get; set; }
	
		
		[Column(TypeName = "decimal(18,2)")]
		public decimal EstimatedPrintHours { get; set; }
		
		public int ColorCount { get; set; }
		
		[Column(TypeName = "decimal(18,2)")]
		public decimal MaterialCostPerGram { get; set; }
		
		//Pricing Breakdown Results
		[Column(TypeName = "decimal(18,2)")]
		public decimal MaterialSubtotal { get; set; }
		
		[Column(TypeName = "decimal(18,2)")]
		public decimal LaborMachineSubtotal { get; set; }
		
		[Column(TypeName = "decimal(18,2)")]
		public decimal MultiColorSurcharge { get; set; }
		
		[Column(TypeName = "decimal(18,2)")]
		public decimal BaseHandlingFee { get; set; }
		
		[Column(TypeName = "decimal(18,2)")]
		public decimal FinalTotalPrice { get; set; }
		
		public DateTime CalculatedAt { get; set; }
		public string? StripePaymentIntentId { get; set; }
		
	}
}
