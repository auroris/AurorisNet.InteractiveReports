# AurorisNet.InteractiveReports.Client.FileDownload

CSV and HTML downloads for [Interactive Reports](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md). The package adds one route that runs a report document without paging and returns the result as a file, with the report's labels, format masks, filters and sort order.

The Download menu of the report UI uses this route.

## Install

```sh
dotnet add package AurorisNet.InteractiveReports.Client.FileDownload
```

```csharp
using InteractiveReport.AspNetCore;
using InteractiveReport.Client.FileDownload;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddInteractiveReports(builder.Configuration);
builder.Services.AddInteractiveReportFileDownload();

var app = builder.Build();
app.MapInteractiveReportFileDownload("/api/download");
app.Run();
```

In an application that already uses [AurorisNet.InteractiveReports](https://www.nuget.org/packages/AurorisNet.InteractiveReports/), keep its registration and add the two file-download lines. Call `AddInteractiveReports` once. The package also works without the browser package, for a service that only returns files.

Post a report document to `POST /api/download/{name}/csv` or `POST /api/download/{name}/html`. The route:

- authorizes the `Export` action and requires the report's `download` feature;
- returns every matching row, up to the report's `maxRows`;
- sets the response header `X-IR-Truncated` to `true` when a CSV file was cut at that cap;
- refuses an HTML file that would be incomplete, with status `422`.

The package targets .NET 8 and brings in `AurorisNet.InteractiveReports.AspNetCore` and `AurorisNet.InteractiveReports.Core`. The five packages are released together. Use one version for all of them. C# namespaces are `InteractiveReport.*`.

## Documentation

- [Repository README](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/README.md)
- [File downloads](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/API.md#file-downloads)
- [Downloads in the browser element](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/EMBEDDING.md#downloads-and-printing)
- [Authorization](https://github.com/auroris/AurorisNet.InteractiveReports/blob/main/docs/AUTHORIZATION.md)
