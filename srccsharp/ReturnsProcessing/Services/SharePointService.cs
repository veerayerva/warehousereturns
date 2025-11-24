using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using Newtonsoft.Json.Linq;
using WarehouseReturns.ReturnsProcessing.Configuration;
using WarehouseReturns.ReturnsProcessing.Models;

namespace WarehouseReturns.ReturnsProcessing.Services;

/// <summary>
/// Service interface for SharePoint operations
/// </summary>
public interface ISharePointService
{
    Task<QcItem?> GetListItemAsync(string listItemId, string correlationId);
    Task<byte[]?> GetAttachmentAsync(string listItemId, string fileName, string correlationId);
    Task<(byte[]? ImageData, string? ContentType)> DownloadImageFromSharePointUrlAsync(string sharePointUrl, string correlationId);
    Task UpdateListItemAsync(string listItemId, ProcessingResult result, string correlationId);
    Task<bool> TestConnectionAsync();
    Task<List<SharePointListInfo>> DiscoverAvailableListsAsync(string correlationId);
    Task<List<SharePointFieldInfo>> GetAllListFieldsAsync(string correlationId);
}

/// <summary>
/// SharePoint service implementation using Microsoft Graph API
/// </summary>
public class SharePointService : ISharePointService
{
    private readonly ILogger<SharePointService> _logger;
    private readonly GraphServiceClient _graphClient;
    private readonly SharePointSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly TokenCredential _credential;

    public SharePointService(
        ILogger<SharePointService> logger,
        IOptions<SharePointSettings> sharePointSettings,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _settings = sharePointSettings.Value;

        // Validate configuration
        if (!_settings.IsValid())
            throw new InvalidOperationException(
                $"Invalid SharePoint configuration. Authentication method: {_settings.AUTHENTICATION_METHOD}. " +
                "For ClientCredentials, ensure TenantId, ClientId, and ClientSecret are provided.");

        // Initialize credentials and Graph client
        (_credential, _graphClient) = InitializeGraphClient();
        _httpClient = httpClientFactory.CreateClient();

        _logger.LogInformation(
            $"SharePointService initialized with {_settings.AUTHENTICATION_METHOD} authentication");
    }

    /// <summary>
    ///     Initialize Graph Service Client with appropriate authentication
    /// </summary>
    private (TokenCredential credential, GraphServiceClient client) InitializeGraphClient()
    {
        var scopes = new[] { "https://graph.microsoft.com/.default" };
        TokenCredential credential;

        if (_settings.IsManagedIdentity)
        {
            _logger.LogInformation("Using Managed Identity for SharePoint authentication");
            credential = new DefaultAzureCredential();
            return (credential, new GraphServiceClient(credential, scopes));
        }

        if (_settings.IsClientCredentials)
        {
            _logger.LogInformation("Using Client Credentials for SharePoint authentication");
            credential = new ClientSecretCredential(
                _settings.TENANT_ID,
                _settings.CLIENT_ID,
                _settings.CLIENT_SECRET);
            return (credential, new GraphServiceClient(credential, scopes));
        }

        throw new InvalidOperationException(
            $"Unsupported authentication method: {_settings.AUTHENTICATION_METHOD}. " +
            "Supported methods are: ManagedIdentity, ClientCredentials");
    }

