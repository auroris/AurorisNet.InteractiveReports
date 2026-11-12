using InteractiveReport.Core.Model;
using InteractiveReport.Core.SavedReports;
using Oracle.ManagedDataAccess.Client;

namespace InteractiveReport.Core.Tests;

/// <summary>Live Oracle execution of the dialect-neutral saved-report contract.</summary>
public sealed class OracleSavedReportStoreTests : SavedReportStoreCorpus
{
    private const string TableName = "IR_SAVED_REPORTS_TEST";

    private static string? ConnectionString => Environment.GetEnvironmentVariable("IR_TEST_ORACLE");

    protected override SqlSavedReportStore CreateStore()
    {
        var connectionString = ConnectionString;
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(connectionString),
            "set IR_TEST_ORACLE to run live Oracle saved-report verification");

        DropTable(connectionString!);
        return new SqlSavedReportStore(
            () => new SavedReportStoreConfig(
                "Saved",
                ReportDialect.Oracle,
                AutoCreate: true,
                TableName: TableName),
            new FixedConnectionFactory(() => new OracleConnection(connectionString!)));
    }

    protected override Task CleanUp()
    {
        if (ConnectionString is { } connectionString) DropTable(connectionString);
        return Task.CompletedTask;
    }

    private static void DropTable(string connectionString)
        => OracleTestSchema.Drop(connectionString, $"DROP TABLE \"{TableName}\"");
}
