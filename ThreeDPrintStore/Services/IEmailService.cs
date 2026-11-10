using System.Threading.Tasks;
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Services
{
    public interface IEmailService
    {
        public Task SendEmailAsync(PrintRequest request, PrintQuote quote, string stripeCheckoutUrl); //SendQuoteEmailAsync
    }
}
