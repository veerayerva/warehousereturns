# Build and Test Script for PieceInfo API

Write-Host "🔧 Building and Testing PieceInfo API..." -ForegroundColor Green

# Set error action preference
$ErrorActionPreference = "Stop"

try {
    # Navigate to test directory
    Write-Host "📂 Navigating to test directory..." -ForegroundColor Yellow
    Set-Location $PSScriptRoot

    # Clean previous build artifacts
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path "bin") { Remove-Item -Recurse -Force "bin" }
    if (Test-Path "obj") { Remove-Item -Recurse -Force "obj" }

    # Restore NuGet packages
    Write-Host "📦 Restoring NuGet packages..." -ForegroundColor Yellow
    dotnet restore --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        throw "Package restore failed with exit code $LASTEXITCODE"
    }

    # Build the test project
    Write-Host "🔨 Building test project..." -ForegroundColor Yellow
    dotnet build --no-restore --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    # Run the tests
    Write-Host "🧪 Running tests..." -ForegroundColor Yellow
    dotnet test --no-build --verbosity normal --logger "console;verbosity=detailed"

    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed with exit code $LASTEXITCODE"
    }

    # Generate coverage report (optional)
    Write-Host "📊 Generating coverage report..." -ForegroundColor Yellow
    dotnet test --no-build --collect:"XPlat Code Coverage" --verbosity quiet

    Write-Host "✅ All tests completed successfully!" -ForegroundColor Green

} catch {
    Write-Host "❌ Error occurred: $_" -ForegroundColor Red
    exit 1
}

Write-Host "" -ForegroundColor White
Write-Host "🎉 Build and test process completed!" -ForegroundColor Green
Write-Host "📄 Check the output above for test results and coverage information." -ForegroundColor Cyan