using System.Text.Json;
using System.Text.Json.Serialization;

namespace CaldovaRetailOps.Services;

public class CatalogProduct
{
    [JsonPropertyName("sku")]
    public string Sku { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("price")]
    public double Price { get; set; }
}

public class CatalogWave
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";

    [JsonPropertyName("effectiveDate")]
    public DateTime EffectiveDate { get; set; }

    // Null when the content team has staged a future wave's metadata ahead
    // of time but hasn't populated its product list yet.
    [JsonPropertyName("products")]
    public List<CatalogProduct>? Products { get; set; }
}

/// <summary>
/// Resolves the storefront's "active" product catalog from a set of dated
/// catalog waves (Data/catalog-waves.json). Merchandising stages future
/// waves ahead of their launch; the App Setting <c>Catalog__AsOfDate</c>
/// controls which wave is considered active "as of" a given date and is
/// normally left unset, defaulting to the current date.
/// </summary>
public class CatalogService
{
    private readonly List<CatalogWave> _waves;

    public CatalogService(IConfiguration configuration)
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "catalog-waves.json");
        var json = File.ReadAllText(dataPath);
        _waves = JsonSerializer.Deserialize<List<CatalogWave>>(json) ?? new List<CatalogWave>();
        Configuration = configuration;
    }

    private IConfiguration Configuration { get; }

    private CatalogWave GetActiveWave()
    {
        var asOfSetting = Configuration["Catalog:AsOfDate"];
        var asOf = !string.IsNullOrWhiteSpace(asOfSetting) && DateTime.TryParse(asOfSetting, out var parsed)
            ? parsed
            : DateTime.UtcNow;

        // Pick the most recent wave whose effective date has arrived. If a
        // future wave's effective date has been reached by an incorrectly
        // forward-dated Catalog__AsOfDate, it becomes "active" even though
        // its product list may not be populated yet.
        return _waves
            .Where(w => w.EffectiveDate <= asOf)
            .OrderByDescending(w => w.EffectiveDate)
            .First();
    }

    public (string Label, int TotalCount, List<CatalogProduct> Products) GetPage(int page, int pageSize)
    {
        var wave = GetActiveWave();

        // Direct property access on a null Products list throws a genuine
        // NullReferenceException when a staged wave's catalog hasn't been
        // populated yet - this is the realistic failure mode when
        // Catalog__AsOfDate is mistakenly pointed at a future wave.
        var totalCount = wave.Products.Count;

        var pageItems = wave.Products
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (wave.Label, totalCount, pageItems);
    }
}
