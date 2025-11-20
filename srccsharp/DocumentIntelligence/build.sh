#!/bin/bash

# DocumentIntelligence API - Build and Test Automation Script
# This script provides a complete build, test, and deployment automation workflow
# Usage: ./build.sh [command] [options]
#
# Commands:
#   build       - Build the application (default)
#   test        - Run tests with coverage
#   clean       - Clean build artifacts
#   deploy      - Build and deploy to Azure
#   dev         - Start local development environment
#   ci          - Continuous Integration build (used by pipelines)
#   help        - Show this help message

set -e  # Exit on any error
set -o pipefail  # Exit on pipe failures

# Script configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$SCRIPT_DIR"
TEST_DIR="$SCRIPT_DIR/../../testcsharp/DocumentIntelligence"
SOLUTION_FILE="$SCRIPT_DIR/../../WarehouseReturns.sln"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}ℹ️  INFO${NC}: $1"
}

log_success() {
    echo -e "${GREEN}✅ SUCCESS${NC}: $1"
}

log_warning() {
    echo -e "${YELLOW}⚠️  WARNING${NC}: $1"
}

log_error() {
    echo -e "${RED}❌ ERROR${NC}: $1"
}

log_section() {
    echo
    echo -e "${BLUE}🔷 $1${NC}"
    echo "=================================================="
}

# Utility functions
check_prerequisites() {
    log_section "Checking Prerequisites"
    
    # Check .NET SDK
    if ! command -v dotnet &> /dev/null; then
        log_error ".NET SDK not found. Please install .NET 8.0 SDK"
        exit 1
    fi
    
    local dotnet_version=$(dotnet --version)
    log_info "Found .NET SDK version: $dotnet_version"
    
    # Check Azure Functions Core Tools
    if ! command -v func &> /dev/null; then
        log_warning "Azure Functions Core Tools not found. Install with: npm install -g azure-functions-core-tools@4"
    else
        local func_version=$(func --version)
        log_info "Found Azure Functions Core Tools version: $func_version"
    fi
    
    # Check project files exist
    if [[ ! -f "$PROJECT_DIR/DocumentIntelligence.csproj" ]]; then
        log_error "Project file not found: $PROJECT_DIR/DocumentIntelligence.csproj"
        exit 1
    fi
    
    if [[ ! -f "$TEST_DIR/DocumentIntelligence.Tests.csproj" ]]; then
        log_error "Test project file not found: $TEST_DIR/DocumentIntelligence.Tests.csproj"
        exit 1
    fi
    
    log_success "Prerequisites check completed"
}

clean_build() {
    log_section "Cleaning Build Artifacts"
    
    cd "$PROJECT_DIR"
    
    log_info "Cleaning main project..."
    dotnet clean --verbosity quiet
    
    log_info "Cleaning test project..."
    cd "$TEST_DIR"
    dotnet clean --verbosity quiet
    
    # Remove additional artifacts
    log_info "Removing additional build artifacts..."
    find "$PROJECT_DIR" -name "bin" -type d -exec rm -rf {} + 2>/dev/null || true
    find "$PROJECT_DIR" -name "obj" -type d -exec rm -rf {} + 2>/dev/null || true
    find "$TEST_DIR" -name "bin" -type d -exec rm -rf {} + 2>/dev/null || true
    find "$TEST_DIR" -name "obj" -type d -exec rm -rf {} + 2>/dev/null || true
    
    # Clean test results
    rm -rf "$TEST_DIR/TestResults" 2>/dev/null || true
    rm -rf "$PROJECT_DIR/TestResults" 2>/dev/null || true
    
    log_success "Clean completed"
}

restore_packages() {
    log_section "Restoring NuGet Packages"
    
    cd "$PROJECT_DIR"
    
    log_info "Restoring main project packages..."
    dotnet restore --verbosity quiet
    
    log_info "Restoring test project packages..."
    cd "$TEST_DIR"
    dotnet restore --verbosity quiet
    
    log_success "Package restoration completed"
}

