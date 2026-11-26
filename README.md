# Interactive Reports

Oracle APEX-style interactive reports for ASP.NET Core (.NET 8+). Configure a `SELECT` query, and users can search, filter, sort, control-break, compute columns, highlight rows, group, pivot, chart, save views, and export CSV/HTML.

![MIT license](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/license-mit.svg)
![.NET 8](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/dotnet-8.svg)
![.NET 10](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/dotnet-10.svg)
![Windows](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/windows.svg)
![Linux](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/linux.svg)
![Databases](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/databases.svg)
[![Live demo](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/demo.svg)](https://interactive-report.pages.dev/)

![Interactive report toolbar, settings chips, control break, highlight, computed column, and pager](docs/images/hero.png)

Try the [live demo](https://interactive-report.pages.dev/) (runs in-browser against SQLite WASM). Supports SQL Server, PostgreSQL, Oracle (11g+), and SQLite with one typed expression language and parameterized SQL. The browser UI (`<interactive-report>` web component, viewer page, admin page, EN/FR-CA text, built-in help) is embedded in the NuGet package—no front-end build needed.

## Install

Start with `AurorisNet.InteractiveReports` (pulls in `AspNetCore` and `Core` automatically) plus your database's ADO.NET driver:

```sh
dotnet add package AurorisNet.InteractiveReports
dotnet add package Microsoft.Data.Sqlite
```

Optional add-on packages (all share one version; namespaces are `InteractiveReport.*`):
- `AurorisNet.InteractiveReports.Client.FileDownload` — CSV and self-contained HTML downloads.
- `AurorisNet.InteractiveReports.Client.GraphQL` — query-only GraphQL endpoint for saved reports.

Configure a report in `appsettings.json`:

```json
{
  "ConnectionStrings": { "MainDb": "Data Source=reports.db" },
  "InteractiveReport": {
    "Reports": {
      "orders": {
        "dataSource": "MainDb",
        "provider": "sqlite",
        "sql": "SELECT ORDER_ID, CUSTOMER, AMOUNT, ORDER_DATE FROM ORDERS",
        "authorization": { "allowAnonymous": true }
      }
    }
  }
}
```

Register and map in `Program.cs`:

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

Open `/api/reports/orders/view`, or embed `<interactive-report report="orders"></interactive-report>` with `<script type="module" src="/api/reports/ui/ir.js"></script>`.

> [!WARNING]
> Report actions execute live SQL against the configured database. Point reports at a read replica or reporting database with a read-only account rather than your primary production database.

## Settings

Configured under the `InteractiveReport` section (validated at startup).

### InteractiveReport

- `SavedReports`: `DataSource`/`Provider` or `Connection`, `AutoCreate` (`true`), `TablePrefix`, `TableName` (`IR_SAVED_REPORTS`).
- `Administrators`, `AdministratorPolicy`, `IdentityClaim`, `WhoamiEnabled` (`false`), `ViewerPagesEnabled` (`true`).

### Report definitions

Each entry under `InteractiveReport:Reports:<name>` configures `sql` (trusted base `SELECT`, no trailing `ORDER BY`), `dataSource`/`provider` or `connection`, `authorization` (`allowAnonymous` defaults to `false`; optional ASP.NET Core `policy`), `contextParams`, `features`, `defaultState`, `maxRows` (`100000`), `defaultPageSize` (`50`), `maxPageSize` (`1000`), `commandTimeoutSeconds` (`30`), `columns`, `editLink`, and `createLink`.

See [Integration API](docs/API.md#report-definitions) and [Saved reports](docs/SAVED-REPORTS.md) for every setting and default.

## Documentation

| Guide | Covers |
|---|---|
| [Getting started](docs/GETTING-STARTED.md) & [Samples](samples/Minimal/README.md) | First report, data sources, saved reports, [Windows Auth sample](samples/WindowsAuthentication/README.md) |
| [User guide](docs/USER-GUIDE.md) | Filters, pivots, charts, administration (same text as built-in `?` help) |
| [Embedding](docs/EMBEDDING.md) | `<interactive-report>` attributes, JS API, events, theming, edit/create links |
| [Integration API](docs/API.md) & [Authorization](docs/AUTHORIZATION.md) | All settings, REST routes, policies, row restrictions |
| [Saved reports](docs/SAVED-REPORTS.md) & [GraphQL](docs/GRAPHQL.md) | Persistence, JSON report files, GraphQL adapter |
| [Architecture](docs/ARCHITECTURE.md), [Developing](docs/DEVELOPING.md), [Testing](docs/TESTING.md) | Pipeline, building, Workbench, Playwright & live-DB tests, releases |

## License

[MIT](LICENSE)
