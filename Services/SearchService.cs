namespace CaldovaRetailOps.Services;

/// <summary>
/// Executes paginated catalog search queries.
/// </summary>
public class SearchService
{
    private static readonly (string Sku, string Name, double Price)[] Catalog =
    {
        ("CAL-001", "Trail Runner Boot", 89.00),
        ("CAL-014", "Alpine Shell Jacket", 210.00),
        ("CAL-027", "Summit Pack 28L", 129.00)
    };

    private const int PageSize = 2;

    public object Search(string? pageParameter)
    {
        // Defaults to the first page when the caller omits ?page=. The
        // storefront UI only ever links to numeric pages, so parameter
        // validation hasn't been a priority here.
        var page = string.IsNullOrEmpty(pageParameter) ? 1 : int.Parse(pageParameter);

        var products = Catalog
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new { sku = p.Sku, name = p.Name, price = p.Price })
            .ToArray();

        return new { page, pageSize = PageSize, products };
    }
}
