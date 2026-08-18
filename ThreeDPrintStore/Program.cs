using Microsoft.EntityFrameworkCore;
using ThreeDPrintStore.Models;
using ThreeDPrintStore.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

// 2. Register our SQLite database file (store.db)
builder.Services.AddDbContext<StoreDbContext>(options =>
    options.UseSqlite("Data Source=store.db"));

// 3. Register our pricing and shipping calculator services
builder.Services.AddScoped<ShippingService>();
builder.Services.AddScoped<PricingService>();

// 4. Configure Session memory for the Shopping Basket
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Basket expires after 30 mins of inactivity
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Seed the SQLite database with products automatically on boot
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<StoreDbContext>();
    ThreeDPrintStore.Data.DbInitializer.Seed(context);
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Activate Session memory BEFORE Authorization
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
