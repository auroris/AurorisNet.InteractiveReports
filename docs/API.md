# Integration API

The server-side reference: packages, registration, report definitions, extension points, REST routes and errors. The browser element is described in [Embedding the report](EMBEDDING.md), and the access rules in [Authorization](AUTHORIZATION.md).

## Packages and namespaces

| Package | Namespace | Contents |
|---|---|---|
| `AurorisNet.InteractiveReports` | `InteractiveReport.Client.Json` | REST routes, the packaged viewer and administration pages, and the browser bundle. |
| `AurorisNet.InteractiveReports.AspNetCore` | `InteractiveReport.AspNetCore` | Registration, configuration, authorization and `IInteractiveReportServer`. |
| `AurorisNet.InteractiveReports.Core` | `InteractiveReport.Core.*` | Report models, the expression language, SQL generation, execution and the storage contracts. |
| `AurorisNet.InteractiveReports.Client.FileDownload` | `InteractiveReport.Client.FileDownload` | The CSV and HTML download route. |
| `AurorisNet.InteractiveReports.Client.GraphQL` | `InteractiveReport.Client.GraphQL` | The query-only GraphQL endpoint for saved reports. |

Each of the three client packages references the ASP.NET Core package, which references Core. All five are released together with one version number.

## Register and map the server

```csharp
using InteractiveReport.AspNetCore;
using InteractiveReport.Client.Json;

var builder = WebApplication.CreateBuilder(args);

var reports = builder.Services.AddInteractiveReports(builder.Configuration);
builder.Services.AddInteractiveReportJson();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapInteractiveReportJson("/api/reports")
    .RequireAuthorization("ReportingUsers")
    .RequireRateLimiting("reports");
app.Run();
```

`AddInteractiveReports` reads the `InteractiveReport` section, or the section named by its third argument, and returns an `InteractiveReportBuilder`. `MapInteractiveReportJson` returns a `RouteGroupBuilder`, so endpoint conventions apply to every route under the prefix. The packaged pages and browser assets stay anonymous under such a convention, because they are marked `AllowAnonymous`.

A convention on the route group and a report's own `authorization` setting are separate checks. A group that requires authorization also requires it for a report that sets `allowAnonymous`.

### Builder methods

