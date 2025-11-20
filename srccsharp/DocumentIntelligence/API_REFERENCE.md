# DocumentIntelligence API Reference

## Overview

The DocumentIntelligence API provides enterprise-grade document processing capabilities using Azure Document Intelligence (formerly Form Recognizer). This API enables automated extraction of text, key-value pairs, tables, and structured data from various document formats including PDFs, images, and Office documents.

## Base URL

```
Local Development: http://localhost:7072/api
Production: https://<your-function-app>.azurewebsites.net/api
```

## Authentication

The API uses Azure Active Directory (AAD) authentication and Function Key authentication:

- **Function Key**: Add `?code=<function-key>` to requests
- **AAD Bearer Token**: Add `Authorization: Bearer <token>` header

## Content Types

- **Request**: `application/json`, `multipart/form-data`
- **Response**: `application/json`

## Rate Limits

- **Concurrent Requests**: 10 per client
- **Request Rate**: 100 requests per minute per client
- **File Size**: Maximum 50MB per document

---

## Endpoints

### 1. Analyze Document

Analyzes a document and extracts structured information using Azure Document Intelligence.

#### Request

```http
POST /api/analyze-document
Content-Type: application/json
Authorization: Bearer <token>
```

#### Request Body

```json
{
  "documentUrl": "https://example.com/document.pdf",
  "modelId": "prebuilt-document",
  "features": ["keyValuePairs", "tables", "entities"],
  "pages": "1-5",
  "locale": "en-US"
}
```

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `documentUrl` | string | Yes | Public URL of the document to analyze |
| `modelId` | string | No | Document Intelligence model ID (default: "prebuilt-document") |
| `features` | array | No | Features to extract (keyValuePairs, tables, entities, etc.) |
| `pages` | string | No | Page range to analyze (e.g., "1-3,5") |
| `locale` | string | No | Document locale for better OCR accuracy |

#### Response

```json
{
  "documentId": "doc_12345",
  "status": "completed",
  "modelId": "prebuilt-document",
  "results": {
    "content": "Full extracted text content...",
    "pages": [
      {
        "pageNumber": 1,
        "angle": 0,
        "width": 8.5,
        "height": 11,
        "unit": "inch",
        "lines": [
          {
            "content": "Document title",
            "boundingBox": [1.0, 1.0, 4.0, 1.5],
            "spans": [{"offset": 0, "length": 14}]
          }
        ]
      }
    ],
    "keyValuePairs": [
      {
        "key": {
          "content": "Name:",
          "boundingBox": [1.0, 2.0, 2.0, 2.3],
          "spans": [{"offset": 20, "length": 5}]
        },
        "value": {
          "content": "John Doe",
          "boundingBox": [2.1, 2.0, 3.5, 2.3],
          "spans": [{"offset": 26, "length": 8}]
        },
        "confidence": 0.95
      }
    ],
    "tables": [
      {
        "rowCount": 3,
        "columnCount": 2,
        "boundingBox": [1.0, 3.0, 7.0, 5.0],
        "spans": [{"offset": 100, "length": 150}],
        "cells": [
          {
            "kind": "columnHeader",
            "rowIndex": 0,
            "columnIndex": 0,
            "content": "Item",
            "boundingBox": [1.0, 3.0, 4.0, 3.5],
            "spans": [{"offset": 100, "length": 4}]
          }
        ]
      }
    ],
    "entities": [
      {
        "category": "Person",
        "subCategory": "Name",
        "content": "John Doe",
        "offset": 26,
        "length": 8,
        "confidence": 0.89
      }
    ]
  },
  "metadata": {
    "processingTime": "00:00:05.123",
    "documentType": "application/pdf",
    "pageCount": 5,
    "language": "en"
  },
  "warnings": []
}
```

#### Response Codes

| Code | Description |
|------|-------------|
| 200 | Analysis completed successfully |
| 202 | Analysis started (long-running operation) |
| 400 | Invalid request parameters |
| 401 | Authentication required |
| 403 | Insufficient permissions |
| 413 | Document too large |
| 415 | Unsupported media type |
| 422 | Document processing failed |
| 429 | Rate limit exceeded |
| 500 | Internal server error |

#### Error Response

