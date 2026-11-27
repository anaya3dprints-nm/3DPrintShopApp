namespace ThreeDPrintStore.Models;

public record CatalogProduct(int Id, string Name, long PriceCents, int MaxQuantity, string Image, string Description);

public static class ProductCatalog
{
    public static readonly IReadOnlyList<CatalogProduct> Products = Array.AsReadOnly(new[]
    {
        new CatalogProduct(1, "La Llorona Duck", 500, 99, "lallorona.jpg", "Inspired by Southwestern folklore with a spooky 3D-printed twist."),
        new CatalogProduct(2, "Roswell Alien Duck", 500, 5, "alien.jpg", "Features iconic extraterrestrial styling in high-speed PLA."),
        new CatalogProduct(3, "Green Chile Duck", 500, 99, "greenchile.jpg", "A playful tribute combining New Mexico's green chile with a classic duck design."),
        new CatalogProduct(4, "Red Chile Duck", 500, 5, "redchile.jpg", "A collectible inspired by New Mexico red chile culture.")
    });

    public static CatalogProduct? Find(int id) => Products.FirstOrDefault(p => p.Id == id);
}
