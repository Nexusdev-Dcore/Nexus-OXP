# Azure App Service deployment guide

This ASP.NET Core MVC app is best deployed to Azure App Service.

## Why Azure App Service

- supports ASP.NET Core directly
- works with SQL Server connection strings
- supports environment variables for secrets
- easy to configure with identity and SMTP settings

## Recommended setup

1. Create a new Azure App Service for the .NET app.
2. Set the .NET runtime to .NET 10 if available in your tenant.
3. Add the following app settings in Azure:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__DefaultConnection=Server=<server>;Database=DBNEXUSOXP;User Id=<user>;Password=<password>;TrustServerCertificate=True;MultipleActiveResultSets=True
NEXUSOXP_ADMIN_EMAIL=admin@gmail.com
NEXUSOXP_ADMIN_PASSWORD=<strong-production-password>
Smtp__Host=smtp.gmail.com
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__Username=<gmail-address>
Smtp__Password=<gmail-app-password>
Smtp__FromName=NexusOXP
Smtp__FromAddress=<gmail-address>
```

4. Publish the project using Visual Studio or `dotnet publish`.
5. Deploy the generated publish output to the App Service.

## Publish command

```powershell
dotnet publish .\NexusOXP.csproj -c Release -o .\publish
```

## Security notes

- do not store real secrets in source control
- use App Service application settings or Azure Key Vault for production secrets
- keep the admin password strong and environment-specific
- do not use an ordinary Gmail password; use an App Password

## Recommended production architecture

- App Service for the ASP.NET Core app
- Azure SQL Database or a managed SQL Server instance for the database
- Gmail SMTP or another mail provider for password reset emails
