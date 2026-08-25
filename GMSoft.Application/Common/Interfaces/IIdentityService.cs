using GMSoft.Application.Common.Models;

namespace GMSoft.Application.Common.Interfaces;

/// <summary>Autenticación de usuarios. Lo implementa la capa Data sobre Identity.</summary>
public interface IIdentityService
{
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}
