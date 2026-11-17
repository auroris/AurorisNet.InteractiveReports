# Getting started

This guide adds a first report to an ASP.NET Core application, then saved reports and an authorization rule. For a host you can run as it is, see the [minimal sample](../samples/Minimal/README.md) or the [Windows Authentication sample](../samples/WindowsAuthentication/README.md).

## Prerequisites

- An ASP.NET Core application that targets .NET 8 or later.
- A SQL Server, PostgreSQL, Oracle or SQLite database the application can reach.
- The ADO.NET driver for that database, referenced by the application: `Microsoft.Data.SqlClient`, `Npgsql`, `Oracle.ManagedDataAccess.Core` or `Microsoft.Data.Sqlite`.

## 1. Install the packages

```sh
dotnet add package AurorisNet.InteractiveReports
dotnet add package Microsoft.Data.Sqlite
```

`AurorisNet.InteractiveReports` holds the REST API and the browser UI, and brings in the server and engine packages. Add `AurorisNet.InteractiveReports.Client.FileDownload` when users should download CSV and HTML files:

```sh
dotnet add package AurorisNet.InteractiveReports.Client.FileDownload
```

`AurorisNet.InteractiveReports.Client.GraphQL` is described in the [GraphQL adapter guide](GRAPHQL.md). The C# namespaces of all packages are `InteractiveReport.*`.

## 2. Configure a data source and a report

Add a connection string and one report definition to `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "MainDb": "Data Source=reports.db",
    "MainDb_ProviderName": "Microsoft.Data.Sqlite"
  },
  "InteractiveReport": {
    "Reports": {
      "orders": {
        "dataSource": "MainDb",
        "sql": "SELECT ORDER_ID, CUSTOMER, AMOUNT, ORDER_DATE FROM ORDERS",
        "authorization": { "allowAnonymous": true }
      }
    }
  }
}
```

`dataSource` names an entry under `ConnectionStrings`. A value that contains `=` is read as a literal connection string instead.

The server needs to know the database provider. It reads `ConnectionStrings:MainDb_ProviderName`, as above, or the report's own `provider` setting: `sqlite`, `sqlServer`, `postgres` or `oracle`. A literal connection string always needs `provider`.

`sql` is trusted application configuration and stays on the server. Write a `SELECT` without a final `ORDER BY`: users choose the sort order.

A report requires a signed-in caller unless it sets `allowAnonymous`. This example sets it so the first run works before authentication is configured. Remove it for a report that should require sign-in.

The [settings tables](../README.md#settings) list every option.

## 3. Register and map the server

In `Program.cs`:

```csharp
using InteractiveReport.AspNetCore;
using InteractiveReport.Client.Json;

var builder = WebApplication.CreateBuilder(args);
var reports = builder.Services.AddInteractiveReports(builder.Configuration);
builder.Services.AddInteractiveReportJson();

var app = builder.Build();
// The application's authentication and authorization middleware goes here.

app.MapInteractiveReportJson("/api/reports");
app.Run();
```

With the file-download package, register and map it too:

```csharp
using InteractiveReport.Client.FileDownload;

builder.Services.AddInteractiveReportFileDownload();

// After builder.Build():
app.MapInteractiveReportFileDownload("/api/download");
```

The application chooses both route prefixes. This documentation uses `/api/reports` and `/api/download`, which are the defaults. The browser element finds a download prefix that follows this pattern by itself; for another prefix, set its `download-base` attribute.

## 4. Open the report

Run the application and browse to the packaged viewer page:

```text
/api/reports/orders/view
```

To place the report in a page of the application, load the packaged module and add the element:

```html
<script type="module" src="/api/reports/ui/ir.js"></script>
<interactive-report report="orders"></interactive-report>
```

[Embedding the report](EMBEDDING.md) describes the element's attributes, JavaScript API, events and theming.

If the report does not appear, check these first:

- The connection string points to a database the application can reach.
- The `SELECT` runs for the database account in the connection string.
- The application references the ADO.NET driver package.
- The request is signed in, or the report sets `allowAnonymous`.

An invalid definition stops the application at startup with a message that names the report. A failed request returns the [coded error](API.md#errors) described in the API reference.

## Add saved reports and administration

The first integration stores nothing. To let users save reports, name a database for them:

```json
{
  "InteractiveReport": {
    "Administrators": [ "identity-value-of-the-first-administrator" ],
    "SavedReports": {
      "dataSource": "MainDb",
      "tablePrefix": "MYAPP_"
    }
  }
}
```

`SavedReports:dataSource` follows the same rules as a report's `dataSource`. The server creates `MYAPP_IR_SAVED_REPORTS` and `MYAPP_IR_ADMINISTRATORS` when they are first needed.

The packaged administration page is then at:

```text
/api/reports/admin
```

An entry in `Administrators` must equal the identity value the server resolves for the caller, character for character. To see that value, set `InteractiveReport:WhoamiEnabled` to `true`, sign in and open `/api/reports/whoami`. Turn the setting off afterwards.

An application that already knows its administrators can decide the question itself with `UseAdministrators(...)` or `AdministratorPolicy`: see [Administrators](AUTHORIZATION.md#administrators). [Saved reports](SAVED-REPORTS.md) covers storage, defaults, source-controlled documents and the administration page.

## Add an authorization rule

The server always applies its own rules: sign-in, the report's policy, saved-report ownership and the administrator check. The application can add a rule for each operation. A callback returns `false` to deny:

```csharp
reports.UseAuthorization((request, cancellationToken) =>
{
    var allowed = request.Action switch
    {
        InteractiveReportAction.Export => request.User.HasClaim("reports", "export"),
        _ => true,
    };

    return ValueTask.FromResult(allowed);
});
```

A registered callback is asked about every protected operation, so it must return `true` for the operations it has no opinion on. Read [Authorization](AUTHORIZATION.md) before you deploy a callback: it lists every action, the ASP.NET Core resource-handler alternative, row restrictions and the response each denial produces.

## Connections created in code

Register a connection factory when configuration cannot create the connection, for example for a profiling wrapper or a factory that picks a tenant's database:

```csharp
reports.AddConnection("MainDb", services => new SqlConnection(connectionString));
```

The report then sets `"connection": "MainDb"` in place of `dataSource`. The server detects the SQL dialect from the connection type. Pass a `ReportDialect` as the third argument when the factory returns a wrapper type.

## Umbraco

An Umbraco site keeps its connection string in `ConnectionStrings:umbracoDbDSN`, with the provider in `ConnectionStrings:umbracoDbDSN_ProviderName`. A report over the Umbraco database can therefore set `"dataSource": "umbracoDbDSN"` and no `provider`.
