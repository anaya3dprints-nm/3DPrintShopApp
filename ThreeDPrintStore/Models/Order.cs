using System.ComponentModel.DataAnnotations;

namespace ThreeDPrintStore.Models;

public enum OrderStatus { PendingPayment, Paid, InProduction, Shipped }

public class Order
{
    public int Id { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public OrderStatus Status { get; set; }
    public long SubtotalCents { get; set; }
    public long ShippingCents { get; set; }
    public long TotalCents { get; set; }
    [MaxLength(3)] public string Currency { get; set; } = "usd";
    [MaxLength(255)] public string? StripeSessionId { get; set; }
    [MaxLength(64)] public string CheckoutToken { get; set; } = "";
    [MaxLength(255)] public string? StripePaymentIntentId { get; set; }
    [MaxLength(255)] public string? CustomerEmail { get; set; }
    [MaxLength(255)] public string? ShippingName { get; set; }
    [MaxLength(1000)] public string? ShippingAddress { get; set; }
    public bool PremiumCacheDrop { get; set; }
    [MaxLength(100)] public string? SponsorName { get; set; }
    [MaxLength(100)] public string? SocialHandle { get; set; }
    [MaxLength(500)] public string? SponsorMessage { get; set; }
    [MaxLength(255)] public string? TrackingNumber { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public List<OrderNotification> Notifications { get; set; } = new();
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    [MaxLength(255)] public string Name { get; set; } = "";
    public long UnitPriceCents { get; set; }
    public int Quantity { get; set; }
}

public class OrderNotification
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    [MaxLength(30)] public string Kind { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
    public DateTime? SendingUntilUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public int Attempts { get; set; }
    [MaxLength(500)] public string? LastError { get; set; }
}
