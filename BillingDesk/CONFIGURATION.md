# BillingDesk - Configuration Guide

## Database Connection

The application connects to the SQL Server database `PortfolioBillingDemo`.

### Default Connection String

The default connection string in `appsettings.json` uses Windows Integrated Security:

```
Server=(local);Database=PortfolioBillingDemo;Integrated Security=true;Encrypt=false;TrustServerCertificate=true;
```

### Local Development Configuration

For local development with custom connection strings or SQL authentication:

1. **Option 1: Using User Secrets (Recommended)**
   ```powershell
   cd BillingDesk
   dotnet user-secrets init
   dotnet user-secrets set "ConnectionStrings:BillingDb" "Server=YOUR_SERVER;Database=PortfolioBillingDemo;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=false;TrustServerCertificate=true;"
   ```

2. **Option 2: Using appsettings.local.json**
   Create `appsettings.local.json` in the BillingDesk directory (this file is gitignored):
   ```json
   {
	 "ConnectionStrings": {
	   "BillingDb": "Server=YOUR_SERVER;Database=PortfolioBillingDemo;User Id=YOUR_USER;Password=YOUR_PASSWORD;Encrypt=false;TrustServerCertificate=true;"
	 }
   }
   ```

### Environment Detection

- The application automatically loads `appsettings.json` (shared settings)
- Environment-specific overrides can be placed in `appsettings.{ENVIRONMENT}.json`
- Local development uses `appsettings.Development.json` for logging configuration
- User secrets are loaded last and override all other sources

## Logging

Logging is configured through `appsettings.json` and environment-specific files:

- **Production**: Information level (default)
- **Development**: Debug level (more verbose)
- Entity Framework Core queries are logged at Information level in production, Debug in development

## Security Notes

- **Never commit** passwords, connection strings with credentials, or API keys
- User secrets are stored outside the project directory
- `appsettings.local.json` is in `.gitignore`
- Integrated Windows Security (trusted connection) is preferred when possible
