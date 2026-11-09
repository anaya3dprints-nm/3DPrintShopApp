using System.Threading.Tasks;
using Custom3DPrints.Models;

namespace ThreeDPrintStore.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(PrintRequest request, PrintQuote quote, string stripeCheckoutUrl); //SendQuoteEmailAsync
    }
}
