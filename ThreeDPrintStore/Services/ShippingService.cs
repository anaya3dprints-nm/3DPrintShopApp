using System; //Imports the base .NET features — strings, math functions, basic types, etc.
using System.Linq; //Imports LINQ extensions.Used for functions like .Contains(), .Where(), etc.


namespace ThreeDPrintStore.Services //This means the class belongs to your application’s “Services” layer.

{
    public class ShippingService
    {
        
        /** This creates an array containing all ZIP codes considered “local” to Albuquerque.
            private → only this class can access it
            readonly → cannot be reassigned after initialization
            array of ZIP strings → used to determine whether a customer qualifies for local delivery
        Your shipping calculation checks against this list.
        Target ZIP codes for the Albuquerque area
        **/
        private readonly string[] AlbuquerqueZips = { 
            "87101", "87102", "87104", "87106", "87108", "87109", "87110", 
            "87111", "87112", "87113", "87114", "87120", "87121", "87122" 
        };

        public decimal CalculateShipping(string city, string zip) //defines a public method that calculates shipping cost based on city and zip code
        {
            /**
                Checks whether either:
                    city is missing
                    zip is missing
                If either is empty, the function immediately returns $8.50 as a default shipping rate.
                m suffix means “this is a decimal literal,” not a float or double.
            **/
            if (string.IsNullOrEmpty(city) || string.IsNullOrEmpty(zip)) 
                return 8.50m; // Default fallback shipping fee
            
            /**
                This line sets isLocal to true if:
                    The city, trimmed of whitespace, equals “Albuquerque”
                    Case-insensitive comparison (OrdinalIgnoreCase)
                    OR the trimmed ZIP exists in the Albuquerque ZIP list
                This covers both:
                    customers typing “Albuquerque”
                    customers entering a ZIP code inside the ABQ metro area
            **/
            bool isLocal = city.Trim().Equals("Albuquerque", StringComparison.OrdinalIgnoreCase) 
                           || AlbuquerqueZips.Contains(zip.Trim());
                             
            return isLocal ? 0.00m : 8.50m; // $0.00 for ABQ, $8.50 flat rate outside
        }
    }
}
