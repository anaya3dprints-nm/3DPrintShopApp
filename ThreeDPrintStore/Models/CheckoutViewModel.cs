namespace ThreeDPrintStore.Models;
public class CheckoutViewModel
{
    public bool PremiumCacheDrop { get; set; }

    public string? SponsorName { get; set; }

    public string? SocialHandle { get; set; }

    public string? SponsorMessage { get; set; }

    public List<CartItem> CartItems { get; set; }
        = new();
}