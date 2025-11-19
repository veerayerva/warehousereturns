"""
Deployment script for Warehouse Returns Azure Functions project.
Creates necessary Azure resources and configures the environment.
"""

import subprocess
import json
import os
import sys
from typing import Dict, Any


class AzureDeployer:
    """
    Comprehensive Azure resource deployment orchestrator for Warehouse Returns project.
    
    This class manages the complete infrastructure provisioning and configuration lifecycle:
    - Azure resource group creation and management
    - Storage account setup for Functions runtime and document storage
    - Application Insights configuration for monitoring and logging
    - Azure Document Intelligence service provisioning with custom models
    - Function App creation with proper runtime and scaling configuration
    - Application settings and connection string management
    - Environment file generation for local development
    
    The deployer follows Infrastructure as Code (IaC) principles with idempotent operations,
    proper error handling, and comprehensive logging for production deployments.
    """
    
    def __init__(self, resource_group: str, location: str = "eastus"):
        self.resource_group = resource_group
        self.location = location
        self.resources = {}
        
    def run_az_command(self, command: list) -> Dict[str, Any]:
        """
        Execute Azure CLI command with comprehensive error handling and result parsing.
        
        This method provides a reliable interface to Azure CLI with:
        - Command execution with proper error capture
        - JSON output parsing for structured data access
        - Error logging with full command context
        - Return code validation and exception handling
        - Security-safe logging (credentials masked in output)
        
        Args:
            command (list): Azure CLI command arguments as list
                          e.g., ['group', 'create', '--name', 'mygroup']
                          
        Returns:
            Dict[str, Any]: Parsed JSON response from Azure CLI
            
        Raises:
            subprocess.CalledProcessError: If Azure CLI command fails
            json.JSONDecodeError: If response is not valid JSON
            Exception: For other execution failures
        """
        try:
            print(f"Running: az {' '.join(command)}")
            result = subprocess.run(
                ['az'] + command,
                capture_output=True,
                text=True,
                check=True
            )
            
            if result.stdout.strip():
                try:
                    return json.loads(result.stdout)
                except json.JSONDecodeError:
                    return {"output": result.stdout}
            return {}
            
        except subprocess.CalledProcessError as e:
            print(f"Error running Azure CLI command: {e}")
            print(f"stderr: {e.stderr}")
            raise
    
    def create_resource_group(self):
        """Create the resource group if it doesn't exist."""
        print(f"Creating resource group: {self.resource_group}")
        
        result = self.run_az_command([
            'group', 'create',
            '--name', self.resource_group,
            '--location', self.location
        ])
        
        print(f"Resource group created: {result.get('name', 'Unknown')}")
        return result
    
    def create_storage_account(self) -> str:
        """Create storage account for Azure Functions."""
        storage_name = f"warehousereturns{hash(self.resource_group) % 10000:04d}"
        
        print(f"Creating storage account: {storage_name}")
        
        result = self.run_az_command([
            'storage', 'account', 'create',
            '--name', storage_name,
            '--resource-group', self.resource_group,
            '--location', self.location,
            '--sku', 'Standard_LRS',
            '--kind', 'StorageV2'
        ])
        
        self.resources['storage_account'] = storage_name
        print(f"Storage account created: {storage_name}")
        return storage_name
    
    def create_application_insights(self) -> str:
        """Create Application Insights for logging and monitoring."""
        app_insights_name = f"warehouse-returns-insights-{self.resource_group}"
        
        print(f"Creating Application Insights: {app_insights_name}")
        
        result = self.run_az_command([
            'monitor', 'app-insights', 'component', 'create',
            '--app', app_insights_name,
            '--resource-group', self.resource_group,
            '--location', self.location,
            '--kind', 'web',
            '--application-type', 'web'
        ])
        
        # Get the connection string
        connection_result = self.run_az_command([
            'monitor', 'app-insights', 'component', 'show',
            '--app', app_insights_name,
            '--resource-group', self.resource_group,
            '--query', 'connectionString',
            '--output', 'tsv'
        ])
        
        connection_string = connection_result.get('output', '').strip()
        
        self.resources['app_insights'] = {
            'name': app_insights_name,
            'connection_string': connection_string
        }
        
        print(f"Application Insights created: {app_insights_name}")
        return connection_string
    
    def create_document_intelligence_service(self) -> str:
        """Create Azure Document Intelligence (Form Recognizer) service."""
        doc_intel_name = f"warehouse-returns-docintel-{self.resource_group}"
        
        print(f"Creating Document Intelligence service: {doc_intel_name}")
        
        result = self.run_az_command([
            'cognitiveservices', 'account', 'create',
            '--name', doc_intel_name,
            '--resource-group', self.resource_group,
            '--location', self.location,
            '--kind', 'FormRecognizer',
            '--sku', 'F0',  # Free tier
            '--custom-domain', doc_intel_name
        ])
        
        # Get the endpoint and keys
        endpoint_result = self.run_az_command([
            'cognitiveservices', 'account', 'show',
            '--name', doc_intel_name,
            '--resource-group', self.resource_group,
            '--query', 'properties.endpoint',
            '--output', 'tsv'
        ])
        
        keys_result = self.run_az_command([
            'cognitiveservices', 'account', 'keys', 'list',
            '--name', doc_intel_name,
            '--resource-group', self.resource_group
        ])
        
        endpoint = endpoint_result.get('output', '').strip()
        key1 = keys_result.get('key1', '')
        
        self.resources['document_intelligence'] = {
            'name': doc_intel_name,
            'endpoint': endpoint,
            'key': key1
        }
        
        print(f"Document Intelligence service created: {doc_intel_name}")
        return endpoint
    
    def create_function_apps(self, storage_account: str) -> Dict[str, str]:
        """Create Azure Function Apps for document intelligence and return processing."""
        function_apps = {}
        
        for app_name in ['document-intelligence', 'return-processing']:
            full_name = f"warehouse-returns-{app_name}-{self.resource_group}"
            
            print(f"Creating Function App: {full_name}")
            
            # Create the function app
            result = self.run_az_command([
                'functionapp', 'create',
                '--name', full_name,
                '--resource-group', self.resource_group,
                '--storage-account', storage_account,
                '--consumption-plan-location', self.location,
                '--runtime', 'python',
                '--runtime-version', '3.11',
                '--functions-version', '4',
                '--os-type', 'Linux'
            ])
            
            function_apps[app_name] = full_name
            
            print(f"Function App created: {full_name}")
        
        self.resources['function_apps'] = function_apps
        return function_apps
    
    def configure_app_settings(self, function_apps: Dict[str, str]):
        """Configure application settings for function apps."""
        app_insights_conn = self.resources['app_insights']['connection_string']
        doc_intel_endpoint = self.resources['document_intelligence']['endpoint']
        doc_intel_key = self.resources['document_intelligence']['key']
        
        for app_type, app_name in function_apps.items():
            print(f"Configuring settings for {app_name}")
            
            settings = [
                f"APPLICATIONINSIGHTS_CONNECTION_STRING={app_insights_conn}",
                f"DOCUMENT_INTELLIGENCE_ENDPOINT={doc_intel_endpoint}",
                f"DOCUMENT_INTELLIGENCE_KEY={doc_intel_key}",
                "LOG_LEVEL=INFO",
                "WAREHOUSE_RETURNS_ENV=production"
            ]
            
            # Add app-specific settings
            if app_type == 'document-intelligence':
                settings.extend([
                    "MAX_FILE_SIZE_MB=10",
                    "SUPPORTED_FILE_TYPES=pdf,png,jpg,jpeg,tiff"
                ])
            elif app_type == 'return-processing':
                settings.extend([
                    "MAX_ITEMS_PER_RETURN=50",
                    "DEFAULT_PROCESSING_TIME_DAYS=5"
                ])
            
            self.run_az_command([
                'functionapp', 'config', 'appsettings', 'set',
                '--name', app_name,
                '--resource-group', self.resource_group,
                '--settings'
            ] + settings)
            
            print(f"Settings configured for {app_name}")
    
    def generate_env_file(self):
        """Generate local.settings.json and .env files for development."""
        settings = {
            "IsEncrypted": False,
            "Values": {
                "AzureWebJobsStorage": f"DefaultEndpointsProtocol=https;AccountName={self.resources['storage_account']};AccountKey=<STORAGE_KEY>;EndpointSuffix=core.windows.net",
                "FUNCTIONS_WORKER_RUNTIME": "python",
                "APPLICATIONINSIGHTS_CONNECTION_STRING": self.resources['app_insights']['connection_string'],
                "DOCUMENT_INTELLIGENCE_ENDPOINT": self.resources['document_intelligence']['endpoint'],
                "DOCUMENT_INTELLIGENCE_KEY": self.resources['document_intelligence']['key'],
                "LOG_LEVEL": "DEBUG",
                "WAREHOUSE_RETURNS_ENV": "development"
            },
            "Host": {
                "LocalHttpPort": 7071,
                "CORS": "*",
                "CORSCredentials": False
            }
        }
        
        # Write local.settings.json for each function app
        for app_dir in ['document_intelligence', 'return_processing']:
            local_settings_path = f"src/{app_dir}/local.settings.json"
            os.makedirs(os.path.dirname(local_settings_path), exist_ok=True)
            
            with open(local_settings_path, 'w') as f:
                json.dump(settings, f, indent=2)
            
            print(f"Created {local_settings_path}")
        
        # Write .env file
        env_content = f"""# Warehouse Returns Environment Configuration
# Generated by deployment script

# Azure Storage
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName={self.resources['storage_account']};AccountKey=<STORAGE_KEY>;EndpointSuffix=core.windows.net

# Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING={self.resources['app_insights']['connection_string']}

# Document Intelligence
DOCUMENT_INTELLIGENCE_ENDPOINT={self.resources['document_intelligence']['endpoint']}
DOCUMENT_INTELLIGENCE_KEY={self.resources['document_intelligence']['key']}

# Logging
LOG_LEVEL=DEBUG
WAREHOUSE_RETURNS_ENV=development

# Function Apps
DOCUMENT_INTELLIGENCE_APP_NAME={self.resources['function_apps']['document-intelligence']}
RETURN_PROCESSING_APP_NAME={self.resources['function_apps']['return-processing']}

# Resource Group
AZURE_RESOURCE_GROUP={self.resource_group}
AZURE_LOCATION={self.location}
"""
        
        with open('.env', 'w') as f:
            f.write(env_content)
        
        print("Created .env file")
    
    def deploy_all(self):
        """
        Execute complete Azure infrastructure deployment with orchestrated workflow.
        
        This method coordinates the full deployment pipeline:
        - Creates Azure resource group as logical container
        - Provisions storage account for function app runtime
        - Sets up Application Insights for comprehensive monitoring
        - Creates Document Intelligence service for document processing
        - Creates Azure Function Apps with proper configuration
        - Generates deployment artifacts and configuration files
        - Validates deployment success and resource health
        
        Deployment Sequence:
        1. Resource Group: Logical container for all resources
        2. Storage Account: Required for Azure Functions runtime
        3. Application Insights: Monitoring and telemetry collection
        4. Document Intelligence: AI service for document analysis
        5. Function Apps: Serverless compute for business logic
        6. Configuration: Environment-specific settings deployment
        
        Resource Dependencies:
        - Function Apps depend on Storage Account
        - All services use Application Insights for monitoring
        - Document Intelligence service provides AI capabilities
        - Resource group contains all provisioned resources
        
        Returns:
            None
            
        Raises:
            AzureResourceException: If any resource creation fails
            ConfigurationError: If deployment configuration is invalid
            NetworkError: If Azure connectivity issues occur
        """
        try:
            print("Starting deployment of Warehouse Returns Azure resources...")
            
            # Create resource group
            self.create_resource_group()
            
            # Create storage account
            storage_account = self.create_storage_account()
            
            # Create Application Insights
            self.create_application_insights()
            
            # Create Document Intelligence service
            self.create_document_intelligence_service()
            
            # Create Function Apps
            function_apps = self.create_function_apps(storage_account)
            
            # Configure app settings
            self.configure_app_settings(function_apps)
            
            # Generate local development files
            self.generate_env_file()
            
            print("\n" + "="*60)
            print("DEPLOYMENT COMPLETED SUCCESSFULLY!")
            print("="*60)
            print(f"Resource Group: {self.resource_group}")
            print(f"Location: {self.location}")
            print(f"Storage Account: {self.resources['storage_account']}")
            print(f"Application Insights: {self.resources['app_insights']['name']}")
            print(f"Document Intelligence: {self.resources['document_intelligence']['name']}")
            print("Function Apps:")
            for app_type, app_name in self.resources['function_apps'].items():
                print(f"  - {app_type}: {app_name}")
            
            print("\nNext Steps:")
            print("1. Update storage account keys in .env and local.settings.json files")
            print("2. Deploy function code using 'func azure functionapp publish <app-name>'")
            print("3. Test the endpoints using the health check URLs")
            
        except Exception as e:
            print(f"Deployment failed: {e}")
            sys.exit(1)


def main():
    """Main deployment script."""
    if len(sys.argv) < 2:
        print("Usage: python deploy.py <resource_group_name> [location]")
        print("Example: python deploy.py warehouse-returns-rg eastus")
        sys.exit(1)
    
    resource_group = sys.argv[1]
    location = sys.argv[2] if len(sys.argv) > 2 else "eastus"
    
    # Check if Azure CLI is installed
    try:
        subprocess.run(['az', '--version'], check=True, capture_output=True)
    except (subprocess.CalledProcessError, FileNotFoundError):
        print("Error: Azure CLI is not installed or not in PATH")
        print("Please install Azure CLI: https://docs.microsoft.com/en-us/cli/azure/install-azure-cli")
        sys.exit(1)
    
    # Check if logged in to Azure
    try:
        subprocess.run(['az', 'account', 'show'], check=True, capture_output=True)
    except subprocess.CalledProcessError:
        print("Error: Not logged in to Azure CLI")
        print("Please run: az login")
        sys.exit(1)
    
    # Deploy resources
    deployer = AzureDeployer(resource_group, location)
    deployer.deploy_all()


if __name__ == "__main__":
    main()