    /// <summary>
    /// Get a SharePoint list item by ID
    /// </summary>
    public async Task<QcItem?> GetListItemAsync(string listItemId, string correlationId)
    {
        try
        {
            _logger.LogInformation($"Getting SharePoint list item {listItemId} (Correlation: {correlationId})");

            var site = await GetSiteAsync(correlationId);
            if (site?.Id == null)
                return null;

            var listItem = await _graphClient.Sites[site.Id]
                .Lists[_settings.SHAREPOINT_LIST_ID]
                .Items[listItemId]
                .GetAsync();

            if (listItem?.Fields?.AdditionalData == null)
            {
                _logger.LogWarning($"List item {listItemId} not found (Correlation: {correlationId})");
                return null;
            }

            var fields = listItem.Fields.AdditionalData;
            _logger.LogDebug($"[DEBUG] List item fields: {JsonSerializer.Serialize(fields)}");

            var attachmentUrls = await GetItemAttachmentUrlsAsync(
                site.Id,
                listItemId,
                GetField<string>(fields, "PieceImage"),
                GetField<string>(fields, "SerialImage"),
                correlationId);

            var qcItem = MapToQcItem(fields, attachmentUrls);
            _logger.LogInformation(
                $"Retrieved QcItem {listItemId} - Title: {qcItem.Title}, PieceNumber: {qcItem.PieceNumber} (Correlation: {correlationId})");

            return qcItem;
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 401 || ex.ResponseStatusCode == 403)
        {
            _logger.LogError(
                $"Access denied retrieving list item {listItemId}: {ex.ResponseStatusCode} (Correlation: {correlationId})");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting list item {listItemId} (Correlation: {correlationId})");
            return null;
        }
    }

    // Helper: Get site with caching (call once per request)
    private async Task<Site?> GetSiteAsync(string correlationId)
    {
        try
        {
            var uri = new Uri(_settings.SHAREPOINT_SITE_URL);
            var host = uri.Host;
            var pathParts = uri.PathAndQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var siteIdentifier = $"{host}:/{string.Join("/", pathParts)}";

            var site = await _graphClient.Sites[siteIdentifier].GetAsync(config =>
                config.QueryParameters.Select = new[] { "id" });

            if (site?.Id == null)
                _logger.LogError($"Could not resolve site: {siteIdentifier} (Correlation: {correlationId})");

            return site;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error resolving SharePoint site (Correlation: {correlationId})");
            return null;
        }
    }

    // Helper: Map SharePoint fields to QcItem
    private QcItem MapToQcItem(IDictionary<string, object?> fields, Dictionary<string, string> attachments)
    {
        return new QcItem
        {
            Title = GetField<string>(fields, "Title"),
            PieceNumber = GetField<string>(fields, "PieceNumber"),
            SerialNumber = GetField<string>(fields, "SerialNumber"),
            Comments = GetField<string>(fields, "Comments"),
            Status = GetField<string>(fields, "Status"),
            DamageImage1Link = GetField<string>(fields, "DamageImage1Link"),
            DamageImage2Link = GetField<string>(fields, "DamageImage2Link"),
            DamageImage3Link = GetField<string>(fields, "DamageImage3Link"),
            DamageImage4Link = GetField<string>(fields, "DamageImage4Link"),
            DamageImage5Link = GetField<string>(fields, "DamageImage5Link"),
            SerialImageLink = ExtractUrlFromHyperlinkField(fields, "SerialImageLink"),
            ReasonCategory = GetField<string>(fields, "ReasonCategory"),
            ReasonCode = GetField<string>(fields, "ReasonCode"),
            LocationCode = GetField<string>(fields, "LocationCode"),
            QCNumber = GetField<int?>(fields, "QCNumber"),
            QCFileName = GetField<string>(fields, "QCFileName"),
            SkuNumber = GetField<string>(fields, "SkuNumber"),
            WHSELOC = GetField<string>(fields, "WHSELOC"),
            Vendor = GetField<string>(fields, "Vendor"),
            Family = GetField<string>(fields, "Family"),
            OrgPO = GetField<string>(fields, "OrgPO"),
            ModelNumber = GetField<string>(fields, "ModelNumber"),
            RackLocation = GetField<string>(fields, "RackLocation"),
            PieceImage = attachments.GetValueOrDefault("PieceImage", string.Empty),
            SerialImage = attachments.GetValueOrDefault("SerialImage")
        };
    }

