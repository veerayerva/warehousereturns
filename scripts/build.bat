@echo off
REM ===================================================================
REM Azure Functions Build Script for Windows
REM ===================================================================
REM This script provides comprehensive build and validation for the 
REM PieceInfo API Azure Functions application with production-ready
REM checks and error handling.
REM
REM Usage: build.bat [clean|test|deploy]
REM   clean  - Clean previous build artifacts
REM   test   - Run tests after build
REM   deploy - Build and prepare for deployment
REM ===================================================================

setlocal enabledelayedexpansion

REM ===================================================================
REM CONFIGURATION VARIABLES
REM ===================================================================
set "PROJECT_NAME=PieceInfo API"
set "PYTHON_VERSION=3.11"
set "VENV_PATH=.venv"
set "SRC_PATH=src\pieceinfo_api"
set "TESTS_PATH=tests"
set "LOG_FILE=build.log"

REM Parse command line arguments
set "BUILD_MODE=%1"
if "%BUILD_MODE%"=="" set "BUILD_MODE=default"

echo ===================================================================
echo %PROJECT_NAME% - Build Script
echo Mode: %BUILD_MODE%
echo Time: %DATE% %TIME%
echo ===================================================================

REM ===================================================================
REM STEP 1: ENVIRONMENT VALIDATION
REM ===================================================================
echo [1/7] Validating build environment...

REM Check if Python is installed
python --version >nul 2>&1
if errorlevel 1 (
    echo ❌ ERROR: Python is not installed or not in PATH
    echo Please install Python %PYTHON_VERSION% or later
    exit /b 1
)

REM Check Python version
for /f "tokens=2" %%i in ('python --version 2^>^&1') do set CURRENT_PYTHON=%%i
echo ✅ Python version: %CURRENT_PYTHON%

REM Check if virtual environment exists
if not exist "%VENV_PATH%\Scripts\activate.bat" (
    echo ❌ ERROR: Virtual environment not found at %VENV_PATH%
    echo Please run: python -m venv %VENV_PATH%
    exit /b 1
)

echo ✅ Virtual environment found

REM ===================================================================
REM STEP 2: ACTIVATE VIRTUAL ENVIRONMENT
REM ===================================================================
echo [2/7] Activating virtual environment...

call "%VENV_PATH%\Scripts\activate.bat"
if errorlevel 1 (
    echo ❌ ERROR: Failed to activate virtual environment
    exit /b 1
)

echo ✅ Virtual environment activated

REM ===================================================================
REM STEP 3: CLEAN BUILD (IF REQUESTED)
REM ===================================================================
if "%BUILD_MODE%"=="clean" (
    echo [3/7] Cleaning build artifacts...
    
    if exist "__pycache__" rmdir /s /q "__pycache__"
    if exist "*.pyc" del /q "*.pyc"
    if exist ".pytest_cache" rmdir /s /q ".pytest_cache"
    if exist "build" rmdir /s /q "build"
    if exist "dist" rmdir /s /q "dist"
    if exist "*.egg-info" rmdir /s /q "*.egg-info"
    
    echo ✅ Build artifacts cleaned
) else (
    echo [3/7] Skipping clean (use 'clean' argument to clean)
)

REM ===================================================================
REM STEP 4: INSTALL/UPDATE DEPENDENCIES
REM ===================================================================
echo [4/7] Installing/updating dependencies...

pip install --upgrade pip
if errorlevel 1 (
    echo ❌ ERROR: Failed to upgrade pip
    exit /b 1
)

pip install -r requirements.txt
if errorlevel 1 (
    echo ❌ ERROR: Failed to install requirements
    exit /b 1
)

echo ✅ Dependencies installed successfully

REM ===================================================================
REM STEP 5: CODE VALIDATION
REM ===================================================================
echo [5/7] Validating code quality...

REM Check if source directory exists
if not exist "%SRC_PATH%" (
    echo ❌ ERROR: Source directory not found: %SRC_PATH%
    exit /b 1
)

REM Run syntax check
echo   - Checking Python syntax...
python -m py_compile %SRC_PATH%\function_app.py
if errorlevel 1 (
    echo ❌ ERROR: Syntax errors found in function_app.py
    exit /b 1
)

REM Run imports check
echo   - Validating imports...
python -c "import sys; sys.path.append('%SRC_PATH%'); import function_app"
if errorlevel 1 (
    echo ❌ ERROR: Import validation failed
    exit /b 1
)

echo ✅ Code validation passed

REM ===================================================================
REM STEP 6: RUN TESTS (IF REQUESTED OR IN TEST MODE)
REM ===================================================================
if "%BUILD_MODE%"=="test" (
    echo [6/7] Running test suite...
    
    if not exist "%TESTS_PATH%" (
        echo ⚠️  WARNING: Tests directory not found: %TESTS_PATH%
        echo Tests will be skipped
    ) else (
        pytest %TESTS_PATH% -v --tb=short
        if errorlevel 1 (
            echo ❌ ERROR: Tests failed
            exit /b 1
        )
        echo ✅ All tests passed
    )
) else (
    echo [6/7] Skipping tests (use 'test' argument to run tests)
)

REM ===================================================================
REM STEP 7: DEPLOYMENT PREPARATION (IF REQUESTED)
REM ===================================================================
if "%BUILD_MODE%"=="deploy" (
    echo [7/7] Preparing for deployment...
    
    REM Check for required environment variables
    if "%OCP_APIM_SUBSCRIPTION_KEY%"=="" (
        echo ⚠️  WARNING: OCP_APIM_SUBSCRIPTION_KEY not set
    )
    
    REM Validate Azure Functions configuration
    if not exist "host.json" (
        echo ❌ ERROR: host.json not found
        exit /b 1
    )
    
    if not exist "local.settings.json" (
        echo ⚠️  WARNING: local.settings.json not found - create from .env.example
    )
    
    echo ✅ Deployment preparation completed
) else (
    echo [7/7] Skipping deployment preparation (use 'deploy' argument)
)

REM ===================================================================
REM BUILD COMPLETION
REM ===================================================================
echo.
echo ===================================================================
echo ✅ BUILD COMPLETED SUCCESSFULLY
echo Project: %PROJECT_NAME%
echo Mode: %BUILD_MODE%
echo Time: %DATE% %TIME%
echo ===================================================================

REM Generate build summary
echo Build Summary > build-summary.txt
echo Project: %PROJECT_NAME% >> build-summary.txt
echo Mode: %BUILD_MODE% >> build-summary.txt
echo Time: %DATE% %TIME% >> build-summary.txt
echo Status: SUCCESS >> build-summary.txt

echo.
echo Next steps:
if "%BUILD_MODE%"=="deploy" (
    echo 1. Review local.settings.json configuration
    echo 2. Deploy using: func azure functionapp publish your-function-app-name
) else (
    echo 1. Run tests: build.bat test
    echo 2. Deploy: build.bat deploy
)

endlocal
exit /b 0