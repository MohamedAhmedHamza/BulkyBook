namespace BulkyBook.Models.ViewModels
{
    public class CheckoutVM
    {
        public OrderHeader Order { get; set; } = new OrderHeader();
        public string ClientSecret { get; set; } = string.Empty;
        public string PublishableKey { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public bool IsTestMode => PublishableKey.StartsWith("pk_test_", StringComparison.Ordinal);
    }
}
