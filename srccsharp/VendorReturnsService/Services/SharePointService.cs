using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Azure.Identity;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using VendorReturnsService.Configuration;
using VendorReturnsService.Models;
using Azure.Core;

namespace VendorReturnsService.Services;

/// <summary>
/// Service for SharePoint operations using Microsoft Graph API and REST API.
/// </summary>
public class SharePointService : ISharePointService
{
    private readonly SharePointSettings _settings;
    private readonly ILogger<SharePointService> _logger;
    private readonly HttpClient _httpClient;
    private GraphServiceClient? _graphClient;

    public SharePointService(
        SharePointSettings settings,
        ILogger<SharePointService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("SharePoint");
    }

    /// <summary>
    /// Initializes Graph client with appropriate authentication.
    /// </summary>
    private GraphServiceClient GetGraphClient(string correlationId)
    {
        if (_graphClient != null)
            return _graphClient;

        _logger.LogInformation("[{CorrelationId}] Initializing Graph client with {AuthMode} authentication",
            correlationId, _settings.AuthenticationMode);

        TokenCredential credential;

        if (_settings.AuthenticationMode == "Certificate")
        {
            X509Certificate2? certificate = null;

            // Load certificate from file or store
            if (!string.IsNullOrEmpty(_settings.CertificatePath))
            {
                certificate = new X509Certificate2(
                    _settings.CertificatePath,
                    _settings.CertificatePassword);
            }
            else if (!string.IsNullOrEmpty(_settings.CertificateThumbprint))
            {
                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    _settings.CertificateThumbprint.Replace(" ", ""),
                    false);

                if (certs.Count > 0)
                    certificate = certs[0];
            }

            if (certificate == null)
                throw new InvalidOperationException("Certificate not found");

            credential = new ClientCertificateCredential(
                _settings.TenantId,
                _settings.ClientId,
                certificate);
        }
        else
        {
            credential = new ClientSecretCredential(
                _settings.TenantId,
                _settings.ClientId,
                _settings.ClientSecret);
        }