    // Extract SharePoint hyperlink field (which comes as JSON object with Url and Description)
    private HyperlinkField? ExtractUrlFromHyperlinkField(IDictionary<string, object?> fields, string fieldName)
    {
        if (fields?.TryGetValue(fieldName, out var value) == true && value != null)
        {
            try
            {
                // SharePoint returns hyperlink fields as JSON objects: {"Description":"...", "Url":"..."}
                if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
                {
                    var description = jsonElement.TryGetProperty("Description", out var descProperty) 
                        ? descProperty.GetString() 
                        : null;
                    var url = jsonElement.TryGetProperty("Url", out var urlProperty) 
                        ? urlProperty.GetString() 
                        : null;

                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        return new HyperlinkField
                        {
                            Description = description,
                            Url = url
                        };
                    }
                }
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    // Generic helper: Get typed field value
    private T? GetField<T>(IDictionary<string, object?> fields, string fieldName)
    {
        if (fields?.TryGetValue(fieldName, out var value) == true && value != null)
            try
            {
                return value is T typedValue ? typedValue : (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }

        return default;
    }


    /// <summary>
    ///     Get attachment file names for a list item by parsing JSON fields
    /// </summary>
    private async Task<Dictionary<string, string>> GetItemAttachmentUrlsAsync(
        string siteId,
        string listItemId,
        string? pieceImageJson,
        string? serialImageJson,
        string correlationId)
    {
        var attachmentFileNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            _logger.LogInformation($"Extracting attachment file names from JSON fields (Correlation: {correlationId})");

            var pieceFileName = ExtractFileName(pieceImageJson);
            var serialFileName = ExtractFileName(serialImageJson);

            if (!string.IsNullOrEmpty(pieceFileName))
            {
                attachmentFileNames["PieceImage"] = pieceFileName;
                _logger.LogInformation($"PieceImage file: {pieceFileName} (Correlation: {correlationId})");
            }

            if (!string.IsNullOrEmpty(serialFileName))
            {
                attachmentFileNames["SerialImage"] = serialFileName;
                _logger.LogInformation($"SerialImage file: {serialFileName} (Correlation: {correlationId})");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Error extracting attachment file names (Correlation: {correlationId})");
        }

        return attachmentFileNames;
    }

    private string? ExtractFileName(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var obj = JObject.Parse(json);
            return obj.Value<string>("fileName")
                   ?? obj.Value<string>("originalImageName");
        }
        catch
        {
            return null;
        }
    }


    /// <summary>
    /// Download attachment from SharePoint list item using SharePoint REST API
    /// </summary>
    public async Task<byte[]?> GetAttachmentAsync(string listItemId, string fileName, string correlationId)
    {
        try
        {
            _logger.LogInformation(
                $"[ATTACHMENT] Downloading attachment: {fileName} from item {listItemId} (Correlation: {correlationId})");

            // Build SharePoint REST API URL for attachment
            // Format: {siteUrl}/_api/web/lists(guid'{listId}')/items({itemId})/AttachmentFiles('{fileName}')/$value
            var attachmentUrl =
                $"{_settings.SHAREPOINT_SITE_URL}/_api/web/lists(guid'{_settings.SHAREPOINT_LIST_ID}')/items({listItemId})/AttachmentFiles('{Uri.EscapeDataString(fileName)}')/$value";

            _logger.LogInformation($"[ATTACHMENT] SharePoint REST API URL: {attachmentUrl} (Correlation: {correlationId})");
            _logger.LogInformation("Credential type: {Type}", _credential.GetType().FullName);
            // Get access token for SharePoint REST API
            var sharePointScope = $"https://{new Uri(_settings.SHAREPOINT_SITE_URL).Host}/.default";
            _logger.LogInformation($"[ATTACHMENT] Requesting token with scope: {sharePointScope} (Correlation: {correlationId})");

            var tokenResult = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { sharePointScope }),
                CancellationToken.None);

            _logger.LogInformation($"[ATTACHMENT] Successfully obtained access token (Correlation: {correlationId})");

            // Make HTTP request to download attachment
            using var request = new HttpRequestMessage(HttpMethod.Get, attachmentUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"[ATTACHMENT] File not found: {fileName} (Correlation: {correlationId})");
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    $"[ATTACHMENT] Download failed. Status: {response.StatusCode}, Error: {errorContent} (Correlation: {correlationId})");
                return null;
            }

