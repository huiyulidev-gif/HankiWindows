namespace Hanki.Core.Authentication;

/// <summary>
/// Profile information for the signed-in Supabase user. Populated directly from the
/// token-exchange response's <c>user</c> object -- no additional network call is made.
/// </summary>
public sealed record AuthUser(string Id, string Email, string Name, string? AvatarUrl);
