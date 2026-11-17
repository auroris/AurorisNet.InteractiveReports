# AurorisNet.InteractiveReports.Core

The engine of [Interactive Reports](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md). It turns a trusted report definition and a user's report document into validated, parameterized SQL for SQL Server, PostgreSQL, Oracle or SQLite, runs it, and shapes the result.

The package holds the report and document models, the expression language, schema discovery, validation, SQL generation, execution, and the saved-report and administrator stores. It targets .NET 8 and has no ASP.NET Core dependency.

## Install

Most applications install [AurorisNet.InteractiveReports](https://www.nuget.org/packages/AurorisNet.InteractiveReports/), which adds the REST API and the browser UI and brings in this package.

Reference Core by itself to work with the report models or to call the engine outside ASP.NET Core:

```sh
dotnet add package AurorisNet.InteractiveReports.Core
```

`ReportExecutor` runs a report document against a resolved definition. It performs no authorization: the caller supplies the database connections, the definition and the context parameters, and decides who may run the report. In an ASP.NET Core request, use `IInteractiveReportServer` from the server package, which applies the authorization and saved-report rules.

The application references the ADO.NET driver of its database.

## Related packages

| Package | Adds |
|---|---|
| [AurorisNet.InteractiveReports](https://www.nuget.org/packages/AurorisNet.InteractiveReports/) | REST API, viewer and administration pages, and the browser element. |
| [AurorisNet.InteractiveReports.AspNetCore](https://www.nuget.org/packages/AurorisNet.InteractiveReports.AspNetCore/) | Configuration, authorization and `IInteractiveReportServer`. |
| [AurorisNet.InteractiveReports.Client.FileDownload](https://www.nuget.org/packages/AurorisNet.InteractiveReports.Client.FileDownload/) | CSV and HTML downloads. |
| [AurorisNet.InteractiveReports.Client.GraphQL](https://www.nuget.org/packages/AurorisNet.InteractiveReports.Client.GraphQL/) | A query-only GraphQL endpoint for saved reports. |

The five packages are released together. Use one version for all of them. C# namespaces are `InteractiveReport.*`.

## Documentation

- [Repository README](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md)
- [Architecture](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/ARCHITECTURE.md)
- [Integration API, with direct engine calls](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/API.md#execute-in-process)
- [Live demo](https://interactive-report.pages.dev/)
