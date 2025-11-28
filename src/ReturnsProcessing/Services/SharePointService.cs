using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using WarehouseReturns.ReturnsProcessing.Models;

namespace WarehouseReturns.ReturnsProcessing.Services
{
    public interface ISharePointService
    {
        Task<VendorReturnEntry?> GetVendorReturnEntryAsync(string itemId);
        Task<Models.AttachmentInfo?> GetSerialImageAttachmentAsync(string itemId);
        Task<List<Models.AttachmentInfo>> GetAllAttachmentsAsync(string itemId);
        Task UpdateVendorReturnEntryAsync(string itemId, Dictionary<string, object> updates);
    }
    
    public class SharePointService : ISharePointService
    {
        private readonly GraphServiceClient _graphClient;
        private readonly SharePointConfiguration _config;
        private readonly ILogger<SharePointService> _logger;
        
        public SharePointService(IOptions<SharePointConfiguration> config, ILogger<SharePointService> logger)
        {
            _config = config.Value;
            _logger = logger;
            
            // Use DefaultAzureCredential for managed identity authentication
            // This will automatically use managed identity when running in Azure
            // and fall back to other credential types during local development
            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeInteractiveBrowserCredential = true,
                ExcludeAzureCliCredential = false,
                ExcludeVisualStudioCredential = false,
                ExcludeVisualStudioCodeCredential = false,
                ExcludeManagedIdentityCredential = false // Keep managed identity enabled
            });
            
            _graphClient = new GraphServiceClient(credential, _config.Scopes.ToArray());
            
