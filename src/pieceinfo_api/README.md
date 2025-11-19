# PieceInfo API Usage Examples

## Overview
The PieceInfo API aggregates data from three external APIs to provide comprehensive piece information in a single response.

## 🔒 HTTPS/SSL Configuration

For **production deployments**, enable HTTPS/SSL verification:

### Quick Setup
```bash
# Enable SSL for production
.\configure_ssl.ps1 production

# Check SSL status
.\configure_ssl.ps1 status

# Enable SSL manually
.\configure_ssl.ps1 enable
```

### Manual Configuration
Update `local.settings.json`:
```json
{
  "Values": {
    "VERIFY_SSL": "true",
    "EXTERNAL_API_BASE_URL": "https://apim-prod.nfm.com"
  }
}
```

📖 **Complete HTTPS Guide**: See [HTTPS_SSL_GUIDE.md](./HTTPS_SSL_GUIDE.md) for detailed configuration options.

## External APIs Integration

The API calls three external endpoints in sequence:

1. **Piece Inventory Location**: `https://apim-dev.nfm.com/ihubservices/product/piece-inventory-location/{piece_number}`
2. **Product Master**: `https://apim-dev.nfm.com/ihubservices/product/product-master/{sku}`
3. **Vendor Details**: `https://apim-dev.nfm.com/ihubservices/product/vendor/{vendor_code}`

## API Endpoints

### Get Single Piece Information
```http
GET /api/v1/pieces/170080637
```

**Response:**
```json
{
  "piece_inventory_key": "170080637",
  "sku": "67007500",
  "vendor_code": "VIZIA",
  "warehouse_location": "WHKCTY",
  "rack_location": "R03-019-03",
  "serial_number": "SZVOU5GB1600294",
  "description": "ALL-IN-ONE SOUNDBAR",
  "model_no": "SV210D-0806",
  "brand": "VIZBC",
  "family": "ELECTR",
  "category": "EHMAUD",
  "group": "HMSBAR",
  "purchase_reference_number": "6610299377*2",
  "vendor_name": "NIGHT & DAY",
  "vendor_address": {
    "address_line1": "3901 N KINGSHIGHWAY BLVD",
    "address_line2": "",
    "city": "SAINT LOUIS",
    "state": "MO",
    "zip_code": "63115"
  },
  "vendor_contact": {
    "rep_name": "John Nicholson",
    "primary_rep_email": "jpnick@kc.rr.com",
    "secondary_rep_email": "gmail.com",
    "exec_email": null
  },
  "vendor_policies": {
    "serial_number_required": false,
    "vendor_return": false
  }
}
```

### Batch Request
```http
POST /api/v1/pieces/batch
Content-Type: application/json

{
  "piece_numbers": ["170080637", "170080638", "170080639"],
  "correlation_id": "batch-request-123"
}
```

**Response:**
```json
{
  "results": {
    "170080637": {
      "piece_inventory_key": "170080637",
      "sku": "67007500",
      "vendor_code": "VIZIA",
      "description": "ALL-IN-ONE SOUNDBAR"
    }
  },
  "errors": {
    "170080638": {
      "error": "not_found", 
      "message": "Piece number 170080638 not found"
    }
  },
  "summary": {
    "total_requested": 3,
    "successful": 1,
    "failed": 2
  },
  "correlation_id": "batch-request-123"
}
```

### Health Check
```http
GET /api/v1/pieces/health
```

## Error Handling

The API provides structured error responses:

### Validation Error (400)
```json
{
  "error": "validation_error",
  "message": "Piece number is required",
  "correlation_id": "abc-123"
}
```

### Not Found (404)
```json
{
  "error": "not_found",
  "message": "Piece number 999999999 not found",
  "correlation_id": "abc-123"
}
```

### Rate Limit (429)
```json
{
  "error": "rate_limit_exceeded",
  "message": "Rate limit exceeded, please try again later",
  "details": {
    "retry_after": 60
  }
}
```

### Timeout (504)
```json
{
  "error": "timeout_error",
  "message": "Request timed out while retrieving piece information"
}
```

## Configuration

Environment variables for the function app:

```bash
EXTERNAL_API_BASE_URL=https://apim-dev.nfm.com
API_TIMEOUT_SECONDS=30
API_MAX_RETRIES=3
MAX_BATCH_SIZE=10
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...
```

## Logging and Monitoring

The API includes comprehensive logging:

- **Request/Response logging** with correlation tracking
- **Business event logging** for key operations
- **Performance metrics** in Application Insights
- **Error tracking** with exception details

Example log entries:
```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "INFO", 
  "message": "Piece info aggregation completed successfully",
  "correlation_id": "abc-123",
  "piece_number": "170080637",
  "sku": "67007500",
  "vendor_code": "VIZIA"
}
```

## Data Deduplication

The API automatically removes duplicate properties when merging data:

- **SKU**: Present in both piece inventory and product master (uses piece inventory as primary)
- **Vendor**: Present in all three APIs (uses piece inventory for vendor_code, vendor API for details)
- **Family**: Present in both piece inventory and product master (uses product master as canonical)

## Testing

Run tests for the PieceInfo API:

```bash
# Run all PieceInfo API tests
python -m pytest tests/pieceinfo_api/ -v

# Run specific test file
python -m pytest tests/pieceinfo_api/test_models.py -v

# Run with coverage
python -m pytest tests/pieceinfo_api/ --cov=pieceinfo_api --cov-report=html
```

## Local Development

1. Start the function app:
```bash
cd src/pieceinfo_api
func start --port 7074
```

2. Test the endpoint:
```bash
curl http://localhost:7074/api/v1/pieces/170080637
```

3. Test batch endpoint:
```bash
curl -X POST http://localhost:7074/api/v1/pieces/batch \
  -H "Content-Type: application/json" \
  -d '{"piece_numbers": ["170080637"]}'
```