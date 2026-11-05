namespace BulkyBook.Models.ViewModels
{
    /// <summary>Catalog page model: filters + current page of products.</summary>
    public class ProductListVM
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public IEnumerable<Category> Categories { get; set; } = new List<Category>();

        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public string Sort { get; set; } = "newest";

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 12;
        public int TotalCount { get; set; }
        public int TotalBooks { get; set; }

        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
        public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || CategoryId.HasValue;
    }
}
