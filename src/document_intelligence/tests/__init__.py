"""
Test Package for Document Intelligence Function App

This package contains comprehensive tests for:
- Unit tests for individual components (models, services, repositories)
- Integration tests for service interactions
- End-to-end tests for function app endpoints
- Mock tests for Azure API interactions
- Performance and load testing utilities
"""

import os
import sys

# Add source directory to path for imports
test_dir = os.path.dirname(__file__)
src_dir = os.path.join(test_dir, '..')
sys.path.insert(0, src_dir)

# Add parent directories for shared components
parent_dir = os.path.join(test_dir, '../../..')
sys.path.insert(0, parent_dir)

# Test environment configuration
os.environ.setdefault('PYTEST_RUNNING', '1')
os.environ.setdefault('LOG_LEVEL', 'DEBUG')
os.environ.setdefault('AZURE_FUNCTIONS_ENVIRONMENT', 'Testing')
os.environ.setdefault('ENABLE_BLOB_STORAGE', 'false')
os.environ.setdefault('CONFIDENCE_THRESHOLD', '0.8')

__version__ = '1.0.0'