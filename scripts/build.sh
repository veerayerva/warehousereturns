#!/bin/bash
# ===================================================================
# Azure Functions Build Script for Linux/macOS
# ===================================================================
# This script provides comprehensive build and validation for the 
# PieceInfo API Azure Functions application with production-ready
# checks and error handling.
#
# Usage: ./build.sh [clean|test|deploy]
#   clean  - Clean previous build artifacts
#   test   - Run tests after build
#   deploy - Build and prepare for deployment
# ===================================================================

set -e  # Exit on any error
set -u  # Exit on undefined variable

# ===================================================================
# CONFIGURATION VARIABLES
# ===================================================================
PROJECT_NAME="PieceInfo API"
PYTHON_VERSION="3.11"
VENV_PATH=".venv"
SRC_PATH="src/pieceinfo_api"
TESTS_PATH="tests"
LOG_FILE="build.log"

# Parse command line arguments
BUILD_MODE="${1:-default}"

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging function
log() {
    echo -e "${BLUE}$(date '+%Y-%m-%d %H:%M:%S')${NC} - $1" | tee -a "$LOG_FILE"
}

error() {
    echo -e "${RED}❌ ERROR: $1${NC}" | tee -a "$LOG_FILE"
    exit 1
}

warning() {
    echo -e "${YELLOW}⚠️  WARNING: $1${NC}" | tee -a "$LOG_FILE"
}

success() {
    echo -e "${GREEN}✅ $1${NC}" | tee -a "$LOG_FILE"
}

# ===================================================================
# MAIN BUILD PROCESS
# ===================================================================
echo "===================================================================" | tee "$LOG_FILE"
log "$PROJECT_NAME - Build Script"
log "Mode: $BUILD_MODE"
log "Host: $(uname -s) $(uname -m)"
echo "===================================================================" | tee -a "$LOG_FILE"

# ===================================================================
# STEP 1: ENVIRONMENT VALIDATION
# ===================================================================
log "[1/7] Validating build environment..."

# Check if Python is installed
if ! command -v python3 &> /dev/null; then
    error "Python 3 is not installed or not in PATH"
fi

# Check Python version
CURRENT_PYTHON=$(python3 --version 2>&1 | cut -d' ' -f2)
success "Python version: $CURRENT_PYTHON"

# Check if virtual environment exists
if [ ! -f "$VENV_PATH/bin/activate" ]; then
    error "Virtual environment not found at $VENV_PATH. Please run: python3 -m venv $VENV_PATH"
fi

success "Virtual environment found"

# ===================================================================
# STEP 2: ACTIVATE VIRTUAL ENVIRONMENT
# ===================================================================
log "[2/7] Activating virtual environment..."

source "$VENV_PATH/bin/activate"
success "Virtual environment activated"

# ===================================================================
# STEP 3: CLEAN BUILD (IF REQUESTED)
# ===================================================================
if [ "$BUILD_MODE" == "clean" ]; then
    log "[3/7] Cleaning build artifacts..."
    
    find . -type d -name "__pycache__" -exec rm -rf {} + 2>/dev/null || true
    find . -type f -name "*.pyc" -delete 2>/dev/null || true
    rm -rf .pytest_cache build dist *.egg-info 2>/dev/null || true
    
    success "Build artifacts cleaned"
else
    log "[3/7] Skipping clean (use 'clean' argument to clean)"
fi

# ===================================================================
# STEP 4: INSTALL/UPDATE DEPENDENCIES
# ===================================================================
log "[4/7] Installing/updating dependencies..."

pip install --upgrade pip || error "Failed to upgrade pip"
pip install -r requirements.txt || error "Failed to install requirements"

success "Dependencies installed successfully"

# ===================================================================
# STEP 5: CODE VALIDATION
# ===================================================================
log "[5/7] Validating code quality..."

# Check if source directory exists
if [ ! -d "$SRC_PATH" ]; then
    error "Source directory not found: $SRC_PATH"
fi

# Run syntax check
log "  - Checking Python syntax..."
python3 -m py_compile "$SRC_PATH/function_app.py" || error "Syntax errors found in function_app.py"

# Run imports check
log "  - Validating imports..."
PYTHONPATH="$SRC_PATH" python3 -c "import function_app" || error "Import validation failed"

# Check code style (if flake8 is available)
if command -v flake8 &> /dev/null; then
    log "  - Checking code style..."
    flake8 "$SRC_PATH" --max-line-length=120 --ignore=E203,W503 || warning "Code style issues found"
fi

success "Code validation passed"

# ===================================================================
# STEP 6: RUN TESTS (IF REQUESTED OR IN TEST MODE)
# ===================================================================
if [ "$BUILD_MODE" == "test" ]; then
    log "[6/7] Running test suite..."
    
    if [ ! -d "$TESTS_PATH" ]; then
        warning "Tests directory not found: $TESTS_PATH"
        log "Tests will be skipped"
    else
        pytest "$TESTS_PATH" -v --tb=short || error "Tests failed"
        success "All tests passed"
    fi
else
    log "[6/7] Skipping tests (use 'test' argument to run tests)"
fi

# ===================================================================
# STEP 7: DEPLOYMENT PREPARATION (IF REQUESTED)
# ===================================================================
if [ "$BUILD_MODE" == "deploy" ]; then
    log "[7/7] Preparing for deployment..."
    
    # Check for required environment variables
    if [ -z "${OCP_APIM_SUBSCRIPTION_KEY:-}" ]; then
        warning "OCP_APIM_SUBSCRIPTION_KEY not set"
    fi
    
    # Validate Azure Functions configuration
    if [ ! -f "host.json" ]; then
        error "host.json not found"
    fi
    
    if [ ! -f "local.settings.json" ]; then
        warning "local.settings.json not found - create from .env.example"
    fi
    
    # Create deployment package info
    cat > deployment-info.json << EOF
{
    "project": "$PROJECT_NAME",
    "build_mode": "$BUILD_MODE",
    "build_time": "$(date -Iseconds)",
    "python_version": "$CURRENT_PYTHON",
    "host": "$(uname -s) $(uname -m)",
    "git_commit": "$(git rev-parse HEAD 2>/dev/null || echo 'unknown')",
    "git_branch": "$(git branch --show-current 2>/dev/null || echo 'unknown')"
}
EOF
    
    success "Deployment preparation completed"
else
    log "[7/7] Skipping deployment preparation (use 'deploy' argument)"
fi

# ===================================================================
# BUILD COMPLETION
# ===================================================================
echo ""
echo "===================================================================" | tee -a "$LOG_FILE"
success "BUILD COMPLETED SUCCESSFULLY"
log "Project: $PROJECT_NAME"
log "Mode: $BUILD_MODE"
log "Duration: Build completed at $(date '+%Y-%m-%d %H:%M:%S')"
echo "===================================================================" | tee -a "$LOG_FILE"

# Generate build summary
cat > build-summary.txt << EOF
Build Summary
=============
Project: $PROJECT_NAME
Mode: $BUILD_MODE
Time: $(date '+%Y-%m-%d %H:%M:%S')
Status: SUCCESS
Python Version: $CURRENT_PYTHON
Host: $(uname -s) $(uname -m)
EOF

echo ""
log "Next steps:"
if [ "$BUILD_MODE" == "deploy" ]; then
    log "1. Review local.settings.json configuration"
    log "2. Deploy using: func azure functionapp publish your-function-app-name"
else
    log "1. Run tests: ./build.sh test"
    log "2. Deploy: ./build.sh deploy"
fi

exit 0