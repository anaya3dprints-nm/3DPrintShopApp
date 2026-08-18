using System;
using System.Linq;

namespace ThreeDPrintStore.Services
{
    public class ShippingService
    {
        // Target ZIP codes for the Albuquerque area
        private readonly string[] AlbuquerqueZips = { 
            "87101", "87102", "87104", "87106", "87108", "87109", "87110", 
            "87111", "87112", "87113", "87114", "87120", "87121", "87122" 
        };

        public decimal CalculateShipping(string city, string zip)
        {
            if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(zip)) 
                return 8.50m; // Default fallback shipping fee
            
            // Check if user typed Albuquerque OR used a local ZIP code
            bool isLocal = city.Trim().Equals("Albuquerque", StringComparison.OrdinalIgnoreCase) 
                           || AlbuquerqueZips.Contains(zip.Trim());
                             
            return isLocal ? 0.00m : 8.50m; // $0.00 for ABQ, $8.50 flat rate outside
        }
    }
}
