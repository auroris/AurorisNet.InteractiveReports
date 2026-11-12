using System.Data.Common;
using InteractiveReport.Core.Authorization;
using InteractiveReport.Core.Execution;

namespace InteractiveReport.Core.Tests;

/// <summary>
/// Dialect-neutral administrator-grant contract. Identities are opaque keys compared
/// ordinally: a case variant is a different account on every database, whatever its
/// default collation says about the two strings.
/// </summary>
public abstract class AdministratorStoreCorpus : IAsyncLifetime
{
    private SqlAdministratorStore? _store;

    /// <summary>Build (or skip) the store. Called once per test via the Store property.</summary>
    protected abstract SqlAdministratorStore CreateStore();

    /// <summary>Removes whatever the store created; live targets drop their scratch table here after every test.</summary>
    protected virtual Task CleanUp() => Task.CompletedTask;

    ValueTask IAsyncLifetime.InitializeAsync() => ValueTask.CompletedTask;

    ValueTask IAsyncDisposable.DisposeAsync() => new(CleanUp());

    private SqlAdministratorStore Store => _store ??= CreateStore();

    private static string[] Sorted(IEnumerable<string> identities)
        => identities.Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task Grants_are_idempotent_case_sensitive_and_revocable()
    {
        Assert.Empty(await Store.List(TestContext.Current.CancellationToken));
        Assert.False(await Store.IsAdministrator("alice", TestContext.Current.CancellationToken));

        await Store.Grant(" Alice ", TestContext.Current.CancellationToken);
        await Store.Grant("Alice", TestContext.Current.CancellationToken);
        await Store.Grant("bob", TestContext.Current.CancellationToken);

        Assert.Equal(["Alice", "bob"], Sorted(await Store.List(TestContext.Current.CancellationToken)));
        Assert.True(await Store.IsAdministrator("Alice", TestContext.Current.CancellationToken));
        Assert.True(await Store.IsAdministrator(" Alice ", TestContext.Current.CancellationToken));
        // Identities are opaque: a case variant is a different account.
        Assert.False(await Store.IsAdministrator("alice", TestContext.Current.CancellationToken));

        // Granting the variant adds a row rather than refreshing the existing one, even where
        // the database's default collation would call the two strings equal.
        await Store.Grant("alice", TestContext.Current.CancellationToken);
        Assert.Equal(["Alice", "alice", "bob"], Sorted(await Store.List(TestContext.Current.CancellationToken)));
        Assert.True(await Store.IsAdministrator("alice", TestContext.Current.CancellationToken));

        // Revoking a variant that was never granted removes nothing.
        Assert.False(await Store.Revoke("ALICE", TestContext.Current.CancellationToken));
        Assert.True(await Store.IsAdministrator("Alice", TestContext.Current.CancellationToken));
        Assert.True(await Store.Revoke("Alice", TestContext.Current.CancellationToken));
        Assert.False(await Store.Revoke("Alice", TestContext.Current.CancellationToken));
        Assert.False(await Store.IsAdministrator("Alice", TestContext.Current.CancellationToken));
        Assert.True(await Store.IsAdministrator("alice", TestContext.Current.CancellationToken));
        Assert.Equal(["alice", "bob"], Sorted(await Store.List(TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Blank_identities_are_rejected_before_reaching_the_database()
    {
        // Store is touched outside the assertion lambdas so an unconfigured live target
        // skips instead of failing the exception-type check.
        var store = Store;
        await Assert.ThrowsAsync<ArgumentException>(() => store.Grant(" ", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => store.IsAdministrator("", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => store.Revoke("", TestContext.Current.CancellationToken));
        Assert.Empty(await store.List(TestContext.Current.CancellationToken));
    }

    protected sealed class FixedConnectionFactory(Func<DbConnection> open) : IReportConnectionFactory
    {
        public DbConnection CreateConnection(string name) => open();
    }
}
