using JetBrains.Annotations;

namespace Sandbox.Migrations;

[UsedImplicitly]
public class OrleansRemindersTables : IMigrationGroup
{
    public IEnumerable<Migration> Migrations()
    {
        // from https://learn.microsoft.com/ru-ru/dotnet/orleans/host/configuration-guide/adonet-configuration
        const string sql = """
                           -- Orleans Reminders table - https://learn.microsoft.com/dotnet/orleans/grains/timers-and-reminders
                           CREATE TABLE OrleansRemindersTable
                           (
                               ServiceId NVARCHAR(150) NOT NULL,
                               GrainId VARCHAR(150) NOT NULL,
                               ReminderName NVARCHAR(150) NOT NULL,
                               StartTime DATETIME NOT NULL,
                               Period BIGINT NOT NULL,
                               GrainHash INT NOT NULL,
                               Version INT NOT NULL,
                           
                               CONSTRAINT PK_RemindersTable_ServiceId_GrainId_ReminderName PRIMARY KEY(ServiceId, GrainId, ReminderName)
                           );

                           INSERT INTO OrleansQuery(QueryKey, QueryText)
                           VALUES
                           (
                               'UpsertReminderRowKey','
                               INSERT INTO OrleansRemindersTable
                               (
                                   ServiceId,
                                   GrainId,
                                   ReminderName,
                                   StartTime,
                                   Period,
                                   GrainHash,
                                   Version
                               )
                               VALUES
                               (
                                   @ServiceId,
                                   @GrainId,
                                   @ReminderName,
                                   @StartTime,
                                   @Period,
                                   @GrainHash,
                                   last_insert_id(0)
                               )
                               ON DUPLICATE KEY
                               UPDATE
                                   StartTime = @StartTime,
                                   Period = @Period,
                                   GrainHash = @GrainHash,
                                   Version = last_insert_id(Version+1);
                           
                               SELECT last_insert_id() AS Version;
                           ');

                           INSERT INTO OrleansQuery(QueryKey, QueryText)
                           VALUES
                           (
                               'ReadReminderRowsKey','
                               SELECT
                                   GrainId,
                                   ReminderName,
                                   StartTime,
                                   Period,
                                   Version
                               FROM OrleansRemindersTable
                               WHERE
                                   ServiceId = @ServiceId AND @ServiceId IS NOT NULL
                                   AND GrainId = @GrainId AND @GrainId IS NOT NULL;
                           ');

                           INSERT INTO OrleansQuery(QueryKey, QueryText)
                           VALUES
                           (
                               'ReadReminderRowKey','
                               SELECT
                                   GrainId,
                                   ReminderName,
                                   StartTime,
                                   Period,
                                   Version
                               FROM OrleansRemindersTable
                               WHERE
                                   ServiceId = @ServiceId AND @ServiceId IS NOT NULL
                                   AND GrainId = @GrainId AND @GrainId IS NOT NULL
                                   AND ReminderName = @ReminderName AND @ReminderName IS NOT NULL;
                               ');

                           INSERT INTO OrleansQuery(QueryKey, QueryText)
                           VALUES
                           (
                               'ReadRangeRows1Key','
                               SELECT
                                   GrainId,
                                   ReminderName,
                                   StartTime,
                                   Period,
                                   Version
                               FROM OrleansRemindersTable
                               WHERE
                                   ServiceId = @ServiceId AND @ServiceId IS NOT NULL
                                   AND GrainHash > @BeginHash AND @BeginHash IS NOT NULL
                                   AND GrainHash <= @EndHash AND @EndHash IS NOT NULL;
                           ');

                           INSERT INTO OrleansQuery(QueryKey, QueryText)
                           VALUES
                           (
                               'ReadRangeRows2Key','
                               SELECT
                                   GrainId,
                                   ReminderName,
                                   StartTime,
                                   Period,
                                   Version
                               FROM OrleansRemindersTable
                               WHERE
                                   ServiceId = @ServiceId AND @ServiceId IS NOT NULL
                                   AND ((GrainHash > @BeginHash AND @BeginHash IS NOT NULL)
                                   OR (GrainHash <= @EndHash AND @EndHash IS NOT NULL));
                           ');

                           INSERT INTO OrleansQuery(QueryKey, QueryText)
                           VALUES
                           (
                               'DeleteReminderRowKey','
                               DELETE FROM OrleansRemindersTable
                               WHERE
                                   ServiceId = @ServiceId AND @ServiceId IS NOT NULL
                                   AND GrainId = @GrainId AND @GrainId IS NOT NULL
                                   AND ReminderName = @ReminderName AND @ReminderName IS NOT NULL
                                   AND Version = @Version AND @Version IS NOT NULL;
                               SELECT ROW_COUNT();
                           ');

                           INSERT INTO OrleansQuery(QueryKey, QueryText)
                           VALUES
                           (
                               'DeleteReminderRowsKey','
                               DELETE FROM OrleansRemindersTable
                               WHERE
                                   ServiceId = @ServiceId AND @ServiceId IS NOT NULL;
                           ');
                           """;

        yield return new Migration(2024_11_15_15_00, sql, "orleans reminders tables");
    }
}
