# Saved reports

A saved report stores a user's report document: search, filters, columns, sorting, breaks, aggregates, computed columns, highlights and views. This guide covers storage, visibility, defaults, documents kept in source control, and the administration page. The [User guide](USER-GUIDE.md#saved-reports) describes saving from the report itself.

## Terms

| Term | Meaning |
|---|---|
| Report | One definition under `InteractiveReport:Reports`. Its saved reports belong to it. |
| Private saved report | Visible to its owner and to administrators. |
| Global saved report | Visible to every caller who may view the report. |
| Default | The one saved report a first-time viewer opens. It is always global. |
| Configured document | A saved report whose state is a JSON file named in `documentFiles`. It is global and read-only. |
| Built default | The document the server builds from the definition's `defaultState` when a report has no stored default. It is never stored. |

Every saved report has a database-generated integer id that is unique across all reports.

## Enable storage

The package stores nothing until the application names a database:

```json
{
  "InteractiveReport": {
    "SavedReports": {
      "dataSource": "MainDb",
      "tablePrefix": "MYAPP_"
    }
  }
}
```

`dataSource` takes a `ConnectionStrings` name or a literal connection string, with the same [provider rules](API.md#data-sources) as a report. `connection` names a factory registered with `AddConnection` instead. The [settings table](../README.md#interactivereport) lists the table-name options.

The example uses two tables, `MYAPP_IR_SAVED_REPORTS` and `MYAPP_IR_ADMINISTRATORS`. With `autoCreate` at its default, the server creates each when it is first needed.

When operators create the tables, set `autoCreate` to `false` and run the statements the stores publish for the configured dialect:

```csharp
using InteractiveReport.Core.Authorization;
using InteractiveReport.Core.SavedReports;

var saved = app.Services.GetRequiredService<ISavedReportStore>() as SqlSavedReportStore;
var grants = app.Services.GetRequiredService<IAdministratorStore>() as SqlAdministratorStore;

foreach (var statement in SqlSavedReportStore.SchemaStatements(saved!.Configuration))
    Console.WriteLine(statement);
foreach (var statement in SqlAdministratorStore.SchemaStatements(grants!.Configuration))
    Console.WriteLine(statement);
```

The current release does not migrate an older table layout.

Without storage, reports still work: a report's saved-report list is empty and it opens with its built default. Every other saved-report and administration operation answers `500` until storage is configured. A configured store that cannot be reached answers `500` for saved-report operations, and report queries are unaffected.

## Ownership and visibility

The owner of a saved report is the identity value of the caller who created it. [Administrators](AUTHORIZATION.md#administrators) explains how the server reads that value. Comparison is ordinal and case-sensitive.

`GET /api/reports/{name}` returns:

- every saved report of the report, to an administrator;
- global, default and configured saved reports, and the caller's own, to everyone else.

Titles have 1 to 200 characters, and comparison ignores case. A global title must differ from every other public title of the report, including the titles of configured documents. A private title must differ from those public titles and from its owner's other private titles. A collision answers `409`. Configured documents themselves may repeat any title.

### Defaults

A report has at most one stored default.

- Selecting a saved report as the default publishes it. The former default stays global.
- An update cannot clear the default flag. Select another default instead.
- Deleting the default is allowed for its owner and for administrators. The report then opens with its built default until another is selected.
- When a configured document declares `default: true`, configuration owns the choice, and an API request to replace it answers `409`.

## Loading

`GET /api/reports/{name}/{id}` and `GET /api/reports/{name}/default` return `{ summary, result }`: the saved report's metadata and its document with data. A stored document that cannot be read or run falls back to the stored default, then to the built default. [Loading](API.md#loading) gives the rules.

Listing, loading and downloading never change storage. Only a save, an update, a delete, an upload and synchronization write.

Saving uses `POST /api/reports/{name}/saved` for a new saved report and `PUT /api/reports/{id}` for an existing one. The server validates the state against the report's current columns before it stores it.

## Source-controlled documents

A report can ship saved reports as files. List them in the definition, relative to the application's content root:

```json
{
  "InteractiveReport": {
    "Reports": {
      "orders": {
        "dataSource": "MainDb",
        "sql": "SELECT ORDER_ID, CUSTOMER, AMOUNT FROM ORDERS",
        "documentFiles": [
          "ReportDocuments/orders.default.json",
          "ReportDocuments/orders.finance.json"
        ]
      }
    }
  }
}
```

Each file holds a title, an optional default flag and a report document:

```json
{
  "title": "Finance",
  "default": false,
  "state": {
    "activeTable": "base",
    "tables": {
      "base": {
        "from": "definition",
        "composables": [
          { "kind": "filter", "filters": [ { "expr": "AMOUNT > 0" } ] },
          { "kind": "sort", "sorts": [ { "col": "AMOUNT", "dir": "desc" } ] }
        ]
      }
    }
  }
}
```

- `title` is required, with 1 to 200 characters.
- At most one file of a report may set `default` to `true`. A second one stops startup.
- The [JSON Schema](../schemas/report-document.schema.json) describes the file. A `$schema` property that points to it gives editors validation and completion.
- Copy the files to the build and publish output. The Workbench project file shows a `Content` rule for its `ReportDocuments` folder.

### Synchronization

A configured document's state stays in its file. The database holds a row with its id, report, file path, title and default flag, so it appears in the saved-report list. The application creates those rows by calling the synchronizer, usually once after the host is built:

```csharp
using InteractiveReport.AspNetCore;

await app.Services.GetRequiredService<ConfiguredReportDocumentSynchronizer>()
    .EnsureSynced();
```

`EnsureSynced` compares the `documentFiles` of every report with the database:

- A file without a row gets one, with the file's title and default flag. A new configured default replaces the stored default, which stays global.
- A row whose file is no longer listed is deleted.
- A file that already has a row keeps the row's title and default flag. Later edits to the file change the state it serves and nothing else.
- A listed file that is missing from disk is logged as a warning and gets no row.

**Nothing else synchronizes.** Listing and loading read the rows as they are, so a new `documentFiles` entry stays invisible until the application calls `EnsureSynced`. Synchronization needs [storage](#enable-storage).

The server reads a file's state when the document is loaded, and reads it again after the file changes. When the file is missing or invalid at that moment, loading falls back like any other failed document and leaves the row in place.

## Administration page

The packaged page lists every saved report of every report. Browse to it under the JSON prefix:

```text
/api/reports/admin
```

Name the first administrator in configuration, using the caller's identity value:

```json
{
  "InteractiveReport": {
    "Administrators": [ "identity-value-of-the-first-administrator" ]
  }
}
```

`InteractiveReport:WhoamiEnabled` serves `GET /api/reports/whoami`, which shows the signed-in caller's identity value. Turn it off once the administrators are set up.

On the page an administrator can publish or unpublish a saved report, select a default, reassign an owner, view the stored state, download and upload document files, and delete. Configured documents can be viewed and downloaded only. The [User guide](USER-GUIDE.md#appendix-the-administration-page) describes each control.

The **Administrators** editor replaces the database administrator list as a whole. Entries from `InteractiveReport:Administrators` are shown and cannot be removed there. The editor is hidden when the application decides administrators with a callback or a policy.

The account pickers on the page search the identities the server already knows and, when the application registers one, its user directory. See [Administration user directory](API.md#administration-user-directory).

To build the page into the application's own layout, load `{prefix}/ui/ir-admin.js` and place `<interactive-report-admin>` in a page.

## Import and export

**Download** on the administration page returns a saved report as a `{ title, default, state }` file, the format of `documentFiles`. To move a layout into source control:

1. Build the layout as a private saved report and check it against live data.
2. Download its file from the administration page.
3. Commit the file and add its path to `documentFiles`.
4. Deploy, call `EnsureSynced`, and check the saved-report list.

**Upload JSON** validates a file's state against the chosen report's current columns and stores it as a private saved report of the administrator who uploads it. The file's `default` flag is ignored. Publish the saved report afterwards if it should be global.

For a report that restricts rows for each caller, a downloaded file and a stored state carry no column lists, so data-derived pivot column names do not travel between callers.

## Before you deploy

- The database account can create the two tables, or operators created them with `autoCreate: false`.
- On Oracle, creating the saved-report table needs `CREATE SEQUENCE`. On Oracle 11g it also needs `CREATE TRIGGER`.
- On SQL Server, tables that operators create keep the `Latin1_General_100_BIN2` collation that the published statements declare for the title key, title scope and identity columns.
- Configured document files are in the published content root, and the application calls `EnsureSynced`.
- The entries of `InteractiveReport:Administrators` equal the identity values the server resolves, and `WhoamiEnabled` is off.
- Backups cover both tables.
- Application rules allow the saved-report actions users need. See [Actions](AUTHORIZATION.md#actions).
