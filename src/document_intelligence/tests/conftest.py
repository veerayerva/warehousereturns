"""
Test Fixtures and Utilities

Shared test fixtures, mock data, and utility functions for Document Intelligence tests.
"""

import pytest
import asyncio
import json
import os
from typing import Dict, Any, Optional
from datetime import datetime
from unittest.mock import Mock, AsyncMock

# Import Azure Functions testing utilities
import azure.functions as func

# Import Document Intelligence components
from models import (
    DocumentAnalysisUrlRequest,
    DocumentAnalysisFileRequest, 
    DocumentAnalysisResponse,
    SerialFieldResult,
    AnalysisStatus,
    FieldExtractionStatus,
    ErrorResponse,
    ErrorCode
)
from models.azure_document_intelligence_model import (
    AzureDocIntelResponse,
    AnalyzeResult,
    DocumentField,
    BoundingRegion
)


@pytest.fixture
def sample_url_request():
    """Sample URL-based document analysis request."""
    return DocumentAnalysisUrlRequest(
        document_url="https://example.com/sample-document.pdf",
        model_id="serialnumber",
        document_type="warehouse_return",
        confidence_threshold=0.8
    )


@pytest.fixture  
def sample_file_request():
    """Sample file-based document analysis request."""
    return DocumentAnalysisFileRequest(
        model_id="serialnumber",
        document_type="warehouse_return",
        confidence_threshold=0.7
    )


@pytest.fixture
def sample_document_bytes():
    """Sample document file content for testing."""
    # Create a minimal PDF-like byte sequence for testing
    pdf_header = b"%PDF-1.4\n"
    pdf_content = b"Sample document content for testing"
    pdf_footer = b"\n%%EOF"
    return pdf_header + pdf_content + pdf_footer


@pytest.fixture
def sample_serial_field_success():
    """Sample successful serial field extraction result."""
    return SerialFieldResult(
        field_name="Serial",
        value="SN123456789",
        confidence=0.92,
        status=FieldExtractionStatus.EXTRACTED,
        extraction_metadata={
            "meets_threshold": True,
            "extraction_success": True,
            "raw_extracted_value": "SN123456789"
        }
    )


@pytest.fixture
def sample_serial_field_low_confidence():
    """Sample low-confidence serial field extraction result."""
    return SerialFieldResult(
        field_name="Serial",
        value=None,  # Not returned due to low confidence
        confidence=0.45,
        status=FieldExtractionStatus.LOW_CONFIDENCE,
        extraction_metadata={
            "meets_threshold": False,
            "extraction_success": True,
            "raw_extracted_value": "SN987654321"
        }
    )


@pytest.fixture
def sample_analysis_response_success():
    """Sample successful document analysis response."""
    return DocumentAnalysisResponse(
        analysis_id="analysis-test-123",
        status=AnalysisStatus.SUCCEEDED,
        serial_field=SerialFieldResult(
            field_name="Serial",
            value="SN123456789",
            confidence=0.92,
            status=FieldExtractionStatus.EXTRACTED
        ),
        document_metadata={
            "source_type": "url",
            "document_url": "https://example.com/test.pdf",
            "document_type": "warehouse_return",
            "model_id": "serialnumber"
        },
        processing_metadata={
            "processing_time_ms": 5500,
            "azure_api_version": "2023-07-31",
            "confidence_threshold": 0.8,
            "model_used": "serialnumber"
        },
        created_at=datetime.utcnow(),
        completed_at=datetime.utcnow(),
        correlation_id="corr-test-123"
    )


@pytest.fixture
def mock_azure_response_success():
    """Mock successful Azure Document Intelligence API response."""
    
    # Create mock document field for serial number
    serial_field = DocumentField(
        type="string",
        valueString="SN123456789",
        content="SN123456789",
        confidence=0.92,
        spans=[{"offset": 100, "length": 11}],
        boundingRegions=[
            BoundingRegion(
                pageNumber=1,
                polygon=[{"x": 100, "y": 200}, {"x": 200, "y": 200}, {"x": 200, "y": 220}, {"x": 100, "y": 220}]
            )
        ]
    )
    
    # Create mock analyze result
    analyze_result = AnalyzeResult(
        apiVersion="2023-07-31",
        modelId="serialnumber",
        stringIndexType="textElements",
        content="Sample document content with Serial: SN123456789",
        pages=[{
            "pageNumber": 1,
            "angle": 0,
            "width": 8.5,
            "height": 11,
            "unit": "inch"
        }],
        documents=[{
            "docType": "serialnumber",
            "confidence": 0.92,
            "fields": {
                "Serial": serial_field.model_dump()
            },
            "spans": [{"offset": 0, "length": 50}]
        }]
    )
    
    return AzureDocIntelResponse(
        status="succeeded",
        createdDateTime="2024-01-15T10:30:00Z",
        lastUpdatedDateTime="2024-01-15T10:30:15Z", 
        analyzeResult=analyze_result
    )


