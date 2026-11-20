# Interactive Reports

Oracle APEX-style interactive reports for ASP.NET Core: a developer configures a `SELECT`, and users search, filter, sort, break, compute, highlight, group, pivot, chart, save and download the result.

![MIT license](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/license-mit.svg)
![.NET 8](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/dotnet-8.svg)
![.NET 10](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/dotnet-10.svg)
![Windows](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/windows.svg)
![Linux](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/linux.svg)
![Databases](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/databases.svg)
[![Live demo](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/demo.svg)](https://interactive-report.pages.dev/)

![An interactive report: toolbar, settings chips, a control break by region, a highlighted cell, a computed column and the pager](docs/images/hero.png)

Try the [live demo](https://interactive-report.pages.dev/). It runs the report UI in the browser against a SQLite database compiled to WebAssembly.

A report is configuration: a name, a data source and a `SELECT`. The server reads the columns from that statement, and the `<interactive-report>` browser element gives users a workspace over the result.

- **Report state is data.** Everything a user does becomes a JSON report document. The server validates it and compiles it into parameterized SQL that runs in the report's database. User input reaches the database only as command parameters.
- **One expression language.** Filters, highlights and computed columns use the same typed expressions on SQL Server, PostgreSQL, Oracle and SQLite.
- **Saved reports.** Users save private layouts, administrators publish them, and an application can ship layouts as JSON files in source control.
- **Downloads.** The current view downloads as CSV or as a self-contained HTML document, with every matching row.
- **Host authorization.** Reports use the application's ASP.NET Core identity and policies. The application can add its own rule for each operation and a row restriction for each caller.
- **Packaged UI.** The NuGet package embeds the browser bundle, a viewer page, an administration page, English and Canadian French text, and the built-in help. The application needs no front-end build.

## Install

Add the package to an ASP.NET Core application that targets .NET 8 or later, with the ADO.NET driver for the report database:

```sh
dotnet add package AurorisNet.InteractiveReports
dotnet add package Microsoft.Data.Sqlite
```

`AurorisNet.InteractiveReports` brings in `AurorisNet.InteractiveReports.AspNetCore` and `AurorisNet.InteractiveReports.Core`. The optional adapters are `AurorisNet.InteractiveReports.Client.FileDownload` for CSV and HTML downloads and `AurorisNet.InteractiveReports.Client.GraphQL` for GraphQL queries. C# namespaces are `InteractiveReport.*`.

Configure a report in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "MainDb": "Data Source=reports.db"
  },
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

Register the services and map the endpoints in `Program.cs`:

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

Browse to `/api/reports/orders/view` for the packaged viewer page, or place the element in a page of the application:

```html
<script type="module" src="/api/reports/ui/ir.js"></script>
<interactive-report report="orders"></interactive-report>
```

[Getting started](docs/GETTING-STARTED.md) continues with data sources, saved reports and authorization. The [minimal sample](samples/Minimal/README.md) is a runnable host with one report, and the [Windows Authentication sample](samples/WindowsAuthentication/README.md) adds sign-in, administration and authorization callbacks.

> [!WARNING]
> Report queries run against the configured database on every user action. Point reports at a reporting database or read replica through a read-only, least-privileged account, and keep the application on a trusted network when you can. Avoid running interactive reports against the primary production database.

## Settings

The server binds the `InteractiveReport` configuration section. `AddInteractiveReports(configuration, "OtherSection")` reads another section. Definitions are validated when the host starts, and an invalid one stops startup with a message that names the report.

### InteractiveReport

| Setting | Default | What it does |
|---|---|---|
| `Reports` | empty | Report definitions, keyed by report name. Names are case-insensitive. The names `ui`, `saved`, `whoami` and `admin`, and names that start with `__`, are reserved. |
| `SavedReports:DataSource` | none | Database for saved reports and administrator grants: a `ConnectionStrings` name, or a literal connection string. Set this or `Connection`. With neither, nothing is persisted and no table or file is created. |
| `SavedReports:Provider` | none | Provider token for `DataSource`: `sqlite`, `sqlServer`, `postgres` or `oracle`. |
| `SavedReports:Connection` | none | A connection registered with `AddConnection`, in place of `DataSource`. |
| `SavedReports:AutoCreate` | `true` | Creates the two tables when they are first needed. Set `false` when operators create them. |
| `SavedReports:TablePrefix` | empty | Prefix for both table names. `APP_` gives `APP_IR_SAVED_REPORTS` and `APP_IR_ADMINISTRATORS`. |
| `SavedReports:TableName` | `IR_SAVED_REPORTS` | Base name of the saved-report table. Table names use letters, digits and `_` only. |
| `Authorization:TableName` | `IR_ADMINISTRATORS` | Base name of the administrator table, in the same database. It must differ from the saved-report table name. |
| `Administrators` | empty | Identity values with administrator rights. Matching is ordinal and case-sensitive. Read only when the application registers no `UseAdministrators` callback and sets no `AdministratorPolicy`. |
| `AdministratorPolicy` | none | Name of an ASP.NET Core policy that decides who administers. While it is set, `Administrators` and the database grants are ignored. |
| `IdentityClaim` | none | Claim type that holds a caller's identity value. When unset, the server reads `ClaimTypes.NameIdentifier`, then `sub`, then `Identity.Name`. When set, it reads only this claim. |
| `WhoamiEnabled` | `false` | Serves `GET {prefix}/whoami`, which returns the caller's identity value, administrator status and claims. **Enable it while you set up administrators, then turn it off.** |
| `ViewerPagesEnabled` | `true` | Serves the packaged pages `{prefix}/{name}/view` and `{prefix}/admin`. The pages hold no data: the API calls they make are authorized. |
| `UserDirectory:MaxResults` | `50` | Most accounts one administration lookup returns. Between 1 and 1000. |
| `UserDirectory:CacheSeconds` | `60` | How long the application directory's answer to a lookup without search text is reused for the same administrator. `0` asks every time. |

### Report definitions

Each entry under `InteractiveReport:Reports` is one report. [Report definitions](docs/API.md#report-definitions) explains data sources, features and context parameters.

| Setting | Default | What it does |
|---|---|---|
| `sql` | required | The base `SELECT`. **It is trusted application code and never leaves the server.** It runs as a derived table, so it must not end with `ORDER BY`. |
| `dataSource` | none | A `ConnectionStrings` name (a value without `=`) or a literal connection string. Set this or `connection`. |
| `provider` | none | Provider token for `dataSource`: `sqlite`, `sqlServer`, `postgres` or `oracle`. Optional when `ConnectionStrings:{name}_ProviderName` names the ADO.NET provider. |
| `connection` | none | A connection registered with `AddConnection`. |
| `title` | from the name | Display title. `order_feed` becomes `Order Feed`. |
| `authorization.allowAnonymous` | `false` | Admits callers who are not signed in. **Without it, a report requires an authenticated caller.** |
| `authorization.policy` | none | ASP.NET Core policy the caller must also pass. It cannot be combined with `allowAnonymous`. |
| `contextParams` | none | Parameters of `sql` that the server resolves, by default from a claim: `{ "tenantId": { "claim": "tenant_id" } }`. |
| `features` | all | Feature tokens the report offers. The UI hides the rest. The server enforces `download` and `savedReports`. |
| `defaultState` | none | The report document a first-time viewer starts from. |
| `documentFiles` | none | Paths of saved-report JSON files, relative to the content root. See [Saved reports](docs/SAVED-REPORTS.md#source-controlled-documents). |
| `maxRows` | `100000` | Row cap for unpaged queries and downloads. `0` or less removes the cap. |
| `defaultPageSize` | `50` | Page size when a request names none. Between 1 and `maxPageSize`. |
| `maxPageSize` | `1000` | Largest page size a request may select. At most `maxRows`. |
| `maxPivotColumns` | `60` | Most distinct column combinations a pivot may produce. Between 1 and 10000. |
| `maxChartPoints` | `1000` | Most points a chart may draw. Between 1 and 10000. A chart over the limit is a validation error. |
| `commandTimeoutSeconds` | `30` | Timeout of each database command. |
| `consistency` | `none` | `snapshot` runs the statements of one request in one database snapshot. SQL Server needs `ALLOW_SNAPSHOT_ISOLATION ON`. |
| `timeZone` | server setting | Session time zone, such as `Pacific/Auckland` or `+13:00`, set on Oracle and PostgreSQL connections. SQL Server and SQLite ignore it. |
| `columnLabels` | none | Display labels by column name. |
| `columns` | none | Per-column `label`, `hideLabel`, `sortable`, `filterable` and `helpText`. |
| `editLink` | none | Per-row edit link: `urlTemplate`, `label`, `target`, `mode`. |
| `createLink` | none | Toolbar create button: `url`, `label`, `target`, `mode`. |

[Embedding the report](docs/EMBEDDING.md#edit-links-create-buttons-and-column-overrides) describes `columns`, `editLink` and `createLink`.

## Supported versions

| Dependency | Supported |
|---|---|
| .NET | The five libraries target `net8.0` and run on .NET 8 or later. The tests and samples run on .NET 10. |
| SQL Server | `Microsoft.Data.SqlClient` or `System.Data.SqlClient` |
| PostgreSQL | `Npgsql` |
| Oracle | `Oracle.ManagedDataAccess.Core`. Oracle 11g uses a compatibility mode that the server selects from the database version. |
| SQLite | `Microsoft.Data.Sqlite` |
| Browser | The bundle is an ES2022 module that defines custom elements with a shadow root. |

The host application references the database driver. The packages reference none.

## Documentation

| Guide | What it covers |
|---|---|
| [Getting started](docs/GETTING-STARTED.md) | A first report, data sources, saved-report storage and the first authorization rule. |
| [User guide](docs/USER-GUIDE.md) | How to use a report and the administration page. The same text is the built-in help behind the **?** button. |
| [Embedding the report](docs/EMBEDDING.md) | The `<interactive-report>` element: attributes, JavaScript API, events, theming, renderers, edit links and create buttons. |
| [Integration API](docs/API.md) | Report definitions, server registration, extension points, REST routes and error codes. |
| [Authorization](docs/AUTHORIZATION.md) | Actions and resources, application callbacks, administrators, row restrictions and denial responses. |
| [Saved reports](docs/SAVED-REPORTS.md) | Storage, visibility, defaults, source-controlled documents, import and export, and the administration page. |
| [GraphQL adapter](docs/GRAPHQL.md) | The query-only GraphQL endpoint for saved reports. |
| [Architecture](docs/ARCHITECTURE.md) | Packages, the request pipeline, SQL generation and the browser client. |
| [Developing](docs/DEVELOPING.md) | Building the client, running Workbench, packing and releasing. |
| [Testing](docs/TESTING.md) | The test suites and the live-database setup. |

## Developing

You need the .NET 10 SDK, Node.js 20 or later and Playwright's Chromium. Building and testing work on Windows and Linux. The pack and release scripts are PowerShell and use Windows paths.

```sh
npm ci
npx playwright install chromium   # on Linux: npx playwright install --with-deps chromium
npm run build                     # browser bundles, packaged help and the .NET solution
npm run start                     # Workbench at http://localhost:5042
npm test                          # client unit, browser-server and documentation tests
dotnet test                       # Core, ASP.NET Core and live-database test projects
npm run test:e2e                  # Playwright suite against Workbench
npm run pack:nuget                # scripts/pack.ps1: build, test and pack into artifacts/packages
```

The live-database tests run the engine against real SQL Server, Oracle and PostgreSQL servers. They run on a developer's machine and never on GitHub: set the `IR_TEST_*` connection strings and run `dotnet test tests/InteractiveReport.Live.Tests`. Without the variables each test skips. [Testing](docs/TESTING.md#live-database-tests) has the setup, and [Developing](docs/DEVELOPING.md) has the rest.

## Releases

The packages follow the .NET platform packages: `Microsoft.Extensions.*` and the packages that move with the monthly .NET servicing release. Dependabot checks NuGet and npm each week. `ci.yml` tests each update and merges it. An update ships when it is of a package that a `src/` project references or of an npm package that is bundled into the browser UI, and each week `weekly-release.yml` publishes the merged updates that ship as one release. Versions are `MAJOR.MINOR.PATCH`. One release publishes all five packages, with symbols and `SHA256SUMS.txt`, to nuget.org and GitHub. The [release guide](https://github.com/auroris/AurorisNet.Build/blob/main/docs/releasing.md) describes the workflows and how to start a release by hand.

## License

[MIT](LICENSE)
