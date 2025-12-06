namespace Verifactu.Portal.Models;

public sealed class ApiKeyDto
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string MaskedValue { get; init; }

    public DateTime CreatedAt { get; init; }
}
