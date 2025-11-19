"""
Test server to simulate Azure Functions for development testing.
This allows us to test our Document Intelligence functionality without the azure-functions-worker dependency.
"""

from flask import Flask, request, jsonify, send_from_directory
import asyncio
import os
import sys
import logging
from typing import Dict, Any
import json

# Add the current directory to Python path for imports
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from function_app import process_document, get_analysis_result, document_health_check, get_swagger_doc, swagger_ui
from azure.functions import HttpRequest, HttpResponse

app = Flask(__name__)
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class MockHttpRequest:
    """Mock HttpRequest to simulate Azure Functions HttpRequest"""
    def __init__(self, flask_request):
        self._flask_request = flask_request
        self._body = None
        
    def get_body(self):
        if self._body is None:
            self._body = self._flask_request.get_data()
        return self._body
    
    def get_json(self):
        try:
            return self._flask_request.get_json() or {}
        except:
            return {}
    
    @property
    def method(self):
        return self._flask_request.method
        
    @property
    def url(self):
        return self._flask_request.url
        
    @property
    def headers(self):
        return dict(self._flask_request.headers)
        
    @property
    def params(self):
        return dict(self._flask_request.args)

def convert_response(func_response):
    """Convert Azure Functions HttpResponse to Flask response"""
    if hasattr(func_response, 'get_body'):
        body = func_response.get_body()
        if isinstance(body, bytes):
            body = body.decode('utf-8')
    else:
        body = str(func_response)
    
    try:
        # Try to parse as JSON
        json_body = json.loads(body)
        return jsonify(json_body), getattr(func_response, 'status_code', 200)
    except:
        # Return as plain text
        return body, getattr(func_response, 'status_code', 200)

@app.route('/api/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    try:
        mock_req = MockHttpRequest(request)
        response = document_health_check(mock_req)
        return convert_response(response)
    except Exception as e:
        logger.error(f"Health check error: {e}")
        return jsonify({"error": "Health check failed", "details": str(e)}), 500

@app.route('/api/process-document', methods=['POST'])
def process_document_route():
    """Process document endpoint (supports file upload and document_url)"""
    try:
        mock_req = MockHttpRequest(request)
        response = process_document(mock_req)
        return convert_response(response)
    except Exception as e:
        logger.error(f"Process document error: {e}")
        return jsonify({"error": "Document processing failed", "details": str(e)}), 500

@app.route('/api/analysis-result/<operation_id>', methods=['GET'])
def get_analysis_result(operation_id):
    """Get analysis result endpoint"""
    try:
        # Add operation_id to the request args
        request.view_args = {'operation_id': operation_id}
        mock_req = MockHttpRequest(request)
        mock_req.route_params = {"operation_id": operation_id}
        response = get_analysis_result(mock_req)
        return convert_response(response)
    except Exception as e:
        logger.error(f"Get analysis result error: {e}")
        return jsonify({"error": "Failed to get analysis result", "details": str(e)}), 500

@app.route('/api/swagger', methods=['GET'])
def swagger_doc():
    """Swagger documentation endpoint"""
    try:
        mock_req = MockHttpRequest(request)
        response = get_swagger_doc(mock_req)
        # Ensure response body is valid JSON and content type is application/json
        body = response.get_body()
        if isinstance(body, bytes):
            body = body.decode('utf-8')
        return body, 200, {'Content-Type': 'application/json'}
    except Exception as e:
        logger.error(f"Swagger doc error: {e}")
        return jsonify({"error": "Failed to get swagger doc", "details": str(e)}), 500

@app.route('/api/docs', methods=['GET'])
def swagger_ui_route():
    """Swagger UI endpoint"""
    try:
        mock_req = MockHttpRequest(request)
        response = swagger_ui(mock_req)
        return convert_response(response)
    except Exception as e:
        logger.error(f"Swagger UI error: {e}")
        return jsonify({"error": "Failed to get swagger UI", "details": str(e)}), 500

@app.route('/', methods=['GET'])
def root():
    """Root endpoint with available routes"""
    routes = {
        "message": "Document Intelligence Function App Test Server",
        "available_endpoints": {
            "health": "/api/health",
            "process_document": "/api/process-document",
            "get_analysis_result": "/api/analysis-result/{operation_id}",
            "swagger_doc": "/api/swagger",
            "swagger_ui": "/api/docs"
        }
    }
    return jsonify(routes)

if __name__ == '__main__':
    print("Starting Document Intelligence Test Server...")
    print("Available endpoints:")
    print("  - Health Check: http://localhost:7071/api/health")
    print("  - Process Document: http://localhost:7071/api/process-document")
    print("  - Analysis Result: http://localhost:7071/api/analysis-result/{operation_id}")
    print("  - Swagger Doc: http://localhost:7071/api/swagger")
    print("  - Swagger UI: http://localhost:7071/api/docs")
    print("  - Root: http://localhost:7071/")
    
    app.run(host='0.0.0.0', port=7071, debug=True)