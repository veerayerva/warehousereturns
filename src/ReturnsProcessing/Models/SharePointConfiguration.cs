namespace WarehouseReturns.ReturnsProcessing.Models
{
    public class SharePointConfiguration
    {
        public string SiteUrl { get; set; } = string.Empty;
        public string ListName { get; set; } = string.Empty;
        public string TenantDomain { get; set; } = string.Empty;
        public string SitePath { get; set; } = string.Empty;
        
        // Managed Identity uses default scopes - no need for client credentials
        public List<string> Scopes { get; set; } = new() { "https://graph.microsoft.com/.default" };
    }
}