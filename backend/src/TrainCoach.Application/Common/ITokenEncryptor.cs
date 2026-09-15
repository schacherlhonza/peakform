namespace TrainCoach.Application.Common;

/// <summary>Encrypts/decrypts integration OAuth tokens before they touch the database. Implemented
/// in Infrastructure using ASP.NET Core Data Protection. Never store a raw token — see docs/security.md.</summary>
public interface ITokenEncryptor
{
    string Protect(string plainText);
    string Unprotect(string cipherText);
}
