# Cloudflare Deployment

This project includes Wrangler so you can deploy the static files in `wwwroot` to Cloudflare Pages:

```powershell
npm run cf:deploy
```

To confirm Wrangler is available:

```powershell
npm run cf:version
```

Use this instead of the Cloudflare dashboard uploader when Cloudflare says:

```text
This uploader does not yet support projects that require a build process. Multiple JavaScript files were found. Please use wrangler deploy instead for full feature support.
```

## Important

`wwwroot` is static content only. This ASP.NET Core MVC app still needs a .NET server for controllers, Razor views, login, registration, Identity, SQL access, and background email flow. Cloudflare Pages cannot run the C# backend by itself.

## Recommended hosting for this app

Use a real .NET host such as Azure App Service, IIS, or a container host.

For this project, Azure App Service is the best fit because it supports ASP.NET Core, environment variables, and SQL Server connectivity without requiring a custom server setup.

See [AZURE_DEPLOY.md](AZURE_DEPLOY.md) for the recommended production setup.