```json
{
  "error": {
    "code": "InvalidDocumentFormat",
    "message": "The document format is not supported",
    "details": [
      {
        "code": "UnsupportedFormat",
        "message": "Only PDF, JPEG, PNG, BMP, TIFF formats are supported"
      }
    ]
  },
  "requestId": "req_12345",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

---

### 2. Upload and Analyze Document

Uploads a document file and analyzes it in a single request.

#### Request

```http
POST /api/upload-analyze
Content-Type: multipart/form-data
Authorization: Bearer <token>
```

#### Form Data

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `file` | file | Yes | Document file to upload and analyze |
| `modelId` | string | No | Document Intelligence model ID |
| `features` | string | No | Comma-separated list of features to extract |
| `pages` | string | No | Page range to analyze |
| `locale` | string | No | Document locale |

#### Example cURL

```bash
curl -X POST "http://localhost:7072/api/upload-analyze" \
  -H "Authorization: Bearer <token>" \
  -F "file=@document.pdf" \
  -F "modelId=prebuilt-document" \
  -F "features=keyValuePairs,tables"
```

#### Response

Same as [Analyze Document](#1-analyze-document) response format.

---

### 3. Get Analysis Status

Retrieves the status of a long-running document analysis operation.

#### Request

```http
GET /api/analyze-document/{operationId}/status
Authorization: Bearer <token>
```

#### Path Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `operationId` | string | Yes | Operation ID returned from initial analysis request |

#### Response

```json
{
  "operationId": "op_12345",
  "status": "running",
  "createdDateTime": "2024-01-15T10:30:00Z",
  "lastUpdatedDateTime": "2024-01-15T10:30:15Z",
  "percentCompleted": 75,
  "estimatedTimeRemaining": "00:00:10"
}
```

#### Status Values

| Status | Description |
|--------|-------------|
| `notStarted` | Operation queued but not started |
| `running` | Operation in progress |
| `completed` | Operation completed successfully |
| `failed` | Operation failed |
| `cancelled` | Operation was cancelled |

---

### 4. Get Analysis Result

Retrieves the complete result of a completed document analysis.

#### Request

```http
GET /api/analyze-document/{operationId}/result
Authorization: Bearer <token>
```

#### Response

Same as [Analyze Document](#1-analyze-document) response format when status is `completed`.

---

### 5. List Supported Models

Returns a list of available Document Intelligence models.

#### Request

```http
GET /api/models
Authorization: Bearer <token>
```

#### Response

```json
{
  "models": [
    {
      "modelId": "prebuilt-document",
      "description": "General document analysis for key-value pairs, tables, and entities",
      "supportedFeatures": ["keyValuePairs", "tables", "entities", "languages"],
      "supportedFormats": ["pdf", "jpeg", "png", "bmp", "tiff"],
      "maxFileSize": "50MB"
    },
    {
      "modelId": "prebuilt-invoice",
      "description": "Invoice-specific analysis with field extraction",
      "supportedFeatures": ["fields", "tables", "lineItems"],
      "supportedFormats": ["pdf", "jpeg", "png", "bmp", "tiff"],
      "maxFileSize": "50MB"
    }
  ]
}
```

---

### 6. Health Check

Checks the health and status of the API service.

#### Request

```http
GET /api/health
```

#### Response

```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "version": "1.0.0",
  "services": {
    "documentIntelligence": {
      "status": "healthy",
      "responseTime": "150ms"
    },
    "blobStorage": {
      "status": "healthy",
      "responseTime": "75ms"
    },
    "database": {
      "status": "healthy",
      "responseTime": "25ms"
    }
  },
  "systemInfo": {
    "memory": {
      "used": "512MB",
      "total": "2GB"
    },
    "uptime": "2d 5h 30m"
  }
}
```

---

## Models and Features

### Available Models

#### 1. prebuilt-document
General-purpose document analysis model.

**Features:**
- Text extraction with OCR
- Key-value pairs detection
- Table extraction
- Entity recognition
- Language detection

**Best for:** General documents, contracts, forms

#### 2. prebuilt-invoice
Specialized invoice processing model.

**Features:**
- Invoice fields (vendor, amount, date, etc.)
- Line items extraction
- Tax calculation verification
- Payment terms extraction

**Best for:** Invoices, bills, receipts

#### 3. prebuilt-receipt
Receipt-specific analysis model.

**Features:**
- Merchant information
- Transaction details
- Item-level data
- Tax and tip amounts

**Best for:** Receipts, transaction records

#### 4. prebuilt-layout
Layout analysis without field extraction.

**Features:**
- Text lines and words
- Table structure
- Selection marks
- Reading order

**Best for:** Document layout analysis, OCR preprocessing

### Feature Descriptions

| Feature | Description | Available in Models |
|---------|-------------|-------------------|
| `keyValuePairs` | Extracts key-value relationships | document, invoice |
| `tables` | Extracts table structure and content | All models |
| `entities` | Recognizes named entities (person, location, etc.) | document |
| `languages` | Detects document language | document, layout |
| `fields` | Model-specific structured fields | invoice, receipt |
| `lineItems` | Itemized list extraction | invoice, receipt |

---

## Error Codes

### Client Errors (4xx)

| Code | Error Type | Description |
|------|------------|-------------|
| 400 | `BadRequest` | Invalid request format or parameters |
| 401 | `Unauthorized` | Missing or invalid authentication |
| 403 | `Forbidden` | Insufficient permissions for operation |
| 404 | `NotFound` | Resource or operation not found |
| 413 | `PayloadTooLarge` | Document exceeds size limit |
| 415 | `UnsupportedMediaType` | Document format not supported |
| 422 | `UnprocessableEntity` | Document content cannot be processed |
| 429 | `TooManyRequests` | Rate limit exceeded |

### Server Errors (5xx)

| Code | Error Type | Description |
|------|------------|-------------|
| 500 | `InternalServerError` | Unexpected server error |
| 502 | `BadGateway` | Azure service unavailable |
| 503 | `ServiceUnavailable` | Service temporarily unavailable |
| 504 | `GatewayTimeout` | Processing timeout |

### Document Intelligence Specific Errors

| Error Code | Description |
|------------|-------------|
| `InvalidDocumentFormat` | Unsupported file format |
| `DocumentTooLarge` | File exceeds size limits |
| `DocumentCorrupted` | File is corrupted or unreadable |
| `ModelNotSupported` | Specified model ID is invalid |
| `FeatureNotAvailable` | Requested feature not supported by model |
| `ProcessingFailed` | Document analysis failed |
| `InsufficientQuota` | Service quota exceeded |

---

## SDKs and Code Examples

### C# SDK Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using DocumentIntelligence.Services;

// Configure services
var services = new ServiceCollection();
services.AddDocumentIntelligenceServices(configuration);
var serviceProvider = services.BuildServiceProvider();

// Use the service
var documentService = serviceProvider.GetRequiredService<IDocumentIntelligenceService>();

var request = new DocumentAnalysisRequest
{
    DocumentUrl = "https://example.com/document.pdf",
    ModelId = "prebuilt-document",
    Features = new[] { "keyValuePairs", "tables" }
};

var result = await documentService.AnalyzeDocumentAsync(request);
Console.WriteLine($"Extracted {result.Results.KeyValuePairs.Count} key-value pairs");
```

