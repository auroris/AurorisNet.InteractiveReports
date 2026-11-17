# AurorisNet.InteractiveReports

Oracle APEX-style interactive reports for ASP.NET Core: a developer configures a `SELECT`, and users search, filter, sort, break, compute, highlight, group, pivot, chart, save and download the result.

![MIT license](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/license-mit.svg)
![.NET 8](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/dotnet-8.svg)
![.NET 10](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/dotnet-10.svg)
![Windows](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/windows.svg)
![Linux](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/linux.svg)
![Databases](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/databases.svg)
[![Live demo](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/demo.svg)](https://interactive-report.pages.dev/)

![An interactive report: toolbar, settings chips, a control break by region, a highlighted cell, a computed column and the pager](https://raw.githubusercontent.com/auroris/AurorisNet.InteractiveReports/main/docs/images/hero.png)

This is the package to start with. It adds the REST API, the packaged viewer and administration pages, and the `<interactive-report>` browser element. The browser bundle is embedded, so the application needs no Node.js and no front-end build. Try the [live demo](https://interactive-report.pages.dev/).

## Install

In an ASP.NET Core application that targets .NET 8 or later:

```sh
dotnet add package AurorisNet.InteractiveReports
```

The package brings in `AurorisNet.InteractiveReports.AspNetCore` and `AurorisNet.InteractiveReports.Core`. The application references the ADO.NET driver of its database: SQL Server, PostgreSQL, Oracle and SQLite are supported.

Configure a connection and a report in `appsettings.json`, as the [getting started guide](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/GETTING-STARTED.md) shows. Then register the services and map the routes:

```csharp
using InteractiveReport.AspNetCore;
using InteractiveReport.Client.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInteractiveReports(builder.Configuration);
builder.Services.AddInteractiveReportJson();

var app = builder.Build();
app.MapInteractiveReportJson("/api/reports");
app.Run();
```

For a report named `orders`, open `/api/reports/orders/view`. A report requires a signed-in caller unless its definition sets `allowAnonymous`.

## Related packages

| Package | Adds |
|---|---|
| [AurorisNet.InteractiveReports.Client.FileDownload](https://www.nuget.org/packages/AurorisNet.InteractiveReports.Client.FileDownload/) | CSV and HTML downloads. The report's Download menu needs it. |
| [AurorisNet.InteractiveReports.Client.GraphQL](https://www.nuget.org/packages/AurorisNet.InteractiveReports.Client.GraphQL/) | A query-only GraphQL endpoint for saved reports. |
| [AurorisNet.InteractiveReports.AspNetCore](https://www.nuget.org/packages/AurorisNet.InteractiveReports.AspNetCore/) | The server. Installed with this package. |
| [AurorisNet.InteractiveReports.Core](https://www.nuget.org/packages/AurorisNet.InteractiveReports.Core/) | The engine. Installed with this package. |

The five packages are released together. Use one version for all of them. C# namespaces are `InteractiveReport.*`.

## Documentation

The [repository README](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md) has the settings tables and links every guide:

- [Getting started](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/GETTING-STARTED.md)
- [Embedding the report](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/EMBEDDING.md)
- [Integration API](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/API.md)
- [Authorization](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/AUTHORIZATION.md)
- [Saved reports](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/SAVED-REPORTS.md)
- [User guide](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/USER-GUIDE.md)
