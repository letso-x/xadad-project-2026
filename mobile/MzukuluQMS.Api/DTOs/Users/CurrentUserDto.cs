namespace MzukuluQMS.Api.DTOs.Users;

public sealed class CurrentUserDto
{
    public Guid UserID { get; init; }

    public Guid ExternalAuthID { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}