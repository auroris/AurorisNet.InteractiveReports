# Minimal sample

An ASP.NET Core host with one anonymous `orders` report over SQLite. It uses the packaged viewer page and the JSON API, and configures no saved-report storage.

## Run

You need the .NET 10 SDK and Node.js 20 or later. From the repository root:

```sh
npm ci
npm run build:client
dotnet run --project samples/Minimal --urls http://127.0.0.1:5043
```

Open <http://127.0.0.1:5043>. The root redirects to `/api/reports/orders/view`. Stop the server with Ctrl+C.

At startup the host creates three orders in a shared in-memory SQLite database and keeps one connection open so that report requests can read them. The data is gone when the process exits.

## Check the API

```sh
curl http://127.0.0.1:5043/api/reports/orders/schema
curl http://127.0.0.1:5043/api/reports/orders/default
```

`result.rows` in the second response holds the three orders.

## What to copy

| File | Shows |
|---|---|
| `Minimal.csproj` | References to the server, the JSON client and `Microsoft.Data.Sqlite`. |
| `Program.cs` | The two service registrations and the endpoint mapping. |
| `appsettings.json` | The connection string, its provider name and the report's SQL. |

In an application outside this repository:

- Replace the two project references with one package reference to `AurorisNet.InteractiveReports`, and keep the reference to `Microsoft.Data.Sqlite`. The package contains the browser bundle, so the npm steps are not needed.
- To use an existing SQLite database, remove the connection's `using` block and the `CreateSampleData` method from `Program.cs`, set `ConnectionStrings:SampleDb` to your connection string, and change the report's SQL. `ConnectionStrings:SampleDb_ProviderName` tells the server which driver the connection string is for.
- For a report that requires sign-in, add the application's authentication and remove `allowAnonymous` from the report.

[Getting started](../../docs/GETTING-STARTED.md) continues from here.
