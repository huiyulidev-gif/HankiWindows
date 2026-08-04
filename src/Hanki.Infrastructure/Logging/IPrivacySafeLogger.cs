namespace Hanki.Infrastructure.Logging;

/// <summary>
/// Minimal logging contract implemented by <see cref="PrivacySafeLogger"/>. Exists purely so
/// tests can substitute a fake and assert that no sensitive value (token, code, verifier, URL)
/// is ever passed to it -- <see cref="PrivacySafeLogger"/> itself is sealed and writes to disk.
/// </summary>
public interface IPrivacySafeLogger
{
    void Error(string location, Exception exception);
    void Info(string location);
}