| Method | What it does |
|---|---|
| `AddConnection(name, factory)` | Registers a factory that returns a new, unopened `DbConnection`. The SQL dialect is detected from the connection type. Registering a name again replaces it. |
| `AddConnection(name, factory, dialect)` | The same, with a declared `ReportDialect`, for wrapper or profiler connection types. |
| `UseLogger(logger)` | Sends the package's log events to one `ILogger`. See [Logging](#logging). |
| `UseContextParameterResolver<T>()` | Replaces the claim-based resolver of [context parameters](#context-parameters). `T` is a singleton. |
| `UseRowRestrictions(callback)` | Adds a callback that supplies a SQL predicate for reports whose `sql` contains `{{RowRestriction}}`. See [Row restrictions](AUTHORIZATION.md#row-restrictions). |
| `UseAuthorization(callback)` | Adds an application rule for every protected operation. See [Authorization](AUTHORIZATION.md#direct-callback). |
| `UseAspNetCoreAuthorization()` | Sends every protected operation through ASP.NET Core resource-based authorization. See [Authorization](AUTHORIZATION.md#aspnet-core-resource-handler). |
| `UseAdministrators(callback)` | Makes the application decide who administers. See [Administrators](AUTHORIZATION.md#administrators). |
| `UseUserDirectory(callback)` | Supplies account choices to the administration page. See [Administration user directory](#administration-user-directory). |
| `UseUserProvider<T>()` | The class-based form of the same directory. `T` is scoped. |

### Services

| Service | Use |
|---|---|
| `IInteractiveReportServer` | Runs every report operation with authorization, without HTTP types. The three client packages call it, and so can the application. |
| `IReportAuthorizationService` | The authorization decisions the server makes: report access, actions, administrator status and context parameters. |
| `IReportDefinitionStore` | Resolves a report definition by name. Replaceable. |
| `ISavedReportStore` | Stores saved reports. It makes no access decisions. Replaceable. |
| `ReportExecutor` | Validates and executes a report document against a resolved definition. It performs no authorization. |
| `ReportDefinitionCompiler` | Validates a definition built in code and resolves its connection and dialect. |
| `ConfiguredReportDocumentSynchronizer` | Registers the `documentFiles` of each report in the saved-report store. See [Saved reports](SAVED-REPORTS.md#synchronization). |
| `IrJson.Options` | The `JsonSerializerOptions` of the wire format, for endpoints the application writes. |

## Report definitions

A report definition is one entry under `InteractiveReport:Reports`. Its key is the report name used in every route. The [settings table](../README.md#report-definitions) lists each property with its default. This section explains the properties that need more than a line.

The server validates every definition when the host starts and again when configuration reloads. A validation failure at startup stops the host. After a reload it produces a coded `500` response for requests to that report.

### Data sources

A definition sets `dataSource` or `connection`, never both.

- `dataSource` without `=` names an entry under `ConnectionStrings`. A missing entry is an error.
- `dataSource` with `=` is a literal connection string.
- `connection` names a factory registered with `AddConnection`. Names that start with `__ir:` are reserved.

For `dataSource`, the server takes the provider from the definition's `provider` setting, or from `ConnectionStrings:{name}_ProviderName`:

| `provider` | `_ProviderName` values | Driver package |
|---|---|---|
| `sqlite` | `Microsoft.Data.Sqlite` | `Microsoft.Data.Sqlite` |
| `sqlServer` | `Microsoft.Data.SqlClient`, `System.Data.SqlClient` | `Microsoft.Data.SqlClient` |
| `postgres` | `Npgsql` | `Npgsql` |
| `oracle` | `Oracle.ManagedDataAccess.Client` | `Oracle.ManagedDataAccess.Core` |

The SQL dialect always follows the connection. At startup the server creates each `dataSource` connection once, unopened, to confirm that the driver is installed.

### Base SQL

`sql` is the base `SELECT`. The engine wraps it as a derived table named `ir_base` and never parses or rewrites it, so:

- It must not end with a top-level `ORDER BY`. Sorting belongs to the report document.
- Parameter names of the form `p0`, `p1`, and so on are reserved for the engine.
- The marker `{{RowRestriction}}` asks the application's row-restriction callback for a predicate. Any other `{{...}}` expression in executable SQL is an error.

### Features

`features` lists the feature tokens a report offers. Without the setting, a report offers all of them. Tokens are matched without regard to case, and an unknown or repeated token fails validation.

| Token | What the UI shows |
|---|---|
| `search` | The toolbar search box. |
| `columns` | The Columns dialog and **Hide Column**. |
| `rename` | **Rename** in the column heading menu. |
| `columnSettings` | The Column Settings dialog. |
| `filter` | Filters. |
| `sort` | Sorting. |
| `pagination` | The Pagination menu and the pager buttons. |
| `controlBreak` | Control breaks. |
| `highlight` | Highlights. |
| `aggregate` | Aggregates. |
| `compute` | Computed columns. |
| `groupBy` | The Group By view. |
| `pivot` | The Pivot view. |
| `chart` | The Chart view. |
| `savedReports` | Save, Save As, Delete and the saved-report selector. |
| `download` | The Download menu. |

**Only two tokens are enforced by the server.** Without `download`, the download route answers `403` with `IR-1100`. Without `savedReports`, creating a saved report answers the same; existing saved reports can still be updated and deleted. The other tokens only hide controls: the query route accepts any valid report document, and page JavaScript can [override the controls](EMBEDDING.md#client-controls). Use context parameters and row restrictions to limit data.

The configuration binder reads an empty JSON array as an absent setting, so `"features": []` offers every feature. To lock a report down, list the one or two features it keeps.

### Context parameters

A context parameter is a parameter of the base SQL that the server resolves for each request. A report document cannot set it.

```json
{
  "sql": "SELECT * FROM ORDERS WHERE TENANT_ID = @tenantId",
  "contextParams": {
    "tenantId": { "claim": "tenant_id" }
  }
}
```

Write the placeholder in the provider's own syntax: `@name` on SQL Server and SQLite, `:name` on Oracle. The default resolver binds the value of the named claim, or `NULL` when the caller has no such claim.

Register a resolver when the value comes from another trusted source:

```csharp
using System.Security.Claims;
using InteractiveReport.Core.Execution;
using InteractiveReport.Core.Model;

public sealed class TenantContextResolver(ITenantAccessor tenants) : IContextParameterResolver
{
    public ValueTask<object?> Resolve(
        string name,
        ContextParamSpec spec,
        ClaimsPrincipal? user,
        CancellationToken ct = default)
        => name.Equals("tenantId", StringComparison.OrdinalIgnoreCase)
            ? ValueTask.FromResult<object?>(tenants.RequiredTenantId)
            : throw new InvalidOperationException($"Unknown context parameter '{name}'.");
}

builder.Services
    .AddInteractiveReports(builder.Configuration)
    .UseContextParameterResolver<TenantContextResolver>();
```

The resolver is a singleton, so its dependencies must be safe to hold for the life of the application.

### Row and time limits

`maxRows` caps unpaged queries and downloads. A page size of `0` means all rows, up to `maxRows`. `maxPageSize` must not exceed a positive `maxRows`, and `defaultPageSize` must lie between 1 and `maxPageSize`.

`consistency: "snapshot"` runs every statement of one request in one snapshot: a `SNAPSHOT` transaction on SQL Server, a read-only transaction on Oracle, `REPEATABLE READ` on PostgreSQL and a read transaction on SQLite. The request fails when the database cannot provide the snapshot. On SQL Server that means `ALLOW_SNAPSHOT_ISOLATION` is off.

## Call the server from application code

`IInteractiveReportServer` gives an endpoint written by the application the same definition lookup, authorization, row restriction and context parameters as the packaged routes:

```csharp
using InteractiveReport.AspNetCore;
using InteractiveReport.Core.Model;

app.MapPost("/internal/orders/query", async (
    ReportState state,
    HttpContext http,
    IInteractiveReportServer reports,
    CancellationToken ct) =>
{
    var result = await reports.Query(
        "orders",
        state,
        InteractiveReportHttpRequest.Context(http),
        ct);

    return result.Failure is null
        ? Results.Json(result.Value!, IrJson.Options)
        : InteractiveReportHttpResult.Failure(result.Failure, http);
});
```

Each method returns an `InteractiveReportServerResult<T>` with either `Value` or `Failure`. Pass a failure to `InteractiveReportHttpResult.Failure` unchanged: it applies the status codes and the error shape described under [Errors](#errors).

| Method | Operation |
|---|---|
| `ListConfigurations` | The reports the caller may view. |
| `GetSchema` | Columns, default document, features and limits of one report. |
| `Query`, `QueryForDownload` | Run a submitted report document. The second authorizes `Export` and requires the `download` feature. |
| `Lov` | Distinct values of one column. |
| `ListSavedReports`, `LoadDocument`, `LoadDefaultDocument` | List and load saved reports. |
| `SaveDocument`, `UpdateDocument`, `DeleteDocument` | Change saved reports. |
| `ExportDocument`, `ImportDocument` | Download and upload a saved report as a document file. |
| `ListAdministrators`, `SetAdministrators`, `ListAuthorizationUsers` | Administration. |
| `DescribeIdentity` | The `whoami` answer. |

## Execute in process

`ReportExecutor` runs a report document without authorization. The caller resolves the definition and supplies the context parameters, and so becomes the trust boundary. Use it for background work. In a request, use `IInteractiveReportServer`.

```csharp
using InteractiveReport.Core.Definitions;
using InteractiveReport.Core.Execution;
using InteractiveReport.Core.Model;

var definition = await definitions.Find("orders", ct)
    ?? throw new KeyNotFoundException("Report 'orders' was not found.");

var contextParams = new Dictionary<string, object?> { ["tenantId"] = tenant.Id };

var result = await executor.Query(definition, state, contextParams, ct);

var values = await executor.Lov(definition, new ReportLovRequest
{
    Document = state,
    Table = state.ActiveTable,
    Column = "STATUS",
    Search = "pen",
}, contextParams, ct);
```

`values.Items` holds at most `ReportExecutor.MaxLovItems` (50) values. The executor rejects a definition whose SQL still contains `{{RowRestriction}}`.

## File downloads

```csharp
using InteractiveReport.Client.FileDownload;

builder.Services.AddInteractiveReportFileDownload();
// After builder.Build():
app.MapInteractiveReportFileDownload("/api/download");
```

`POST {prefix}/{name}/{format}` accepts a report document and returns it as `csv` or `html`. The route:

1. Replaces the document's paging with all rows.
2. Authorizes the `Export` action and requires the `download` feature.
3. Runs the document as an ordinary query, capped at `maxRows`.
4. Sets the response header `X-IR-Truncated` to `true` when the cap cut the result.

CSV applies the report's labels and format masks, writes a link column as its link text and an image column as its URL, and omits hidden renderer source columns. Text that a spreadsheet would read as a formula is neutralized. The file starts with a UTF-8 byte order mark.

HTML is one document with its styles included: labels, masks, control breaks and totals, with a data table in place of a chart. It is refused with `422` and `IR-1102` when the row cap cut the result or the document exceeds 4,194,304 characters. `HtmlReportPresentation.Render(title, result)` produces the same HTML for the application's own printing or document service; its optional arguments choose `A4`, `Letter` or `Legal` paper and landscape orientation.

An unknown format answers `400` with `IR-1101`. The formats come from the registered `IInteractiveReportDownloadWriter`, which an application can replace with its own.

## Replace the definition store or the saved-report store

The built-in `IReportDefinitionStore` reads configuration. Replace it after `AddInteractiveReports` to load definitions from elsewhere:

```csharp
using InteractiveReport.Core.Definitions;
using Microsoft.Extensions.DependencyInjection.Extensions;

builder.Services.Replace(
    ServiceDescriptor.Singleton<IReportDefinitionStore, DatabaseReportDefinitionStore>());
```

A custom store is responsible for every report it returns:

- Return a new `ReportDefinition` object for each call, with `Name`, `Connection` and `Dialect` set. `ReportDefinitionCompiler.Compile(name, definition)` copies and validates a definition and resolves its connection the way the built-in store does.
- Implement `IReportDefinitionAuthorizationStore` as well when the store can return a report's name and `authorization` block cheaply. The server then checks access before it loads the full definition.
- Set `ReportDefinition.SourceName` when several report names are variants of one source report. Authorization and row-restriction callbacks receive it as `SourceReportName`. It grants nothing by itself.

A replaced store no longer serves the configured reports or the built-in `__saved-reports` listing behind the administration page.

`ISavedReportStore` is replaceable in the same way. Its methods only store and read: the server makes the ownership and visibility decisions. An implementation must return detached copies and honor the compare-and-swap arguments of `Update`, `Delete` and `ReplaceDefault`.

## Administration user directory

The administration page lets an administrator pick accounts when reassigning a saved report or editing the administrator list. Each picker searches `GET {prefix}/admin/users`, which merges two sources:

- the identities the server already knows: configured and database administrators, saved-report owners and the caller;
- an optional directory supplied by the application, which adds display names.

The lookup offers choices only. It grants no access, and a picker also accepts an identity value typed in full.

Register the directory as a callback that answers a search with .NET identities:

```csharp
using System.Security.Claims;
using InteractiveReport.AspNetCore;

reports.UseUserDirectory(async (search, ct) =>
{
    var users = search.RequestServices.GetRequiredService<UserManager<AppUser>>();
    var principals = search.RequestServices
        .GetRequiredService<IUserClaimsPrincipalFactory<AppUser>>();
    var matches = await users.Users
        .Where(user => search.Search == null || user.UserName!.Contains(search.Search))
        .OrderBy(user => user.UserName)
        .Take(search.Limit)
        .ToListAsync(ct);

    var identities = new List<ClaimsIdentity>();
    foreach (var user in matches)
        identities.Add((await principals.CreateAsync(user)).Identities.First());
    return identities;
});
```

`InteractiveReportUserSearch` carries the `Administrator` principal, the trimmed `Search` text (`null` when the picker opens without text), the result `Limit` and the `RequestServices`.

The server reads each identity's value the way it reads a signed-in caller's, so the value an administrator picks is the value that account has when it signs in. The display name is the identity's `Name`, then a `name`, `preferred_username`, UPN or email claim, then the identity value. An identity without a usable value fails the lookup with a `500`.

`UseUserProvider<T>()` registers a class in place of the callback. Its one method returns display names and identity values directly:

```csharp
public sealed class ReportUserProvider(IApplicationUsers users) : IInteractiveReportUserProvider
{
    public async ValueTask<IReadOnlyCollection<InteractiveReportUser>?> SearchUsers(
        InteractiveReportUserSearch search,
        CancellationToken ct = default)
        => (await users.Search(search.Search, search.Limit, ct))
            .Select(user => new InteractiveReportUser(user.DisplayName, user.SubjectId))
            .ToArray();
}
```

An entry with an empty display name or value, or two entries with one value, fail the lookup.

Directory entries come first, in the directory's order. Known identities follow in alphabetical order. `null` or an empty answer leaves the known identities. `UserDirectory:MaxResults` bounds the merged answer, and `truncated` is `true` when the directory filled the limit, so the page asks for a narrower search. `UserDirectory:CacheSeconds` reuses the directory's answer to a lookup without search text, for each administrator. Searches are never cached, and known identities are read on every lookup.

## REST routes

The table uses the default prefixes. Every request body must be sent as `Content-Type: application/json`; ASP.NET Core answers another content type with an empty `415`. Responses carry `Cache-Control: no-store`, except the browser assets, which use `no-cache` with an `ETag`.

| Method and route | What it does |
|---|---|
| `GET /api/reports` | Lists the configured reports the caller may view, as `{ name, title }`, ordered by title. |
| `GET /api/reports/{name}/schema` | Returns the report's columns, default document, features, limits, expression functions and link settings. |
| `POST /api/reports/{name}/query` | Runs the posted report document and returns a result with rows and the accepted `document`. |
| `POST /api/reports/{name}/lov` | Returns at most 50 distinct values of one column of the posted document. |
| `GET /api/reports/{name}` | Lists the saved reports of one report that the caller may load. Administrators receive all of them. |
| `GET /api/reports/{name}/default` | Loads the report's default document with its data, as `{ summary, result }`. `?pageSize=n` sets the first page size. |
| `GET /api/reports/{name}/{id}` | Loads one saved report with its data, as `{ summary, result }`. |
| `POST /api/reports/{name}/saved` | Creates a saved report from `{ title, state, isGlobal }`. Answers `201`. |
| `PUT /api/reports/{id}` | Updates a saved report. `title`, `state`, `isGlobal`, `isDefault` and `owner` are each optional. |
| `DELETE /api/reports/{id}` | Deletes a saved report. Answers `204`. |
| `POST /api/download/{name}/{format}` | Returns the posted report document as a `csv` or `html` file. Mapped by the file-download package. |
| `GET /api/reports/whoami` | Returns the caller's identity value, administrator status and claims. Answers `404` unless `WhoamiEnabled` is `true`. |
| `GET /api/reports/admin/users` | Searches account choices with the optional `search` query (at most 200 characters). Returns `{ items, truncated }`. |
| `GET /api/reports/admin/administrators` | Returns `{ configured, database, managedByApplication }`. |
| `PUT /api/reports/admin/administrators` | Replaces the database administrator list with `{ identities }`. Answers `204`. |
| `GET /api/reports/admin/saved/{id}/document` | Downloads a saved report as a `{ title, default, state }` document file. |
| `POST /api/reports/admin/{name}/documents` | Imports a document file as a private saved report of the caller. Answers `201`. |
| `GET /api/reports/ui/{file}` | A packaged browser asset: `ir.js`, `ir-admin.js`, `ir-chart.js`, their source maps and `help.{locale}.html`. Anonymous. |
| `GET /api/reports/{name}/view` | The packaged viewer page. `?saved-report=id` opens a saved report. Anonymous. |
| `GET /api/reports/admin` | The packaged administration page. Anonymous. |

The routes under `/admin`, except the page itself, require an administrator. `MapInteractiveReportJson` attaches summaries, tags, and request and response types to the data routes for the application's OpenAPI generator.

### Saved-report summaries

A summary has `id`, `reportName`, `title`, `isGlobal`, `isDefault`, `mine`, `isReadOnly` and `modifiedUtc`. `id` is a database-generated integer, unique across all reports, and a JSON number. `isReadOnly` marks a document that comes from a `documentFiles` entry.

A saved report that does not exist, belongs to another report, or is hidden from the caller answers `404` with `IR-1002` in each case.

### Loading

`GET {name}/{id}` and `GET {name}/default` run the stored document and return it with its data, so a client needs no second request. When the stored document cannot be read or fails validation or execution, the server tries the report's stored default, then a default built from the definition. Each is tried once. `summary` describes the document that succeeded and is `null` for the built default. A denial stops the load at once.

Loading never writes. A document that failed stays as it was.

`POST {name}/query` runs the posted document once, with no fallback. The document needs no saved-report id: a client may post a changed copy of a loaded document or one it built itself.

### Report documents

```http
POST /api/reports/orders/query HTTP/1.1
Content-Type: application/json

{
  "page": { "index": 1, "size": 50 },
  "search": "acme",
  "activeTable": "base",
  "tables": {
    "base": {
      "from": "definition",
      "composables": [
        { "kind": "filter", "filters": [ { "expr": "STATUS = 'Open'" } ] },
        { "kind": "sort", "sorts": [ { "col": "AMOUNT", "dir": "desc" } ] },
        { "kind": "select", "columns": [ "ORDER_ID", "CUSTOMER", "AMOUNT" ] }
      ]
    }
  }
}
```

A document holds named tables and names one as `activeTable`. Each table reads `from` the reserved name `definition` or from another table, and carries a list of composables:

| `kind` | Properties |
|---|---|
| `filter` | `filters`: `expr`, `enabled` |
| `compute` | `computed`: `id`, `label`, `expr`, `enabled` |
| `sort` | `sorts`: `col`, `dir` (`asc`, `desc`), `nulls` (`first`, `last`) |
| `select` | `columns`: the visible columns, in order |
| `labels` | `labels`: column name to heading |
| `formats` | `formats`: column name to `mask`, `align`, `bold`, `italic`, `fg`, `bg`, `classes`, `displayAs`, `urlColumn`, `textColumn`, `command`, `keyColumn` |
| `break` | `breaks`: column names |
| `aggregate` | `aggregates`: `col`, `fn` |
| `highlight` | `highlights`: `id`, `name`, `sequence`, `scope` (`row`, `cell`), `col`, `style` (`bg`, `fg`), `expr`, `enabled` |
| `group` | `by`, `values`: `id`, `col`, `fn` |
| `pivot` | `rows`, `cols`, `values`, `totals` |
| `chart` | `type` (`bar`, `line`, `area`, `pie`), `label`, `value`, `fn`, `orientation`, `sort`, `labelAxisTitle`, `valueAxisTitle` |

`fn` is `count`, `sum`, `avg`, `median`, `min`, `max` or `countDistinct`. A table holds at most one of `group`, `pivot` and `chart`; a second view of the same data is a second table that reads from the first. The order of composables in the list does not change the result: [Architecture](ARCHITECTURE.md#operation-order) gives the order the engine applies them in. The [JSON Schema](../schemas/report-document.schema.json) describes the whole format, and the [User guide](USER-GUIDE.md#writing-expressions) the expression language.

A document may hold at most 64 tables and 512 composables, with 20 computed columns, 50 filters and 50 highlights. Search text is at most 200 characters.

Use the `document` of a successful response as the new state. The server adds a `schema` list to each table for editors, and moves settings that no longer apply, such as a sort on a removed column, into the result's `ignored` list instead of failing. A table's `schema` in a posted document is never trusted.

A result holds `columns`, `availableColumns`, `rows`, `page`, `totalRows`, `aggregates`, `breakTotals`, `breakContinues`, `highlights`, `ignored`, `configuredLabels`, `elapsedMs` and `document`. `configuredLabels` is the definition's label map; a client applies the document's `labels` over it.

### Numbers in JSON

64-bit integers and decimals in rows are written as strings in invariant format, so a JavaScript client does not round them. Their column still reports `type: "number"`. 32-bit integers and floating-point values are JSON numbers.

### List of values

```http
POST /api/reports/orders/lov HTTP/1.1
Content-Type: application/json

{
  "document": { "activeTable": "base", "tables": { "base": { "from": "definition" } } },
  "table": "base",
  "column": "CUSTOMER",
  "search": "ac"
}
```

```json
{
  "table": "base",
  "column": "CUSTOMER",
  "type": "text",
  "items": ["Acme Corp", "Acme Services"],
  "truncated": false
}
```

The server compiles the complete posted document, so its filters and computed columns apply, and reads the one column of its active table. `table` must name that active table. `search` is an optional partial match that ignores case, applied before the limit of 50. The route authorizes the `Query` action.

## Errors

Every failure of a data route is one JSON object:

```json
{
  "code": "IR-1201",
  "description": "One or more report settings are invalid.",
  "title": "Report state failed validation",
  "details": "tables.base.composables[0].filters[0].expr: unknown column 'OLD_NAME'"
}
```

`code` identifies the failure and does not change between versions or languages. `title` and `description` are English text that a client may replace by code. `details` carries what is specific to the request. An unexpected failure answers `500` with a `traceId` that matches the server log, and no other detail.

| Status | When |
|---|---|
| `400` | The request or report document is invalid. |
| `401` | The operation needs a signed-in caller. The response carries `WWW-Authenticate: InteractiveReport`. |
| `403` | The caller is signed in and the operation is denied or the feature is off. |
| `404` | The report or saved report does not exist, or the caller may not know that it does. Only `code`, `title` and `description` are returned. |
| `409` | A saved-report title is taken, or configuration owns the default. |
| `415` | The request body is not declared as JSON. The body is empty. |
| `422` | An HTML download would be incomplete. |
| `500` | An unexpected failure, with `traceId`. |

| Code | Meaning |
|---|---|
| `IR-1000` | Authentication required. |
| `IR-1001` | Report not found, or access is hidden. |
| `IR-1002` | Saved report not found, or access is hidden. |
| `IR-1003` | Endpoint not available (`whoami` is off). |
| `IR-1004` | Authorization denied. |
| `IR-1005` | An authorization callback, handler or policy failed unexpectedly. |
| `IR-1100` | The feature is not enabled for the report. |
| `IR-1101` | Unsupported download format. |
| `IR-1102` | The download exceeds the row cap or the HTML size limit. |
| `IR-1200` | The report document is not valid JSON. |
| `IR-1201` | The report document failed validation. `details` lists a path and message for each error. |
| `IR-1202` | The report could not be processed. |
| `IR-1300`, `IR-1303` | The save or update request is not valid JSON. |
| `IR-1301` | The saved-report title must have 1 to 200 characters. |
| `IR-1302`, `IR-1308` | The request must include report state. |
| `IR-1304` | The owner must be a non-empty identity value of at most 400 characters. |
| `IR-1305`, `IR-1306`, `IR-1307` | An uploaded document file is not valid JSON, has an invalid title, or has no state. |
| `IR-1309` | A saved report with this title already exists. |
| `IR-1310` | A configured document uses this title. |
| `IR-1311` | Configured documents cannot be updated or deleted. |
| `IR-1312` | The default cannot be unset. Select another default. |
| `IR-1313` | Configuration selects the default, so the API cannot replace it. |
| `IR-1400`, `IR-1406` | The administrator list request is not valid JSON, or has no `identities` list. |
| `IR-1402` | An identity must have 1 to 400 characters. |
| `IR-1405` | User search text may have at most 200 characters. |
| `IR-1500` | The GraphQL endpoint accepts HTTP GET and POST only. |

`GET {name}/default` answers an invalid `pageSize` with an ASP.NET Core validation problem, not with this shape.

## Logging

The package writes no log output until the application supplies a logger, during registration or when mapping the routes:

```csharp
var reportLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("InteractiveReport");
app.MapInteractiveReportJson("/api/reports", reportLogger);
```

| Level | Events |
|---|---|
| `Information` | Startup validation, each request with its status and duration, queries, saved-report changes and document synchronization. |
| `Debug` | Authorization decisions, schema-cache activity and the SQL text of each command. |
| `Warning`, `Error` | Fallbacks, skipped settings and failures, with the request's trace id. |

Request bodies and SQL parameter values are never logged.

## GraphQL

`AddInteractiveReportGraphQL` and `MapInteractiveReportGraphQL` add a query-only endpoint that lists and runs saved reports. See the [GraphQL adapter guide](GRAPHQL.md).
