# Browser UI tests

Playwright tests that drive the packaged UI in Chromium against a real Workbench host.

```sh
npm run test:e2e
```

The shared configuration, `playwright.config.js`, starts a new Workbench process on `http://127.0.0.1:5042` for each run and never reuses a running one. It stores saved reports in `test-results/ui/workbench-saved.db`, so a run leaves `samples/Workbench/App_Data` alone.

Locators use accessible roles and labels through the elements' open shadow roots. The tests therefore use the UI as a user does and call no element methods.

| File | Covers |
|---|---|
| `application.spec.js` | Report loading, a configured document, query, search, paging, changes of the element's attributes, CSV download, saved reports, the administration page, and what a non-administrator may do. |
| `composition-*.spec.js` | The engine through the browser and server together: operation order independent of the order in the document, a parent table's export against a table's own result, highlight precedence, pivots, format inheritance, rollback after a validation error, CSV content, and break totals across pages. |
| `lov-filter.spec.js` | Filtering through the value picker. |
| `packaged-pages.spec.js` | The packaged viewer and administration pages. |
| `support.js` | Helpers that create temporary private saved reports, so a test can load a deep or hostile document through the UI. |

Tests give their saved reports random names and delete them in `finally` blocks, so they can run in parallel and leave little behind when one fails.
