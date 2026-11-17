# Embedding the report

How to place the `<interactive-report>` element in a page and control it: attributes, JavaScript API, events, controls, theming, column renderers, edit links and create buttons.

## Load the element

```html
<script type="module" src="/api/reports/ui/ir.js"></script>

<interactive-report report="orders"></interactive-report>
```

`ir.js` is served by `MapInteractiveReportJson` and defines the element. The element renders in a shadow root with its own styles, so page styles such as a CSS reset do not reach it, and its styles do not reach the page.

When it connects, the element requests the report's schema, its saved-report list, and one document with data: the saved report named by `saved-report`, or the report's default. It renders that response without a second query. The server authorizes each of these requests.

The chart renderer, `ir-chart.js`, loads the first time a report shows a chart.

## Attributes and properties

| Attribute | Property | Meaning |
|---|---|---|
| `report` | | Required. The report name. Changing it loads the other report. |
| `saved-report` | | Id of the saved report to open. A missing or hidden id opens the default with a warning. |
| `api-base` | `apiBase` | Prefix of the JSON routes, relative or absolute. Default: the folder above the module URL, so `/api/reports/ui/ir.js` gives `/api/reports`. |
| `download-base` | `downloadBase` | Prefix of the download route. Default: `api-base` with a final `reports` segment replaced by `download`, otherwise `/api/download`. |
| `report-title` | `reportTitle` | Shows a heading. An empty value uses the report's title. Without the attribute there is no heading. |
| `empty-message` | `emptyMessage` | Text shown when the result has no rows. |
| `controls` | `controls` | The controls to show: names separated by spaces, or an array on the property. An empty list hides every optional control. Without it, the report's `features` decide. |
| `initial-page-size` | `initialPageSize` | Page size of the first load. It does not change the paging of a saved report opened by `saved-report`. |
| `lang` | | Language of the UI. See [Language](#language). |
| `theme` | `theme` | `light` or `dark`. Without it the element follows the page. See [Dark mode](#dark-mode). |
| `disabled` | `disabled` | Makes every control of the element inert. |
| `stylesheet` | `styleSheet` | URL of a stylesheet to load inside the shadow root. `null` removes it. |
| | `initialDocument` | A report document to run on the first load in place of the default. |
| | `columnPresentation` | Per-column `label`, `helpText` and `hideLabel`, keyed by column name. |
| | `editLink`, `createLink` | Replace the definition's [link settings](#edit-links-create-buttons-and-column-overrides). `null` hides the link and `undefined` restores the definition's. |
| | `printHandler` | A function that adds **Print…** to the Actions menu. See [Downloads and printing](#downloads-and-printing). |
| | `reportId` | Read-only. Id of the saved report the working document belongs to, as a string, or `null`. |
| | `definitionName` | Read-only. The active report name. |

Property values are copied on the way in and out, so changing an object after assigning it has no effect.

### Configuring several instances

Each element is configured by itself. A page, a CMS block or an application shell can create them in code:

```js
const report = document.createElement("interactive-report");
report.setAttribute("report", "orders");
report.reportTitle = "Large orders";
report.initialPageSize = 10;
report.controls = ["search", "columns", "filter", "sort", "pagination"];
report.initialDocument = {
  activeTable: "base",
  tables: { base: { from: "definition", composables: [
    { kind: "filter", filters: [{ expr: "AMOUNT >= 500" }] }
  ] } }
};
report.columnPresentation = { CUSTOMER: { label: "Customer name" } };
document.querySelector("main").append(report);
```

The first load uses `saved-report` when set, then `initialDocument`, then the report's default. An `initialDocument` is posted to the query route, and an invalid one shows an error. `initialPageSize` overrides the document's page size and is limited by the report's `maxPageSize`.

Changing the title, controls, empty message, column presentation or links updates the display without a query. A changed `initialDocument` or `initialPageSize` applies the next time the element loads a report. To replace the view of a running element, call `submitReportDocument`.

`columnPresentation` and the link properties change what the user sees and nothing about what the server allows. An `editLink` override may only use placeholders that the definition's `editLink` already names, because the server sends only those column values with each row. Link URLs must be relative or use `http` or `https`.

The Workbench page [`embedding.html`](../samples/Workbench/wwwroot/embedding.html) shows two elements over one report.

## JavaScript API

The document, list-of-values and export methods work once the first load has completed. Before that they throw an `InvalidStateError`.

| Method | Result |
|---|---|
| `getReportDocument()` | A copy of the working report document: the last accepted document plus edits that wait for a query in flight. |
| `submitReportDocument(document)` | Runs the document and adopts the result. Resolves to a copy of the result, or `undefined` when the query was cancelled or replaced. A failure restores the previous document and rejects. |
| `getListOfValues({ document, table, column, search, signal })` | Resolves to `{ table, column, type, items, truncated }` with at most 50 distinct values. `document` and `table` default to the element's current ones. |
| `getExport(format = "csv", { signal })` | Resolves to `{ blob, filename, contentType, truncated }` without starting a browser download. `format` is `csv` or `html`. |
| `setControlEnabled(name, enabled)` | Shows or hides one control. `null` returns to the report's `features`. Returns the effective state. |
| `setControlOverrides(overrides)` | Applies several overrides at once. Returns the override map. |
| `clearControlOverrides()` | Removes every override. |
| `isControlEnabled(name)` | The effective state of one control. |
| `getControlOverrides()` | A copy of the override map. |

```js
const report = document.querySelector("interactive-report");

const state = report.getReportDocument();
state.search = "urgent";
state.page.index = 1;
const result = await report.submitReportDocument(state);
console.log(result?.rows);

const values = await report.getListOfValues({ column: "STATUS", search: "pen" });
console.log(values.items);
```

Documents are plain objects. Parse a JSON string before passing it. [Report documents](API.md#report-documents) describes the format.

`search` in `getListOfValues` is a partial match that ignores case. The packaged value picker turns the chosen text into an exact condition, `LOWER(column) = LOWER(value)`, or into `WILDCARD_MATCH(column, pattern)` when the typed text contains `*`.

## Events

All events bubble and cross the shadow boundary.

| Event | When | `detail` |
|---|---|---|
| `ir-before-query` | Before the element posts a document to the query route. Cancelable. | `{ document, source, requestId, signal }` |
| `ir-query-complete` | After a load or query result is rendered. | `{ document, result, submitted, source, requestId }` |
| `ir-edit` | The user activates a row's edit link. Cancelable. | `{ url, row }` |
| `ir-create` | The user activates the create button. Cancelable. | `{ url }` |
| `ir-action` | The user clicks a button in an action column. | `{ command, row, column }` |

`source` is `initial`, `saved-report`, `user`, `host` or `refresh`.

A listener of `ir-before-query` may change `detail.document` during the event: the element sends the changed document. `preventDefault()` cancels the query. The first load and saved-report loads run on the server and fire only `ir-query-complete`, with `submitted` set to `null`.

```js
report.addEventListener("ir-before-query", event => {
  event.detail.document.search = event.detail.document.search?.trim();
});

report.addEventListener("ir-query-complete", event => {
  auditReportQuery(event.detail.source, event.detail.document);
});
```

The detail of `ir-query-complete` is a copy. To act on it, submit a changed copy with `submitReportDocument`.

### One query at a time

The element runs at most one query. An edit on an idle element is sent at once. Edits made while a query is in flight collect in the working document. When the query returns, its result is rendered and `ir-query-complete` fires, and one more query then carries the collected edits.

A new load, a saved-report load and `submitReportDocument` cancel a query in flight and discard collected edits. A download posts the working document and leaves the query alone.

## Client controls

A report's `features` setting decides which controls the element shows at first. The page can change that:

```js
report.setControlEnabled("filter", true);
report.setControlEnabled("download", false);
report.setControlEnabled("filter", null); // follow the report's features again

report.setControlOverrides({ search: true, sort: false, savedReports: true });
report.clearControlOverrides();
```

The control names are the feature tokens: `search`, `columns`, `rename`, `columnSettings`, `filter`, `sort`, `pagination`, `controlBreak`, `highlight`, `aggregate`, `compute`, `groupBy`, `pivot`, `chart`, `savedReports` and `download`. Case is ignored.

Precedence is: an override set by `setControlEnabled` or `setControlOverrides`, then the `controls` list, then the report's `features`. Overrides stay when `report` changes.

**Controls are presentation.** Showing a control gives the user no permission: the server still refuses a download or a save that the report's `features` or the application's rules do not allow. Hiding a control removes no permission either. See [Features](API.md#features).

`disabled` makes the whole element inert for a while and keeps the overrides:

```js
report.disabled = true;
await performHostOperation();
report.disabled = false;
```

## Downloads and printing

**Actions → Download** offers CSV and HTML. Both need the file-download package on the server: see [File downloads](API.md#file-downloads). The element posts its working document to `{download-base}/{report}/{format}`.

`getExport` returns the same file to the page, which decides what to do with it:

```js
const controller = new AbortController();
const file = await report.getExport("csv", { signal: controller.signal });
await uploadReport(file.blob, file.filename, file.contentType);
```

`truncated` is `true` when the report's row cap cut the file.

**Actions → Print…** appears when the page assigns a `printHandler`, and follows the `download` control. The element has no printing of its own: the handler receives the element and opens the application's print flow. A rejected promise shows in the report's error banner. Assign `null` to remove the entry.

```js
report.printHandler = async element => {
  await openApplicationPrintDialog(await element.getExport("html"));
};
```

## Theming

Set theme properties on the element, and style its parts from the page:

```css
interactive-report {
  --ir-accent: #7c3aed;
  --ir-accent-soft: #f3e8ff;
  --ir-font: Inter, system-ui, sans-serif;
  --ir-radius: 0.5rem;
}

interactive-report::part(toolbar) {
  padding: 1rem;
}
```

| Kind | Names |
|---|---|
| Colours | `--ir-accent`, `--ir-accent-soft`, `--ir-border`, `--ir-border-light`, `--ir-bg`, `--ir-bg-soft`, `--ir-bg-header`, `--ir-text`, `--ir-text-muted`, `--ir-danger` |
| Shape and type | `--ir-radius`, `--ir-font`, `--ir-font-size` |
| Charts | `--ir-chart-1` to `--ir-chart-8` (series colours; the first is the single-series colour), `--ir-chart-grid`, `--ir-chart-text` |
| Parts | `surface`, `heading`, `toolbar`, `notices`, `chips`, `table-container`, `table`, `chart-container`, `pager`, `menu`, `submenu`, `dialog` |

### Dark mode

The element has a dark palette. It chooses a theme in this order:

1. `theme="dark"` or `theme="light"` on the element, or the `theme` property.
2. An ancestor with `data-theme`, `data-bs-theme` or `theme` set to `dark` or `light`, or with the class `dark` or `light`.
3. The browser's `prefers-color-scheme`.

```html
<interactive-report report="orders" theme="dark"></interactive-report>
```

Charts, menus and dialogs follow the theme.

### Language

The element includes English (`en`) and Canadian French (`fr-CA`). It uses the nearest `lang` attribute, on itself or an ancestor, then the page's language, then the browser's languages, then English. Every French variant uses the Canadian French text.

```html
<interactive-report lang="fr-CA" report="orders"></interactive-report>
```

The language covers the UI text, error messages, and the formatting of numbers and dates. Report titles, column labels and data come from the application and stay as they are.

The packaged viewer and administration pages take their language from ASP.NET Core request localization when the application uses it, otherwise from the `Accept-Language` header.

### Host stylesheets and CSS classes

`stylesheet` loads one stylesheet inside the shadow root, after the packaged styles, so its rules can target the report's internals and stay out of the page:

```html
<interactive-report report="orders" stylesheet="/css/orders-report.css"></interactive-report>
```

```css
.amount-column { font-variant-numeric: tabular-nums; }
.emphasized { font-weight: 700; }
```

Users apply such classes to a column under **Column Settings → CSS Classes**. The classes go on the column's heading, data cells and aggregate cells. A column takes at most 20 class names. A name starts with a letter or `_`, continues with letters, digits, `_` or `-`, and must not start with `ir-`.

A report document can therefore choose among the application's classes. It cannot carry CSS or name a stylesheet. The page's Content Security Policy applies to the stylesheet request.

## Column renderers

**Column Settings → Display As** offers three renderers, stored in the column's `formats` entry:

| `displayAs` | Renders | Uses |
|---|---|---|
| (none) | The value as text. | |
| `link` | A link. | `urlColumn` for the address, `textColumn` for the text. |
| `image` | An image. | `urlColumn` for the address. |
| `action` | A button that fires `ir-action`. | `command`, and `keyColumn` for the row key. |

- Link addresses may be relative or use `http`, `https`, `mailto` or `tel`. Image addresses may be relative or use `http` or `https`. Any other address, such as `javascript:` or `data:`, shows as plain text.
- The source columns need not be visible. The server checks them against the schema and sends them as hidden row data.
- `action` is set in a definition's `defaultState` or a document file. Column Settings does not offer it and keeps it when the user changes other settings. A row whose value is `NULL` shows no button. The administration page's listing uses this renderer.
- A CSV download writes a link's text, an image's address and an action's label.

## Edit links, create buttons and column overrides

A report definition can add an edit link to each row, a create button to the toolbar, and per-column settings:

```json
"orders": {
  "dataSource": "MainDb",
  "sql": "SELECT ORDER_ID, CUSTOMER, AMOUNT, NOTES FROM ORDERS",
  "editLink": {
    "urlTemplate": "/orders/{ORDER_ID}/edit",
    "label": "Edit order"
  },
  "createLink": {
    "url": "/orders/new",
    "label": "New order"
  },
  "columns": {
    "AMOUNT": { "helpText": "Order total before tax." },
    "NOTES": { "hideLabel": true, "sortable": false, "filterable": false }
  }
}
```

### Edit link

| Setting | Default | Meaning |
|---|---|---|
| `urlTemplate` | required | The address, with at least one `{COLUMN}` placeholder. At most 2048 characters. |
| `label` | `Edit` | Accessible name and tooltip of the pencil. |
| `target` | `_self` | Browsing target. `_blank` adds `rel="noopener"`. |
| `mode` | `navigate` | `navigate` renders a link. `event` renders a button that only fires `ir-edit`. |

The pencil is the first column of the Grid view. It has no heading, and appears in no column list, search, sort, filter, download or other view.

Placeholders name columns of the base query. The server sends their values with each row, hidden when the column is, and the element encodes them into the address. A row with a `NULL` placeholder value shows no pencil. When a placeholder names a column the query does not return, the server logs a warning and the report has no edit column.

### Create button

| Setting | Default | Meaning |
|---|---|---|
| `url` | required in `navigate` mode | A constant address. Placeholders are rejected. |
| `label` | `Create` | Button text. |
| `target` | `_self` | Browsing target. |
| `mode` | `navigate` | `navigate` renders a link. `event` renders a button that only fires `ir-create`. |

The button follows **Actions** on the toolbar in every view. Configuring it shows it: `features` and control overrides do not hide it.

Both addresses are trusted configuration, so a custom scheme such as `myapp:orders/{ORDER_ID}` or a named target passes through to the browser.

### Opening the application's own editor

Both controls fire a cancelable event on every activation, in either mode. In `navigate` mode the link then follows its address unless a listener calls `preventDefault()`. In `event` mode the event is the whole behavior, and `createLink.url` may be left out.

```js
report.addEventListener("ir-create", event => {
  event.preventDefault();
  openOrderEditor(null);
});

report.addEventListener("ir-edit", event => {
  event.preventDefault();
  openOrderEditor(event.detail.row.ORDER_ID);
});

// After the application saves, run the current document again. The grid shows the change
// and keeps the user's search, filters, sort and page.
await report.submitReportDocument(report.getReportDocument());
```

`event.detail.row` includes the hidden placeholder columns. The Workbench page `crud.html` and the live demo use this pattern for a slide-over editor.

### Column overrides

| Setting | Meaning |
|---|---|
| `label` | The column's display label. Setting it here and in `columnLabels` for one column is an error. |
| `hideLabel` | Shows the table heading without text. Menus and dialogs keep the label. |
| `sortable: false` | Removes sorting and control breaks for the column. |
| `filterable: false` | Removes filtering for the column. |
| `helpText` | A note at the bottom of the column's heading menu. At most 1000 characters. |

The server enforces `sortable` and `filterable`: it moves a sort, break or filter that breaks the rule from a submitted document into the result's `ignored` list. A saved report made before the restriction still loads. A `defaultState` that sorts or breaks on a column marked `sortable: false` stops startup.