### JavaScript/TypeScript Example

```typescript
interface DocumentAnalysisRequest {
  documentUrl: string;
  modelId?: string;
  features?: string[];
  pages?: string;
  locale?: string;
}

class DocumentIntelligenceClient {
  constructor(private baseUrl: string, private authToken: string) {}

  async analyzeDocument(request: DocumentAnalysisRequest): Promise<any> {
    const response = await fetch(`${this.baseUrl}/api/analyze-document`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${this.authToken}`
      },
      body: JSON.stringify(request)
    });

    if (!response.ok) {
      throw new Error(`API Error: ${response.status} ${response.statusText}`);
    }

    return await response.json();
  }
}

// Usage
const client = new DocumentIntelligenceClient('http://localhost:7072', 'your-token');
const result = await client.analyzeDocument({
  documentUrl: 'https://example.com/document.pdf',
  modelId: 'prebuilt-document',
  features: ['keyValuePairs', 'tables']
});
```

### Python Example

```python
import requests
import json
from typing import Optional, List

class DocumentIntelligenceClient:
    def __init__(self, base_url: str, auth_token: str):
        self.base_url = base_url
        self.auth_token = auth_token
        self.session = requests.Session()
        self.session.headers.update({
            'Authorization': f'Bearer {auth_token}',
            'Content-Type': 'application/json'
        })
    
    def analyze_document(self, 
                        document_url: str,
                        model_id: Optional[str] = None,
                        features: Optional[List[str]] = None,
                        pages: Optional[str] = None,
                        locale: Optional[str] = None) -> dict:
        
        request_data = {
            'documentUrl': document_url,
            'modelId': model_id or 'prebuilt-document',
            'features': features or ['keyValuePairs', 'tables'],
            'pages': pages,
            'locale': locale
        }
        
        # Remove None values
        request_data = {k: v for k, v in request_data.items() if v is not None}
        
        response = self.session.post(
            f'{self.base_url}/api/analyze-document',
            json=request_data
        )
        
        response.raise_for_status()
        return response.json()

