# AurorisNet.InteractiveReports.Client.GraphQL

A query-only GraphQL endpoint for [Interactive Reports](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md), built on GraphQL.NET. A client lists the reports it may view, lists the saved reports of one report, and runs a saved report with its own paging, search and sorting. Requests pass through the same server and authorization as the REST API.

## Install

```sh
dotnet add package AurorisNet.InteractiveReports.Client.GraphQL
```

```csharp
using InteractiveReport.AspNetCore;
using InteractiveReport.Client.GraphQL;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInteractiveReports(builder.Configuration);
builder.Services.AddInteractiveReportGraphQL();

var app = builder.Build();
app.MapInteractiveReportGraphQL("/graphql");
app.Run();
```

The endpoint accepts queries over HTTP GET and POST. Its schema has no mutation or subscription type, and the package contains no GraphQL IDE.

The endpoint runs saved reports, so the application needs saved-report storage (`InteractiveReport:SavedReports`) and saved reports to run. Users create them in the report UI of [AurorisNet.InteractiveReports](https://www.nuget.org/packages/AurorisNet.InteractiveReports/), or the application ships them as JSON files. In an application that already uses that package, add the two GraphQL lines and call `AddInteractiveReports` once.

The package targets .NET 8 and brings in `AurorisNet.InteractiveReports.AspNetCore` and `AurorisNet.InteractiveReports.Core`. The five packages are released together. Use one version for all of them. C# namespaces are `InteractiveReport.*`.

## Documentation

- [Repository README](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md)
- [GraphQL adapter: schema, arguments and errors](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/GRAPHQL.md)
- [Saved reports](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/SAVED-REPORTS.md)
- [Authorization](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/AUTHORIZATION.md)
