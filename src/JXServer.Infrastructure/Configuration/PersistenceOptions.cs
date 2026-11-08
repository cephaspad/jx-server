namespace JXServer.Infrastructure.Configuration;

/// <summary>
/// Configuration options for EF Core 10 database persistence.
/// </summary>
public sealed class PersistenceOptions
{
    /// <summary>
    /// Configuration section name (<c>Persistence</c>).
    /// </summary>
    public const string SectionName = "Persistence";

    /// <summary>
    /// Gets or sets the database provider (<c>Sqlite</c> or <c>SqlServer</c>).
    /// </summary>
    public string Provider { get; set; } = "Sqlite";

    /// <summary>
    /// Gets or sets the database connection string.
    /// </summary>
    public string ConnectionString { get; set; } = "Data Source=jxserver-dev.db";

    /// <summary>
    /// Gets or sets a value indicating whether the schema should be automatically created at startup (<c>EnsureCreatedAsync</c>).
    /// </summary>
    public bool AutoInitializeDatabase { get; set; } = true;
}
