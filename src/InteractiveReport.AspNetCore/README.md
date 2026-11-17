# AurorisNet.InteractiveReports.AspNetCore

The ASP.NET Core server of [Interactive Reports](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md). It registers the report engine with dependency injection and adds configured report definitions, database connections, authorization, context parameters, row restrictions and saved-report storage.

Its `IInteractiveReportServer` runs every report operation with authorization and without HTTP types. The browser, file-download and GraphQL packages all call it.

## Install

Most applications install [AurorisNet.InteractiveReports](https://www.nuget.org/packages/AurorisNet.InteractiveReports/), which adds the REST API and the browser UI and brings in this package.

Install this package by itself when the application calls `IInteractiveReportServer` from its own code or endpoints. It maps no routes and contains no browser UI:

```sh
dotnet add package AurorisNet.InteractiveReports.AspNetCore
```

```csharp
using InteractiveReport.AspNetCore;

builder.Services.AddInteractiveReports(builder.Configuration);
```

The package targets .NET 8 and brings in `AurorisNet.InteractiveReports.Core`. The application references the ADO.NET driver of its database and sets up authentication. Saved reports are stored only when `InteractiveReport:SavedReports` names a database.

## Related packages

| Package | Adds |
|---|---|
| [AurorisNet.InteractiveReports](https://www.nuget.org/packages/AurorisNet.InteractiveReports/) | REST API, viewer and administration pages, and the browser element. |
| [AurorisNet.InteractiveReports.Client.FileDownload](https://www.nuget.org/packages/AurorisNet.InteractiveReports.Client.FileDownload/) | CSV and HTML downloads. |
| [AurorisNet.InteractiveReports.Client.GraphQL](https://www.nuget.org/packages/AurorisNet.InteractiveReports.Client.GraphQL/) | A query-only GraphQL endpoint for saved reports. |

The five packages are released together. Use one version for all of them. C# namespaces are `InteractiveReport.*`.

## Documentation

- [Repository README, with the settings tables](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md)
- [Integration API](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/API.md)
- [Authorization](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/AUTHORIZATION.md)
- [Saved reports](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/SAVED-REPORTS.md)
- [Architecture](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/ARCHITECTURE.md)
