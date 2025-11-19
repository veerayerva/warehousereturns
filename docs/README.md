# Warehouse Returns System - Master Documentation Index

## 📚 Complete Documentation Suite

This is your comprehensive guide to understanding the Warehouse Returns system. The documentation is organized from high-level architecture to detailed implementation specifics.

## 🗺️ Documentation Structure

### **1. System Overview & Architecture** 
📄 **[System Architecture](./SYSTEM_ARCHITECTURE.md)**
- High-level system design and component relationships
- Data flow diagrams and integration patterns  
- Security, monitoring, and extensibility architecture
- Deployment and operational considerations

### **2. Detailed Component Documentation**

#### **Core Function Apps**
📄 **[Document Intelligence Function App](./DOCUMENT_INTELLIGENCE_DETAILED.md)**
- Complete function-by-function documentation
- Azure Document Intelligence integration details
- Blob storage management for low-confidence documents  
- Request/response handling and error management

📄 **[PieceInfo API Function App](./PIECEINFO_API_DETAILED.md)**
- REST API endpoints with Swagger documentation
- External API integration patterns
- Data aggregation and HTTP client security
- Health monitoring and performance tracking

#### **Supporting Components**
📄 **[Shared Components Framework](./SHARED_COMPONENTS_DETAILED.md)**
- Centralized logging configuration and business event tracking
- HTTP middleware for request/response logging
- Cross-cutting concerns and utility functions
- Application Insights integration patterns

📄 **[Models & Data Contracts](./MODELS_DATA_CONTRACTS_DETAILED.md)**
- Request and response model definitions
- Azure API integration models
- Error handling and validation frameworks
- Data serialization and type safety

#### **Infrastructure & Deployment**
📄 **[Deployment & Infrastructure](./DEPLOYMENT_INFRASTRUCTURE_DETAILED.md)**
- Automated Azure resource provisioning scripts
- CI/CD pipeline configuration
- Environment management and configuration
- Security and compliance considerations

### **3. End-to-End Process Documentation**
📄 **[Complete Process Flow](./COMPLETE_PROCESS_FLOW.md)**
- Step-by-step walkthrough of document processing
- Decision logic and confidence evaluation
- Blob storage workflow for continuous improvement
- Comprehensive logging and monitoring examples

## 🎯 Quick Start Guides

### **For Developers**
1. **Start Here**: [System Architecture](./SYSTEM_ARCHITECTURE.md) for overall understanding
2. **Implementation**: [Document Intelligence Details](./DOCUMENT_INTELLIGENCE_DETAILED.md) for main processing logic
3. **Integration**: [Models & Data Contracts](./MODELS_DATA_CONTRACTS_DETAILED.md) for API contracts
4. **Debugging**: [Shared Components](./SHARED_COMPONENTS_DETAILED.md) for logging and middleware

### **For Operations Teams**
1. **System Overview**: [System Architecture](./SYSTEM_ARCHITECTURE.md) for operational context
2. **Process Understanding**: [Complete Process Flow](./COMPLETE_PROCESS_FLOW.md) for end-to-end workflows
3. **Deployment**: [Deployment & Infrastructure](./DEPLOYMENT_INFRASTRUCTURE_DETAILED.md) for environment management
4. **Monitoring**: [Document Intelligence Details](./DOCUMENT_INTELLIGENCE_DETAILED.md) for health checks and blob storage

### **For Integration Partners**
1. **API Contracts**: [Models & Data Contracts](./MODELS_DATA_CONTRACTS_DETAILED.md) for request/response formats
2. **PieceInfo API**: [PieceInfo API Details](./PIECEINFO_API_DETAILED.md) for external data aggregation
3. **Error Handling**: [Complete Process Flow](./COMPLETE_PROCESS_FLOW.md) for error scenarios
4. **Security**: [System Architecture](./SYSTEM_ARCHITECTURE.md) for authentication and authorization

## 📋 Key System Capabilities

### **Document Processing Capabilities**
- **AI-Powered Extraction**: Azure Document Intelligence with custom "serialnumber" model
- **Multiple Input Formats**: File upload, URL-based, and base64-encoded document processing
- **Confidence Evaluation**: Automatic quality assessment with configurable thresholds (default 0.7)
- **Continuous Improvement**: Low-confidence documents stored for model retraining

### **Storage & Data Management**
- **Organized Storage**: Date-based hierarchical blob storage structure
- **Metadata Tracking**: Comprehensive analysis metadata with correlation tracking
- **Retry Logic**: Resilient storage operations with exponential backoff
- **Container Management**: Automatic container creation and organization

### **Monitoring & Observability**  
- **Structured Logging**: ASCII-prefixed logs with correlation ID tracking
- **Business Events**: Key workflow events for operational analytics
- **Performance Metrics**: Request duration, confidence distribution, error rates
- **Health Monitoring**: Service dependency health checks and alerting

