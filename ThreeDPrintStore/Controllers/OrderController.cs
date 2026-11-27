using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreeDPrintStore.Data;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;

namespace ThreeDPrintStore.Controllers;

[Authorize(Policy = "Admin")]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    public OrderController(ApplicationDbContext db, IConfiguration configuration) => (_db, _configuration) = (db, configuration);

    [HttpGet]
    public async Task<IActionResult> Index() => View(await _db.Orders.AsNoTracking().Include(o => o.Items)
        .Include(o => o.Notifications).OrderByDescending(o => o.CreatedAtUtc).ToListAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, OrderStatus status, string? trackingNumber)
    {
        if (status != OrderStatus.InProduction && status != OrderStatus.Shipped) return BadRequest();
        trackingNumber = trackingNumber?.Trim();
        if (status == OrderStatus.Shipped && (string.IsNullOrEmpty(trackingNumber) || trackingNumber.Length > 255))
            return BadRequest("A tracking number of at most 255 characters is required.");
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var expected = status == OrderStatus.InProduction ? OrderStatus.Paid : OrderStatus.InProduction;
        var changed = await _db.Orders.Where(o => o.Id == id && o.Status == expected)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, status)
                .SetProperty(o => o.TrackingNumber, status == OrderStatus.Shipped ? trackingNumber : null));
        if (changed == 0) return BadRequest("Invalid order transition.");
        _db.OrderNotifications.Add(new OrderNotification { OrderId = id, Kind = status == OrderStatus.Shipped ? "Shipment" : "Production" });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryNotification(int id)
    {
        if (!await _db.OrderNotifications.AnyAsync(n => n.Id == id)) return NotFound();
        await new OrderNotificationSender(_db, _configuration).SendAsync(id);
        return RedirectToAction(nameof(Index));
    }
}
