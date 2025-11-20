# DocumentIntelligence API - Build and Test Automation Script (PowerShell)
# This script provides a complete build, test, and deployment automation workflow
# Usage: .\build.ps1 [command] [options]
#
# Commands:
#   build       - Build the application (default)
#   test        - Run tests with coverage
#   clean       - Clean build artifacts
#   deploy      - Build and deploy to Azure
#   dev         - Start local development environment
#   ci          - Continuous Integration build (used by pipelines)
#   help        - Show this help message

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("build", "test", "clean", "deploy", "dev", "ci", "publish", "security", "format", "help")]
    [string]$Command = "build",
    
    [Parameter(Position = 1)]
    [string]$Configuration = "Debug",
    
    [Parameter(Position = 2)]
    [string]$OutputPath = "./publish",
    
    [switch]$Coverage,
    [switch]$SkipTests,
    [string]$FunctionAppName,
    [switch]$Verbose
)

# Script configuration
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = $ScriptDir
$TestDir = Join-Path (Split-Path (Split-Path $ScriptDir -Parent) -Parent) "testcsharp\DocumentIntelligence"
$SolutionFile = Join-Path (Split-Path (Split-Path $ScriptDir -Parent) -Parent) "WarehouseReturns.sln"

# Global error handling
$ErrorActionPreference = "Stop"
$VerbosePreference = if ($Verbose) { "Continue" } else { "SilentlyContinue" }

# Logging functions with colors and emojis
function Write-Info {
    param([string]$Message)
    Write-Host "ℹ️  INFO: $Message" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "✅ SUCCESS: $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠️  WARNING: $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "❌ ERROR: $Message" -ForegroundColor Red
}

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "🔷 $Title" -ForegroundColor Blue
    Write-Host ("=" * 50) -ForegroundColor Blue
}

# Utility functions
function Test-Prerequisites {
    Write-Section "Checking Prerequisites"
    
    # Check .NET SDK
    try {
        $dotnetVersion = & dotnet --version
        Write-Info "Found .NET SDK version: $dotnetVersion"
    }
    catch {
        Write-Error ".NET SDK not found. Please install .NET 8.0 SDK"
        exit 1
    }
    
    # Check Azure Functions Core Tools
    try {
        $funcVersion = & func --version
        Write-Info "Found Azure Functions Core Tools version: $funcVersion"
    }
    catch {
        Write-Warning "Azure Functions Core Tools not found. Install with: npm install -g azure-functions-core-tools@4"
    }
    
    # Check project files exist
    $projectFile = Join-Path $ProjectDir "DocumentIntelligence.csproj"
    if (-not (Test-Path $projectFile)) {
        Write-Error "Project file not found: $projectFile"
        exit 1
    }
    
    $testProjectFile = Join-Path $TestDir "DocumentIntelligence.Tests.csproj"
    if (-not (Test-Path $testProjectFile)) {
        Write-Error "Test project file not found: $testProjectFile"
        exit 1
    }
    
    Write-Success "Prerequisites check completed"
}

