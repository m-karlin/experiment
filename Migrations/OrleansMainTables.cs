using JetBrains.Annotations;

namespace Sandbox.Migrations;

[UsedImplicitly]
public class OrleansMainTables : IMigrationGroup
{
    public IEnumerable<Migration> Migrations()
    {
        // from https://learn.microsoft.com/ru-ru/dotnet/orleans/host/configuration-guide/adonet-configuration
        const string sql = """
                           CREATE TABLE OrleansQuery
                           (
                               QueryKey VARCHAR(64) NOT NULL,
                               QueryText VARCHAR(8000) NOT NULL,
                               CONSTRAINT OrleansQuery_Key PRIMARY KEY(QueryKey)
                           );
                           """;

        yield return new Migration(2024_11_13_15_00, sql, "orleans main tables");
    }
}
