# Architecture

How Interactive Reports is put together: the packages, the path of a request, how a report document becomes SQL, and what the browser does. Read it before changing the engine or writing a new transport.

An application supplies a report definition, which holds a trusted `SELECT`. A user supplies a report document, which describes how to filter, shape and present that query. The engine validates the document, compiles it into parameterized SQL for the report's database, runs it, and returns rows with the accepted document.

## System context

```text
 Browser or API client
          |
          v
  +----------------------------- ASP.NET Core host ----------------------------+
  |                                                                            |
  |  JSON adapter       GraphQL adapter       File-download adapter            |
  |       \                  |                       /                         |
  |        +---------- IInteractiveReportServer -----------+                   |
  |                            |                                               |
  |             definitions, authorization, saved reports                      |
  |                            |                                               |
  |                       ReportExecutor                                       |
  |                            |                                               |
  |        validation -> bound relation plan -> SQL -> result shaping          |
  +----------------------------|-----------------------------------------------+
                               |
                    ADO.NET connections
                      /                \
             report database     saved-report store
```

The saved-report store holds report documents and administrator grants. It holds no report rows.

## Design rules

**The server owns what executes.** The definition holds the base `SELECT`, the connection, the limits and the access settings. It is loaded on the server by name. SQL text and connection details are never read from a report document and never sent to a client. The engine treats the `SELECT` as an opaque relation: it wraps the statement and never parses or rewrites it.

**Report documents are data.** A document names columns and declares operations. The engine checks every name against the schema, parses expressions with its own closed grammar, binds them to typed columns, and writes the SQL itself. User values become command parameters, and column names are resolved to schema entries before the SQL backend quotes them.

**Every transport calls one boundary.** `IInteractiveReportServer` owns definition lookup, authorization, saved reports, queries and administration. The JSON, GraphQL and file-download packages translate their transport into its calls, so visibility, ownership, validation and failures are the same everywhere.

**Relational work runs in the database.** Filters, search, computed columns, grouping, pivots, sorting, aggregates and highlight conditions are pushed into the report database. The server shapes the returned rows and keeps no copy of them. Results are therefore live, and report load falls on the configured database.

**One relation feeds every result.** The page, the row count, the aggregates, the control-break totals, the pivot totals and the highlight markers come from one validated relation plan. They may run as separate statements, and they share the same predicates and columns.

## Packages

The five libraries target .NET 8. Dependencies point from the client packages to the server and from the server to Core, which has no ASP.NET Core dependency.

```text
InteractiveReport.Core
        ^
        |
InteractiveReport.AspNetCore
        ^
        |
        +-- InteractiveReport.Client.Json
        +-- InteractiveReport.Client.GraphQL
        +-- InteractiveReport.Client.FileDownload
```

| Project or directory | NuGet package | Responsibility |
|---|---|---|
| `src/InteractiveReport.Core` | `AurorisNet.InteractiveReports.Core` | Report and document models, validation, planning, expression binding, SQL generation, schema discovery, execution, and the saved-report and administrator stores. |
| `src/InteractiveReport.AspNetCore` | `AurorisNet.InteractiveReports.AspNetCore` | Registration, configured definitions, connections, startup validation, authorization, document-file synchronization and `IInteractiveReportServer`. |
| `src/InteractiveReport.Client.Json` | `AurorisNet.InteractiveReports` | REST routes, error responses, packaged pages and the embedded browser bundle. |
| `src/InteractiveReport.Client.GraphQL` | `AurorisNet.InteractiveReports.Client.GraphQL` | The query-only GraphQL endpoint. |
| `src/InteractiveReport.Client.FileDownload` | `AurorisNet.InteractiveReports.Client.FileDownload` | The download route and the CSV and HTML renderers. |
| `src/client` | | Source of the report and administration elements. `npm run build:client` bundles it into `Client.Json`. |
| `src/browser-server` | | A JavaScript implementation of the server over SQLite WebAssembly, used only by the live demo. |
| `src/demo` | | The live demo's page and sample data. |
| `samples/Workbench` | | Development host with every package over a seeded SQLite database. |

The three client packages are siblings. Each calls `IInteractiveReportServer` directly; none calls another's endpoint.

## Request model

A request has three inputs:

1. A `ReportDefinition`: the trusted SQL and the server's policy for it.
2. A `ReportState`: the report document.
3. An `InteractiveReportRequestContext`: the caller's `ClaimsPrincipal`, the request's services and a trace id.

