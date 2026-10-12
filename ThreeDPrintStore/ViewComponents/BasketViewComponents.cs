using Microsoft.AspNetCore.Mvc;

public class BasketViewComponent : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        int count = HttpContext.Session.GetInt32("BasketItemCount") ?? 0;
        return View(count);
    }
}