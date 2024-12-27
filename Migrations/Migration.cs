namespace Sandbox.Migrations;

public record Migration(long Identifier, string SqlString, string? Description = null);