function Invoke-CleanBuild {
    Write-Section "Cleaning Build Artifacts"
    
    Push-Location $ProjectDir
    try {
        Write-Info "Cleaning main project..."
        & dotnet clean --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Clean failed" }
        
        Write-Info "Cleaning test project..."
        Push-Location $TestDir
        try {
            & dotnet clean --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Test clean failed" }
        }
        finally {
            Pop-Location
        }
        
        # Remove additional artifacts
        Write-Info "Removing additional build artifacts..."
        Get-ChildItem -Path $ProjectDir -Recurse -Name "bin" -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Get-ChildItem -Path $ProjectDir -Recurse -Name "obj" -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Get-ChildItem -Path $TestDir -Recurse -Name "bin" -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        Get-ChildItem -Path $TestDir -Recurse -Name "obj" -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
        
        # Clean test results
        $testResultsPaths = @(
            (Join-Path $TestDir "TestResults"),
            (Join-Path $ProjectDir "TestResults")
        )
        
        foreach ($path in $testResultsPaths) {
            if (Test-Path $path) {
                Remove-Item $path -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
        
        Write-Success "Clean completed"
    }
    finally {
        Pop-Location
    }
}

function Invoke-RestorePackages {
    Write-Section "Restoring NuGet Packages"
    
    Push-Location $ProjectDir
    try {
        Write-Info "Restoring main project packages..."
        & dotnet restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }
        
        Write-Info "Restoring test project packages..."
        Push-Location $TestDir
        try {
            & dotnet restore --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Test package restore failed" }
        }
        finally {
            Pop-Location
        }
        
        Write-Success "Package restoration completed"
    }
    finally {
        Pop-Location
    }
}

function Invoke-BuildApplication {
    param(
        [string]$BuildConfiguration = "Debug"
    )
    
    Write-Section "Building Application ($BuildConfiguration)"
    
    Push-Location $ProjectDir
    try {
        Write-Info "Building main project in $BuildConfiguration mode..."
        & dotnet build --configuration $BuildConfiguration --no-restore --verbosity quiet
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        
        Write-Info "Building test project in $BuildConfiguration mode..."
        Push-Location $TestDir
        try {
            & dotnet build --configuration $BuildConfiguration --no-restore --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Test build failed" }
        }
        finally {
            Pop-Location
        }
        
        Write-Success "Build completed successfully"
    }
    finally {
        Pop-Location
    }
}

function Invoke-RunTests {
    param(
        [bool]$CollectCoverage = $false,
        [string]$TestConfiguration = "Debug"
    )
    
    Write-Section "Running Tests"
    
    Push-Location $TestDir
    try {
        $testArgs = @(
            "test",
            "--configuration", $TestConfiguration,
            "--no-build",
            "--verbosity", "normal"
        )
        
        if ($CollectCoverage) {
            Write-Info "Running tests with coverage collection..."
            $testArgs += @("--collect:XPlat Code Coverage", "--logger", "trx")
        }
        else {
            Write-Info "Running tests..."
        }
        
        # Run tests
        & dotnet @testArgs
        $testExitCode = $LASTEXITCODE
        
        if ($testExitCode -eq 0) {
            Write-Success "All tests passed"
            
            if ($CollectCoverage) {
                Invoke-GenerateCoverageReport
            }
        }
        else {
            Write-Error "Tests failed with exit code: $testExitCode"
            exit $testExitCode
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-GenerateCoverageReport {
    Write-Section "Generating Coverage Report"
    
    Push-Location $TestDir
    try {
        # Check if reportgenerator tool is installed
        $toolList = & dotnet tool list -g
        if ($toolList -notmatch "reportgenerator") {
            Write-Info "Installing reportgenerator tool..."
            & dotnet tool install -g dotnet-reportgenerator-globaltool --verbosity quiet
            if ($LASTEXITCODE -ne 0) { throw "Failed to install reportgenerator" }
        }
        
        # Find coverage files
        $coverageFiles = Get-ChildItem -Path "TestResults" -Recurse -Name "coverage.cobertura.xml" -ErrorAction SilentlyContinue
        
        if ($coverageFiles) {
            $coverageReports = ($coverageFiles | ForEach-Object { Join-Path "TestResults" $_ }) -join ";"
            
            Write-Info "Generating HTML coverage report..."
            & reportgenerator `
                -reports:$coverageReports `
                -targetdir:"TestResults\Coverage" `
                -reporttypes:Html `
                -verbosity:Warning
            
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Coverage report generated: TestResults\Coverage\index.html"
            }
        }
        else {
            Write-Warning "No coverage files found"
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-SecurityScan {
    Write-Section "Security Scan"
    
    Push-Location $ProjectDir
    try {
        Write-Info "Scanning for vulnerable packages..."
        & dotnet list package --vulnerable --include-transitive
        
        $scanExitCode = $LASTEXITCODE
        
        if ($scanExitCode -eq 0) {
            Write-Success "No security vulnerabilities found"
        }
        else {
            Write-Warning "Security scan completed with warnings - review output above"
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-CodeAnalysis {
    Write-Section "Code Analysis"
    
    Push-Location $ProjectDir
    try {
        Write-Info "Running code analysis..."
        
        # Check code formatting
        Write-Info "Checking code formatting..."
        & dotnet format --verify-no-changes --verbosity diagnostic
        
        $formatExitCode = $LASTEXITCODE
        
        if ($formatExitCode -eq 0) {
            Write-Success "Code formatting is correct"
        }
        else {
            Write-Warning "Code formatting issues detected. Run 'dotnet format' to fix."
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-PublishApplication {
    param(
        [string]$PublishConfiguration = "Release",
        [string]$PublishOutputDir = "./publish"
    )
    
    Write-Section "Publishing Application"
    
    Push-Location $ProjectDir
    try {
        Write-Info "Publishing application in $PublishConfiguration mode to $PublishOutputDir..."
        
        & dotnet publish `
            --configuration $PublishConfiguration `
            --output $PublishOutputDir `
            --no-restore `
            --verbosity quiet
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Application published to: $PublishOutputDir"
        }
        else {
            throw "Publish failed"
        }
    }
    finally {
        Pop-Location
    }
}

function Start-LocalDevelopment {
    Write-Section "Starting Local Development Environment"
    
    Push-Location $ProjectDir
    try {
        # Check if local.settings.json exists
        $localSettingsPath = Join-Path $ProjectDir "local.settings.json"
        $templatePath = Join-Path $ProjectDir "local.settings.template.json"
        
        if (-not (Test-Path $localSettingsPath)) {
            if (Test-Path $templatePath) {
                Write-Info "Creating local.settings.json from template..."
                Copy-Item $templatePath $localSettingsPath
                Write-Warning "Please configure local.settings.json with your Azure service endpoints"
            }
            else {
                Write-Error "local.settings.json not found and no template available"
                exit 1
            }
        }
        
        Write-Info "Starting Azure Functions host..."
        & func start --port 7072
    }
    finally {
        Pop-Location
    }
}

function Invoke-DeployToAzure {
    param(
        [string]$AppName
    )
    
    if (-not $AppName) {
        Write-Error "Function app name required for deployment"
        Write-Info "Usage: .\build.ps1 deploy -FunctionAppName <function-app-name>"
        exit 1
    }
    
    Write-Section "Deploying to Azure"
    
    # Build in release mode
    Invoke-BuildApplication "Release"
    
    # Run tests
    if (-not $SkipTests) {
        Invoke-RunTests $false "Release"
    }
    
    # Security scan
    Invoke-SecurityScan
    
    # Deploy
    Push-Location $ProjectDir
    try {
        Write-Info "Deploying to Azure Function App: $AppName"
        & func azure functionapp publish $AppName
        
        if ($LASTEXITCODE -eq 0) {
            Write-Success "Deployment completed"
        }
        else {
            throw "Deployment failed"
        }
    }
    finally {
        Pop-Location
    }
}

function Invoke-ContinuousIntegration {
    Write-Section "Continuous Integration Build"
    
    # Full CI pipeline
    Test-Prerequisites
    Invoke-CleanBuild
    Invoke-RestorePackages
    Invoke-BuildApplication "Release"
    Invoke-CodeAnalysis
    Invoke-SecurityScan
    
    if (-not $SkipTests) {
        Invoke-RunTests $true "Release"
    }
    
    Invoke-PublishApplication "Release" "./ci-publish"
    
    Write-Success "CI build completed successfully"
}

function Show-Help {
    @"
DocumentIntelligence API - Build and Test Automation Script (PowerShell)

Usage: .\build.ps1 [command] [options]

Commands:
  build                         - Build the application (default)
  test                          - Run tests
  clean                         - Clean build artifacts
  deploy                        - Build and deploy to Azure Function App
  dev                           - Start local development environment
  ci                            - Full CI pipeline (clean, build, test, analyze)
  publish                       - Publish application for deployment
  security                      - Run security vulnerability scan
  format                        - Format code according to .editorconfig
  help                          - Show this help message

Parameters:
  -Configuration <Debug|Release> - Build configuration (default: Debug)
  -Coverage                      - Collect code coverage when running tests
  -SkipTests                     - Skip running tests
  -FunctionAppName <name>        - Azure Function App name for deployment
  -OutputPath <path>             - Output path for publish command
  -Verbose                       - Enable verbose output

Examples:
  .\build.ps1                                    # Build in Debug mode
  .\build.ps1 build -Configuration Release       # Build in Release mode
  .\build.ps1 test -Coverage                     # Run tests with coverage report
  .\build.ps1 deploy -FunctionAppName my-func-app # Deploy to Azure
  .\build.ps1 ci -SkipTests                      # Full CI pipeline without tests

Environment Variables:
  BUILD_CONFIGURATION                            - Default build configuration
  AZURE_FUNCTION_APP_NAME                        - Default Azure Function App name
"@
}

# Main script logic
function Invoke-Main {
    try {
        switch ($Command) {
            "build" {
                Test-Prerequisites
                Invoke-CleanBuild
                Invoke-RestorePackages
                Invoke-BuildApplication $Configuration
            }
            "test" {
                Test-Prerequisites
                Invoke-RunTests $Coverage.IsPresent $Configuration
            }
            "clean" {
                Invoke-CleanBuild
            }
            "deploy" {
                $appName = $FunctionAppName ?? $env:AZURE_FUNCTION_APP_NAME
                Test-Prerequisites
                Invoke-DeployToAzure $appName
            }
            "dev" {
                Test-Prerequisites
                Invoke-BuildApplication "Debug"
                Start-LocalDevelopment
            }
            "ci" {
                Invoke-ContinuousIntegration
            }
            "publish" {
                Test-Prerequisites
                Invoke-RestorePackages
                Invoke-BuildApplication $Configuration
                Invoke-PublishApplication $Configuration $OutputPath
            }
            "security" {
                Test-Prerequisites
                Invoke-SecurityScan
            }
            "format" {
                Push-Location $ProjectDir
                try {
                    & dotnet format
                    if ($LASTEXITCODE -eq 0) {
                        Write-Success "Code formatting completed"
                    }
                }
                finally {
                    Pop-Location
                }
            }
            "help" {
                Show-Help
            }
            default {
                Write-Error "Unknown command: $Command"
                Write-Host ""
                Show-Help
                exit 1
            }
        }
    }
    catch {
        Write-Error "Script failed: $($_.Exception.Message)"
        exit 1
    }
}

# Execute main function
Invoke-Main