The result holds the active table's rows and metadata, and a detached copy of the accepted document. The browser adopts that copy.

### Definitions

`IReportDefinitionStore` resolves a definition by name. The default `ConfigurationReportDefinitionStore` reads the options monitor and returns a validated copy on each lookup, with the connection resolved and the SQL dialect taken from it. Request code cannot change configuration or see a half-reloaded definition.

A definition may hold a default report document. A request's top-level values (`search`, `page`, `activeTable`, `tables`) each replace the default's value, and the engine works on a deep copy.

### Documents and tables

A report document holds a case-insensitive map of named tables and names one as `activeTable`. Each table reads from the reserved source `definition` or from another table:

```text
definition -> orders -> regional-summary -> chart
                \
                 +-> customer-pivot
```

Compilation rejects a missing parent, a cycle, a chain deeper than 64 tables, and a document over the structural limits.

A table has two outputs:

- Its **export**: the relation, the public schema and the labels and formats that a child table reads.
- Its **local result**: column selection, sort order, breaks, aggregates and highlights, used only when the table is the active one.

A child starts from its parent's export. The parent's page size, visible columns and sort order do not change the child's data.

### Operation order

The order of entries in a table's `composables` list has no meaning. `CanonicalTableNormalizer` turns the list into a fixed specification that the engine applies in this order:

```text
parent export
    -> shape (group, pivot or chart), at most one per table
    -> computed columns, in dependency order
    -> filters
    -> labels and formats
    -> search of the active request
    -> selection, sort, breaks, aggregates and highlights of the active table
    -> paging
```

Computed columns may use other computed columns of the same table. The normalizer orders them and rejects a cycle.

Each table's `schema` list in a document is a cache for editors and renderers. The engine derives the schema again on every request, never binds against the cached list, and writes a fresh list into the returned document.

## Server pipeline

```text
adapter
  -> IInteractiveReportServer
  -> report access check
  -> definition copy
  -> action, ownership and feature checks
  -> row restriction and context parameters
  -> ReportExecutor
       -> structural validation and defaults
       -> base schema: discovery or cache
       -> table compilation
       -> expression parsing and typed binding
       -> bound relation plan
       -> SqlKata lowering and result queries
       -> execution in one read scope
       -> row and metadata shaping
  -> success or coded failure
```

### Access checks

When the definition store implements `IReportDefinitionAuthorizationStore`, the server first loads only the report name and its `authorization` block, so a denied request fails before connection strings and SQL are read.

The server then decides the action and resource: saved-report ownership, the administrator check, the feature check and the application's rules. For SQL that contains `{{RowRestriction}}` it calls the row-restriction callbacks, resolves the context parameters, and binds both into a copy of the definition. [Authorization](AUTHORIZATION.md) gives the complete rules.

### Validation

`StateStructureValidator` checks the whole document first: null members, table names that differ only by case, unknown composable kinds, and size limits. Errors name the exact document path. Schema-dependent binding runs only on a document that passed.

### Schema discovery

`SchemaDiscovery` wraps the definition's SQL in a query that returns no rows and reads the column names and types from the reader. `SchemaCache` shares one discovery among concurrent requests for the same report, connection, dialect, SQL and context parameters. A failed discovery is removed so the next request tries again. A configuration reload clears the cache.

The schema of a computed column, a group or a chart follows from the plan. A pivot's columns depend on data, so compiling a pivot runs one bounded query for the distinct column values.

### Planning and binding

`ComposableTableCompiler` compiles the active table and its ancestors, and any table whose `schema` list needs a refresh. An ancestor shared by several tables is compiled once per request.

Binding turns names and expressions into typed relation nodes that are independent of the database: the configured SQL source, references to parent exports, shape, compute, filter and metadata relations, and the request's search. Each node carries its output columns with identity, type, lineage, label, format and source path.

Expressions pass through a lexer, a parser, a typed binder and a dialect-aware emitter. The set of functions and operators is closed. A computed column must produce a value, and a filter or highlight must produce a condition. `NOW()` is one UTC instant per request.

### SQL generation

`SqlKataRelationLowerer` turns the relation tree into SqlKata queries and maps each logical column to a physical alias. SqlKata supplies identifier quoting, parameters and paging syntax for each database.

`CanonicalLocalResultBinder` binds the active table's own instructions. `TerminalExecutionBundleBuilder` then derives the result queries from the one lowered relation: the page or download rows, the row count, the aggregates, the control-break totals and the pivot totals. Highlight conditions run as extra columns of the row query; result shaping turns them into row and cell markers and removes them from the rows.

