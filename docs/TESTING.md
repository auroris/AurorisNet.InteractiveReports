# Testing

The test suites, what each needs, and how to set up the databases for the live-database tests.

## Suites

| Command | Suite | Needs |
|---|---|---|
| `dotnet test` | `InteractiveReport.Core.Tests` and `InteractiveReport.AspNetCore.Tests`, on in-memory and file-backed SQLite. Also the live-database project, whose tests skip without their variables. | .NET 10 SDK |
| `npm test` | Builds the client, then runs the three suites below. | Node.js |
| `npm run test:unit` | Element tests in `tests/ui/*.test.js`, on happy-dom. | Node.js |
| `npm run test:server` | Tests of the demo's `src/browser-server`. | Node.js |
| `npm run test:docs` | Documentation checks in `tests/docs`. | Node.js |
| `npm run test:e2e` | Playwright tests in `tests/ui/e2e`, against Workbench. | Chromium |
| `npm run test:all` | `npm test` and `npm run test:e2e`. | |

CI runs the Core and ASP.NET Core projects, the element tests, the browser-server tests and the documentation checks on each pull request. The Playwright and live-database tests run on a developer's machine.

### What the .NET projects cover

- **Core**: exact SQL and parameter order for each of the four dialects, the expression parser and emitter, document validation, SQL-safety cases, the saved-report and administrator stores, the CSV writer and HTML renderer, and end-to-end runs on SQLite.
- **ASP.NET Core**: the mapped routes on a real local server: authorization and denial responses, saved reports across a restart, configured documents, row restrictions, GraphQL, downloads and OpenAPI metadata. One test confirms that a host without saved-report storage creates no database, file or table.

### Documentation checks

`npm run test:docs` reads every Markdown file in the repository and fails when:

- a relative link or a heading anchor does not resolve;
- an `npm run` command names a script that `package.json` does not have;
- a code fence has no language, or is not closed;
- a page uses a retired API name.

`DocumentationContractTests` in the ASP.NET Core project compares the documentation with the code. It requires `docs/API.md` to list every mapped route with its method and every feature token, `docs/AUTHORIZATION.md` to list every `InteractiveReportAction`, and `docs/GRAPHQL.md` to list every argument of the `report` field with its type.

## Browser tests

```sh
npx playwright install chromium
npm run test:e2e
```

Playwright starts a new Workbench process on `http://127.0.0.1:5042`, so stop a running Workbench first. Saved reports go to `test-results/ui/workbench-saved.db`, which keeps the tests away from `samples/Workbench/App_Data`. A failed test keeps its trace and screenshot under `test-results/ui`.

The tests cover loading and querying, search, paging, CSV download, charts, saved reports across a page reload, the administration page, and what a non-administrator may do. They create saved reports with random names and delete them even when a test fails. The [tests' README](../tests/ui/e2e/README.md) describes the files.

## Live-database tests

`tests/InteractiveReport.Live.Tests` runs the engine against real SQL Server, Oracle and PostgreSQL servers. These tests run on a developer's machine and never on GitHub. Run them before a release that changes SQL generation, execution or the stores.

A test runs only when its variable holds a connection string. Otherwise it skips.

| Variable | Database |
|---|---|
| `IR_TEST_SQLSERVER` | SQL Server |
| `IR_TEST_ORACLE` | Oracle |
| `IR_TEST_POSTGRES` | PostgreSQL |

```powershell
$env:IR_TEST_SQLSERVER = "Server=<host>;Database=irtest;User Id=irtest;Password=<password>;TrustServerCertificate=True"
$env:IR_TEST_ORACLE    = "User Id=irtest;Password=<password>;Data Source=<host>:1521/XEPDB1"
$env:IR_TEST_POSTGRES  = "Host=<host>;Port=5432;Database=irtest;Username=irtest;Password=<password>"
dotnet test tests/InteractiveReport.Live.Tests
```

`run-live-tests.ps1` in the repository root sets the three variables for the maintainer's test machine, checks that each database port answers, and runs the project. `-Filter` passes a `dotnet test --filter` expression.

> [!WARNING]
> Point the variables at scratch databases. The tests create, fill and drop tables, and the test account needs rights that a report account should never have.

### What the tests do

- Create the table `IR_TEST_ORDERS` when it is missing or has another shape, and fill it with the same ten rows before each run. The table stays between runs.
- Run filters, search, highlights, aggregates, control breaks, computed columns, date functions, group by, pivot, context parameters and downloads against it. The expected numbers are the same on every database.
- Run the saved-report scenario through the ASP.NET Core host for each database: save a document, restart, load it.
- Run the saved-report store tests in `IR_SAVED_REPORTS_TEST` and the administrator store tests in `IR_ADMINISTRATORS_TEST`, and drop both tables afterwards.
- Run the engine tests and both store tests again in Oracle 11g mode against the same Oracle server. A guard fails any statement that uses syntax Oracle 11g lacks or an identifier longer than 30 bytes.

Sorting and comparing text follow each database's collation. Text order matches across databases only when each test database uses a binary collation.

On PostgreSQL the tests create `IR_TEST_ORDERS` without quotes, so the names fold to lower case. That is intended: it tests the engine's case-insensitive column matching.

### SQL Server setup

1. Install SQL Server with mixed-mode authentication, enable TCP/IP, and open port 1433 in the firewall.
2. Create the database and login, as `sa`:

   ```sql
   CREATE DATABASE irtest;
   CREATE LOGIN irtest WITH PASSWORD = '<password>';
   USE irtest;
   CREATE USER irtest FOR LOGIN irtest;
   ALTER ROLE db_owner ADD MEMBER irtest;
   USE master;
   ALTER DATABASE irtest SET ALLOW_SNAPSHOT_ISOLATION ON;
   ```

3. Check that snapshot isolation is on. The state must be `ON`, not `IN_TRANSITION_TO_ON`:

   ```sql
   SELECT snapshot_isolation_state_desc FROM sys.databases WHERE name = N'irtest';
   ```

The snapshot test skips, and names this setting, when the database has it off. `TrustServerCertificate=True` in the connection string accepts the server's self-signed certificate, which is acceptable for a test machine only.

### Oracle setup

1. Install Oracle Database XE. Its default pluggable database service is `XEPDB1`, on port 1521.
2. Create the test user, as `system`:

   ```sql
   CREATE USER irtest IDENTIFIED BY "<password>";
   GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE TRIGGER TO irtest;
   ALTER USER irtest QUOTA UNLIMITED ON USERS;
   ```

`CREATE SEQUENCE` backs the identity column of the saved-report table. Without it, creating the table fails with `ORA-01031` and every Oracle saved-report test fails. `CREATE TRIGGER` is needed by the Oracle 11g store tests, which skip and name the grant when it is missing.

When the snapshot tests skip with a message that the instance rejects a read-only snapshot of the seed table, the server's clock or time zone changed while the instance was running. Oracle then answers `ORA-01466`. The tests recreate the seed table once. If that does not help, restart the Oracle instance and listener.

### PostgreSQL setup

1. Install PostgreSQL. Set `listen_addresses` in `postgresql.conf` to cover the network the tests come from, add a `host all all <test-network>/24 scram-sha-256` rule to `pg_hba.conf`, and open port 5432 in the firewall.
2. Create the role and database, as `postgres`:

   ```sql
   CREATE ROLE irtest LOGIN PASSWORD '<password>';
   CREATE DATABASE irtest OWNER irtest;
   ```