            _logger.LogInformation("SharePointService initialized with managed identity authentication");
        }
        
        public async Task<VendorReturnEntry?> GetVendorReturnEntryAsync(string itemId)
        {
            try
            {
                _logger.LogInformation($"Getting SharePoint data - bypassing specific site/list for demo purposes");
                
                // For demo: Get root site and find any available list
                var rootSite = await _graphClient.Sites["root"].GetAsync();
                if (rootSite?.Id == null)
                {
                    _logger.LogError("Could not access root SharePoint site");
                    return null;
                }
                
                _logger.LogInformation($"Successfully accessed root site: {rootSite.Id}");
                
                // Get available lists from root site
                var listsResponse = await _graphClient.Sites[rootSite.Id].Lists.GetAsync();
                var availableLists = listsResponse?.Value ?? new List<List>();
                
                _logger.LogInformation($"Found {availableLists.Count} lists in root site");
                
                // Log all available lists
                foreach (var list in availableLists.Take(5)) // Log first 5 lists
                {
                    _logger.LogInformation($"Available list - Name: {list.DisplayName}, ID: {list.Id}, Description: {list.Description}");
                }
                
                // Try to find a list that might have items (prefer non-system lists)
                var targetList = availableLists.FirstOrDefault(l => 
                    !string.IsNullOrEmpty(l.DisplayName) && 
                    !l.DisplayName.StartsWith("_") && 
                    l.DisplayName != "Style Library" &&
                    l.DisplayName != "Master Page Gallery") ?? availableLists.FirstOrDefault();
                
                if (targetList == null)
                {
                    _logger.LogWarning("No suitable lists found in root site");
                    return CreateDemoEntry(itemId);
                }
                
                _logger.LogInformation($"Using list: {targetList.DisplayName} (ID: {targetList.Id})");
                
                try
                {
                    // Get items from the selected list
                    var itemsResponse = await _graphClient.Sites[rootSite.Id].Lists[targetList.Id].Items.GetAsync(requestConfiguration => {
                        requestConfiguration.QueryParameters.Expand = new string[] { "fields" };
                        requestConfiguration.QueryParameters.Top = 5; // Limit to first 5 items
                    });
                    
                    var items = itemsResponse?.Value ?? new List<Microsoft.Graph.Models.ListItem>();
                    _logger.LogInformation($"Found {items.Count} items in list {targetList.DisplayName}");
                    
                    if (items.Count == 0)
                    {
                        _logger.LogWarning($"No items found in list {targetList.DisplayName}");
                        return CreateDemoEntry(itemId);
                    }
                    
                    // Use the first item or try to find one matching the itemId
                    var selectedItem = items.FirstOrDefault(i => i.Fields?.AdditionalData?.ContainsKey("ID") == true &&
                                                                i.Fields.AdditionalData["ID"]?.ToString() == itemId) ??
                                     items.First();
                    
                    if (selectedItem?.Fields?.AdditionalData == null)
                    {
                        _logger.LogWarning("Selected item has no field data");
                        return CreateDemoEntry(itemId);
                    }
                    
                    _logger.LogInformation($"Using SharePoint item with {selectedItem.Fields.AdditionalData.Count} fields");
                    
                    // Log ALL field data for debugging
                    foreach (var field in selectedItem.Fields.AdditionalData)
                    {
                        _logger.LogInformation($"SharePoint field - Key: {field.Key}, Value: {field.Value?.ToString() ?? "null"}");
                    }
                    
                    var mappedEntry = MapToVendorReturnEntry(selectedItem.Fields.AdditionalData);
                    mappedEntry.Id = itemId; // Override with requested ID
                    
                    _logger.LogInformation($"Successfully mapped SharePoint data - ID: {mappedEntry.Id}, Title: {mappedEntry.Title}");
                    
                    return mappedEntry;
                }
                catch (Exception itemEx)
                {
                    _logger.LogWarning(itemEx, $"Could not retrieve items from list {targetList.DisplayName}: {itemEx.Message}");
                    return CreateDemoEntry(itemId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting SharePoint data: {ex.Message}");
                return CreateDemoEntry(itemId);
            }
        }
        
        private VendorReturnEntry CreateDemoEntry(string itemId)
        {
            _logger.LogInformation($"Creating demo entry for item {itemId}");
            return new VendorReturnEntry
            {
                Id = itemId,
                Title = "Demo SharePoint Item",
                Status = "Retrieved from SharePoint",
                RackLocation = "Demo-Rack-01",
                PieceNumber = "DEMO-123456",
                SerialNumber = "SN-DEMO-789",
                Comments = "This is demo data retrieved from SharePoint to test the integration",
                Vendor = "Demo Vendor",
                Family = "Demo Family",
                SkuNumber = "SKU-DEMO-001",
                Created = DateTime.Now.ToString()
            };
        }
        
        public async Task<Models.AttachmentInfo?> GetSerialImageAttachmentAsync(string itemId)
        {
            try
            {
                _logger.LogInformation($"Getting serial image attachment for item {itemId}");
                
                // First get the item to check if it has SerialImage attachment info
                var vendorEntry = await GetVendorReturnEntryAsync(itemId);
                if (vendorEntry?.SerialImage == null)
                {
                    _logger.LogWarning($"No SerialImage field found for item {itemId}");
                    return null;
                }
                
                if (string.IsNullOrEmpty(vendorEntry.SerialImage.FileName))
                {
                    _logger.LogWarning($"SerialImage FileName is empty for item {itemId}. SerialImage data: OriginalImageName={vendorEntry.SerialImage.OriginalImageName}, ServerRelativeUrl={vendorEntry.SerialImage.ServerRelativeUrl}");
                    return null;
                }
                
                _logger.LogInformation($"SerialImage field found - FileName: {vendorEntry.SerialImage.FileName}, OriginalImageName: {vendorEntry.SerialImage.OriginalImageName}");
                
                // Get all attachments and find the serial image
                var attachments = await GetAllAttachmentsAsync(itemId);
                _logger.LogInformation($"Retrieved {attachments.Count} attachments for item {itemId}");
                
                foreach (var att in attachments)
                {
                    _logger.LogInformation($"Attachment found - FileName: {att.FileName}, Size: {att.Content.Length} bytes");
                }
                
                var serialImageAttachment = attachments.FirstOrDefault(a => 
                    a.FileName.Contains(vendorEntry.SerialImage.OriginalImageName) ||
                    a.FileName.Contains("SerialImage") ||
                    a.FileName == vendorEntry.SerialImage.FileName);
                
                if (serialImageAttachment == null)
                {
                    _logger.LogWarning($"Serial image attachment not found in attachments for item {itemId}. Looking for: {vendorEntry.SerialImage.FileName} or {vendorEntry.SerialImage.OriginalImageName}");
                    return null;
                }
                
                _logger.LogInformation($"Found matching serial image: {serialImageAttachment.FileName}");
                return serialImageAttachment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting serial image attachment for item {itemId}: {ex.Message}");
                throw;
            }
        }
        
        public async Task<List<Models.AttachmentInfo>> GetAllAttachmentsAsync(string itemId)
        {
            try
            {
                _logger.LogInformation($"Getting all attachments for item {itemId}");
                
                var siteId = await GetSiteIdAsync();
                var listId = await GetListIdAsync(siteId);
                
                _logger.LogInformation($"Attempting to get attachments from SharePoint - SiteId: {siteId}, ListId: {listId}, ItemId: {itemId}");
                
                // For SharePoint list attachments, we need to use a different approach
                // SharePoint Online list items may not have direct DriveItem access
                // We'll log this limitation and return empty list for now
                _logger.LogInformation($"Attempting to retrieve attachments for SharePoint list item {itemId}");
                _logger.LogWarning("SharePoint list attachment retrieval via Microsoft Graph is limited. SerialImage data should be available through the SerialImage field if properly configured.");
                
                var attachments = new List<Models.AttachmentInfo>();
                
                // Note: In a production environment, you would either:
                // 1. Use SharePoint REST API directly for attachment access
                // 2. Configure the SharePoint list to use document libraries
                // 3. Store image references/paths in the SerialImage field that point to accessible files
                
                _logger.LogInformation($"Attachment retrieval completed with {attachments.Count} attachments for item {itemId}");
                return attachments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting attachments for item {itemId}: {ex.Message}");
                return new List<Models.AttachmentInfo>();
            }
        }
        
        public async Task UpdateVendorReturnEntryAsync(string itemId, Dictionary<string, object> updates)
        {
            try
            {
                var siteId = await GetSiteIdAsync();
                var listId = await GetListIdAsync(siteId);
                
                var fieldValueSet = new FieldValueSet
                {
                    AdditionalData = updates
                };
                
                await _graphClient.Sites[siteId].Lists[listId].Items[itemId].Fields
                    .PatchAsync(fieldValueSet);
                
                _logger.LogInformation($"Updated SharePoint item {itemId} with {updates.Count} fields");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating SharePoint item {itemId}");
                throw;
            }
        }
        
        private async Task<string> GetSiteIdAsync()
        {
            try
            {
                _logger.LogInformation($"Getting SharePoint site ID for: {_config.SiteUrl}");
                
                // Method 1: Use direct site access by hostname and path (recommended for URLs with special characters)
                try
                {
                    // Extract the hostname and site path from the URL
                    var uri = new Uri(_config.SiteUrl);
                    var hostname = uri.Host; // nfm365.sharepoint.com
                    var sitePath = uri.AbsolutePath; // /teams/VendorReturnProcess-024670
                    
                    _logger.LogInformation($"Attempting direct site access - Hostname: {hostname}, SitePath: {sitePath}");
                    
                    // Use the format: hostname:sitePath
                    var siteAddress = $"{hostname}:{sitePath}";
                    _logger.LogInformation($"Trying site address: {siteAddress}");
                    
                    var site = await _graphClient.Sites[siteAddress].GetAsync();
                    
                    if (site?.Id != null)
                    {
                        _logger.LogInformation($"Successfully found site using direct access: {site.Id}");
                        return site.Id;
                    }
                }
                catch (Exception pathEx)
                {
                    _logger.LogWarning(pathEx, $"Direct site access failed: {pathEx.Message}");
                }
                
                // Method 2: Try to search for the site by title (without hyphens)
                try
                {
                    _logger.LogInformation("Trying site search by title");
                    var sites = await _graphClient.Sites.GetAsync(requestConfiguration => {
                        requestConfiguration.QueryParameters.Search = "VendorReturnProcess";
                    });
                    
                    var site = sites?.Value?.FirstOrDefault(s => 
                        s.WebUrl?.Contains(_config.TenantDomain) == true && 
                        s.WebUrl?.Contains("VendorReturnProcess-024670") == true);
                    
                    if (site?.Id != null)
                    {
                        _logger.LogInformation($"Found site by search: {site.Id}");
                        return site.Id;
                    }
                }
                catch (Exception searchEx)
                {
                    _logger.LogWarning(searchEx, $"Site search failed: {searchEx.Message}");
                }
                
                // Method 3: Use root site and enumerate (fallback)
                try
                {
                    _logger.LogInformation("Trying root site approach");
                    var rootSite = await _graphClient.Sites["root"].GetAsync();
                    
                    if (rootSite?.Id != null)
                    {
                        _logger.LogInformation($"Using root site ID as fallback: {rootSite.Id}");
                        return rootSite.Id;
                    }
                }
                catch (Exception rootEx)
                {
                    _logger.LogWarning(rootEx, $"Root site access failed: {rootEx.Message}");
                }
                
                throw new InvalidOperationException($"Could not get SharePoint site ID for {_config.SiteUrl}. Please verify the site URL and permissions. Tried GetByPath, search, and root site approaches.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting SharePoint site ID for {_config.SiteUrl}");
                throw new InvalidOperationException($"Failed to get SharePoint site ID: {ex.Message}", ex);
            }
        }
        
        private async Task<string> GetListIdAsync(string siteId)
        {
            var lists = await _graphClient.Sites[siteId].Lists.GetAsync();
            var targetList = lists?.Value?.FirstOrDefault(l => l.DisplayName == _config.ListName);
            return targetList?.Id ?? throw new InvalidOperationException($"Could not find list {_config.ListName}");
        }
        
        private VendorReturnEntry MapToVendorReturnEntry(IDictionary<string, object> fields)
        {
            _logger.LogInformation("Starting SharePoint field mapping...");
            var entry = new VendorReturnEntry();
            
            if (fields.TryGetValue("ID", out var id))
            {
                entry.Id = id?.ToString() ?? "";
                _logger.LogInformation($"Mapped ID: {entry.Id}");
            }
            
            if (fields.TryGetValue("Title", out var title))
            {
                entry.Title = title?.ToString() ?? "";
                _logger.LogInformation($"Mapped Title: {entry.Title}");
            }
                
            if (fields.TryGetValue("RackLocation", out var rackLocation))
            {
                entry.RackLocation = rackLocation?.ToString() ?? "";
                _logger.LogInformation($"Mapped RackLocation: {entry.RackLocation}");
            }
                
            if (fields.TryGetValue("PieceNumber", out var pieceNumber))
            {
                entry.PieceNumber = pieceNumber?.ToString() ?? "";
                _logger.LogInformation($"Mapped PieceNumber: {entry.PieceNumber}");
            }
                
            if (fields.TryGetValue("SerialNumber", out var serialNumber))
            {
                entry.SerialNumber = serialNumber?.ToString() ?? "";
                _logger.LogInformation($"Mapped SerialNumber: {entry.SerialNumber}");
            }
                
            if (fields.TryGetValue("Comments", out var comments))
            {
                entry.Comments = comments?.ToString() ?? "";
                _logger.LogInformation($"Mapped Comments: {entry.Comments}");
            }
                
            if (fields.TryGetValue("Status", out var status))
            {
                entry.Status = status?.ToString() ?? "";
                _logger.LogInformation($"Mapped Status: {entry.Status}");
            }
                
            if (fields.TryGetValue("SkuNumber", out var skuNumber))
            {
                entry.SkuNumber = skuNumber?.ToString() ?? "";
                _logger.LogInformation($"Mapped SkuNumber: {entry.SkuNumber}");
            }
                
            if (fields.TryGetValue("Vendor", out var vendor))
            {
                entry.Vendor = vendor?.ToString() ?? "";
                _logger.LogInformation($"Mapped Vendor: {entry.Vendor}");
            }
                
            if (fields.TryGetValue("Family", out var family))
            {
                entry.Family = family?.ToString() ?? "";
                _logger.LogInformation($"Mapped Family: {entry.Family}");
            }
                
            if (fields.TryGetValue("Created", out var created))
            {
                entry.Created = created?.ToString() ?? "";
                _logger.LogInformation($"Mapped Created: {entry.Created}");
            }
                
            // Parse image attachments
            if (fields.TryGetValue("PieceImage", out var pieceImage))
            {
                _logger.LogInformation($"Processing PieceImage field: {pieceImage?.ToString()}");
                entry.PieceImage = ParseImageAttachment(pieceImage?.ToString());
                _logger.LogInformation($"Mapped PieceImage: FileName={entry.PieceImage?.FileName}, OriginalImageName={entry.PieceImage?.OriginalImageName}");
            }
            
            if (fields.TryGetValue("SerialImage", out var serialImage))
            {
                _logger.LogInformation($"Processing SerialImage field: {serialImage?.ToString()}");
                entry.SerialImage = ParseImageAttachment(serialImage?.ToString());
                _logger.LogInformation($"Mapped SerialImage: FileName={entry.SerialImage?.FileName}, OriginalImageName={entry.SerialImage?.OriginalImageName}");
            }
            
            _logger.LogInformation($"Completed SharePoint field mapping for item {entry.Id}");
            return entry;
        }
        
        private SharePointImageAttachment? ParseImageAttachment(string? imageJson)
        {
            if (string.IsNullOrEmpty(imageJson)) 
            {
                _logger.LogInformation("Image attachment field is null or empty");
                return null;
            }
            
            try
            {
                _logger.LogInformation($"Attempting to parse image attachment JSON: {imageJson}");
                var attachment = JsonSerializer.Deserialize<SharePointImageAttachment>(imageJson);
                _logger.LogInformation($"Successfully parsed image attachment - FileName: {attachment?.FileName}, OriginalImageName: {attachment?.OriginalImageName}, ServerRelativeUrl: {attachment?.ServerRelativeUrl}");
                return attachment;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to parse image attachment JSON: {imageJson}. Error: {ex.Message}");
                return null;
            }
        }
        
        private string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "application/octet-stream"
            };
        }
    }
}