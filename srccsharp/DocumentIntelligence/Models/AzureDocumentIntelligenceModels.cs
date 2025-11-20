using System.Text.Json.Serialization;

namespace WarehouseReturns.DocumentIntelligence.Models;

/// <summary>
/// Bounding region coordinates for extracted fields
/// </summary>
public class BoundingRegion
{
    /// <summary>
    /// Page number where the region is located
    /// </summary>
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    /// <summary>
    /// Polygon coordinates defining the bounding region
    /// </summary>
    [JsonPropertyName("polygon")]
    public List<double> Polygon { get; set; } = new();
}

/// <summary>
/// Text span indicating character positions in the document
/// </summary>
public class TextSpan
{
    /// <summary>
    /// Starting character offset
    /// </summary>
    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    /// <summary>
    /// Length of the text span
    /// </summary>
    [JsonPropertyName("length")]
    public int Length { get; set; }
}

/// <summary>
/// Document field extracted by Azure Document Intelligence
/// </summary>
public class DocumentField
{
    /// <summary>
    /// Field type (string, number, etc.)
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Field value content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score (0.0 to 1.0)
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Bounding regions where the field was found
    /// </summary>
    [JsonPropertyName("bounding_regions")]
    public List<BoundingRegion> BoundingRegions { get; set; } = new();

    /// <summary>
    /// Text spans for the field
    /// </summary>
    [JsonPropertyName("spans")]
    public List<TextSpan> Spans { get; set; } = new();
}

/// <summary>
/// Document analyzed by Azure Document Intelligence
/// </summary>
public class AnalyzedDocument
{
    /// <summary>
    /// Document type identified by the model
    /// </summary>
    [JsonPropertyName("doc_type")]
    public string DocType { get; set; } = string.Empty;

    /// <summary>
    /// Overall confidence for the document
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Extracted fields from the document
    /// </summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, DocumentField> Fields { get; set; } = new();

    /// <summary>
    /// Bounding regions for the entire document
    /// </summary>
    [JsonPropertyName("bounding_regions")]
    public List<BoundingRegion> BoundingRegions { get; set; } = new();

    /// <summary>
    /// Text spans for the document
    /// </summary>
    [JsonPropertyName("spans")]
    public List<TextSpan> Spans { get; set; } = new();
}

/// <summary>
/// Analysis result from Azure Document Intelligence API
/// </summary>
public class AnalyzeResult
{
    /// <summary>
    /// API version used for analysis
    /// </summary>
    [JsonPropertyName("api_version")]
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>
    /// Model ID used for analysis
    /// </summary>
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Document content as plain text
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Documents analyzed
    /// </summary>
    [JsonPropertyName("documents")]
    public List<AnalyzedDocument> Documents { get; set; } = new();

    /// <summary>
    /// Pages in the document
    /// </summary>
    [JsonPropertyName("pages")]
    public List<DocumentPage> Pages { get; set; } = new();

    /// <summary>
    /// Tables extracted from the document
    /// </summary>
    [JsonPropertyName("tables")]
    public List<DocumentTable> Tables { get; set; } = new();
}

/// <summary>
/// Page information from analyzed document
/// </summary>
public class DocumentPage
{
    /// <summary>
    /// Page number (1-based)
    /// </summary>
    [JsonPropertyName("page_number")]
    public int PageNumber { get; set; }

    /// <summary>
    /// Page width in pixels
    /// </summary>
    [JsonPropertyName("width")]
    public double Width { get; set; }

    /// <summary>
    /// Page height in pixels
    /// </summary>
    [JsonPropertyName("height")]
    public double Height { get; set; }

    /// <summary>
    /// Text units (rotation angle)
    /// </summary>
    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "pixel";

    /// <summary>
    /// Text lines on the page
    /// </summary>
    [JsonPropertyName("lines")]
    public List<DocumentLine> Lines { get; set; } = new();

    /// <summary>
    /// Words on the page
    /// </summary>
    [JsonPropertyName("words")]
    public List<DocumentWord> Words { get; set; } = new();
}

/// <summary>
/// Text line in a document page
/// </summary>
public class DocumentLine
{
    /// <summary>
    /// Line content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Bounding polygon for the line
    /// </summary>
    [JsonPropertyName("polygon")]
    public List<double> Polygon { get; set; } = new();

    /// <summary>
    /// Text spans for the line
    /// </summary>
    [JsonPropertyName("spans")]
    public List<TextSpan> Spans { get; set; } = new();
}

/// <summary>
/// Word in a document page
/// </summary>
public class DocumentWord
{
    /// <summary>
    /// Word content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Confidence score for the word
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Bounding polygon for the word
    /// </summary>
    [JsonPropertyName("polygon")]
    public List<double> Polygon { get; set; } = new();

    /// <summary>
    /// Text span for the word
    /// </summary>
    [JsonPropertyName("span")]
    public TextSpan? Span { get; set; }
}

/// <summary>
/// Table extracted from document
/// </summary>
public class DocumentTable
{
    /// <summary>
    /// Number of rows in the table
    /// </summary>
    [JsonPropertyName("row_count")]
    public int RowCount { get; set; }

    /// <summary>
    /// Number of columns in the table
    /// </summary>
    [JsonPropertyName("column_count")]
    public int ColumnCount { get; set; }

    /// <summary>
    /// Table cells
    /// </summary>
    [JsonPropertyName("cells")]
    public List<DocumentTableCell> Cells { get; set; } = new();

    /// <summary>
    /// Bounding regions for the table
    /// </summary>
    [JsonPropertyName("bounding_regions")]
    public List<BoundingRegion> BoundingRegions { get; set; } = new();
}

/// <summary>
/// Cell in a document table
/// </summary>
public class DocumentTableCell
{
    /// <summary>
    /// Row index (0-based)
    /// </summary>
    [JsonPropertyName("row_index")]
    public int RowIndex { get; set; }

    /// <summary>
    /// Column index (0-based)
    /// </summary>
    [JsonPropertyName("column_index")]
    public int ColumnIndex { get; set; }

    /// <summary>
    /// Cell content
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Bounding regions for the cell
    /// </summary>
    [JsonPropertyName("bounding_regions")]
    public List<BoundingRegion> BoundingRegions { get; set; } = new();

    /// <summary>
    /// Text spans for the cell
    /// </summary>
    [JsonPropertyName("spans")]
    public List<TextSpan> Spans { get; set; } = new();
}

/// <summary>
/// Complete response from Azure Document Intelligence API
/// </summary>
public class AzureDocumentIntelligenceResponse
{
    /// <summary>
    /// Analysis operation status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Created timestamp
    /// </summary>
    [JsonPropertyName("created_date_time")]
    public DateTime CreatedDateTime { get; set; }

    /// <summary>
    /// Last updated timestamp
    /// </summary>
    [JsonPropertyName("last_updated_date_time")]
    public DateTime LastUpdatedDateTime { get; set; }

    /// <summary>
    /// Analysis result
    /// </summary>
    [JsonPropertyName("analyze_result")]
    public AnalyzeResult? AnalyzeResult { get; set; }

    /// <summary>
    /// API version used
    /// </summary>
    [JsonPropertyName("api_version")]
    public string ApiVersion { get; set; } = string.Empty;

    /// <summary>
    /// Model ID used for analysis
    /// </summary>
    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Error information if analysis failed
    /// </summary>
    [JsonPropertyName("error")]
    public ErrorResponse? Error { get; set; }
}