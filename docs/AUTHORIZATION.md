# Authorization

How the server decides whether a caller may run an operation, and how an application adds its own rules. This is the reference for actions, callbacks, administrators, row restrictions and denial responses.

Interactive Reports uses the identity the host application establishes. The application's authentication middleware sets `HttpContext.User`, and the server passes that `ClaimsPrincipal` to every check. For each operation it asks one question:

```text
ClaimsPrincipal + action + resource -> allow or deny
```

The JSON, GraphQL and file-download packages all call `IInteractiveReportServer`, so one set of rules covers every transport. Two kinds of route skip the checks because they return no report data: the packaged pages and browser assets, and the opt-in `whoami` diagnostic. What the browser UI shows or hides never counts as a check.

> [!WARNING]
> These rules make a public deployment possible, and a trusted network is still the safer place for reports. For a host on the internet, use a reporting database or read replica and a read-only, least-privileged database account.

## The built-in rules

1. **A report is public or it requires sign-in.** `authorization.allowAnonymous: true` admits everyone. Any other report admits any authenticated caller. To admit only some of them, name an ASP.NET Core `policy` on the definition or register an application rule.
2. **Administrators manage saved reports.** They list every saved report, publish and unpublish, select defaults, reassign owners, delete, download and upload document files, and edit the administrator list. [Administrators](#administrators) explains who they are.
3. **Saved reports have owners.** A private saved report is visible to its owner and to administrators. A global, default or configured one is visible to everyone who may see the report. The owner or an administrator changes or deletes it. Only an administrator publishes, selects a default or reassigns.

Application rules can deny any operation, including an administrator's. They cannot allow what the built-in rules deny.

## Order of checks

A request passes these checks in order. Each must allow it.

1. Endpoint conventions on the route group, such as `MapInteractiveReportJson(...).RequireAuthorization(...)`.
2. The report definition's `authorization` block.
3. The saved-report ownership rules and, for administrator operations, the administrator check.
4. Every application rule registered with `UseAuthorization` or `UseAspNetCoreAuthorization`.
5. The row restriction, for a report whose SQL contains `{{RowRestriction}}`.
6. Context parameters, which the server binds into the base SQL.

The host sets up authentication as usual:

```csharp
builder.Services.AddAuthentication(/* scheme */).AddCookie();
builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

app.MapInteractiveReportJson("/api/reports");
```

## Report policies

A policy on the definition admits only the authenticated callers who pass it:

```json
{
  "InteractiveReport": {
    "Reports": {
      "orders": {
        "dataSource": "MainDb",
        "sql": "SELECT * FROM ORDERS",
        "authorization": { "policy": "MayAccessOrders" }
      }
    }
  }
}
```

- A caller who fails the policy receives `404`, so the report's existence stays hidden.
- `allowAnonymous` and `policy` cannot be combined.
- Naming a policy requires `builder.Services.AddAuthorization()`. Without it the host fails at startup.
- A policy name the policy provider does not know at startup is logged as a warning, because a dynamic provider may resolve it later. A request that meets an unknown policy answers `500`.

Use a report policy for access to the whole report, and an application rule to tell operations apart, such as query from export.

## Actions

Every protected operation emits one or more actions. An application rule receives each one.

| Action | Emitted when | Built-in rule |
|---|---|---|
| `ViewReport` | Listing reports, or reading a report's schema | The report's `authorization` block. |
| `Query` | Running a report document, loading a saved report, or reading a list of values | The report's `authorization` block. |
| `Export` | Downloading a file | The report must offer the `download` feature. |
| `ListSavedReports` | Listing the saved reports of one report | Callers see public documents and their own. Administrators see all. |
| `ReadSavedReport` | Loading one saved report, through REST or GraphQL | Public, owner or administrator. |
| `CreateSavedReport` | Creating a saved report | An authenticated caller, and the `savedReports` feature. |
| `UpdateSavedReport` | Updating a saved report | Owner or administrator. Configured documents are read-only. |
| `DeleteSavedReport` | Deleting a saved report | Owner or administrator. Configured documents cannot be deleted. |
| `PublishGlobalReport` | A create or update changes whether a saved report is global | Administrator. Emitted for publishing and for unpublishing. |
| `SelectDefaultReport` | A create or update selects a new default | Administrator. The new default becomes global, and the former default stays global. |
| `ChangeSavedReportOwner` | A create or update changes the owner | Administrator. |
| `ListAllSavedReports` | An administrator lists a report's saved reports, or uses the built-in `__saved-reports` listing | Administrator. |
| `ListAuthorizationUsers` | Searching accounts on the administration page | Administrator. |
| `ManageAdministrators` | Reading or replacing the administrator list | Administrator. |
| `DownloadReportDocument` | Downloading a saved report as a document file | Administrator. |
| `UploadReportDocument` | Importing a document file | Administrator. The import is private to the caller. |

One request can emit several actions, and every one must be allowed:

- Creating a global saved report emits `CreateSavedReport` and `PublishGlobalReport`.
- An update that changes title, publication, default and owner emits `UpdateSavedReport`, `PublishGlobalReport`, `SelectDefaultReport` and `ChangeSavedReportOwner`.
- Loading a saved report emits `ReadSavedReport` and `Query`.
- Downloading the administrator listing emits `ListAllSavedReports` and `Export`.

The server stops at the first denial, so application rules do not see every action of a denied request. Record audit events where the application accepts an operation, not in a rule.

## The request an application rule receives

A callback receives an `InteractiveReportAuthorizationRequest`. A resource handler receives the action on `InteractiveReportAuthorizationRequirement` and the resource as the handler's resource.

| Member | Meaning |
|---|---|
| `User` | The caller's `ClaimsPrincipal`. |
| `Action` | The `InteractiveReportAction` being decided. |
| `Resource.ReportName` | The report name. |
| `Resource.SourceReportName` | `ReportDefinition.SourceName` when a custom definition store sets it, otherwise the report name. |
| `Resource.SavedReport` | The stored saved report's metadata when the operation concerns one: id, title, owner, global and default flags, and origin. |
| `Resource.Candidate` | The saved report a create, update or upload would store. |
| `RequestServices` | The request's service provider, for scoped services. Callbacks only. |

`Query` and `Export` carry the report name and no report document. Limit rows with a [row restriction](#row-restrictions) or a context parameter, not by inspecting filters.

`Query`, `Export` and list-of-values requests run the document the client posts and never read the saved-report store. A client that loaded a saved report can keep running its copy after the saved report is unpublished or reassigned, as long as it may still query the report.

### The candidate

`Resource.Candidate` is a `SavedReportCandidate` with `Id`, `ReportName`, `Title`, `Public`, `Default`, `Owner`, `State` and `StateChanged`. `Public` is the HTTP field `isGlobal` and `Default` is `isDefault`.

For an update the values are the effective ones after the request is applied to the stored metadata. `State` is the typed `ReportState` the client sent. When the update sends no state, `StateChanged` is `false`, `State` is `null`, and the stored state stays untouched.

A rule may change the candidate. The server validates and stores what the rules leave:

```csharp
using InteractiveReport.AspNetCore;

reports.UseAuthorization((request, cancellationToken) =>
{
    if (request.Action == InteractiveReportAction.CreateSavedReport
        && request.Resource.Candidate is { } candidate)
    {
        // This host never lets a user publish while saving.
        candidate.Public = false;

        var filters = candidate.State?.Tables?.Values
            .SelectMany(table => table.Composables ?? [])
            .Where(composable => composable.Kind.Equals("filter", StringComparison.OrdinalIgnoreCase))
            .SelectMany(composable => composable.Filters ?? [])
            .Count();
        if (filters > 20)
            return ValueTask.FromResult(false);
    }

    return ValueTask.FromResult(true);
});
```

- The base action (`CreateSavedReport`, `UpdateSavedReport` or `UploadReportDocument`) is decided first. Every rule sees the same candidate object.
- The server then derives the administrator actions from the candidate. Setting `Public = false` during a create removes the publication request before the administrator check.
- When a rule adds publication, a default or a new owner, the server decides the matching administrator action before it stores anything.
- A denial discards every change.
- Assigning `Candidate.State` sets `StateChanged`. The state is validated against the report's current schema and stored in canonical form, without unknown JSON members.

## Adding application rules

| Style | Registration | Fits |
|---|---|---|
| [Direct callback](#direct-callback) | `UseAuthorization(...)` | Rules that are small, or that live in one application service. |
| [ASP.NET Core resource handler](#aspnet-core-resource-handler) | `UseAspNetCoreAuthorization()` and an `IAuthorizationHandler` | Applications built on requirements and handlers. |
| [Named policies](#named-policies) | `UseAuthorization(...)` that calls `IAuthorizationService` | Applications with an existing set of policies. |

All registered rules must allow an action. Several callbacks and the resource handler combine with AND:

```csharp
reports
    .UseAuthorization(TenantBoundary)
    .UseAuthorization(LicenceBoundary)
    .UseAspNetCoreAuthorization();
```

### Direct callback

```csharp
using System.Security.Claims;
using InteractiveReport.AspNetCore;

var reports = builder.Services.AddInteractiveReports(builder.Configuration);

reports.UseAuthorization((request, cancellationToken) =>
{
    var allowed = request.Action switch
    {
        InteractiveReportAction.Export => request.User.HasClaim("reports", "export"),
        InteractiveReportAction.CreateSavedReport => request.User.HasClaim("reports", "save"),
        _ => true,
    };

    return ValueTask.FromResult(allowed);
});
```

A callback is asked about every action, so it returns `true` for the actions it does not restrict. The built-in owner and administrator rules still apply to those.

A callback can be asynchronous. Resolve scoped services from the request, never from a variable captured at startup:

```csharp
reports.UseAuthorization(async (request, cancellationToken) =>
{
    var acl = request.RequestServices.GetRequiredService<IReportAcl>();
    return await acl.AllowsAsync(request.User, request.Action, request.Resource, cancellationToken);
});
```

A rule for one report branches on the report name:

```csharp
reports.UseAuthorization((request, _) => ValueTask.FromResult(
    !request.Resource.ReportName.Equals("payroll", StringComparison.OrdinalIgnoreCase)
    || request.User.IsInRole("PayrollReaders")));
```

Return `false` to deny, or throw `InteractiveReportAuthorizationDeniedException`. Any other exception is treated as a failure of the rule and answers `500`.

### ASP.NET Core resource handler

```csharp
using InteractiveReport.AspNetCore;
using Microsoft.AspNetCore.Authorization;

builder.Services
    .AddInteractiveReports(builder.Configuration)
    .UseAspNetCoreAuthorization();

builder.Services.AddScoped<IAuthorizationHandler, ReportAuthorizationHandler>();

public sealed class ReportAuthorizationHandler(IReportAcl acl)
    : AuthorizationHandler<
        InteractiveReportAuthorizationRequirement,
        InteractiveReportAuthorizationResource>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        InteractiveReportAuthorizationRequirement requirement,
        InteractiveReportAuthorizationResource resource)
    {
        if (await acl.AllowsAsync(context.User, requirement.Action, resource))
            context.Succeed(requirement);
    }
}
```

`UseAspNetCoreAuthorization()` registers the ASP.NET Core authorization services and allows nothing by itself. **An action that no handler succeeds is denied**, so a handler must call `context.Succeed(requirement)` for every action the application allows.

### Named policies

A callback can map actions to policies the application already defines:

```csharp
reports.UseAuthorization(async (request, cancellationToken) =>
{
    var policy = request.Action switch
    {
        InteractiveReportAction.Export => "Reports.Export",
        InteractiveReportAction.CreateSavedReport
            or InteractiveReportAction.UpdateSavedReport
            or InteractiveReportAction.DeleteSavedReport => "Reports.Save",
        InteractiveReportAction.ViewReport
            or InteractiveReportAction.Query
            or InteractiveReportAction.ListSavedReports
            or InteractiveReportAction.ReadSavedReport => "Reports.Read",
        _ => "Reports.Administer",
    };

    var authorization = request.RequestServices.GetRequiredService<IAuthorizationService>();
    var result = await authorization.AuthorizeAsync(request.User, request.Resource, policy);
    return result.Succeeded;
});
```

Passing `request.Resource` lets a policy's resource handlers inspect it. This callback already uses ASP.NET Core authorization, so it needs no `UseAspNetCoreAuthorization()`. The `Reports.Administer` policy also suits `InteractiveReport:AdministratorPolicy`.

## Administrators

The first source that the application has set up answers whether a caller is an administrator:

| Order | Source | Set up with |
|---|---|---|
| 1 | Application callback | `reports.UseAdministrators((request, ct) => ...)` |
| 2 | ASP.NET Core policy | `InteractiveReport:AdministratorPolicy` |
| 3 | Built-in lists | `InteractiveReport:Administrators`, and the list an administrator edits on the administration page |

```csharp
reports.UseAdministrators((request, cancellationToken) =>
    ValueTask.FromResult(request.User.IsInRole("ReportAdministrators")));
```

```json
{
  "InteractiveReport": {
    "AdministratorPolicy": "Reports.Administer"
  }
}
```

- **A callback or policy replaces the built-in lists.** While either is set, the configured list and the database list are ignored, the administration page hides its Administrators editor, and `whoami` reports `administratorsManagedByApplication: true`. A callback that answers `false` for everyone locks administration until the application changes it.
- An unauthenticated caller is never an administrator, and the callback is not asked about one.
- Application rules still receive every administrator action and can deny it.
- A callback or policy that throws answers `500` with a trace id.

The built-in lists match a caller by identity value. The server reads that value from `ClaimTypes.NameIdentifier`, then `sub`, then `Identity.Name`. When `InteractiveReport:IdentityClaim` is set, it reads only that claim. Matching is ordinal and case-sensitive. Saved-report ownership uses the same value. Enable `InteractiveReport:WhoamiEnabled` to see the value for a signed-in caller.

The database list is stored beside the saved reports, so it needs [saved-report storage](SAVED-REPORTS.md#enable-storage). The entries of `InteractiveReport:Administrators` cannot be changed from the administration page.

The built-in report `__saved-reports`, which is the listing on the administration page, is visible to administrators only.

## Row restrictions

A row restriction gives each caller a subset of a report's rows. The restricted rows are absent from counts, totals, charts, value lists and downloads as well.

Put the marker `{{RowRestriction}}` in the report's SQL where the predicate belongs:

```json
{
  "InteractiveReport": {
    "Reports": {
      "products": {
        "dataSource": "MainDb",
        "sql": "SELECT p.ProductID, p.ProductName FROM Products p WHERE {{RowRestriction}}"
      }
    }
  }
}
```

The marker is case-sensitive and is the only opt-in. **A report without the marker never calls the callback.** A marker inside a SQL comment, string or quoted identifier is plain text.

Register a callback that returns a `RowRestriction`:

```csharp
using InteractiveReport.AspNetCore;
using Microsoft.AspNetCore.Authorization;

var reports = builder.Services.AddInteractiveReports(builder.Configuration);

reports.UseRowRestrictions(async (request, ct) =>
{
    if (request.ReportName != "products")
        return RowRestriction.NotApplicable;

    var authorization = request.RequestServices.GetRequiredService<IAuthorizationService>();
    var permitted = await authorization.AuthorizeAsync(request.User, "ViewControlledGoods");

    return permitted.Succeeded
        ? RowRestriction.Unrestricted
        : RowRestriction.Where("p.CG = ?", 0);
});
```

`InteractiveReportRowRestrictionRequest` has these members:

| Member | Meaning |
|---|---|
| `User` | The caller's `ClaimsPrincipal`. It may be anonymous. |
| `UserId` | The caller's identity value, or `null` for an anonymous caller. |
| `ReportName` | The report name. |
| `SourceReportName` | `ReportDefinition.SourceName` when set, otherwise the report name. |
| `RequestServices` | The request's service provider. |

The callback returns one of four results:

| Result | Effect |
|---|---|
| `RowRestriction.Where(expression, values...)` | Inserts the predicate, in parentheses, with the values bound as parameters. |
| `RowRestriction.Unrestricted` | Allows every row. The marker becomes `(1 = 1)` when no other callback adds a predicate. |
| `RowRestriction.NotApplicable` | Leaves the decision to another callback. |
| `RowRestriction.Deny` | Refuses the operation: `401` for an anonymous caller, `403` otherwise. |

Several callbacks combine their predicates with `AND`. `Unrestricted` does not remove another callback's predicate, and one `Deny` refuses the operation.

**A report with the marker needs a decision.** The operation answers `500` when no callback is registered, when every callback returns `NotApplicable`, when a callback returns `null` or throws, or when placeholders and values do not match. The server never falls back to the unrestricted query. A callback that throws `InteractiveReportAuthorizationDeniedException` has the effect of `Deny`.

### Writing the predicate

Write the expression without `WHERE`. The report's SQL decides where it goes and how it combines with other conditions:

```sql
SELECT c.CustomerID, SUM(od.Quantity) AS TotalQuantity
FROM Customers c
JOIN Orders o ON c.CustomerID = o.CustomerID
JOIN OrderDetails od ON o.OrderID = od.OrderID
JOIN Products p ON od.ProductID = p.ProductID
WHERE o.Status = 'Complete'
  AND {{RowRestriction}}
GROUP BY c.CustomerID
```

Here `RowRestriction.Where("p.CG = ?", 0)` removes rows before the aggregate, and `CG` need not be a report column.

- The expression is native SQL in the scope of the marker. Use that scope's aliases and the database's identifier quoting.
- Each `?` outside quotes and comments binds one value, in order. Supply one placeholder for each value: `Where("p.ProductID IN (?, ?, ?)", 1, 2, 3)`. Collections are not expanded.
- Pass `(object?)null` to bind SQL `NULL`.
- Write `??` for a literal question mark, such as PostgreSQL's JSON operator.
- The marker may appear several times, for example in each branch of a `UNION`. Every occurrence receives the same predicate.
- `Where("1 = 0")` runs the report with no rows. `Deny` refuses the operation.
- **The expression is trusted application code. Never build it from caller input.** Pass caller input as values.

### When the callback runs

The server calls the callback after the operation is authorized and before it reads data. One decision covers schema discovery, rows, counts, totals, charts, pivots, value lists and downloads of that operation, on every transport. The server asks again for every operation and stores no decision.

- `allowAnonymous` and the marker are independent. A public report with the marker calls the callback with an anonymous `User` and a `null` `UserId`.
- Administrators and saved-report owners pass through the callback like everyone else.
- Operations that read no report data, such as listing saved reports, do not call it.
- A saved report stores no column list for its tables while its report restricts rows for each caller, by marker or by context parameter. Pivot column names come from data, and the server derives them again for each caller.
- `ReportExecutor` does not call the callback. It refuses SQL that still contains the marker.

## Denials and errors

| Condition | Response |
|---|---|
| The operation needs a signed-in caller | `401` with `IR-1000` |
| A signed-in caller is denied an operation on something they can see | `403` with `IR-1004` |
| The caller may not know that the report or saved report exists | `404` with `IR-1001` or `IR-1002` |
| A feature the report does not offer | `403` with `IR-1100` |
| A callback returns `false` or throws `InteractiveReportAuthorizationDeniedException` | The applicable denial above |
| The request is cancelled | Cancellation propagates |
| A callback, handler or policy throws anything else | Logged, and `500` with `IR-1005` and a trace id |

A report policy the caller fails, a private saved report of another owner, and every administrator route answer `404` to a signed-in caller who is denied. The message of `InteractiveReportAuthorizationDeniedException` is never sent to the client.

## Hints for the packaged UI

The UI uses three hints to decide which administration controls to show. They change nothing about what the server allows.

- A schema response includes `authorization.mayRequestAdministration`.
- `whoami`, when enabled, includes `isAdministrator`, `administratorSource` (`application`, `policy`, `configuration`, `database` or `none`) and `administratorsManagedByApplication`.
- `GET {prefix}/admin/administrators` includes `managedByApplication`.

## Testing application rules

Test rules through the mapped routes, so the report check, the built-in rules and the status codes run together. The repository's own tests show the pattern: `AuthorizationHttpTests.cs`, `InteractiveReportAuthorizationHttpTests.cs` and `GraphQLHttpTests.cs` under `tests/InteractiveReport.AspNetCore.Tests`.