@pytest.fixture
def mock_azure_response_low_confidence():
    """Mock Azure response with low confidence serial extraction."""
    
    # Create mock document field with low confidence
    serial_field = DocumentField(
        type="string",
        valueString="SN987654321",
        content="SN987654321",
        confidence=0.45,  # Below typical threshold
        spans=[{"offset": 80, "length": 11}],
        boundingRegions=[
            BoundingRegion(
                pageNumber=1,
                polygon=[{"x": 50, "y": 300}, {"x": 150, "y": 300}, {"x": 150, "y": 320}, {"x": 50, "y": 320}]
            )
        ]
    )
    
    analyze_result = AnalyzeResult(
        apiVersion="2023-07-31", 
        modelId="serialnumber",
        stringIndexType="textElements",
        content="Sample document content with unclear Serial: SN987654321",
        pages=[{
            "pageNumber": 1,
            "angle": 0,
            "width": 8.5,
            "height": 11,
            "unit": "inch"
        }],
        documents=[{
            "docType": "serialnumber",
            "confidence": 0.45,
            "fields": {
                "Serial": serial_field.model_dump()
            },
            "spans": [{"offset": 0, "length": 60}]
        }]
    )
    
    return AzureDocIntelResponse(
        status="succeeded",
        createdDateTime="2024-01-15T10:30:00Z",
        lastUpdatedDateTime="2024-01-15T10:30:15Z",
        analyzeResult=analyze_result
    )


@pytest.fixture
def mock_http_request_url():
    """Mock HTTP request for URL-based processing."""
    req_body = {
        "document_url": "https://example.com/test-document.pdf",
        "model_id": "serialnumber", 
        "document_type": "warehouse_return",
        "confidence_threshold": 0.8
    }
    
    req = Mock(spec=func.HttpRequest)
    req.get_json.return_value = req_body
    req.headers = {"content-type": "application/json"}
    req.files = {}
    req.params = {}
    
    return req


@pytest.fixture
def mock_http_request_file():
    """Mock HTTP request for file upload processing."""
    # Create mock file object
    mock_file = Mock()
    mock_file.filename = "test-document.pdf"
    mock_file.content_type = "application/pdf"
    mock_file.read.return_value = b"%PDF-1.4\nTest document content\n%%EOF"
    
    req = Mock(spec=func.HttpRequest)
    req.files = {"document": mock_file}
    req.form = {
        "model_id": "serialnumber",
        "document_type": "warehouse_return",
        "confidence_threshold": "0.7"
    }
    req.headers = {"content-type": "multipart/form-data"}
    req.params = {}
    req.get_json.side_effect = ValueError("Not JSON")
    
    return req


@pytest.fixture
def mock_document_intelligence_service():
    """Mock Document Intelligence Service with common responses."""
    mock_service = Mock()
    
    # Mock successful analysis
    async def mock_analyze_url(request, correlation_id=None):
        return mock_azure_response_success(), None
    
    async def mock_analyze_bytes(data, request, filename, content_type, correlation_id=None):
        return mock_azure_response_success(), None
    
    mock_service.analyze_document_from_url = AsyncMock(side_effect=mock_analyze_url)
    mock_service.analyze_document_from_bytes = AsyncMock(side_effect=mock_analyze_bytes)
    mock_service.health_check.return_value = {"status": "healthy"}
    
    return mock_service