### **Integration & Scalability**
- **REST APIs**: OpenAPI/Swagger documented endpoints
- **External System Integration**: Secure HTTP client for multi-source data aggregation
- **Serverless Architecture**: Azure Functions with automatic scaling
- **Environment Management**: Configuration-driven multi-environment support

## 🔧 Configuration Reference

### **Environment Variables Summary**
```bash
# Azure Document Intelligence
DOCUMENT_INTELLIGENCE_ENDPOINT=https://your-service.cognitiveservices.azure.com/
DOCUMENT_INTELLIGENCE_KEY=your-api-key
DEFAULT_MODEL_ID=serialnumber

# Blob Storage Configuration  
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=...
BLOB_CONTAINER_PREFIX=warehouse-returns-doc-intel
ENABLE_BLOB_STORAGE=true

# Processing Configuration
CONFIDENCE_THRESHOLD=0.7
MAX_FILE_SIZE_MB=50
SUPPORTED_CONTENT_TYPES=application/pdf,image/jpeg,image/png,image/tiff

# Logging & Monitoring
LOG_LEVEL=INFO
ENABLE_STRUCTURED_LOGGING=true
APPINSIGHTS_INSTRUMENTATIONKEY=your-app-insights-key
```

### **API Endpoints Reference**
```bash
# Document Intelligence API (Port 7071)
POST /api/process-document          # Main document processing endpoint
GET  /api/health                    # Service health check
GET  /api/swagger                   # OpenAPI specification
GET  /api/docs                      # Swagger UI documentation

# PieceInfo API  
GET  /api/piece/{piece_number}      # Aggregate piece information
GET  /api/health                    # Service health check
GET  /api/swagger                   # OpenAPI specification
GET  /api/docs                      # Swagger UI documentation
```

## 📊 System Metrics & KPIs

### **Performance Benchmarks**
- **Average Processing Time**: 1-3 seconds per document
- **Confidence Distribution**: ~80% high confidence (≥0.7), ~20% requiring review
- **Storage Rate**: ~20% of documents stored for manual review
- **Success Rate**: >99% successful processing (excluding invalid inputs)

### **Operational Metrics**
- **Throughput**: Designed for 1000+ documents per hour
- **Availability**: >99.9% uptime with Azure Functions hosting
- **Error Rate**: <1% processing errors under normal conditions
- **Storage Growth**: ~100MB per 1000 processed documents (varies by document size)

## 🔍 Troubleshooting Quick Reference

### **Common Issues & Solutions**

**Document Processing Failures**:
1. Check Azure Document Intelligence service availability and API keys
2. Validate document format and file size limits
3. Review confidence threshold configuration
4. Examine correlation ID in logs for request tracking

**Blob Storage Issues**:
1. Verify Azure Storage connection string and container permissions
2. Check container naming configuration (`BLOB_CONTAINER_PREFIX`)
3. Validate storage account accessibility and quotas
4. Review retry logic and error handling in logs

**API Integration Problems**:
1. Confirm endpoint URLs and authentication configuration
2. Check external API connectivity and rate limiting
3. Validate SSL/TLS certificate configurations
4. Review correlation ID propagation across services

## 🚀 Getting Started Checklist

### **Development Environment Setup**
- [ ] Clone repository and install Python dependencies
- [ ] Configure Azure Document Intelligence service and obtain API keys
- [ ] Set up Azure Storage account for blob storage
- [ ] Create `local.settings.json` with required environment variables
- [ ] Start Azure Functions runtime (`func start`)
- [ ] Test health endpoints and basic document processing
- [ ] Verify blob storage functionality with low-confidence documents

### **Production Deployment**
- [ ] Run automated deployment script for Azure resources
- [ ] Configure Application Insights for monitoring and alerting
- [ ] Deploy function apps using Azure Functions Core Tools
- [ ] Validate model access and performance in production
- [ ] Set up monitoring dashboards and alert rules
- [ ] Perform end-to-end testing with production data
- [ ] Document operational procedures and runbooks

## 📞 Support & Resources

### **Development Resources**
- **Azure Functions Documentation**: https://docs.microsoft.com/azure/azure-functions/
- **Azure Document Intelligence**: https://docs.microsoft.com/azure/cognitive-services/form-recognizer/
- **Azure Blob Storage**: https://docs.microsoft.com/azure/storage/blobs/

### **Operational Support** 
- **Application Insights**: Monitor real-time performance and errors
- **Azure Portal**: Manage resources and view system health
- **Log Analytics**: Query structured logs and business events
- **Storage Explorer**: Browse and manage blob storage contents

This documentation suite provides complete coverage of the Warehouse Returns system, enabling developers, operators, and integration partners to understand, deploy, and maintain the solution effectively.