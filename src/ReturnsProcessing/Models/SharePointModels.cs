using System.Text.Json.Serialization;

namespace WarehouseReturns.ReturnsProcessing.Models
{
    public class VendorReturnEntry
    {
        [JsonPropertyName("ID")]
        public string Id { get; set; } = string.Empty;
        
        [JsonPropertyName("Title")]
        public string Title { get; set; } = string.Empty;
        
        [JsonPropertyName("RackLocation")]
        public string RackLocation { get; set; } = string.Empty;
        
        [JsonPropertyName("PieceNumber")]
        public string PieceNumber { get; set; } = string.Empty;
        
        [JsonPropertyName("PieceImage")]
        public SharePointImageAttachment? PieceImage { get; set; }
        
        [JsonPropertyName("SerialNumber")]
        public string SerialNumber { get; set; } = string.Empty;
        
        [JsonPropertyName("SerialImage")]
        public SharePointImageAttachment? SerialImage { get; set; }
        
        [JsonPropertyName("Comments")]
        public string Comments { get; set; } = string.Empty;
        
        [JsonPropertyName("Status")]
        public string Status { get; set; } = string.Empty;
        
        [JsonPropertyName("SkuNumber")]
        public string SkuNumber { get; set; } = string.Empty;
        
        [JsonPropertyName("Vendor")]
        public string Vendor { get; set; } = string.Empty;
        
        [JsonPropertyName("Family")]
        public string Family { get; set; } = string.Empty;
        
        [JsonPropertyName("Created")]
        public string Created { get; set; } = string.Empty;
        
        [JsonPropertyName("Attachments")]
        public string Attachments { get; set; } = string.Empty;
    }
    
    public class SharePointImageAttachment
    {
        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;
        
        [JsonPropertyName("originalImageName")]
        public string OriginalImageName { get; set; } = string.Empty;
        
        [JsonPropertyName("serverRelativeUrl")]
        public string? ServerRelativeUrl { get; set; }
        
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("serverUrl")]
        public string? ServerUrl { get; set; }
    }
    
    public class SharePointListResponse
    {
        [JsonPropertyName("value")]
        public List<VendorReturnEntry> Value { get; set; } = new();
    }
    
    public class AttachmentInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string ServerRelativeUrl { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string ContentType { get; set; } = string.Empty;
    }
}