using System.Data.Common;
using InteractiveReport.Core.Execution;
using InteractiveReport.Core.SavedReports;

namespace InteractiveReport.Core.Tests;

/// <summary>Dialect-neutral saved-report persistence contract.</summary>
public abstract class SavedReportStoreCorpus : IAsyncLifetime
{
    private SqlSavedReportStore? _store;

    /// <summary>Build (or skip) the store. Called once per test via the Store property.</summary>
    protected abstract SqlSavedReportStore CreateStore();

    /// <summary>Removes whatever the store created; live targets drop their scratch objects here after every test.</summary>
    protected virtual Task CleanUp() => Task.CompletedTask;

    ValueTask IAsyncLifetime.InitializeAsync() => ValueTask.CompletedTask;

    ValueTask IAsyncDisposable.DisposeAsync() => new(CleanUp());

    private SqlSavedReportStore Store => _store ??= CreateStore();

    private static SavedReport Make(string title, string owner, bool global = false, string report = "orders") => new()
    {
        Id = 0,
        ReportName = report,
        Title = title,
        Owner = owner,
        IsGlobal = global,
        StateJson = """{"activeTable":"orders","tables":{"orders":{"from":"definition","composables":[{"kind":"filter","filters":[]}]}}}""",
    };

    [Fact]
    public async Task Create_get_roundtrip_preserves_fields_and_stamps_modified()
    {
        var report = Make("West region", "alice");
        await Store.Create(report, TestContext.Current.CancellationToken);

        var loaded = await Store.Get(report.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(report.Title, loaded.Title);
        Assert.Equal("alice", loaded.Owner);
        Assert.False(loaded.IsGlobal);
        Assert.False(loaded.IsDefault);
        Assert.Equal(report.StateJson, loaded.StateJson);
        Assert.True(loaded.ModifiedUtc > DateTime.UtcNow.AddMinutes(-1));
        Assert.Equal(SavedReportOrigin.User, loaded.Origin);
    }

    [Fact]
    public async Task Get_returns_a_detached_snapshot()
    {
        var report = Make("Detached", "alice");
        await Store.Create(report, TestContext.Current.CancellationToken);

        var first = (await Store.Get(report.Id, TestContext.Current.CancellationToken))!;
        first.Owner = "mallory";
        first.StateJson = """{"search":"mutated locally"}""";

        var second = (await Store.Get(report.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal("alice", second.Owner);
        Assert.Equal(report.StateJson, second.StateJson);
    }

    [Fact]
    public async Task Configured_rows_roundtrip_generated_id_origin_file_reference_and_null_state()
    {
        var stamp = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var report = new SavedReport
        {
            Id = 0,
            ReportName = "orders",
            SourceFile = "ReportDocuments/regional.json",
            Title = "Regional View",
            Owner = null,
            IsGlobal = true,
            StateJson = null,
            ModifiedUtc = stamp,
            Origin = SavedReportOrigin.Configured,
        };
        await Store.Create(report, TestContext.Current.CancellationToken);

        var loaded = await Store.Get(report.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.True(loaded.Id > 0);
        Assert.Equal(report.SourceFile, loaded.SourceFile);
        Assert.Null(loaded.Owner);
        Assert.Null(loaded.StateJson);
        Assert.True(loaded.IsGlobal);
        Assert.Equal(SavedReportOrigin.Configured, loaded.Origin);
        Assert.True(loaded.ModifiedUtc >= stamp);
    }

    [Fact]
    public async Task ListFamily_returns_the_complete_unfiltered_named_family()
    {
        var anchor = Make("Anchor", "alice", global: true);
        var alicePrivate = Make("Alice private", "alice");
        var bobPrivate = Make("Bob private", "bob");
        var otherFamily = Make("Other family", "bob", report: "big-orders");
        await Store.Create(anchor, TestContext.Current.CancellationToken);
        await Store.Create(alicePrivate, TestContext.Current.CancellationToken);
        await Store.Create(bobPrivate, TestContext.Current.CancellationToken);
        await Store.Create(otherFamily, TestContext.Current.CancellationToken);

        var family = await Store.ListFamily(anchor.ReportName, TestContext.Current.CancellationToken);

        Assert.Equal(3, family.Count);
        Assert.Contains(family, report => report.Id == alicePrivate.Id);
        Assert.Contains(family, report => report.Id == bobPrivate.Id);
        Assert.DoesNotContain(family, report => report.Id == otherFamily.Id);
        Assert.Empty(await Store.ListFamily("missing-family", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReplaceDefault_atomically_promotes_one_global_report_and_retains_the_previous_default()
    {
        var original = Make("Original", "admin");
        original.IsGlobal = true;
        original.IsDefault = true;
        await Store.Create(original, TestContext.Current.CancellationToken);
        var candidate = Make("Executive", "bob");
        await Store.Create(candidate, TestContext.Current.CancellationToken);
        var replacement = candidate with { IsGlobal = true, IsDefault = true };

        Assert.True(await Store.ReplaceDefault(replacement, candidate, original, TestContext.Current.CancellationToken));

        var selected = await Store.FindDefault("orders", TestContext.Current.CancellationToken);
        Assert.Equal(candidate.Id, selected?.Id);
        Assert.True(selected?.IsGlobal);
        var prior = await Store.Get(original.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(prior);
        Assert.False(prior.IsDefault);
        Assert.True(prior.IsGlobal);
        Assert.Equal(2, (await Store.ListFamily("orders", TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task ListAll_spans_reports_and_owners()
    {
        await Store.Create(Make("A", "alice"), TestContext.Current.CancellationToken);
        await Store.Create(Make("B", "bob", report: "big-orders"), TestContext.Current.CancellationToken);

        var all = await Store.ListAll(TestContext.Current.CancellationToken);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ListOwners_projects_distinct_owners_and_skips_unowned_rows()
    {
        await Store.Create(Make("A", "alice"), TestContext.Current.CancellationToken);
        await Store.Create(Make("B", "alice", report: "big-orders"), TestContext.Current.CancellationToken);
        await Store.Create(Make("C", "Bob", global: true), TestContext.Current.CancellationToken);
        await Store.Create(new SavedReport
        {
            Id = 0,
            ReportName = "orders",
            SourceFile = "ReportDocuments/unowned.json",
            Title = "Unowned",
            Owner = null,
            IsGlobal = true,
            StateJson = null,
            Origin = SavedReportOrigin.Configured,
        }, TestContext.Current.CancellationToken);

        var owners = await Store.ListOwners(TestContext.Current.CancellationToken);

        // Owners are identities: distinct ordinally, so a case variant is a different account.
        Assert.Equal(["Bob", "alice"], owners.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task FindTitleCollision_is_normalized_visibility_scoped_and_can_exclude_the_current_row()
    {
        var orders = Make("West region", "alice");
        var other = Make("West region", "bob", report: "big-orders");
        await Store.Create(orders, TestContext.Current.CancellationToken);
        await Store.Create(other, TestContext.Current.CancellationToken);

        var found = await Store.FindTitleCollision("orders", "  WEST REGION  ", owner: "alice", isPublic: false, ct: TestContext.Current.CancellationToken);

        Assert.Equal(orders.Id, found?.Id);
        Assert.Null(await Store.FindTitleCollision("orders", "West region", "alice", isPublic: false, exceptId: orders.Id, ct: TestContext.Current.CancellationToken));
        Assert.Equal(other.Id, (await Store.FindTitleCollision("big-orders", "west region", "bob", isPublic: false, ct: TestContext.Current.CancellationToken))?.Id);
    }

    [Fact]
    public async Task Update_rewrites_row_including_owner_reassignment()
    {
        var report = Make("Original", "alice");
        await Store.Create(report, TestContext.Current.CancellationToken);

        var expected = report with { };
        report.Title = "Renamed";
        report.Owner = "bob";
        report.IsGlobal = true;
        Assert.True(await Store.Update(report, expected, TestContext.Current.CancellationToken));

        var loaded = await Store.Get(report.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Renamed", loaded!.Title);
        Assert.Equal("bob", loaded.Owner);
        Assert.True(loaded.IsGlobal);
    }

    [Fact]
    public async Task Update_and_delete_reject_a_stale_authoritative_snapshot()
    {
        var report = Make("Concurrent", "alice");
        await Store.Create(report, TestContext.Current.CancellationToken);
        var expected = (await Store.Get(report.Id, TestContext.Current.CancellationToken))!;

        // A state-only write proves ModifiedUtc is a real concurrency predicate, not
        // merely a repeat of the authorization metadata comparisons.
        var winner = expected with { StateJson = """{"search":"winner"}""" };
        Assert.True(await Store.Update(winner, expected, TestContext.Current.CancellationToken));
        // Strictly newer, not merely different: a coarse clock must still yield a
        // usable next CAS revision.
        Assert.True(winner.ModifiedUtc > expected.ModifiedUtc);

        var stale = expected with { Title = "Stale overwrite", Owner = "mallory" };
        Assert.False(await Store.Update(stale, expected, TestContext.Current.CancellationToken));
        Assert.False(await Store.Delete(expected, TestContext.Current.CancellationToken));

        var current = (await Store.Get(report.Id, TestContext.Current.CancellationToken))!;
        Assert.Equal("Concurrent", current.Title);
        Assert.Equal("alice", current.Owner);
        Assert.Equal(winner.StateJson, current.StateJson);
        Assert.True(await Store.Delete(current, TestContext.Current.CancellationToken));
        Assert.Null(await Store.Get(report.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Snapshot_matching_uses_ordinal_authorization_strings()
    {
        var report = Make("Ordinal owner", "Alice@Example.test");
        await Store.Create(report, TestContext.Current.CancellationToken);
        var current = (await Store.Get(report.Id, TestContext.Current.CancellationToken))!;

        // These snapshots are not authoritative even on a database whose default
        // collation equates their strings. Application ownership is ordinal.
        var wrongOwner = current with { Owner = "alice@example.test" };
        var wrongReport = current with { ReportName = "ORDERS" };
        var wrongTitle = current with { Title = "ordinal OWNER" };

        Assert.False(await Store.Update(wrongOwner with { IsGlobal = true }, wrongOwner, TestContext.Current.CancellationToken));
        Assert.False(await Store.Delete(wrongOwner, TestContext.Current.CancellationToken));
        Assert.False(await Store.Update(wrongReport with { IsGlobal = true }, wrongReport, TestContext.Current.CancellationToken));
        Assert.False(await Store.Delete(wrongReport, TestContext.Current.CancellationToken));
        Assert.False(await Store.Update(wrongTitle with { IsGlobal = true }, wrongTitle, TestContext.Current.CancellationToken));
        Assert.False(await Store.Delete(wrongTitle, TestContext.Current.CancellationToken));

        Assert.Equal(current, await Store.Get(report.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_removes_and_reports_missing_honestly()
    {
        var report = Make("Doomed", "alice");
        await Store.Create(report, TestContext.Current.CancellationToken);

        Assert.True(await Store.Delete(report.Id, TestContext.Current.CancellationToken));
        Assert.False(await Store.Delete(report.Id, TestContext.Current.CancellationToken));
        Assert.Null(await Store.Get(report.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Duplicate_user_titles_for_one_report_are_rejected_atomically()
    {
        await Store.Create(Make("West region", "alice"), TestContext.Current.CancellationToken);

        // Same normalized title (trim + case): the unique index is the atomic
        // backstop behind the endpoints' advisory pre-check.
        await Assert.ThrowsAsync<SavedReportTitleConflictException>(
            () => Store.Create(Make("  west REGION ", "alice"), TestContext.Current.CancellationToken));

        // Private names owned by different users do not share a visible namespace.
        await Store.Create(Make("West region", "bob"), TestContext.Current.CancellationToken);

        // A different report definition keeps its own title namespace.
        await Store.Create(Make("West region", "bob", report: "big-orders"), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Renaming_onto_an_existing_title_is_rejected()
    {
        await Store.Create(Make("Keep", "alice"), TestContext.Current.CancellationToken);
        var other = Make("Rename me", "alice");
        await Store.Create(other, TestContext.Current.CancellationToken);

        var expected = other with { };
        other.Title = "keep";

        await Assert.ThrowsAsync<SavedReportTitleConflictException>(() => Store.Update(other, expected, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Configured_rows_may_duplicate_user_and_configured_titles_without_tripping_uniqueness()
    {
        // A checked-in document is a deployment declaration. Its title does not
        // participate in the user-facing uniqueness constraint, even when another
        // configured file or a visible user row already has that title.
        await Store.Create(Make("Shared title", "alice"), TestContext.Current.CancellationToken);
        var configured = new SavedReport
        {
            Id = 0,
            ReportName = "orders",
            SourceFile = "ReportDocuments/shared.json",
            Title = "Shared title",
            Owner = null,
            IsGlobal = true,
            StateJson = null,
            ModifiedUtc = DateTime.UtcNow,
            Origin = SavedReportOrigin.Configured,
        };
        await Store.Create(configured, TestContext.Current.CancellationToken);
        await Store.Create(configured with
        {
            Id = 0,
            SourceFile = "ReportDocuments/also-shared.json",
        }, TestContext.Current.CancellationToken);

        Assert.Equal(3, (await Store.ListFamily("orders", TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Create_rejects_a_structurally_invalid_row_before_it_reaches_the_database()
    {
        // A broken row (null STATE_JSON) must be rejected outright rather than
        // surfacing as a provider error the synchronizer could mistake for a lost
        // insert race and mark as synced. (Store is touched outside the assertion
        // lambda so an unconfigured live target skips instead of failing the type
        // check.)
        var store = Store;
        var broken = Make("Broken", "alice");
        broken.StateJson = null!;

        await Assert.ThrowsAsync<ArgumentException>(() => store.Create(broken, TestContext.Current.CancellationToken));
        Assert.Equal(0, broken.Id);
    }

    protected sealed class FixedConnectionFactory(Func<DbConnection> open) : IReportConnectionFactory
    {
        public DbConnection CreateConnection(string name) => open();
    }
}
