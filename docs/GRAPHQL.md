# GraphQL adapter

`AurorisNet.InteractiveReports.Client.GraphQL` adds a query-only GraphQL endpoint, built on GraphQL.NET. A client lists the reports it may view, lists the saved reports of one report, and runs a saved report with its own paging, search and sorting.

The endpoint runs saved reports only. To create or change one, use the report UI or the [REST routes](API.md#rest-routes), then run it here by id.

## Install and map

```sh
dotnet add package AurorisNet.InteractiveReports.Client.GraphQL
```

```csharp
using InteractiveReport.AspNetCore;
using InteractiveReport.Client.GraphQL;

builder.Services.AddInteractiveReports(builder.Configuration);
builder.Services.AddInteractiveReportGraphQL();

var app = builder.Build();
app.MapInteractiveReportGraphQL("/graphql")
    .RequireRateLimiting("reports");
```

The package brings in the server and engine packages. It works with or without `AurorisNet.InteractiveReports`, and needs [saved-report storage](SAVED-REPORTS.md#enable-storage) to have anything to run.

- The path defaults to `/graphql`. `MapInteractiveReportGraphQL` returns an `IEndpointConventionBuilder` for the usual endpoint conventions.
- The endpoint accepts queries over HTTP GET and POST. Batched requests, form posts and WebSockets are off, and the schema has no mutation or subscription type. Another method, or a WebSocket upgrade, answers `405` with the REST error object and code `IR-1500`.
- Run the application's authentication middleware before the endpoint.
- `AddInteractiveReportGraphQL` registers GraphQL.NET's System.Text.Json serializer for the whole host. An application that serves its own GraphQL.NET schema with another serializer registers that serializer after this call.

The package contains no GraphQL IDE. Workbench adds the `GraphQL.Server.Ui.GraphiQL` package and maps it in Development:

```csharp
using GraphQL.Server.Ui.GraphiQL;

if (app.Environment.IsDevelopment())
{
    app.MapGraphQLGraphiQL("/graphiql", new GraphiQLOptions
    {
        GraphQLEndPoint = "/graphql",
        GraphQLWsSubscriptions = true,
    });
}
```

`GraphQLWsSubscriptions = true` selects the GraphiQL fetcher that opens a WebSocket only for subscriptions. The older fetcher opens one during schema introspection, which this endpoint refuses.

## Schema

```graphql
type Query {
  reports: [InteractiveReportConfiguration!]!
  savedReports(report: String!): [InteractiveReportSavedReport!]!
  report(
    id: ID!
    report: String
    page: Int
    pageSize: Int
    search: String
    sort: [InteractiveReportSortInput!]
  ): InteractiveReportResult
}

input InteractiveReportSortInput {
  col: String!
  dir: InteractiveReportSortDirection   # ASC (default) or DESC
  nulls: InteractiveReportNullPlacement # FIRST or LAST; omitted keeps the database default
}
```

**An operation may select one root field.** Each root field reads the database, so an operation that selects several, through aliases or fragments, fails validation before any resolver runs. The introspection fields `__schema`, `__type` and `__typename` are exempt. An operation may expand at most 256 fragment spreads. The schema executes one field at a time.

### reports

Lists the configured reports the caller may view, ordered by title, like `GET /api/reports`. Each entry has `name` and `title`.

An anonymous caller receives `UNAUTHENTICATED` when every report requires sign-in. A signed-in caller who may view none receives an empty list.

### savedReports

Lists the saved reports of one report that the caller may load, like `GET /api/reports/{name}`: global, default and configured ones, and the caller's own. An administrator receives all of them. An unknown or hidden report answers `NOT_FOUND`.

```graphql
{
  savedReports(report: "orders") {
    id
    title
    isDefault
    isGlobal
    mine
    isReadOnly
    modifiedUtc
  }
}
```

`id` is the value the `report` field accepts. It is a string here, as every GraphQL `ID` is, and a JSON number in the REST routes.

### report

Runs one saved report.

| Argument | Meaning |
|---|---|
| `id` | The saved report's id. |
| `report` | Optional report name. When given, the saved report must belong to that report, as in `GET /api/reports/{name}/{id}`; otherwise the answer is `NOT_FOUND`. |
| `page` | Page number, from 1. |
| `pageSize` | Page size. `0` returns all rows, up to the report's `maxRows`. Positive sizes are limited by `maxPageSize`. |
| `search` | Replaces the saved search text. An empty string clears it. |
| `sort` | Replaces the sort of the document's active table. An empty list clears it. |

An omitted or `null` argument keeps what the saved report holds. The arguments change a copy of the document: nothing is written back. They are accepted whatever `features` the report offers, as on the REST query route.

A sort on an unknown or unsortable column is dropped and reported in the result's `ignored` list. Check `ignored` when an order seems to have no effect.

When the saved report cannot be read or run, the server tries the report's stored default and then its built default, with the same arguments, as described under [Loading](API.md#loading).

```graphql
query RunSavedReport($id: ID!, $report: String, $page: Int, $pageSize: Int,
                     $search: String, $sort: [InteractiveReportSortInput!]) {
  report(id: $id, report: $report, page: $page, pageSize: $pageSize, search: $search, sort: $sort) {
    columns { name label type computed }
    rows
    page { index size }
    totalRows
    truncated
    ignored { kind detail }
    elapsedMs
  }
}
```

```json
{
  "id": "42",
  "report": "orders",
  "page": 1,
  "pageSize": 100,
  "search": "acme",
  "sort": [{ "col": "ORDER_DATE", "dir": "DESC", "nulls": "LAST" }]
}
```

`rows` is a scalar that holds JSON objects keyed by the names in `columns`. Grouping, pivots and computed columns change the row shape at run time, so rows have no fixed GraphQL type. 64-bit integers and decimals in rows are strings, as in the REST routes, and their column still reports `type: "number"`. `totalRows` and `elapsedMs` are GraphQL `Long` values.

`truncated` is `true` when an unpaged query stopped at `maxRows`.

## Which saved reports to rely on

Every saved report is eligible: private, global, default and configured.

For a report that another application depends on, use a [configured document](SAVED-REPORTS.md#source-controlled-documents). Its state changes only when the file changes. An owner can change or delete an ordinary saved report at any time.

## Authorization

GraphQL uses the same server and the same rules as REST. See [Authorization](AUTHORIZATION.md).

- `reports` emits `ViewReport` for each configured report and leaves out the denied ones.
- `savedReports` emits `ListSavedReports`.
- `report` loads the saved report, checks that the caller may read it (public, owner or administrator), and emits `ReadSavedReport` and `Query`. Application rules receive the saved report's metadata with both. The server then applies the row restriction and context parameters and runs the document.

A saved report the caller may not read answers `NOT_FOUND`, the same as one that does not exist.

## Errors

A failed field appears in the response's `errors` array, normally with HTTP 200.

| Code | Meaning |
|---|---|
| `BAD_USER_INPUT` | An argument is out of range: `id` is not a positive integer, `page` is below 1, `pageSize` is negative, or a sort entry has an empty `col`. |
| `NOT_FOUND` | The report or saved report does not exist, or the caller may not know that it does. |
| `UNAUTHENTICATED` | The operation needs a signed-in caller. |
| `FORBIDDEN` | The caller is signed in and the operation is denied. |
| `REPORT_VALIDATION_FAILED` | The document, with the arguments applied, does not validate against the report's current columns. |
| `INTERNAL_SERVER_ERROR` | Authorization, storage or execution failed unexpectedly. |

An error from the server also carries these extensions:

| Extension | Content |
|---|---|
| `reportErrorCode` | The REST `IR-` code. See [Errors](API.md#errors). |
| `details`, `validation` | The validation paths and messages, when the document is invalid. |
| `traceId` | The id that matches the server log, for an unexpected failure. |

The error message is the English text of the `IR-` code. Database error text and generated SQL are never returned.

## Limits to set in the host

The endpoint runs report queries against the report database. Apply the application's authentication, CORS, request-size, timeout and rate-limiting policies to it, and prefer positive page sizes over `pageSize: 0`.
