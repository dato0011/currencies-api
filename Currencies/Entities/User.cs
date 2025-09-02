namespace Currencies.Entities;

/// <summary>
/// Represents a user entity with authentication and authorization details.
/// </summary>
public record User
{
    /// <summary>
    /// Gets the unique identifier for the user.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets the username used for user authentication.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Gets the hashed password for secure user authentication.
    /// </summary>
    public required string PasswordHash { get; init; }

    /// <summary>
    /// Gets the role assigned to the user for authorization purposes.
    /// </summary>
    public required string Role { get; init; }
}