# Usage
client = DocumentIntelligenceClient('http://localhost:7072', 'your-token')
result = client.analyze_document(
    document_url='https://example.com/document.pdf',
    features=['keyValuePairs', 'tables', 'entities']
)

print(f"Extracted {len(result['results']['keyValuePairs'])} key-value pairs")
```

---

## Webhooks and Callbacks

### Webhook Configuration

For long-running operations, you can configure webhooks to receive notifications when processing completes.

#### Request

```http
POST /api/analyze-document
Content-Type: application/json
Authorization: Bearer <token>
```

```json
{
  "documentUrl": "https://example.com/document.pdf",
  "modelId": "prebuilt-document",
  "webhook": {
    "url": "https://your-app.com/webhook/document-analysis",
    "headers": {
      "X-Webhook-Secret": "your-secret-key"
    },
    "events": ["completed", "failed"]
  }
}
```

#### Webhook Payload

```json
{
  "eventType": "analysis.completed",
  "operationId": "op_12345",
  "status": "completed",
  "timestamp": "2024-01-15T10:30:00Z",
  "data": {
    "documentId": "doc_12345",
    "resultUrl": "https://api.example.com/api/analyze-document/op_12345/result"
  }
}
```

---

## Rate Limiting and Quotas

### Rate Limits

| Limit Type | Value | Scope |
|------------|-------|-------|
| Requests per minute | 100 | Per client |
| Concurrent operations | 10 | Per client |
| Document size | 50MB | Per request |
| Pages per document | 2000 | Per request |

### Response Headers

Rate limit information is included in response headers:

```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1642248600
X-RateLimit-Retry-After: 60
```

### Quota Management

```json
{
  "quota": {
    "monthly": {
      "limit": 10000,
      "used": 2500,
      "remaining": 7500,
      "resetDate": "2024-02-01T00:00:00Z"
    },
    "daily": {
      "limit": 1000,
      "used": 150,
      "remaining": 850,
      "resetDate": "2024-01-16T00:00:00Z"
    }
  }
}
```

---

## Security

### Authentication

#### Azure Active Directory (Recommended)

```http
Authorization: Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6...
```

#### Function Key

```http
GET /api/analyze-document?code=your-function-key
```

### Data Security

- All data transmission uses TLS 1.2+
- Documents are temporarily stored in encrypted Azure Blob Storage
- Processed documents are automatically deleted after 24 hours
- API logs do not contain sensitive document content
- All operations are audited and logged

### Compliance

- **SOC 2 Type II** certified
- **GDPR** compliant data handling
- **HIPAA** eligible configuration available
- **ISO 27001** certified infrastructure

---

## Monitoring and Observability

### Application Insights Integration

The API automatically logs telemetry data to Azure Application Insights:

- Request/response metrics
- Performance counters
- Error rates and exceptions
- Custom business events

### Health Monitoring

```http
GET /api/health/detailed
```

```json
{
  "status": "healthy",
  "checks": [
    {
      "name": "DocumentIntelligence",
      "status": "healthy",
      "duration": "00:00:00.150",
      "data": {
        "endpoint": "https://region.api.cognitive.microsoft.com/",
        "quota": "8500/10000"
      }
    },
    {
      "name": "BlobStorage",
      "status": "healthy",
      "duration": "00:00:00.075"
    }
  ]
}
```

### Metrics and Alerts

Key metrics to monitor:

- **Request Rate**: Requests per minute
- **Success Rate**: Percentage of successful requests
- **Response Time**: P95 response time
- **Error Rate**: Rate of 4xx/5xx responses
- **Queue Depth**: Pending operations count

---

## Troubleshooting

### Common Issues

#### 1. Document Format Not Supported

**Error**: `415 Unsupported Media Type`

**Solution**: Ensure document is in supported format (PDF, JPEG, PNG, BMP, TIFF)

#### 2. Document Too Large

**Error**: `413 Payload Too Large`

**Solution**: Reduce document size or split into smaller files

#### 3. Authentication Failed

**Error**: `401 Unauthorized`

**Solution**: Verify Bearer token or function key is valid

#### 4. Rate Limit Exceeded

**Error**: `429 Too Many Requests`

**Solution**: Implement exponential backoff retry logic

### Debug Information

Enable debug logging by setting environment variable:

```
DOCUMENTINTELLIGENCE_LOG_LEVEL=Debug
```

### Support

For technical support:
- **Documentation**: [Internal Wiki](https://wiki.company.com/documentintelligence)
- **Issue Tracking**: [Project Issues](https://github.com/company/documentintelligence/issues)
- **Email**: documentintelligence-support@company.com
- **Slack**: #documentintelligence-support