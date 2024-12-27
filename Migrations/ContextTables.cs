using JetBrains.Annotations;

namespace Sandbox.Migrations;

[UsedImplicitly]
public class ContextTables : IMigrationGroup
{
    public IEnumerable<Migration> Migrations()
    {
        const string sql = """
                           CREATE TABLE Context
                           (
                                id varchar(255) PRIMARY KEY,
                                data JSON
                           );
                           """;

        yield return new Migration(2024_12_28_18_00, sql, "context tables");
    }
}
