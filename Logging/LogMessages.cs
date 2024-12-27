namespace Sandbox.Logging;

internal static partial class LogMessages
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Running migrations")]
    public static partial void RunningMigrations(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Migrations has been successfully applied")]
    public static partial void MigrationsApplied(ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Migrations failed")]
    public static partial void MigrationsFailed(ILogger logger, Exception e);

    [LoggerMessage(EventId = 4, Level = LogLevel.Information, Message = "Saga started")]
    public static partial void SagaStarted(ILogger logger);
    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Saga continued")]
    public static partial void SagaContinued(ILogger logger);
}