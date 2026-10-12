using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace ThreeDPrintStore.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var host = emailSettings["Host"] ?? "smtp.gmail.com";
            var port = int.Parse(emailSettings["Port"] ?? "587");
            var username = emailSettings["Username"] ?? string.Empty;
            var password = emailSettings["Password"] ?? string.Empty;
            var senderEmail = emailSettings["SenderEmail"] ?? username;
            var senderName = emailSettings["SenderName"] ?? "Anaya 3D Prints";

            var emailMessage = new MimeMessage();
            emailMessage.From.Add(new MailboxAddress(senderName, senderEmail));
            emailMessage.To.Add(new MailboxAddress("", email));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart("plain") { Text = message };

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, MailKit.Security.SecureSocketOptions.StartTls);
            if (!string.IsNullOrEmpty(username))
            {
                await client.AuthenticateAsync(username, password);
            }
            await client.SendAsync(emailMessage);
            await client.DisconnectAsync(true);
        }
    }
}