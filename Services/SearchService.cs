namespace CaldovaRetailOps.Services;

/// <summary>
/// Executes paginated catalog search queries against the active catalog wave.
/// </summary>
public class SearchService
{
    private const int PageSize = 2;
    private readonly CatalogService _catalog;

    public SearchService(CatalogService catalog)
    {
        _catalog = catalog;
    }

    public object Search(string? pageParameter)
    {
        // Defaults to the first page when the caller omits ?page=. The
        // storefront UI only ever links to numeric pages, so parameter
        // validation hasn't been a priority here.
        var page = string.IsNullOrEmpty(pageParameter) ? 1 : int.Parse(pageParameter);

        var (label, totalCount, pageItems) = _catalog.GetPage(page, PageSize);

        var products = pageItems
            .Select(p => new { sku = p.Sku, name = p.Name, price = p.Price })
            .ToArray();

        return new { page, pageSize = PageSize, catalogWave = label, totalCount, products };
    }
}