@pytest.fixture
def mock_blob_repository():
    """Mock Blob Storage Repository."""
    mock_repo = Mock()
    
    # Mock successful storage
    async def mock_store_document(*args, **kwargs):
        return {
            "container": "low-confidence",
            "blob_path": "pending-review/analysis-test-123/document.pdf",
            "storage_account": "warehousereturns",
            "stored_at": datetime.utcnow().isoformat()
        }, None
    
    mock_repo.store_low_confidence_document = AsyncMock(side_effect=mock_store_document)
    
    async def mock_health_check():
        return {"status": "healthy", "container_count": 4}
    
    mock_repo.health_check = AsyncMock(side_effect=mock_health_check)
    
    return mock_repo


@pytest.fixture
def mock_processing_service(mock_document_intelligence_service, mock_blob_repository):
    """Mock Document Processing Service with dependencies."""
    from services.document_processing_service import DocumentProcessingService
    
    mock_service = Mock(spec=DocumentProcessingService)
    
    # Mock successful processing methods
    async def mock_process_url(request, correlation_id=None):
        return DocumentAnalysisResponse(
            analysis_id="analysis-test-123",
            status=AnalysisStatus.SUCCEEDED,
            serial_field=SerialFieldResult(
                field_name="Serial",
                value="SN123456789",
                confidence=0.92,
                status=FieldExtractionStatus.EXTRACTED
            ),
            document_metadata={
                "source_type": "url",
                "document_url": str(request.document_url),
                "document_type": request.document_type,
                "model_id": request.model_id
            },
            processing_metadata={
                "processing_time_ms": 5500,
                "confidence_threshold": 0.8
            },
            created_at=datetime.utcnow(),
            completed_at=datetime.utcnow(),
            correlation_id=correlation_id
        )
    
    async def mock_process_bytes(data, filename, content_type, request, correlation_id=None):
        return DocumentAnalysisResponse(
            analysis_id="analysis-test-456", 
            status=AnalysisStatus.SUCCEEDED,
            serial_field=SerialFieldResult(
                field_name="Serial",
                value="SN987654321",
                confidence=0.88,
                status=FieldExtractionStatus.EXTRACTED
            ),
            document_metadata={
                "source_type": "file_upload",
                "filename": filename,
                "content_type": content_type,
                "file_size_bytes": len(data),
                "document_type": request.document_type,
                "model_id": request.model_id
            },
            processing_metadata={
                "processing_time_ms": 4200,
                "confidence_threshold": 0.7
            },
            created_at=datetime.utcnow(),
            completed_at=datetime.utcnow(),
            correlation_id=correlation_id
        )
    
    async def mock_health_check():
        return {
            "service": "document_processing",
            "status": "healthy",
            "components": {
                "document_intelligence": {"status": "healthy"},
                "blob_storage": {"status": "healthy"}
            }
        }
    
    mock_service.process_document_from_url = AsyncMock(side_effect=mock_process_url)
    mock_service.process_document_from_bytes = AsyncMock(side_effect=mock_process_bytes)
    mock_service.health_check = AsyncMock(side_effect=mock_health_check)
    mock_service.confidence_threshold = 0.7
    mock_service.enable_blob_storage = True
    
    return mock_service


@pytest.fixture
def test_environment():
    """Set up test environment variables."""
    original_env = os.environ.copy()
    
    # Set test environment variables
    test_env = {
        'PYTEST_RUNNING': '1',
        'LOG_LEVEL': 'DEBUG',
        'AZURE_FUNCTIONS_ENVIRONMENT': 'Testing',
        'CONFIDENCE_THRESHOLD': '0.8',
        'ENABLE_BLOB_STORAGE': 'false',
        'DOCUMENT_INTELLIGENCE_ENDPOINT': 'https://test-endpoint.cognitiveservices.azure.com/',
        'DOCUMENT_INTELLIGENCE_API_KEY': 'test-api-key',
        'AZURE_STORAGE_CONNECTION_STRING': 'DefaultEndpointsProtocol=https;AccountName=test;AccountKey=test;EndpointSuffix=core.windows.net'
    }
    
    os.environ.update(test_env)
    
    yield test_env
    
    # Restore original environment
    os.environ.clear()
    os.environ.update(original_env)


class AsyncContextManager:
    """Helper class for async context manager testing."""
    
    def __init__(self, return_value=None):
        self.return_value = return_value
    
    async def __aenter__(self):
        return self.return_value
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        return None