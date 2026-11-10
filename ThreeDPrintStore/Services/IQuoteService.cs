using ThreeDPrintStore.Models;
namespace ThreeDPrintStore.Services
{
	public record QuoteCalculationInput(
		int PrintRequestId,
		decimal FilamentGrams,
		decimal EstimatedPrintHours,
		int ColorCount,
		decimal MaterialCostPerGram = 0.03m,
		decimal HourlyMachineRate = 2.50m,
		decimal MultiColorFeePerColor = 2.00m,
		decimal BaseHandlingFee = 4.00m
	);
	
	public interface IQuoteService
	{
		PrintQuote CalculateAndSaveQuote(QuoteCalculationInput input);
	}
}
