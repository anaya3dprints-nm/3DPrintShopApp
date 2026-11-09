using System; //imports the System namespace, which provides basic .NET functionality
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Services //declares the namespace where this service class lives
{
    public class PricingService //class handles cost calculations for 3D printed jobs
    {

        /**
            Defines a method that:
                Returns a decimal (money-safe value)
                Takes:
                    gramsUsed → amount of filament used
                    printHours → number of hours the printer will run
            This method calculates the final price of a custom print. 
        **/
        public PriceBreakdown CalculateCustomPrintPrice(double gramsUsed, double printHours, int colorCount, double shippingCost)
        {
            // You can adjust these base numbers anytime to match your actual operational costs!
            decimal filamentCostPerGram = 0.05m;  // covers premium and waste, support
            decimal hourlyMachineRate = 2.50m;   // Average printer power use
            decimal baseServiceFee = 5.00m;   // Cost to replace nozzles, belts, beds over time
            //decimal profitMarkup = 0.40m;         // Adds a 40% profit margin to the absolute cost

            // Math: Raw materials
            decimal materialCost = (decimal)gramsUsed * filamentCostPerGram;
            decimal machineTimeCost = (decimal)printHours * hourlyMachineRate;
            
            decimal multiColorFee = colorCount > 1 ? (colorCount - 1) * 3.00m : 0.0m;

            decimal subtotal = materialCost + machineTimeCost + baseServiceFee;
            decimal finalPrice = (subtotal * 1.30m) + multiColorFee + (decimal)shippingCost;
            
            return new PriceBreakdown
            {
                Materials =  Math.Round(materialCost, 2),
                MachineTime = Math.Round(machineTimeCost, 2),
                ServiceFee = Math.Round(baseServiceFee + multiColorFee, 2),
                Total = Math.Round(finalPrice, 2)
            };
        }
    }
}