            var imageData = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation(
                $"[ATTACHMENT] Successfully downloaded {imageData.Length} bytes (Correlation: {correlationId})");

            return imageData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"[ATTACHMENT] Error downloading {fileName} for item {listItemId}: {ex.Message} (Correlation: {correlationId})");
            return null;
        }
    }

    /// <summary>
    /// Download image from SharePoint sharing URL using Graph API Drive
    /// Extracts image name from URL and downloads directly by path
    /// Returns both image data and content type from the response
    /// </summary>
    public async Task<(byte[]? ImageData, string? ContentType)> DownloadImageFromSharePointUrlAsync(string sharePointUrl, string correlationId)
    {
        try
        {
            _logger.LogInformation(
                $"[DRIVE-DOWNLOAD] Downloading image from SharePoint URL: {sharePointUrl} (Correlation: {correlationId})");

            if (string.IsNullOrEmpty(sharePointUrl))
            {
                _logger.LogWarning($"[DRIVE-DOWNLOAD] SharePoint URL is empty (Correlation: {correlationId})");
                return (null, null);
            }

            if (string.IsNullOrEmpty(_settings.SHAREPOINT_DRIVE_ID))
            {
                _logger.LogError($"[DRIVE-DOWNLOAD] SHAREPOINT_DRIVE_ID is not configured (Correlation: {correlationId})");
                return (null, null);
            }

            // Extract image name from the SharePoint URL
            // Format: https://nfm365.sharepoint.com/:i:/r/teams/VendorReturnProcess-024670/DamageImages/serialimage.png?...
            var uri = new Uri(sharePointUrl);
            var imageName = System.IO.Path.GetFileName(uri.LocalPath.Split('?')[0]);
            
            _logger.LogInformation($"[DRIVE-DOWNLOAD] Extracted image name: {imageName} (Correlation: {correlationId})");

            // Get access token for Microsoft Graph
            var graphScope = "https://graph.microsoft.com/.default";
            var tokenResult = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { graphScope }),
                CancellationToken.None);

            _logger.LogInformation($"[DRIVE-DOWNLOAD] Successfully obtained Graph API access token (Correlation: {correlationId})");

            // Get the item directly by path to retrieve its content
            var itemUrl = $"https://graph.microsoft.com/v1.0/drives/{_settings.SHAREPOINT_DRIVE_ID}/root:/{imageName}:/content";
            _logger.LogInformation($"[DRIVE-DOWNLOAD] Downloading from: {itemUrl} (Correlation: {correlationId})");

            using var request = new HttpRequestMessage(HttpMethod.Get, itemUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.Token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

            var response = await _httpClient.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogWarning($"[DRIVE-DOWNLOAD] Image not found in drive: {imageName} (Correlation: {correlationId})");
                return (null, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    $"[DRIVE-DOWNLOAD] Failed to download image. Status: {response.StatusCode}, Error: {errorContent} (Correlation: {correlationId})");
                return (null, null);
            }

            // Get content type from response headers
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
            
            var imageData = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation(
                $"[DRIVE-DOWNLOAD] Successfully downloaded {imageData.Length} bytes from drive, Content-Type: {contentType} (Correlation: {correlationId})");

            return (imageData, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"[DRIVE-DOWNLOAD] Unexpected error downloading from SharePoint URL {sharePointUrl}: {ex.Message} (Correlation: {correlationId})");
            return (null, null);
        }
    }

    public async Task UpdateListItemAsync(string listItemId, ProcessingResult result, string correlationId)
    {
        try
        {
            _logger.LogInformation($"Updating SharePoint list item {listItemId} with processing result (Correlation: {correlationId})");
            _logger.LogInformation($"Processing result - Status: {result.Status}, Serial: {result.Serial}, Confidence: {result.ConfidenceScore}");
            
            // For demo purposes, just log the update
            await Task.Delay(100);
            _logger.LogInformation($"Demo update completed for list item {listItemId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating SharePoint list item {listItemId}: {ex.Message}");
            throw;
        }
    }

    /* public async Task<bool> TestConnectionAsync()
    {
        try
        {
            _logger.LogInformation($"Testing SharePoint connection to: {_settings.SHAREPOINT_SITE_URL}");

            // Extract tenant and site path from URL
            var uri = new Uri(_settings.SHAREPOINT_SITE_URL);
            var host = uri.Host;
            var pathParts = uri.PathAndQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (pathParts.Length < 2)
            {
                _logger.LogError($"Invalid SharePoint URL format: {_settings.SHAREPOINT_SITE_URL}");
                return false;
            }

            var sitePath = string.Join("/", pathParts);
            var siteIdentifier = $"{host}:/{sitePath}";
            _logger.LogInformation($"Testing connection to site: {siteIdentifier}");

            // Test by attempting to get the specific site
            var site = await _graphClient.Sites[siteIdentifier].GetAsync();
            var isConnected = site?.Id != null;

            if (isConnected)
            {
                _logger.LogInformation($"SharePoint connection test successful - Site ID: {site.Id}, URL: {site.WebUrl}");
            }
            else
            {
                _logger.LogWarning("SharePoint connection test failed - Site not found");
            }

            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"SharePoint connection test failed: {ex.Message}");
            return false;
        }
    } */

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            _logger.LogInformation($"Testing SharePoint connection to: {_settings.SHAREPOINT_SITE_URL}");

            // Extract components from URL
            var uri = new Uri(_settings.SHAREPOINT_SITE_URL);
            var host = uri.Host;
            var pathParts = uri.PathAndQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // For: /teams/VendorReturnProcess-024670/Lists/VendorReturnedProducts
            if (pathParts.Length < 2)
            {
                _logger.LogError($"Invalid SharePoint URL format: {_settings.SHAREPOINT_SITE_URL}");
                return false;
            }

            // Site path: teams/VendorReturnProcess-024670
            var sitePath = $"{pathParts[0]}/{pathParts[1]}";
            var siteIdentifier = $"{host}:/{sitePath}";

            _logger.LogInformation($"Attempting to access site: {siteIdentifier}");

            // Try to get the site
            var site = await _graphClient.Sites[siteIdentifier]
                .GetAsync(config => { config.QueryParameters.Select = new[] { "id", "displayName", "webUrl" }; });

            if (site?.Id != null)
            {
                _logger.LogInformation($"Site access successful - Site ID: {site.Id}");

                // Try to access the specific list
                try
                {
                    var lists = await _graphClient.Sites[site.Id].Lists
                        .GetAsync(config =>
                        {
                            config.QueryParameters.Select = new[] { "id", "displayName" };
                            config.QueryParameters.Filter = "displayName eq 'VendorReturnedProducts'";
                        });

                    var targetList = lists?.Value?.FirstOrDefault();
                    if (targetList != null)
                    {
                        _logger.LogInformation($"List access successful - List ID: {targetList.Id}");
                        return true;
                    }

                    _logger.LogWarning("List 'VendorReturnedProducts' not found");
                }
                catch (Exception listEx)
                {
                    _logger.LogWarning(listEx, "Could not access lists, but site connection works");
                    return true; // Site connection works even if list enumeration fails
                }
            }

            return false;
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 401 || ex.ResponseStatusCode == 403)
        {
            _logger.LogError(
                $"Access denied. Check Azure AD app permissions and site access grants. Status: {ex.ResponseStatusCode}");
            return false;
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex, $"OData error: {ex.Error?.Code} - {ex.Error?.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"SharePoint connection test failed: {ex.Message}");
            return false;
        }
    }

    public async Task<List<SharePointListInfo>> DiscoverAvailableListsAsync(string correlationId)
    {
        var lists = new List<SharePointListInfo>();
        try
        {
            _logger.LogInformation($"[DISCOVERY] Starting SharePoint list discovery (Correlation: {correlationId})");
            _logger.LogInformation($"[DISCOVERY] Using configured site URL: {_settings.SHAREPOINT_SITE_URL}");
            _logger.LogInformation($"[DISCOVERY] Looking for list: {_settings.SHAREPOINT_LIST_ID}");

            // Extract tenant and site path from URL
            // Example: https://nfm365.sharepoint.com/sites/vrp
            var uri = new Uri(_settings.SHAREPOINT_SITE_URL);
            var host = uri.Host;
            var pathParts = uri.PathAndQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (pathParts.Length < 1)
            {
                _logger.LogError(
                    $"[DISCOVERY] Invalid SharePoint URL format. Expected format: https://tenant.sharepoint.com/sites/site-name (Correlation: {correlationId})");
                return lists;
            }

            // For /sites/vrp format
            var sitePath = string.Join("/", pathParts);
            var siteIdentifier = $"{host}:/{sitePath}";

            _logger.LogInformation($"[DISCOVERY] Site identifier: {siteIdentifier} (Correlation: {correlationId})");

            // First, get the site to obtain its ID (same as TestConnectionAsync)
            var site = await _graphClient.Sites[siteIdentifier]
                .GetAsync(config => { config.QueryParameters.Select = new[] { "id", "displayName", "webUrl" }; });

            if (site?.Id == null)
            {
                _logger.LogError(
                    $"[DISCOVERY] Could not access SharePoint site. Site identifier: {siteIdentifier} (Correlation: {correlationId})");
                return lists;
            }

            _logger.LogInformation(
                $"[DISCOVERY] Site found - ID: {site.Id}, URL: {site.WebUrl} (Correlation: {correlationId})");

            // Now get all lists from the site using the resolved site ID
            // Don't use Select/Filter parameters initially to get all lists
            var listsResponse = await _graphClient.Sites[site.Id].Lists.GetAsync();

            var allLists = listsResponse?.Value ?? new List<List>();

            _logger.LogInformation(
                $"[DISCOVERY] Found {allLists.Count} total lists in site (Correlation: {correlationId})");

            // Process and log each list
            foreach (var list in allLists)
                try
                {
                    var listInfo = new SharePointListInfo
                    {
                        Id = list.Id,
                        DisplayName = list.DisplayName,
                        Description = list.Description,
                        WebUrl = list.WebUrl,
                        ItemCount = 0
                    };

                    lists.Add(listInfo);

                    _logger.LogInformation(
                        $"[DISCOVERY] List - Name: '{list.DisplayName}', ID: {list.Id}, URL: {list.WebUrl} (Correlation: {correlationId})");

                    // Log if this is the target list we're looking for
                    if (list.DisplayName?.Equals(_settings.SHAREPOINT_LIST_ID, StringComparison.OrdinalIgnoreCase) ==
                        true)
                        _logger.LogInformation(
                            $"[DISCOVERY] *** TARGET LIST FOUND *** - Name: '{list.DisplayName}', ID: {list.Id} (Correlation: {correlationId})");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        $"[DISCOVERY] Error processing list: {ex.Message} (Correlation: {correlationId})");
                }

            _logger.LogInformation(
                $"[DISCOVERY] List discovery completed - Total lists processed: {lists.Count} (Correlation: {correlationId})");
        }
        catch (ServiceException ex) when (ex.ResponseStatusCode == 401 || ex.ResponseStatusCode == 403)
        {
            _logger.LogError(ex,
                $"[DISCOVERY] Access denied. Check Azure AD app permissions and site access grants. Status: {ex.ResponseStatusCode} (Correlation: {correlationId})");
        }
        catch (ODataError ex)
        {
            _logger.LogError(ex,
                $"[DISCOVERY] OData error: {ex.Error?.Code} - {ex.Error?.Message} (Correlation: {correlationId})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"[DISCOVERY] Error discovering SharePoint lists: {ex.Message} (Correlation: {correlationId})");
        }

        return lists;
    }

    /// <summary>
    ///     Get all list fields (columns) with their complete metadata from SharePoint
    /// </summary>
    public async Task<List<SharePointFieldInfo>> GetAllListFieldsAsync(string correlationId)
    {
        var fields = new List<SharePointFieldInfo>();
        try
        {
            _logger.LogInformation($"[FIELDS] Getting all list fields metadata (Correlation: {correlationId})");

            // Extract site info
            var uri = new Uri(_settings.SHAREPOINT_SITE_URL);
            var host = uri.Host;
            var pathParts = uri.PathAndQuery.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var sitePath = string.Join("/", pathParts);
            var siteIdentifier = $"{host}:/{sitePath}";

            // Resolve site
            var site = await _graphClient.Sites[siteIdentifier]
                .GetAsync(config => { config.QueryParameters.Select = new[] { "id" }; });

            if (site?.Id == null)
            {
                _logger.LogError($"[FIELDS] Could not resolve site (Correlation: {correlationId})");
                return fields;
            }

            // Get all fields from the list
            var fieldsResponse =
                await _graphClient.Sites[site.Id].Lists[_settings.SHAREPOINT_LIST_ID].Columns.GetAsync();
            var allFields = fieldsResponse?.Value ?? new List<ColumnDefinition>();

            _logger.LogInformation($"[FIELDS] Found {allFields.Count} fields in list (Correlation: {correlationId})");

            // Map all fields with complete metadata
            foreach (var field in allFields)
                try
                {
                    var fieldInfo = new SharePointFieldInfo
                    {
                        Id = field.Id,
                        Name = field.Name,
                        DisplayName = field.DisplayName,
                        Description = field.Description,
                        FieldType = GetColumnTypeName(field),
                        ReadOnly = field.ReadOnly ?? false,
                        Required = field.Required ?? false,
                        Hidden = field.Hidden ?? false,
                        Indexed = field.Indexed ?? false,
                        CanBeDeleted = true, // ColumnDefinition doesn't expose this directly
                        Sealed = false // ColumnDefinition doesn't expose this directly
                    };

                    fields.Add(fieldInfo);

                    _logger.LogInformation(
                        $"[FIELDS] Field - Name: '{field.Name}', DisplayName: '{field.DisplayName}', Type: {GetColumnTypeName(field)}, Required: {field.Required} (Correlation: {correlationId})");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"[FIELDS] Error processing field: {ex.Message} (Correlation: {correlationId})");
                }

            _logger.LogInformation($"[FIELDS] Completed - Total fields: {fields.Count} (Correlation: {correlationId})");

            // Log all fields as JSON for easy reference
            var fieldsJson = JsonSerializer.Serialize(fields,
                new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation($"[FIELDS] Complete fields metadata:\n{fieldsJson}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[FIELDS] Error getting list fields: {ex.Message} (Correlation: {correlationId})");
        }

        return fields;
    }

    /// <summary>
    ///     Get the column type name from a ColumnDefinition
    /// </summary>
    private string GetColumnTypeName(ColumnDefinition field)
    {
        // Check AdditionalData for the field type information
        if (field.AdditionalData?.ContainsKey("columnType") == true)
            return field.AdditionalData["columnType"]?.ToString() ?? "Unknown";

        // Fallback: check for specific field type properties
        return field.GetType().Name;
    }
}