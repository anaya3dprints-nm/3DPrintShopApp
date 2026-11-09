using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;
using Custom3DPrints.Models;
using System.Threading.Tasks;

namespace ThreeDPrintStore.Services
{
    public MailKitEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendQuoteEmailAsync(PrintRequest request, PrintQuote quote, string stripeCheckoutUrl)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("Anaya 3D Prints", _configuration["EmailSettings:SenderEmail"]));
            message.To.Add(new MailboxAddress(request.CustomerName, request.CustomerEmail));
            message.Subject = $"Your 3D Print Quote is Ready! (Order #{request.Id})";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $@"
                    <h2>Hello {request.CustomerName},</h2>
                    <p>We've reviewed your 3D printing request and calculated your final quote based on your file specifications.</p>
                    
                    <h3>Pricing Breakdown:</h3>
                    <ul>
                        <li><strong>Filament Weight:</strong> {quote.FilamentGrams}g (${quote.MaterialSubtotal:N2})</li>
                        <li><strong>Machine Print Time:</strong> {quote.EstimatedPrintHours} hrs (${quote.LaborMachineSubtotal:N2})</li>
                        <li><strong>Color Count Surcharge:</strong> {quote.ColorCount} colors (${quote.MultiColorSurcharge:N2})</li>
                        <li><strong>Handling & Setup:</strong> ${quote.BaseHandlingFee:N2}</li>
                    </ul>
                    
                    <h3>Total Price: ${quote.FinalTotalPrice:N2}</h3>
                    
                    <p>Ready to move forward? Click the link below to securely complete your payment via Stripe and kick off production:</p>
                    <p><a href='{stripeCheckoutUrl}' style='background: #4F46E5; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>Pay Now & Start Printing</a></p>
                    
                    <p>Thank you for choosing Anaya 3D Prints!</p>
                "
            };

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            await client.ConnectAsync(
                _configuration["EmailSettings:SmtpServer"], 
                int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587"), 
                MailKit.Security.SecureSocketOptions.StartTls
            );
            
            await client.AuthenticateAsync(
                _configuration["EmailSettings:Username"], 
                _configuration["EmailSettings:Password"]
            );
            
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
