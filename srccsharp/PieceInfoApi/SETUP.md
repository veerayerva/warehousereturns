# PieceInfo API Setup Guide

## Authentication Configuration

### Required: APIM Subscription Key

The PieceInfo API requires a valid Azure API Management (APIM) subscription key to access external APIs. You're getting 401 errors because this key needs to be configured.

### Setup Steps

1. **Get your APIM Subscription Key:**
   - Go to your Azure portal
   - Navigate to your API Management service (apim-dev.nfm.com)
   - Go to "Subscriptions" in the left menu
   - Find your subscription and copy the Primary or Secondary key

2. **Update local.settings.json:**
   ```json
   {
     "Values": {
       "OCP_APIM_SUBSCRIPTION_KEY": "your-actual-32-character-subscription-key-here"
     }
   }
   ```

3. **Test the API:**
   - Start the function app: `func start`
   - Navigate to: `http://localhost:7074/api/swagger/ui`
   - Test any endpoint to verify authentication works

### Configuration Files

- **Development**: `local.settings.json` (update with your dev subscription key)
- **Production**: Use Azure App Settings or Key Vault for the subscription key

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `OCP_APIM_SUBSCRIPTION_KEY` | ✅ | APIM subscription key for external API access |
| `EXTERNAL_API_BASE_URL` | ✅ | Base URL for external APIs (default: apim-dev.nfm.com) |
| `API_TIMEOUT_SECONDS` | ❌ | HTTP request timeout (default: 30) |
| `API_MAX_RETRIES` | ❌ | Maximum retry attempts (default: 3) |

### Troubleshooting

- **401 Unauthorized**: Check that `OCP_APIM_SUBSCRIPTION_KEY` is set to a valid key
- **404 Not Found**: Verify `EXTERNAL_API_BASE_URL` is correct
- **Timeout Errors**: Increase `API_TIMEOUT_SECONDS` if needed

### Security Notes

- Never commit actual subscription keys to source control
- Use Azure Key Vault for production deployments
- Rotate subscription keys periodically