build_application() {
    local configuration=${1:-Debug}
    
    log_section "Building Application ($configuration)"
    
    cd "$PROJECT_DIR"
    
    log_info "Building main project in $configuration mode..."
    dotnet build --configuration "$configuration" --no-restore --verbosity quiet
    
    log_info "Building test project in $configuration mode..."
    cd "$TEST_DIR"
    dotnet build --configuration "$configuration" --no-restore --verbosity quiet
    
    log_success "Build completed successfully"
}

run_tests() {
    local collect_coverage=${1:-false}
    local configuration=${2:-Debug}
    
    log_section "Running Tests"
    
    cd "$TEST_DIR"
    
    local test_args="--configuration $configuration --no-build --verbosity normal"
    
    if [[ "$collect_coverage" == "true" ]]; then
        log_info "Running tests with coverage collection..."
        test_args="$test_args --collect:\"XPlat Code Coverage\" --logger trx"
    else
        log_info "Running tests..."
    fi
    
    # Run tests
    dotnet test $test_args
    
    local test_exit_code=$?
    
    if [[ $test_exit_code -eq 0 ]]; then
        log_success "All tests passed"
        
        if [[ "$collect_coverage" == "true" ]]; then
            generate_coverage_report
        fi
    else
        log_error "Tests failed with exit code: $test_exit_code"
        exit $test_exit_code
    fi
}

generate_coverage_report() {
    log_section "Generating Coverage Report"
    
    cd "$TEST_DIR"
    
    # Check if reportgenerator tool is installed
    if ! dotnet tool list -g | grep -q reportgenerator; then
        log_info "Installing reportgenerator tool..."
        dotnet tool install -g dotnet-reportgenerator-globaltool --verbosity quiet
    fi
    
    # Find coverage files
    local coverage_files=$(find TestResults -name "coverage.cobertura.xml" | tr '\n' ';' | sed 's/;$//')
    
    if [[ -n "$coverage_files" ]]; then
        log_info "Generating HTML coverage report..."
        reportgenerator \
            -reports:"$coverage_files" \
            -targetdir:"TestResults/Coverage" \
            -reporttypes:Html \
            -verbosity:Warning
        
        log_success "Coverage report generated: TestResults/Coverage/index.html"
    else
        log_warning "No coverage files found"
    fi
}

security_scan() {
    log_section "Security Scan"
    
    cd "$PROJECT_DIR"
    
    log_info "Scanning for vulnerable packages..."
    dotnet list package --vulnerable --include-transitive
    
    local scan_exit_code=$?
    
    if [[ $scan_exit_code -eq 0 ]]; then
        log_success "No security vulnerabilities found"
    else
        log_warning "Security scan completed with warnings - review output above"
    fi
}

code_analysis() {
    log_section "Code Analysis"
    
    cd "$PROJECT_DIR"
    
    log_info "Running code analysis..."
    
    # Check code formatting
    log_info "Checking code formatting..."
    dotnet format --verify-no-changes --verbosity diagnostic
    
    local format_exit_code=$?
    
    if [[ $format_exit_code -eq 0 ]]; then
        log_success "Code formatting is correct"
    else
        log_warning "Code formatting issues detected. Run 'dotnet format' to fix."
    fi
}

publish_application() {
    local configuration=${1:-Release}
    local output_dir=${2:-"./publish"}
    
    log_section "Publishing Application"
    
    cd "$PROJECT_DIR"
    
    log_info "Publishing application in $configuration mode to $output_dir..."
    
    dotnet publish \
        --configuration "$configuration" \
        --output "$output_dir" \
        --no-restore \
        --verbosity quiet
    
    log_success "Application published to: $output_dir"
}

start_local_development() {
    log_section "Starting Local Development Environment"
    
    cd "$PROJECT_DIR"
    
    # Check if local.settings.json exists
    if [[ ! -f "local.settings.json" ]]; then
        if [[ -f "local.settings.template.json" ]]; then
            log_info "Creating local.settings.json from template..."
            cp "local.settings.template.json" "local.settings.json"
            log_warning "Please configure local.settings.json with your Azure service endpoints"
        else
            log_error "local.settings.json not found and no template available"
            exit 1
        fi
    fi
    
    log_info "Starting Azure Functions host..."
    func start --port 7072
}

