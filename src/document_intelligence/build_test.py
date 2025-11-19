#!/usr/bin/env python3
"""
Build Test Script for Document Intelligence Function App

Tests compilation and imports for all modules in the Document Intelligence Function App.
"""

import sys
import os
from pathlib import Path

# Add the proper Python paths
current_dir = Path(__file__).parent
src_dir = current_dir.parent  
sys.path.insert(0, str(src_dir))
sys.path.insert(0, str(current_dir))

def test_imports():
    """Test all module imports."""
    
    print("=== Document Intelligence Build Test ===")
    print(f"Current directory: {current_dir}")
    print(f"Source directory: {src_dir}")
    print(f"Python path: {sys.path[:3]}")
    print()
    
    success_count = 0
    total_tests = 0
    
    # Test 1: Basic model imports
    total_tests += 1
    try:
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
        print("✓ Core models imported successfully")
        success_count += 1
    except Exception as e:
        print(f"✗ Core models import failed: {e}")
    
    # Test 2: Azure model imports
    total_tests += 1
    try:
        from models.AzureDocumentIntelligenceModel import (
            AzureDocIntelResponse,
            AnalyzeResult,
            DocumentField,
            BoundingRegion
        )
        print("✓ Azure Document Intelligence models imported successfully")
        success_count += 1
    except Exception as e:
        print(f"✗ Azure models import failed: {e}")
    
    # Test 3: Azure SDK imports
    total_tests += 1
    try:
        from azure.ai.documentintelligence import DocumentIntelligenceClient
        from azure.storage.blob import BlobServiceClient
        print("✓ Azure SDK packages imported successfully")
        success_count += 1
    except Exception as e:
        print(f"✗ Azure SDK import failed: {e}")
    
    # Test 4: Individual module syntax validation
    total_tests += 1
    try:
        # Test if modules can be parsed without importing (syntax check)
        import ast
        
        service_files = [
            'services/document_intelligence_service.py',
            'services/document_processing_service.py',
            'repositories/blob_storage_repository.py',
            'function_app.py'
        ]
        
        for file_path in service_files:
            if os.path.exists(file_path):
                with open(file_path, 'r', encoding='utf-8') as f:
                    content = f.read()
                    ast.parse(content)  # This will raise SyntaxError if invalid
        
        print("✓ All service modules have valid Python syntax")
        success_count += 1
    except Exception as e:
        print(f"✗ Syntax validation failed: {e}")
    
    # Test 5: Azure Functions specific test
    total_tests += 1
    try:
        import azure.functions as func
        print("✓ Azure Functions runtime available")
        success_count += 1
    except Exception as e:
        print(f"✗ Azure Functions runtime import failed: {e}")
    
    # Test 6: Check required dependencies
    total_tests += 1
    try:
        required_packages = [
            'azure.ai.documentintelligence',
            'azure.storage.blob', 
            'aiohttp',
            'pydantic'
        ]
        
        for package in required_packages:
            __import__(package)
        
        print("✓ All required dependencies available")
        success_count += 1
    except ImportError as e:
        print(f"✗ Missing dependency: {e}")
    
    # Test 7: Model instantiation
    total_tests += 1
    try:
        url_request = DocumentAnalysisUrlRequest(
            document_url="https://example.com/test.pdf",
            model_id="serialnumber",
            document_type="serialnumber"
        )
        
        file_request = DocumentAnalysisFileRequest(
            model_id="serialnumber", 
            document_type="serialnumber"
        )
        
        print("✓ Model instantiation successful")
        success_count += 1
    except Exception as e:
        print(f"✗ Model instantiation failed: {e}")
    
    print()
    print(f"=== Build Test Results ===")
    print(f"Tests passed: {success_count}/{total_tests}")
    print(f"Success rate: {success_count/total_tests*100:.1f}%")
    
    if success_count == total_tests:
        print("🎉 All tests passed! The Document Intelligence Function App is ready for configuration.")
        return True
    else:
        print("⚠️  Some tests failed. Please check the errors above.")
        return False


if __name__ == "__main__":
    success = test_imports()
    sys.exit(0 if success else 1)