#!/usr/bin/env python3
"""
Test script to verify Azure Storage SDK imports and basic connectivity
"""

import os
import sys

def test_imports():
    """Test if Azure Storage SDK can be imported"""
    print("Testing Azure Storage SDK imports...")
    
    try:
        from azure.storage.blob.aio import BlobServiceClient, ContainerClient
        print("✅ Successfully imported azure.storage.blob.aio")
    except ImportError as e:
        print(f"❌ Failed to import azure.storage.blob.aio: {e}")
        return False
    
    try:
        from azure.storage.blob import BlobProperties
        print("✅ Successfully imported azure.storage.blob")
    except ImportError as e:
        print(f"❌ Failed to import azure.storage.blob: {e}")
        return False
    
    try:
        from azure.core.exceptions import AzureError, ResourceNotFoundError
        print("✅ Successfully imported azure.core.exceptions")
    except ImportError as e:
        print(f"❌ Failed to import azure.core.exceptions: {e}")
        return False
    
    return True

def test_connection_string():
    """Test connection string configuration"""
    print("\nTesting connection string configuration...")
    
    connection_string = os.getenv('AZURE_STORAGE_CONNECTION_STRING')
    if not connection_string:
        print("❌ AZURE_STORAGE_CONNECTION_STRING environment variable not set")
        return False
    
    print(f"✅ Connection string found (length: {len(connection_string)})")
    
    # Parse connection string
    try:
        parts = dict(part.split('=', 1) for part in connection_string.split(';') if '=' in part)
        account_name = parts.get('AccountName', 'unknown')
        print(f"✅ Storage account: {account_name}")
        
        if 'AccountKey' in parts:
            print("✅ Account key found")
        else:
            print("❌ Account key not found in connection string")
            return False
            
    except Exception as e:
        print(f"❌ Failed to parse connection string: {e}")
        return False
    
    return True

async def test_blob_client():
    """Test basic blob client creation"""
    print("\nTesting blob client creation...")
    
    try:
        from azure.storage.blob.aio import BlobServiceClient
        
        connection_string = os.getenv('AZURE_STORAGE_CONNECTION_STRING')
        if not connection_string:
            print("❌ No connection string available")
            return False
        
        # Create client
        client = BlobServiceClient.from_connection_string(connection_string)
        print("✅ BlobServiceClient created successfully")
        
        # Test container client
        container_client = client.get_container_client("test-container")
        print("✅ Container client created successfully")
        
        return True
        
    except Exception as e:
        print(f"❌ Failed to create blob client: {type(e).__name__}: {e}")
        return False

def main():
    """Run all tests"""
    print("=== Azure Storage SDK Test ===\n")
    
    # Test imports
    imports_ok = test_imports()
    
    # Test connection string
    config_ok = test_connection_string()
    
    # Test blob client (async)
    import asyncio
    client_ok = asyncio.run(test_blob_client())
    
    print(f"\n=== Test Results ===")
    print(f"Imports: {'✅ PASS' if imports_ok else '❌ FAIL'}")
    print(f"Configuration: {'✅ PASS' if config_ok else '❌ FAIL'}")
    print(f"Client Creation: {'✅ PASS' if client_ok else '❌ FAIL'}")
    
    if imports_ok and config_ok and client_ok:
        print("\n🎉 All tests passed! Azure Storage should work.")
    else:
        print("\n💥 Some tests failed. Check the errors above.")

if __name__ == "__main__":
    main()