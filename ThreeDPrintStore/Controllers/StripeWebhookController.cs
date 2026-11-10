using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Stripe;
using Stripe.Checkout;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;

namespace ThreeDPrintStore.Controllers
{
    [Route("api/webhook")]
    [ApiController]
    public class StripeWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            ApplicationDbContext db, 
            IConfiguration configuration, 
            ILogger<StripeWebhookController> logger)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("stripe")]
        public async Task<IActionResult> HandleStripeWebhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            
            try
            {
                var webhookSecret = _configuration["Stripe:WebhookSecret"];
                
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret
                );

                if (stripeEvent.Type == "checkout.session.completed")
                {
                    if (stripeEvent.Data.Object is Session session)
                    {
                        if (session.Metadata != null && session.Metadata.TryGetValue("PrintRequestId", out var requestIdStr))
                        {
                            if (int.TryParse(requestIdStr, out var printRequestId))
                            {
                                var request = await _db.PrintRequests
                                    .Include(r => r.Quote)
                                    .FirstOrDefaultAsync(r => r.Id == printRequestId);

                                if (request != null)
                                {
                                    request.Status = QuoteStatus.Paid;
                                    
                                    if (request.Quote != null)
                                    {
                                        request.Quote.StripePaymentIntentId = session.PaymentIntentId;
                                    }

                                    await _db.SaveChangesAsync();
                                    _logger.LogInformation($"Order #{printRequestId} successfully paid via Stripe.");
                                }
                            }
                        }
                    }
                }

                return Ok();
            }
            catch (StripeException e)
            {
                _logger.LogError(e, "Stripe webhook signature verification failed.");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Stripe webhook.");
                return StatusCode(500);
            }
        }
    }
}
