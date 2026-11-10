
using ThreeDPrintStore.Models; // Or wherever QuoteStatus is defined
using System.ComponentModel.DataAnnotations;

namespace ThreeDPrintStore.Models
{
	
	public class PrintRequest
	{
		[Key]
		public int Id { get; set; }
		
		[Required]
		[MaxLength(100)]
		public string CustomerName { get; set; } = string.Empty;
		
		[Required]
		[MaxLength(150)]
		public string CustomerEmail { get; set; } = string.Empty;
		[Required]
		public string? ProjectDescription { get; set; }
		
		public string FilePath { get; set; } = string.Empty;
		
		public string MaterialType { get; set; } = "PLA";
		
		public int RequestedColorsCount { get; set; } = 1;
		
		public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
		
		public QuoteStatus Status { get; set; } = QuoteStatus.PendingReview;
		
		public PrintQuote? Quote { get; set; }
	}
}
