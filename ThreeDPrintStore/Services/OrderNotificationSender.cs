using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using ThreeDPrintStore.Data;

namespace ThreeDPrintStore.Services;

public class OrderNotificationSender
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;

    public OrderNotificationSender(ApplicationDbContext db, IConfiguration configuration) => (_db, _configuration) = (db, configuration);

    public async Task<bool> SendAsync(int id)
    {
        var now = DateTime.UtcNow;
        var claimed = await _db.OrderNotifications.Where(n => n.Id == id && n.SentAtUtc == null &&
                (n.SendingUntilUtc == null || n.SendingUntilUtc < now))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.SendingUntilUtc, now.AddMinutes(5))
                .SetProperty(n => n.Attempts, n => n.Attempts + 1));
        if (claimed == 0) return false;
        var notification = await _db.OrderNotifications.Include(n => n.Order).FirstAsync(n => n.Id == id);
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_configuration["EmailSettings:SenderEmail"] ?? ""));
            message.To.Add(MailboxAddress.Parse(notification.Order.CustomerEmail ?? ""));
            message.Subject = notification.Kind switch
            {
                "Confirmation" => $"Order #{notification.OrderId} confirmed",
                "Production" => $"Order #{notification.OrderId} in production",
                _ => $"Order #{notification.OrderId} shipped"
            };
            message.Body = new TextPart("plain")
            {
                Text = notification.Kind switch
                {
                    "Confirmation" => $"Thank you. Your order #{notification.OrderId} has been paid and is being prepared.",
                    "Production" => $"Your order #{notification.OrderId} is now in production.",
                    _ => $"Your order #{notification.OrderId} has shipped. Tracking: {notification.Order.TrackingNumber}"
                }
            };
            using var client = new SmtpClient();
            client.Timeout = 30000;
            await client.ConnectAsync(_configuration["EmailSettings:Host"] ?? "", int.Parse(_configuration["EmailSettings:Port"] ?? "587"), SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_configuration["EmailSettings:Username"] ?? "", _configuration["EmailSettings:Password"] ?? "");
            await client.SendAsync(message);
            notification.SentAtUtc = DateTime.UtcNow;
            notification.LastError = null;
            notification.NextAttemptAtUtc = null;
        }
        catch (Exception)
        {
            // Avoid storing SMTP responses, which can contain customer data or credentials.
            notification.LastError = "Delivery failed; check email configuration and retry.";
            notification.NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(notification.Attempts, 6))));
        }
        notification.SendingUntilUtc = null;
        await _db.SaveChangesAsync();
        return notification.SentAtUtc != null;
    }
}