deploy_to_azure() {
    local function_app_name=${1:-""}
    
    if [[ -z "$function_app_name" ]]; then
        log_error "Function app name required for deployment"
        log_info "Usage: ./build.sh deploy <function-app-name>"
        exit 1
    fi
    
    log_section "Deploying to Azure"
    
    # Build in release mode
    build_application "Release"
    
    # Run tests
    run_tests false "Release"
    
    # Security scan
    security_scan
    
    # Deploy
    cd "$PROJECT_DIR"
    log_info "Deploying to Azure Function App: $function_app_name"
    func azure functionapp publish "$function_app_name"
    
    log_success "Deployment completed"
}

continuous_integration_build() {
    log_section "Continuous Integration Build"
    
    # Full CI pipeline
    check_prerequisites
    clean_build
    restore_packages
    build_application "Release"
    code_analysis
    security_scan
    run_tests true "Release"
    publish_application "Release" "./ci-publish"
    
    log_success "CI build completed successfully"
}

show_help() {
    echo "DocumentIntelligence API - Build and Test Automation Script"
    echo
    echo "Usage: ./build.sh [command] [options]"
    echo
    echo "Commands:"
    echo "  build [Debug|Release]     - Build the application (default: Debug)"
    echo "  test [coverage]           - Run tests (add 'coverage' for coverage report)"
    echo "  clean                     - Clean build artifacts"
    echo "  deploy <app-name>         - Build and deploy to Azure Function App"
    echo "  dev                       - Start local development environment"
    echo "  ci                        - Full CI pipeline (clean, build, test, analyze)"
    echo "  publish [Release] [dir]   - Publish application for deployment"
    echo "  security                  - Run security vulnerability scan"
    echo "  format                    - Format code according to .editorconfig"
    echo "  help                      - Show this help message"
    echo
    echo "Examples:"
    echo "  ./build.sh                    # Build in Debug mode"
    echo "  ./build.sh build Release      # Build in Release mode"
    echo "  ./build.sh test coverage      # Run tests with coverage report"
    echo "  ./build.sh deploy my-func-app # Deploy to Azure"
    echo "  ./build.sh ci                 # Full CI pipeline"
    echo
    echo "Environment Variables:"
    echo "  BUILD_CONFIGURATION           - Default build configuration (Debug/Release)"
    echo "  SKIP_TESTS                    - Skip running tests (true/false)"
    echo "  AZURE_FUNCTION_APP_NAME       - Default Azure Function App name for deployment"
}

# Main script logic
main() {
    local command=${1:-build}
    shift || true
    
    case "$command" in
        "build")
            local config=${1:-${BUILD_CONFIGURATION:-Debug}}
            check_prerequisites
            clean_build
            restore_packages
            build_application "$config"
            ;;
        "test")
            local coverage=${1:-false}
            if [[ "$coverage" == "coverage" ]]; then
                coverage=true
            fi
            check_prerequisites
            run_tests "$coverage"
            ;;
        "clean")
            clean_build
            ;;
        "deploy")
            local app_name=${1:-$AZURE_FUNCTION_APP_NAME}
            check_prerequisites
            deploy_to_azure "$app_name"
            ;;
        "dev")
            check_prerequisites
            build_application "Debug"
            start_local_development
            ;;
        "ci")
            continuous_integration_build
            ;;
        "publish")
            local config=${1:-Release}
            local output=${2:-"./publish"}
            check_prerequisites
            restore_packages
            build_application "$config"
            publish_application "$config" "$output"
            ;;
        "security")
            check_prerequisites
            security_scan
            ;;
        "format")
            cd "$PROJECT_DIR"
            dotnet format
            log_success "Code formatting completed"
            ;;
        "help"|"-h"|"--help")
            show_help
            ;;
        *)
            log_error "Unknown command: $command"
            echo
            show_help
            exit 1
            ;;
    esac
}

# Execute main function with all arguments
main "$@"