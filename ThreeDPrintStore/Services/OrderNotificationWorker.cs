using Microsoft.EntityFrameworkCore;
using ThreeDPrintStore.Data;

namespace ThreeDPrintStore.Services;

public class OrderNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OrderNotificationWorker> _logger;
    public OrderNotificationWorker(IServiceScopeFactory scopes, ILogger<OrderNotificationWorker> logger) => (_scopes, _logger) = (scopes, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var now = DateTime.UtcNow;
                var ids = await db.OrderNotifications.AsNoTracking()
                    .Where(n => n.SentAtUtc == null && (n.NextAttemptAtUtc == null || n.NextAttemptAtUtc <= now) &&
                        (n.SendingUntilUtc == null || n.SendingUntilUtc < now))
                    .OrderBy(n => n.CreatedAtUtc).Select(n => n.Id).Take(20).ToListAsync(stoppingToken);
                foreach (var id in ids)
                    await new OrderNotificationSender(db, configuration).SendAsync(id);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Order notification worker failed; it will retry."); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