### Execution

`ReportConnectionManager` opens one connection for the engine's part of a request, sets the session time zone where the definition names one, and starts the read scope: a snapshot transaction for `consistency: "snapshot"`, and no transaction otherwise.

`ReportQueryReader` runs one command for each result set on that connection, in turn. On Oracle with snapshot consistency it runs them as one batch that returns a `REF CURSOR` for each.

Oracle rejects a snapshot read with `ORA-01466` when a queried table's definition changed after the snapshot began. The manager then ends the scope and runs the whole read once more before it reports the error.

The executor enforces the row, page, pivot, chart and document limits before or during these stages.

## Database

The engine composes over the configured query:

```sql
SELECT ...
FROM (
    /* the definition's SELECT */
) ir_base
WHERE /* predicates written by the engine, with bound values */
ORDER BY /* columns resolved against the schema */
```

Nested derived tables separate the phases, so a computed column is available to a later filter. Count and aggregate queries build on the same relation.

The dialects are SQL Server, PostgreSQL, Oracle and SQLite. The dialect belongs to the connection: the server takes it from the provider token or from the connection type, so a definition cannot claim a dialect its connection does not have. For an Oracle connection the server reads the database version and switches to the Oracle 11g dialect when needed, which pages with `ROWNUM` and uses a sequence and trigger for saved-report ids.

SqlKata renders general SQL. The engine owns the differences SqlKata does not cover: expression functions, date arithmetic, boolean conditions, null ordering, median, snapshot setup and Oracle 11g paging.

The packages reference no ADO.NET driver. The host application's driver is found by type name at run time.

## Saved reports

A saved report holds a report document with its report name, owner, visibility, default flag, modification time and origin. It holds no results.

Loading runs the stored document through the same validation, planning and execution as a submitted one. Only loading falls back: from a failed stored document to the stored default, then to the default built from the definition. Listing and loading never write.

`ISavedReportStore` is the storage boundary, and `SqlSavedReportStore` implements it for the four dialects. `IAdministratorStore` holds the administrator grants in a second table on the same connection. The report database and the saved-report store may be one database or two.

A configured document keeps its state in a file. `ConfiguredReportDocumentSynchronizer.EnsureSynced` gives each file a row in the store, so configured and user-created saved reports are listed and selected in one way. [Saved reports](SAVED-REPORTS.md) describes the behavior.

## Browser

The bundle is a client of the JSON routes. `<interactive-report>` owns the working document, the dialogs, rendering, the chart integration, the language and the events. It renders in a shadow root and needs no framework on the page.

The element runs at most one query at a time and collects edits made while that query runs. On success it adopts the document the server returned. On failure it keeps the last accepted document. Server validation is therefore the point at which a user's edit takes effect.

`<interactive-report-admin>` uses the same routes for the administration page. Neither element is a security boundary: the server decides every operation.

## Lifetime and caching

The engine and store services are singletons that hold no request state or hold thread-safe caches. State that belongs to one request lives in the copied definition, the copied document and the request context.

The base-schema cache is the only cache that concerns report data. It holds column metadata and no rows.

A hosted service validates configuration when the host starts: every definition, its connection and dialect, the driver of each `dataSource`, the document-file declarations, the administrator settings and the table names. It opens no database connection. Saved-report storage is resolved when an operation first needs it.

Cancellation tokens pass from the adapter through authorization, discovery, compilation, commands and reading. A cancelled request does not become a `500`.

## Extension points

| Extension point | Purpose |
|---|---|
| `IReportDefinitionStore`, `IReportDefinitionAuthorizationStore` | Supply definitions from another trusted source. |
| `AddConnection` | Create connections in application code. |
| `IContextParameterResolver` | Resolve the trusted parameters of the base SQL. |
| `UseRowRestrictions` | Supply a predicate for each caller. |
| `UseAuthorization`, `UseAspNetCoreAuthorization` | Add application rules for operations. |
| `UseAdministrators`, `AdministratorPolicy` | Decide who administers. |
| `UseUserDirectory`, `IInteractiveReportUserProvider` | Supply account choices to the administration page. |
| `ISavedReportStore` | Replace saved-report storage. |
| `IInteractiveReportDownloadWriter` | Replace the download formats. |
| `IInteractiveReportServer` | Call the server from application code, or build another transport. |

The relation plan and the SQL lowering are internal. [Integration API](API.md) documents each extension point.
