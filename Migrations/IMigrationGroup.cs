namespace Sandbox.Migrations;

public interface IMigrationGroup
{
    IEnumerable<Migration> Migrations();
}
