# Warehouse Returns System - Architecture Documentation

## 🏗️ System Overview

The Warehouse Returns System is a production-ready Azure Functions application designed to process warehouse return documents and extract serial numbers using Azure Document Intelligence. The system follows a microservices architecture with clear separation of concerns and comprehensive logging.

## 📊 High-Level Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Client Apps   │    │   Azure Portal   │    │  External APIs  │
│   (Web/Mobile)  │    │   (Monitoring)   │    │ (PieceInfo API) │
└─────────┬───────┘    └─────────┬────────┘    └─────────┬───────┘
          │                      │                       │
          │                      │                       │
          ▼                      ▼                       ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Azure Functions Host                        │
├─────────────────┬─────────────────┬─────────────────────────────┤
│ Document Intel  │ Return Process  │     PieceInfo API           │
│ Function App    │ Function App    │     Function App            │
│ (Port 7071)     │                 │                             │
└─────────────────┴─────────────────┴─────────────────────────────┘
          │                      │                       │
          ▼                      ▼                       ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│ Azure Document  │    │  Azure Blob     │    │   Application   │
│ Intelligence    │    │   Storage       │    │    Insights     │
│ (AI Service)    │    │ (File Storage)  │    │  (Monitoring)   │
└─────────────────┘    └─────────────────┘    └─────────────────┘
```

## 🎯 Core Components

### 1. Document Intelligence Function App (`src/document_intelligence/`)
**Purpose**: Processes warehouse return documents and extracts serial numbers with confidence scoring

**Key Features**:
- Azure Document Intelligence API integration
- File upload and URL-based document processing  
- Confidence threshold evaluation (default 0.7)
- Automatic blob storage for low-confidence documents
- Comprehensive error handling and health monitoring

### 2. Return Processing Function App (`src/return_processing/`)  
**Purpose**: Manages warehouse return workflows and business processes

**Key Features**:
- Return request creation and validation
- Status tracking and audit logging
- Queue-based processing
- Business event logging
- Integration with external systems

### 3. PieceInfo API Function App (`src/pieceinfo_api/`)
**Purpose**: Aggregates warehouse piece information from multiple data sources

**Key Features**:
- REST API with Swagger documentation
- Multi-source data aggregation
- SSL/HTTPS support for external APIs
- Comprehensive error handling
- Health check endpoints

### 4. Shared Components (`src/shared/`)
**Purpose**: Common utilities and configurations used across all function apps

**Components**:
- Centralized logging configuration
- HTTP request/response middleware
- Common exception types
- Utility functions

## 🔄 Data Flow

### Document Processing Workflow
1. **Input**: Client uploads document or provides URL
2. **Processing**: Azure Document Intelligence analyzes document
3. **Evaluation**: System evaluates confidence scores against threshold
4. **Routing**: 
   - High confidence (≥0.7): Return results immediately
   - Low confidence (<0.7): Store in blob storage for manual review
5. **Response**: Return analysis results with storage information

### Blob Storage Structure
```
warehouse-returns-doc-intel/
└── low-confidence/
    └── pending-review/
        └── {date}/
            └── {analysis_id}/
                ├── document.{ext}     # Original document
                └── metadata.json      # Analysis metadata
```

## 🛡️ Security & Configuration

### Environment Variables
- **Azure Services**: Connection strings, endpoints, keys
- **Processing**: Confidence thresholds, file size limits
- **Logging**: Application Insights, log levels
- **Security**: CORS settings, authentication levels

### Authentication
- Azure Functions use Function-level authentication
- External API calls secured with managed identities where possible
- Development environment uses local settings

## 📈 Monitoring & Logging

### Structured Logging
- **ASCII Prefixes**: All logs use descriptive prefixes (e.g., `[HTTP-REQUEST]`, `[BLOB-STORAGE-INIT]`)
- **Correlation IDs**: Request tracking across all components
- **Business Events**: Key workflow events logged for analytics
- **Error Handling**: Comprehensive error categorization and reporting

### Health Monitoring
- Health check endpoints for all services
- Azure Application Insights integration
- Dependency health validation
- Performance metrics collection

## 🚀 Deployment Architecture

### Infrastructure Components
- **Resource Group**: Contains all related Azure resources
- **Storage Account**: Functions runtime and blob storage
- **Application Insights**: Centralized monitoring and logging
- **Document Intelligence Service**: AI-powered document analysis
- **Function Apps**: Serverless compute for business logic

### Environment Management
- **Development**: Local Functions runtime with emulated services
- **Staging**: Azure-hosted with separate resource group
- **Production**: Fully managed Azure services with monitoring

## 🔧 Extensibility Points

### Adding New Document Types
1. Create new model in Azure Document Intelligence Studio
2. Update model configuration in environment variables
3. Add document type-specific field extraction logic
4. Update validation rules and confidence thresholds

### Custom Processing Workflows  
1. Extend processing service with new analysis methods
2. Add custom field extractors in models package
3. Implement specific storage patterns in repositories
4. Update API contracts and documentation

### Integration Points
1. **External APIs**: Via HTTP client service with retry logic
2. **Storage Systems**: Through repository pattern abstraction
3. **Message Queues**: Azure Service Bus or Storage Queues
4. **Databases**: Azure SQL, Cosmos DB via connection string configuration

This architecture provides a scalable, maintainable foundation for warehouse return document processing with clear separation of concerns and comprehensive operational support.