# Windows Authentication sample

An ASP.NET Core host with a Windows-authenticated `orders` report, saved reports, the administration page and downloads. `Program.cs` registers authentication and the authorization callbacks, and `ReportAccess.cs` holds the application's access rules.

## Run

Use Windows with the .NET 10 SDK and Node.js 20 or later. From the repository root:

```sh
npm ci
npm run build:client
dotnet run --project samples/WindowsAuthentication --urls http://localhost:5044
```

Open <http://localhost:5044> in a browser that supports Windows Authentication. The host uses ASP.NET Core Negotiate authentication on Kestrel, and the browser's policy may have to allow integrated authentication for this address. [Microsoft's Windows Authentication guide](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/windowsauth) covers IIS and Kerberos.

The host creates three orders in `App_Data/reports.db`, which Git ignores. Saved reports and administrator grants use the same database, and their tables are created on first use. The process account needs write access to `App_Data`: Windows Authentication identifies the caller, and the host still opens the database as itself.

## Name the first administrator

1. Open <http://localhost:5044/api/reports/whoami> and copy the `identity` value. It normally reads `DOMAIN\username` or `MACHINE\username`.
2. Put the value in `InteractiveReport:Administrators` in `appsettings.json`, with the backslash escaped:

   ```json
   "Administrators": [ "DOMAIN\\username" ]
   ```

3. Restart, save a report from the viewer so there is something to administer, and open <http://localhost:5044/api/reports/admin>.

Matching is case-sensitive, so use the value exactly as `whoami` returns it. Entries from `appsettings.json` are read-only on the administration page. Set `InteractiveReport:WhoamiEnabled` to `false` when the administrators are set up.

Windows Authentication supplies the current caller and no account search. The account pickers on the administration page offer the identities the server already knows. To search a directory, register `UseUserDirectory(...)` as the [Integration API](../../docs/API.md#administration-user-directory) describes.

## Access rules

| Code | Behavior in this sample |
|---|---|
| `AddAuthentication(...).AddNegotiate()` and `UseAuthentication()` | Set the caller's Windows principal. |
| `Reports.Read` policy, named by the report definition | Requires sign-in, and membership of `ReportAccess:ReaderGroup` when that is set. |
| `UseAuthorization(...)` | Requires membership of `ReportAccess:ExporterGroup` for downloads. Allows the other known actions, which the built-in owner and administrator rules still decide. Denies an action it does not know. |
| `UseAdministrators(...)`, registered when `ReportAccess:AdministratorGroup` is set | Makes membership of that group the only source of administrator rights. |
| `RequireAuthorization()` on the mapped routes | Requires sign-in for every API call. The packaged pages and browser assets stay anonymous and hold no report data. |

With the default settings, every authenticated Windows user can read the report and save private reports, and **downloads are denied** until an exporter group is set:

```json
"ReportAccess": {
  "ReaderGroup": "DOMAIN\\Report Readers",
  "ExporterGroup": "DOMAIN\\Report Exporters",
  "AdministratorGroup": ""
}
```

Group checks call `ClaimsPrincipal.IsInRole` on the authenticated principal. An administrator still needs the reader group to open a restricted report and the exporter group to download.

Leave `AdministratorGroup` empty to use the entries of `InteractiveReport:Administrators` and the administration page's Administrators editor. Setting it replaces both, and the page hides the editor.

A callback returns `true` to add no restriction. That does not override a report policy, saved-report ownership or the administrator check. [Authorization](../../docs/AUTHORIZATION.md) describes what a callback receives.

## Verify

In PowerShell 7, while the sample runs:

```powershell
Invoke-WebRequest http://localhost:5044/api/reports/orders/schema -SkipHttpErrorCheck
Invoke-RestMethod http://localhost:5044/api/reports/whoami -UseDefaultCredentials -AllowUnencryptedAuthentication
Invoke-RestMethod http://localhost:5044/api/reports/orders/default -UseDefaultCredentials -AllowUnencryptedAuthentication
```

The first request answers `401` with a `WWW-Authenticate: Negotiate` header. The other two return the caller's identity and three report rows. `-AllowUnencryptedAuthentication` lets PowerShell negotiate over the sample's HTTP address; use HTTPS for a deployed host.

With accounts in the matching groups, check that:

- a reader can query and save a private report, and another reader cannot change it;
- a caller outside the reader group cannot query the report;
- a caller outside the exporter group cannot download, administrator or not;
- an administrator named in `appsettings.json` can manage saved reports and the administrator list;
- saved reports and administrator grants are still there after a restart.

## Use the packages

The sample references the repository's projects. In another application, replace them with package references to `AurorisNet.InteractiveReports` and `AurorisNet.InteractiveReports.Client.FileDownload`, and keep the references to `Microsoft.AspNetCore.Authentication.Negotiate` and `Microsoft.Data.Sqlite`.
