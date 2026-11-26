namespace CURE.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;
    public string MigrationConnectionString { get; set; } = string.Empty;
    public string RuntimeRoleName { get; set; } = string.Empty;
    public string RuntimeRolePassword { get; set; } = string.Empty;
}