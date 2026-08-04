namespace Hanki.Core.Authentication;

/// <summary>
/// Outcome of a login attempt. UI code must only ever read <see cref="ErrorMessage"/> --
/// it is always one of the approved Korean strings, never a raw exception message.
/// </summary>
public sealed class AuthResult
{
    private AuthResult(bool isSuccess, bool isCancelled, AuthErrorCode errorCode, string? errorMessage, AuthSession? session)
    {
        IsSuccess = isSuccess;
        IsCancelled = isCancelled;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        Session = session;
    }

    public bool IsSuccess { get; }
    public bool IsCancelled { get; }
    public AuthErrorCode ErrorCode { get; }
    public string? ErrorMessage { get; }
    public AuthSession? Session { get; }

    public static AuthResult Success(AuthSession session) =>
        new(isSuccess: true, isCancelled: false, AuthErrorCode.None, errorMessage: null, session);

    public static AuthResult Cancelled() =>
        new(isSuccess: false, isCancelled: true, AuthErrorCode.None, "로그인이 취소되었습니다.", session: null);

    public static AuthResult Error(AuthErrorCode code, string message) =>
        new(isSuccess: false, isCancelled: false, code, message, session: null);
}
