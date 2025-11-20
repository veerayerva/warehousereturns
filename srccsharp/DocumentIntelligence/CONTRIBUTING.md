# Contributing to DocumentIntelligence API

Welcome to the DocumentIntelligence API project! We appreciate your interest in contributing. This guide will help you understand our development process, coding standards, and how to make meaningful contributions to the project.

## Table of Contents

1. [Code of Conduct](#code-of-conduct)
2. [Getting Started](#getting-started)
3. [Development Environment Setup](#development-environment-setup)
4. [Contributing Process](#contributing-process)
5. [Coding Standards](#coding-standards)
6. [Testing Requirements](#testing-requirements)
7. [Documentation Standards](#documentation-standards)
8. [Pull Request Guidelines](#pull-request-guidelines)
9. [Issue Reporting](#issue-reporting)
10. [Security Reporting](#security-reporting)

## Code of Conduct

This project adheres to a code of conduct that we expect all contributors to follow. Please read our [Code of Conduct](CODE_OF_CONDUCT.md) before contributing.

### Key Principles

- **Be Respectful**: Treat everyone with respect and kindness
- **Be Collaborative**: Work together constructively and help each other
- **Be Professional**: Maintain a professional attitude in all interactions
- **Be Inclusive**: Welcome contributions from people of all backgrounds and experience levels

## Getting Started

### Prerequisites

Before contributing, ensure you have:

- **.NET 8.0 SDK** or later
- **Visual Studio 2022** or **Visual Studio Code**
- **Azure Functions Core Tools** (v4.x)
- **Git** version control
- **Azure subscription** (for testing integration features)

### First-Time Contributors

If you're new to the project:

1. **Fork the repository** to your GitHub account
2. **Clone your fork** locally
3. **Set up the development environment** (see below)
4. **Look for "good first issue"** labels in the issue tracker
5. **Join our discussion channels** for questions and support

## Development Environment Setup

### 1. Clone the Repository

```bash
git clone https://github.com/your-org/warehouse-returns.git
cd warehouse-returns/srccsharp/DocumentIntelligence
```

### 2. Install Dependencies

```bash
# Restore NuGet packages
dotnet restore

# Install development tools
dotnet tool install -g dotnet-ef
dotnet tool install -g dotnet-reportgenerator-globaltool
```

### 3. Configure Local Settings

```bash
# Copy template configuration
cp local.settings.template.json local.settings.json

# Edit local.settings.json with your Azure service endpoints
```

### 4. Build and Test

```bash
# Build the solution
dotnet build

# Run tests
dotnet test

# Start local development server
func start --port 7072
```

### 5. Verify Setup

```bash
# Test health endpoint
curl http://localhost:7072/api/health

# Expected response: {"status":"healthy"}
```

## Contributing Process

### 1. Issue-First Development

- **Check existing issues** before starting work
- **Create an issue** for new features or significant changes
- **Discuss the approach** with maintainers before implementation
- **Get approval** for large changes or new features

### 2. Branch Strategy

We use **GitFlow** branching model:

```
main          ←── Production releases
├── develop   ←── Integration branch
    ├── feature/ISSUE-123-add-new-model-support
    ├── feature/ISSUE-124-improve-error-handling
    └── hotfix/ISSUE-125-fix-security-vulnerability
```

#### Branch Naming Conventions

- **Features**: `feature/ISSUE-###-short-description`
- **Bug fixes**: `bugfix/ISSUE-###-short-description`
- **Hotfixes**: `hotfix/ISSUE-###-short-description`
- **Documentation**: `docs/ISSUE-###-short-description`

### 3. Development Workflow

```bash
# 1. Create and switch to feature branch
git checkout develop
git pull origin develop
git checkout -b feature/ISSUE-123-add-new-model-support

# 2. Make changes and commit
git add .
git commit -m "feat: add support for custom document models

- Implement custom model configuration
- Add model validation logic
- Update API documentation
- Add comprehensive tests

Fixes #123"

# 3. Push and create pull request
git push origin feature/ISSUE-123-add-new-model-support
# Create PR via GitHub interface
```

### 4. Commit Message Standards

We follow [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

#### Commit Types

- **feat**: New feature
- **fix**: Bug fix
- **docs**: Documentation changes
- **style**: Code style changes (formatting, etc.)
- **refactor**: Code refactoring
- **test**: Adding or modifying tests
- **chore**: Maintenance tasks

#### Examples

```bash
feat(api): add support for custom document models

fix(validation): handle edge case in file upload validation

docs(readme): update build instructions

test(integration): add tests for error handling scenarios

chore(deps): update Azure.AI.DocumentIntelligence to v1.0.0
```

## Coding Standards

### C# Coding Conventions

#### 1. Naming Conventions

```csharp
// Classes and methods: PascalCase
public class DocumentIntelligenceService
{
    public async Task<AnalysisResult> AnalyzeDocumentAsync(DocumentRequest request)
    {
        // Implementation
    }
}

// Private fields: _camelCase with underscore prefix
private readonly ILogger<DocumentIntelligenceService> _logger;
private readonly HttpClient _httpClient;

// Local variables and parameters: camelCase
public void ProcessDocument(string documentUrl, CancellationToken cancellationToken)
{
    var analysisResult = await AnalyzeAsync(documentUrl, cancellationToken);
}

// Constants: UPPER_CASE
private const int MAX_FILE_SIZE_BYTES = 50 * 1024 * 1024;
private const string DEFAULT_MODEL_ID = "prebuilt-document";
```

#### 2. Code Documentation

All public APIs must have comprehensive XML documentation:

```csharp
/// <summary>
/// Analyzes a document using Azure Document Intelligence service and extracts structured information.
/// </summary>
/// <param name="request">The document analysis request containing document URL and processing options.</param>
/// <param name="cancellationToken">Cancellation token to cancel the operation if needed.</param>
/// <returns>
/// A task that represents the asynchronous analysis operation. The task result contains the 
/// <see cref="DocumentAnalysisResponse"/> with extracted text, key-value pairs, tables, and entities.
/// </returns>
/// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is null.</exception>
/// <exception cref="ArgumentException">Thrown when the document URL is invalid or unsupported.</exception>
/// <exception cref="DocumentAnalysisException">Thrown when the document analysis fails.</exception>
/// <exception cref="HttpRequestException">Thrown when there's a network error communicating with Azure services.</exception>
/// <example>
/// <code>
/// var request = new DocumentAnalysisRequest
/// {
///     DocumentUrl = "https://example.com/document.pdf",
///     ModelId = "prebuilt-document",
///     Features = new[] { "keyValuePairs", "tables" }
/// };
/// 
/// var result = await service.AnalyzeDocumentAsync(request, cancellationToken);
/// Console.WriteLine($"Extracted {result.Results.KeyValuePairs.Count} key-value pairs");
/// </code>
/// </example>
public async Task<DocumentAnalysisResponse> AnalyzeDocumentAsync(
    DocumentAnalysisRequest request, 
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

#### 3. Error Handling

```csharp
// Use specific exception types
public async Task<Document> GetDocumentAsync(string documentId)
{
    if (string.IsNullOrWhiteSpace(documentId))
        throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

    try
    {
        var document = await _repository.GetAsync(documentId);
        if (document == null)
            throw new DocumentNotFoundException($"Document with ID '{documentId}' not found");

        return document;
    }
    catch (StorageException ex) when (ex.RequestInformation.HttpStatusCode == 404)
    {
        throw new DocumentNotFoundException($"Document with ID '{documentId}' not found", ex);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to retrieve document {DocumentId}", documentId);
        throw new DocumentRetrievalException($"Failed to retrieve document '{documentId}'", ex);
    }
}
```

#### 4. Dependency Injection

```csharp
// Use interfaces for all dependencies
public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly DocumentIntelligenceClient _client;
    private readonly IBlobStorageRepository _blobRepository;
    private readonly ILogger<DocumentIntelligenceService> _logger;
    private readonly IOptions<DocumentIntelligenceOptions> _options;

    public DocumentIntelligenceService(
        DocumentIntelligenceClient client,
        IBlobStorageRepository blobRepository,
        ILogger<DocumentIntelligenceService> logger,
        IOptions<DocumentIntelligenceOptions> options)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _blobRepository = blobRepository ?? throw new ArgumentNullException(nameof(blobRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }
}
```

### Code Quality Rules

#### 1. SOLID Principles

- **Single Responsibility**: Each class has one reason to change
- **Open/Closed**: Open for extension, closed for modification
- **Liskov Substitution**: Derived classes must be substitutable
- **Interface Segregation**: No client should depend on unused methods
- **Dependency Inversion**: Depend on abstractions, not concretions

#### 2. Performance Guidelines

```csharp
// Use ConfigureAwait(false) in library code
public async Task<string> GetDataAsync()
{
    var result = await _httpClient.GetStringAsync(url).ConfigureAwait(false);
    return result;
}

// Dispose resources properly
public async Task ProcessDocumentAsync(Stream documentStream)
{
    using var memoryStream = new MemoryStream();
    await documentStream.CopyToAsync(memoryStream).ConfigureAwait(false);
    
    // Process the stream
}

// Use efficient string operations
public string BuildConnectionString(Dictionary<string, string> parameters)
{
    var sb = new StringBuilder();
    foreach (var kvp in parameters)
    {
        sb.Append($"{kvp.Key}={kvp.Value};");
    }
    return sb.ToString();
}
```

## Testing Requirements

### Test Coverage Requirements

- **Minimum Coverage**: 80% for new code
- **Critical Paths**: 95% coverage for security and data processing code
- **Public APIs**: 100% coverage for all public methods

### Testing Strategy

#### 1. Unit Tests

```csharp
[TestFixture]
public class DocumentIntelligenceServiceTests
{
    private Mock<DocumentIntelligenceClient> _mockClient;
    private Mock<ILogger<DocumentIntelligenceService>> _mockLogger;
    private DocumentIntelligenceService _service;

    [SetUp]
    public void Setup()
    {
        _mockClient = new Mock<DocumentIntelligenceClient>();
        _mockLogger = new Mock<ILogger<DocumentIntelligenceService>>();
        _service = new DocumentIntelligenceService(_mockClient.Object, _mockLogger.Object);
    }

    [Test]
    public async Task AnalyzeDocumentAsync_ValidRequest_ReturnsSuccessfulResult()
    {
        // Arrange
        var request = new DocumentAnalysisRequest
        {
            DocumentUrl = "https://example.com/document.pdf",
            ModelId = "prebuilt-document"
        };

        var expectedResponse = new AnalyzeResult
        {
            ModelId = "prebuilt-document",
            Content = "Sample content"
        };

        _mockClient.Setup(c => c.AnalyzeDocumentAsync(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<AnalyzeDocumentOptions>()))
                  .ReturnsAsync(Response.FromValue(expectedResponse, Mock.Of<Response>()));

        // Act
        var result = await _service.AnalyzeDocumentAsync(request);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Status, Is.EqualTo("completed"));
        Assert.That(result.Results.Content, Is.EqualTo("Sample content"));
    }

    [Test]
    public void AnalyzeDocumentAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.ThrowsAsync<ArgumentNullException>(() => _service.AnalyzeDocumentAsync(null));
    }
}
```

#### 2. Integration Tests

```csharp
[TestFixture]
[Category("Integration")]
public class DocumentIntelligenceIntegrationTests
{
    private HttpClient _httpClient;
    private TestServer _testServer;

    [SetUp]
    public void Setup()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddDocumentIntelligenceServices(builder.Configuration);
        
        var app = builder.Build();
        app.ConfigureDocumentIntelligenceMiddleware();
        
        _testServer = new TestServer(app);
        _httpClient = _testServer.CreateClient();
    }

    [Test]
    public async Task AnalyzeDocument_EndToEnd_ReturnsValidResponse()
    {
        // Arrange
        var request = new
        {
            documentUrl = "https://example.com/test-document.pdf",
            modelId = "prebuilt-document",
            features = new[] { "keyValuePairs", "tables" }
        };

        // Act
        var response = await _httpClient.PostAsJsonAsync("/api/analyze-document", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<DocumentAnalysisResponse>(content);
        
        Assert.That(result.Status, Is.EqualTo("completed"));
        Assert.That(result.Results, Is.Not.Null);
    }
}
```

#### 3. Performance Tests

```csharp
[TestFixture]
[Category("Performance")]
public class PerformanceTests
{
    [Test]
    public async Task AnalyzeDocument_LargeDocument_CompletesWithinTimeLimit()
    {
        // Arrange
        var service = new DocumentIntelligenceService(/* dependencies */);
        var request = CreateLargeDocumentRequest();
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await service.AnalyzeDocumentAsync(request);

        // Assert
        stopwatch.Stop();
        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMinutes(5)), 
            "Document analysis took too long");
        Assert.That(result.Status, Is.EqualTo("completed"));
    }
}
```

### Test Organization

```
tests/
├── unit/
│   ├── Services/
│   │   ├── DocumentIntelligenceServiceTests.cs
│   │   └── DocumentProcessingServiceTests.cs
│   └── Models/
│       └── RequestValidationTests.cs
├── integration/
│   ├── API/
│   │   ├── DocumentAnalysisEndpointTests.cs
│   │   └── HealthCheckEndpointTests.cs
│   └── Infrastructure/
│       └── BlobStorageTests.cs
└── performance/
    ├── LoadTests.cs
    └── StressTests.cs
```

## Documentation Standards

### 1. Code Documentation

- **XML Documentation**: All public APIs
- **Inline Comments**: Complex algorithms and business logic
- **README Files**: Each major component
- **Architecture Decision Records (ADRs)**: Significant design decisions

### 2. API Documentation

```csharp
/// <summary>
/// Represents the response from a document analysis operation.
/// </summary>
/// <remarks>
/// This response contains the results of analyzing a document using Azure Document Intelligence.
/// The response includes extracted content, structured data like key-value pairs and tables,
/// and metadata about the analysis process.
/// 
/// <para>
/// The analysis results are organized by pages, with each page containing lines of text,
/// detected tables, and identified key-value pairs. The confidence scores indicate the
/// reliability of the extracted information.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// {
///   "documentId": "doc_12345",
///   "status": "completed",
///   "results": {
///     "content": "Full document text...",
///     "keyValuePairs": [
///       {
///         "key": { "content": "Name:", "confidence": 0.98 },
///         "value": { "content": "John Doe", "confidence": 0.95 }
///       }
///     ]
///   }
/// }
/// </code>
/// </example>
public class DocumentAnalysisResponse
{
    // Properties with documentation
}
```

### 3. README Requirements

Each component should have a README with:

- **Purpose and scope**
- **Installation instructions**
- **Configuration options**
- **Usage examples**
- **API reference links**
- **Troubleshooting guide**

## Pull Request Guidelines

### PR Checklist

Before submitting a pull request, ensure:

- [ ] **Code follows style guidelines** (run `dotnet format`)
- [ ] **All tests pass** (`dotnet test`)
- [ ] **Test coverage meets requirements** (minimum 80%)
- [ ] **Documentation is updated** (XML docs, README, API docs)
- [ ] **Security review completed** (if applicable)
- [ ] **Breaking changes are documented** (if applicable)
- [ ] **Commit messages follow conventions**
- [ ] **PR description is complete**

### PR Template

```markdown
## Description
Brief description of the changes and their purpose.

## Type of Change
- [ ] Bug fix (non-breaking change fixing an issue)
- [ ] New feature (non-breaking change adding functionality)
- [ ] Breaking change (fix or feature causing existing functionality to change)
- [ ] Documentation update
- [ ] Performance improvement
- [ ] Refactoring

## Related Issues
Fixes #123
Related to #456

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed
- [ ] Performance impact assessed

## Documentation
- [ ] Code comments updated
- [ ] XML documentation updated
- [ ] API documentation updated
- [ ] README updated
- [ ] Release notes updated (if applicable)

## Security Considerations
- [ ] No sensitive data exposed
- [ ] Input validation implemented
- [ ] Security tests added
- [ ] No new security vulnerabilities introduced

## Breaking Changes
List any breaking changes and migration path for users.

## Screenshots/Examples
Include relevant screenshots or code examples demonstrating the changes.
```

### Review Process

1. **Automated Checks**: CI/CD pipeline runs automatically
2. **Peer Review**: At least one team member reviews
3. **Maintainer Review**: Project maintainer provides final approval
4. **Merge**: Squash and merge to maintain clean history

## Issue Reporting

### Bug Reports

Use the bug report template:

```markdown
## Bug Description
Clear and concise description of the bug.

## Environment
- OS: [e.g., Windows 11, Ubuntu 20.04]
- .NET Version: [e.g., 8.0.1]
- Function Runtime: [e.g., 4.0.5]
- Package Version: [e.g., 1.2.3]

## Steps to Reproduce
1. Go to '...'
2. Click on '....'
3. Scroll down to '....'
4. See error

## Expected Behavior
What you expected to happen.

## Actual Behavior
What actually happened.

## Logs/Screenshots
Include relevant logs, error messages, or screenshots.

## Additional Context
Any other context about the problem.
```

### Feature Requests

Use the feature request template:

```markdown
## Feature Description
Clear and concise description of the requested feature.

## Problem Statement
What problem does this feature solve?

## Proposed Solution
Describe your preferred solution.

## Alternative Solutions
Describe alternatives you've considered.

## Use Cases
Describe how this feature would be used.

## Implementation Notes
Any technical considerations or constraints.
```

## Security Reporting

### Vulnerability Disclosure

**DO NOT** create public issues for security vulnerabilities. Instead:

1. **Email**: Send details to security@company.com
2. **Encryption**: Use our PGP key for sensitive information
3. **Response Time**: We aim to respond within 48 hours
4. **Disclosure**: We follow responsible disclosure practices

### Security Review Process

For contributions involving security:

1. **Security Impact Assessment**: Evaluate potential security implications
2. **Threat Modeling**: Consider attack vectors and mitigations
3. **Code Review**: Additional security-focused review
4. **Penetration Testing**: For significant security changes
5. **Documentation**: Update security documentation

## Development Tools and Setup

### Recommended Extensions (VS Code)

```json
{
  "recommendations": [
    "ms-dotnettools.csharp",
    "ms-azuretools.vscode-azurefunctions",
    "ms-vscode.vscode-json",
    "ms-vscode.powershell",
    "esbenp.prettier-vscode",
    "bradlc.vscode-tailwindcss",
    "ms-python.python",
    "github.copilot"
  ]
}
```

### EditorConfig

```ini
root = true

[*]
charset = utf-8
insert_final_newline = true
trim_trailing_whitespace = true

[*.{cs,csx,vb,vbx}]
indent_style = space
indent_size = 4

[*.{json,js,ts,tsx,html,css,scss,yml,yaml}]
indent_style = space
indent_size = 2

[*.md]
trim_trailing_whitespace = false
```

### Pre-commit Hooks

```bash
# Install pre-commit hooks
npm install -g husky lint-staged

# Configure package.json
{
  "husky": {
    "hooks": {
      "pre-commit": "lint-staged"
    }
  },
  "lint-staged": {
    "*.cs": ["dotnet format", "git add"],
    "*.{js,ts,json,md}": ["prettier --write", "git add"]
  }
}
```

## Getting Help

### Communication Channels

- **GitHub Discussions**: For questions and general discussion
- **Issues**: For bug reports and feature requests
- **Email**: For security issues and private matters
- **Slack**: #documentintelligence-dev (internal contributors)

### Office Hours

- **Weekly Sync**: Tuesdays 2:00 PM EST
- **Code Review**: Thursdays 10:00 AM EST
- **Open Forum**: First Friday of each month 3:00 PM EST

## Recognition

We value all contributions and recognize contributors through:

- **Contributors List**: Updated in README.md
- **Release Notes**: Acknowledgment in release notes
- **Hall of Fame**: Annual recognition for outstanding contributions

## License

By contributing to this project, you agree that your contributions will be licensed under the project's [MIT License](LICENSE).

---

Thank you for contributing to the DocumentIntelligence API project! Your contributions help make this project better for everyone.