        _graphClient = new GraphServiceClient(credential);
        return _graphClient;
    }

    /// <summary>
    /// Acquires access token using configured authentication method.
    /// </summary>
    private async Task<string> GetTokenAsync(string correlationId)
    {
        var scopes = new[] { $"https://{GetTenantHost()}/.default" };

        if (_settings.AuthenticationMode == "Certificate")
        {
            _logger.LogInformation("[{CorrelationId}] Using certificate authentication", correlationId);

            X509Certificate2? certificate = null;

            // Load certificate from file or store
            if (!string.IsNullOrEmpty(_settings.CertificatePath))
            {
                certificate = new X509Certificate2(
                    _settings.CertificatePath,
                    _settings.CertificatePassword);
            }
            else if (!string.IsNullOrEmpty(_settings.CertificateThumbprint))
            {
                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(
                    X509FindType.FindByThumbprint,
                    _settings.CertificateThumbprint.Replace(" ", ""),
                    false);

                if (certs.Count > 0)
                    certificate = certs[0];
            }

            if (certificate == null)
                throw new InvalidOperationException("Certificate not found");

            var credential = new ClientCertificateCredential(
                _settings.TenantId,
                _settings.ClientId,
                certificate);

            var tokenContext = new TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(tokenContext);
            return token.Token;
        }
        else
        {
            _logger.LogInformation("[{CorrelationId}] Using client secret authentication", correlationId);

            var credential = new ClientSecretCredential(
                _settings.TenantId,
                _settings.ClientId,
                _settings.ClientSecret);

            var tokenContext = new TokenRequestContext(scopes);
            var token = await credential.GetTokenAsync(tokenContext);
            return token.Token;
        }
    }

    /// <summary>
    /// Extracts tenant host from site URL.
    /// </summary>
    private string GetTenantHost()
    {
        var uri = new Uri(_settings.SiteUrl);
        return uri.Host;
    }

    /// <summary>
    /// Retrieves list items with specified status.
    /// </summary>
    public async Task<List<VendorReturnItem>> GetItemsByStatusAsync(
        string status,
        string? excludeProcessStatus,
        string correlationId)
    {
        try
        {
            _logger.LogInformation(
                "[{CorrelationId}] Fetching items with Status={Status}, ExcludeProcessStatus={ExcludeProcessStatus}",
                correlationId, status, excludeProcessStatus);

            var client = GetGraphClient(correlationId);

            // Extract site ID from URL
            var siteId = await GetSiteIdAsync(client, correlationId);

            // Build filter
            var filter = $"fields/Status eq '{status}'";
            if (!string.IsNullOrEmpty(excludeProcessStatus))
            {
                filter += $" and fields/ProcessStatus ne '{excludeProcessStatus}'";
            }

            var items = await client.Sites[siteId]
                .Lists[_settings.ListId]
                .Items
                .GetAsync(config =>
                {
                    config.QueryParameters.Expand = new[] { "fields" };
                    config.QueryParameters.Filter = filter;
                });

            var returnItems = new List<VendorReturnItem>();

            if (items?.Value != null)
            {
                foreach (var item in items.Value)
                {
                    if (item.Fields?.AdditionalData != null)
                    {
                        returnItems.Add(MapToVendorReturnItem(item.Id!, item.Fields.AdditionalData));
                    }
                }
            }

            _logger.LogInformation("[{CorrelationId}] Retrieved {Count} items", correlationId, returnItems.Count);
            return returnItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error retrieving items by status", correlationId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a single list item by ID.
    /// </summary>
    public async Task<VendorReturnItem?> GetItemByIdAsync(string listItemId, string correlationId)
    {
        try
        {
            _logger.LogInformation("[{CorrelationId}] Fetching item {ListItemId}", correlationId, listItemId);

            var client = GetGraphClient(correlationId);
            var siteId = await GetSiteIdAsync(client, correlationId);

            var item = await client.Sites[siteId]
                .Lists[_settings.ListId]
                .Items[listItemId]
                .GetAsync(config =>
                {
                    config.QueryParameters.Expand = new[] { "fields" };
                });

            if (item?.Fields?.AdditionalData == null)
                return null;

            return MapToVendorReturnItem(item.Id!, item.Fields.AdditionalData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error retrieving item {ListItemId}", correlationId, listItemId);
            throw;
        }
    }

    /// <summary>
    /// Updates a list item with specified fields.
    /// </summary>
    public async Task<bool> UpdateItemAsync(
        string listItemId,
        SharePointUpdateModel updates,
        string correlationId)
    {
        try
        {
            _logger.LogInformation("[{CorrelationId}] Updating item {ListItemId}", correlationId, listItemId);

            var client = GetGraphClient(correlationId);
            var siteId = await GetSiteIdAsync(client, correlationId);

            var fieldValues = new FieldValueSet
            {
                AdditionalData = new Dictionary<string, object>()
            };

            // Map update model to field values
            if (updates.PieceNumber != null)
                fieldValues.AdditionalData["PieceNumber"] = updates.PieceNumber;
            if (updates.SerialNumber != null)
                fieldValues.AdditionalData["SerialNumber"] = updates.SerialNumber;
            if (updates.SkuNumber != null)
                fieldValues.AdditionalData["SkuNumber"] = updates.SkuNumber;
            if (updates.WarehouseLocation != null)
                fieldValues.AdditionalData["WarehouseLocation"] = updates.WarehouseLocation;
            if (updates.Vendor != null)
                fieldValues.AdditionalData["Vendor"] = updates.Vendor;
            if (updates.Family != null)
                fieldValues.AdditionalData["Family"] = updates.Family;
            if (updates.ModelNumber != null)
                fieldValues.AdditionalData["ModelNumber"] = updates.ModelNumber;
            if (updates.RackLocation != null)
                fieldValues.AdditionalData["RackLocation"] = updates.RackLocation;
            if (updates.Status != null)
                fieldValues.AdditionalData["Status"] = updates.Status;
            if (updates.ProcessStatus != null)
                fieldValues.AdditionalData["ProcessStatus"] = updates.ProcessStatus;

            await client.Sites[siteId]
                .Lists[_settings.ListId]
                .Items[listItemId]
                .Fields
                .PatchAsync(fieldValues);

            _logger.LogInformation("[{CorrelationId}] Successfully updated item {ListItemId}", correlationId, listItemId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error updating item {ListItemId}", correlationId, listItemId);
            return false;
        }
    }

    /// <summary>
    /// Downloads an attachment from a list item using SharePoint REST API.
    /// </summary>
    public async Task<byte[]?> DownloadAttachmentAsync(
        string listItemId,
        string fileName,
        string correlationId)
    {
        try
        {
            _logger.LogInformation(
                "[{CorrelationId}] Downloading attachment {FileName} from item {ListItemId}",
                correlationId, fileName, listItemId);

            var token = await GetTokenAsync(correlationId);
            var tenantHost = GetTenantHost();

            // SharePoint REST API endpoint
            var attachmentUrl = $"{_settings.SiteUrl}/_api/web/lists(guid'{_settings.ListId}')/items({listItemId})/AttachmentFiles('{fileName}')/$value";

            var request = new HttpRequestMessage(HttpMethod.Get, attachmentUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var imageData = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation(
                "[{CorrelationId}] Downloaded attachment {FileName} ({Size} bytes)",
                correlationId, fileName, imageData.Length);

            return imageData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Error downloading attachment {FileName} from item {ListItemId}",
                correlationId, fileName, listItemId);
            return null;
        }
    }

    /// <summary>
    /// Downloads an image from SharePoint Drive by URL using Graph API.
    /// </summary>
    public async Task<byte[]?> DownloadImageFromDriveAsync(string imageUrl, string correlationId)
    {
        try
        {
            _logger.LogInformation("[{CorrelationId}] Downloading image from Drive: {ImageUrl}", correlationId, imageUrl);

            var token = await GetTokenAsync(correlationId);

            var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var imageData = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation(
                "[{CorrelationId}] Downloaded image from Drive ({Size} bytes)",
                correlationId, imageData.Length);

            return imageData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error downloading image from Drive: {ImageUrl}", correlationId, imageUrl);
            return null;
        }
    }

    /// <summary>
    /// Gets the SharePoint site ID.
    /// </summary>
    private async Task<string> GetSiteIdAsync(GraphServiceClient client, string correlationId)
    {
        var siteUri = new Uri(_settings.SiteUrl);
        var hostName = siteUri.Host;
        var sitePath = siteUri.AbsolutePath;

        var site = await client.Sites[$"{hostName}:{sitePath}"].GetAsync();

        if (site == null || string.IsNullOrEmpty(site.Id))
            throw new InvalidOperationException("Could not retrieve site ID");

        return site.Id;
    }

    /// <summary>
    /// Maps SharePoint fields to VendorReturnItem model.
    /// </summary>
    private VendorReturnItem MapToVendorReturnItem(string id, IDictionary<string, object> fields)
    {
        return new VendorReturnItem
        {
            Id = id,
            Status = GetFieldValue(fields, "Status") ?? "",
            ProcessStatus = GetFieldValue(fields, "ProcessStatus"),
            PieceNumber = GetFieldValue(fields, "PieceNumber"),
            SerialNumber = GetFieldValue(fields, "SerialNumber"),
            SkuNumber = GetFieldValue(fields, "SkuNumber"),
            WarehouseLocation = GetFieldValue(fields, "WarehouseLocation"),
            Vendor = GetFieldValue(fields, "Vendor"),
            Family = GetFieldValue(fields, "Family"),
            ModelNumber = GetFieldValue(fields, "ModelNumber"),
            RackLocation = GetFieldValue(fields, "RackLocation"),
            PieceImage = GetFieldValue(fields, "PieceImage"),
            SerialImage = GetFieldValue(fields, "SerialImage"),
            PieceImageUrl = GetFieldValue(fields, "PieceImageUrl"),
            SerialImageUrl = GetFieldValue(fields, "SerialImageUrl")
        };
    }

    /// <summary>
    /// Safely retrieves a field value as string.
    /// </summary>
    private string? GetFieldValue(IDictionary<string, object> fields, string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var value))
        {
            if (value is JsonElement jsonElement)
            {
                return jsonElement.ValueKind == JsonValueKind.String
                    ? jsonElement.GetString()
                    : jsonElement.ToString();
            }
            return value?.ToString();
        }
        return null;
    }
}
