using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Controllers;

[Route("api/webhook")]
[ApiController]
public class StripeWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StripeWebhookController> _logger;

    public StripeWebhookController(ApplicationDbContext db, IConfiguration configuration, ILogger<StripeWebhookController> logger)
        => (_db, _configuration, _logger) = (db, configuration, logger);

    [HttpPost("stripe")]
    public async Task<IActionResult> HandleStripeWebhook()
    {
        var secret = _configuration["Stripe:WebhookSecret"];
        if (string.IsNullOrWhiteSpace(secret)) return StatusCode(503);
        Event stripeEvent;
        try
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], secret);
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Invalid Stripe webhook.");
            return BadRequest();
        }
        try
        {
            if ((stripeEvent.Type == "checkout.session.completed" || stripeEvent.Type == "checkout.session.async_payment_succeeded")
                && stripeEvent.Data.Object is Session session && session.PaymentStatus == "paid" &&
                session.Mode == "payment" && session.Currency == "usd" && !string.IsNullOrWhiteSpace(session.PaymentIntentId))
            {
                if (session.Metadata != null && session.Metadata.TryGetValue("order_id", out var orderId) && int.TryParse(orderId, out var id))
                    return await CompleteOrder(session, id);
                if (session.Metadata != null &&
                    (session.Metadata.TryGetValue("request_id", out var requestId) || session.Metadata.TryGetValue("PrintRequestId", out requestId)) &&
                    int.TryParse(requestId, out var requestNumber))
                    return await CompleteQuote(session, requestNumber);
            }
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stripe webhook processing failed; Stripe should retry.");
            return StatusCode(500);
        }
    }

    private async Task<IActionResult> CompleteOrder(Session session, int id)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (order == null) return BadRequest();
        if (!session.Metadata.TryGetValue("order_token", out var token) || token != order.CheckoutToken ||
            (order.StripeSessionId != null && order.StripeSessionId != session.Id) ||
            session.AmountTotal != order.TotalCents || session.Currency != order.Currency)
        {
            _logger.LogWarning("Stripe checkout does not match order {OrderId}.", id);
            return BadRequest();
        }
        if (order.Status != OrderStatus.PendingPayment) return Ok();

        var shipping = session.CollectedInformation?.ShippingDetails;
        var email = session.CustomerDetails?.Email;
        if (string.IsNullOrWhiteSpace(email) || shipping?.Address == null || shipping.Address.Country != "US")
        {
            _logger.LogWarning("Stripe checkout missing US shipping details or customer email for order {OrderId}.", id);
            return StatusCode(500);
        }
        var address = JsonSerializer.Serialize(shipping.Address);
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var changed = await _db.Orders.Where(o => o.Id == id && o.Status == OrderStatus.PendingPayment &&
                o.CheckoutToken == token && (o.StripeSessionId == null || o.StripeSessionId == session.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, OrderStatus.Paid)
                .SetProperty(o => o.StripeSessionId, session.Id)
                .SetProperty(o => o.StripePaymentIntentId, session.PaymentIntentId)
                .SetProperty(o => o.CustomerEmail, email)
                .SetProperty(o => o.ShippingName, shipping.Name)
                .SetProperty(o => o.ShippingAddress, address));
        if (changed == 1)
        {
            _db.OrderNotifications.Add(new OrderNotification { OrderId = id, Kind = "Confirmation" });
            await _db.SaveChangesAsync();
        }
        await transaction.CommitAsync();
        return Ok();
    }

    private async Task<IActionResult> CompleteQuote(Session session, int id)
    {
        var request = await _db.PrintRequests.AsNoTracking().Include(r => r.Quote).FirstOrDefaultAsync(r => r.Id == id);
        if (request?.Quote == null || (session.Metadata.TryGetValue("quote_id", out var quoteId) &&
            (!int.TryParse(quoteId, out var parsedQuoteId) || parsedQuoteId != request.Quote.Id)) ||
            session.AmountTotal != (long)Math.Round(request.Quote.FinalTotalPrice * 100))
        {
            _logger.LogWarning("Stripe checkout does not match quote for request {RequestId}.", id);
            return BadRequest();
        }
        if (request.Status is QuoteStatus.Paid or QuoteStatus.InProduction or QuoteStatus.Shipped or QuoteStatus.Cancelled) return Ok();
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var changed = await _db.PrintRequests.Where(r => r.Id == id &&
                (r.Status == QuoteStatus.QuoteSent || r.Status == QuoteStatus.PendingReview))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, QuoteStatus.Paid));
        if (changed == 1)
            await _db.PrintQuotes.Where(q => q.Id == request.Quote.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(q => q.StripePaymentIntentId, session.PaymentIntentId));
        await transaction.CommitAsync();
        return Ok();
    }
}
