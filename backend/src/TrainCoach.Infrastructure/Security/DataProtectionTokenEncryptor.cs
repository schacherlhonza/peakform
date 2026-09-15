using Microsoft.AspNetCore.DataProtection;
using TrainCoach.Application.Common;

namespace TrainCoach.Infrastructure.Security;

public class DataProtectionTokenEncryptor : ITokenEncryptor
{
    private const string Purpose = "TrainCoach.IntegrationTokens.v1";
    private readonly IDataProtector _protector;

    public DataProtectionTokenEncryptor(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public string Protect(string plainText) => _protector.Protect(plainText);
    public string Unprotect(string cipherText) => _protector.Unprotect(cipherText);
}
