using System; //imports the System namespace, which provides basic .NET functionality
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Data; // or ThreeDPrintStore.Models depending on where your ApplicationDbContext.cs is


namespace ThreeDPrintStore.Services //declares the namespace where this service class lives
{
   public class QuoteService : IQuoteService
	{
		public readonly ApplicationDbContext _db;
		
		public QuoteService(ApplicationDbContext db)
		{
			_db = db;
		}
		
		public PrintQuote CalculateAndSaveQuote(QuoteCalculationInput input)
		{
			var request = _db.PrintRequests.Find(input.PrintRequestId)
				?? throw new InvalidOperationException($"Print request {input.PrintRequestId} not found.");
				
			//formula calculation
			decimal materialSubtotal = input.FilamentGrams * input.MaterialCostPerGram;
			decimal laborMachineSubtotal = input.EstimatedPrintHours * input.HourlyMachineRate;
			decimal multiColorSurcharge = input.ColorCount > 1
				? (input.ColorCount - 1) * input.MultiColorFeePerColor : 0.00m;
				
			decimal totalPrice = materialSubtotal
				+ laborMachineSubtotal
				+ multiColorSurcharge
				+ input.BaseHandlingFee;
			
			var quote = new PrintQuote
			{
				PrintRequestId = input.PrintRequestId,
				FilamentGrams = input.FilamentGrams,
				EstimatedPrintHours = input.EstimatedPrintHours,
				ColorCount = input.ColorCount,
				MaterialCostPerGram = input.MaterialCostPerGram,
				MaterialSubtotal = materialSubtotal,
				LaborMachineSubtotal = laborMachineSubtotal,
				MultiColorSurcharge = multiColorSurcharge,
				BaseHandlingFee = input.BaseHandlingFee,
				FinalTotalPrice = Math.Round(totalPrice, 2, MidpointRounding.AwayFromZero)
			};
			
			//update request status
			request.Status = QuoteStatus.QuoteSent;
			
			_db.PrintQuotes.Add(quote);
			_db.SaveChanges();
			
			return quote;
			
		}
	}
}
