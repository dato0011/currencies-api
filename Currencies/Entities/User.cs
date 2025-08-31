namespace Currencies.Entities;

public record User
{
    public int Id { get; init; }
    public required string Username { get; init; }
    public required string PasswordHash { get; init; }
    public required string Role { get; init; }
}
