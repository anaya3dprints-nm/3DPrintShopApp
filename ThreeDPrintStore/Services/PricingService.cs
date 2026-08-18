using System;

namespace ThreeDPrintStore.Services
{
    public class PricingService
    {
        public decimal CalculateCustomPrintPrice(double gramsUsed, double printHours)
        {
            // You can adjust these base numbers anytime to match your actual operational costs!
            decimal filamentCostPerGram = 0.03m;  // ~$30 for a standard 1kg roll
            decimal electricityPerHour = 0.04m;   // Average printer power use
            decimal machineWearPerHour = 0.12m;   // Cost to replace nozzles, belts, beds over time
            decimal profitMarkup = 0.40m;         // Adds a 40% profit margin to the absolute cost

            // Math: Raw materials
            decimal materialCost = (decimal)gramsUsed * filamentCostPerGram;
            
            // Math: Running time costs
            decimal runningCost = (decimal)printHours * (electricityPerHour + machineWearPerHour);
            
            // Math: Combine and apply profit margin
            decimal baseCost = materialCost + runningCost;
            decimal finalPrice = baseCost * (1 + profitMarkup);
            
            return Math.Round(finalPrice, 2);
        }
    }
}
