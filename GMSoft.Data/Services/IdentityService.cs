using GMSoft.Application.Common.Exceptions;
using GMSoft.Application.Common.Interfaces;
using GMSoft.Application.Common.Models;
using GMSoft.Data.Context;
using GMSoft.Data.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GMSoft.Data.Services;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly AppDbContext _context;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        AppDbContext context)
    {
        _userManager     = userManager;
        _jwtTokenService = jwtTokenService;
        _context         = context;
    }

    public async Task<AuthResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);

        // Mismo mensaje si el usuario no existe o si la contraseña está mal: distinguirlos
        // le confirma a quien prueba credenciales cuáles de los mails son cuentas reales.
        if (user is null)
            throw new UnauthorizedException("Email o contraseña incorrectos.");

        if (await _userManager.IsLockedOutAsync(user))
            throw new ForbiddenException(
                "La cuenta está bloqueada temporalmente por intentos fallidos. Probá de nuevo en unos minutos.");

        if (!user.IsActive)
            throw new ForbiddenException("La cuenta está desactivada.");

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            // Suma el intento fallido: al llegar al tope, Identity bloquea la cuenta.
            await _userManager.AccessFailedAsync(user);
            throw new UnauthorizedException("Email o contraseña incorrectos.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        // Si el usuario es chofer, el token lleva su DriverId para que los endpoints
        // del reparto no tengan que buscarlo en cada request.
        var driverId = await _context.Drivers
            .Where(d => d.ApplicationUserId == user.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var token = _jwtTokenService.GenerateToken(
            new AuthUserData(user.Id, user.Email!, roles, driverId));

        return new AuthResult(
            Token:    token,
            UserId:   user.Id,
            Email:    user.Email!,
            FullName: $"{user.FirstName} {user.LastName}".Trim(),
            Roles:    roles,
            DriverId: driverId);
    }

    public async Task<Guid> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (await _userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException($"Ya existe una cuenta con el email {email}.");

        var user = new ApplicationUser
        {
            UserName       = email,
            Email          = email,
            EmailConfirmed = true,
            FirstName      = firstName,
            LastName       = lastName,
            IsActive       = true
        };

        var created = await _userManager.CreateAsync(user, password);
        Garantizar(created);

        var assigned = await _userManager.AddToRoleAsync(user, role);
        Garantizar(assigned);

        return user.Id;
    }

    public async Task SetPasswordAsync(
        Guid userId,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Usuario", userId);

        // Sin pedir la contraseña anterior: la asigna el admin, no la cambia el dueño.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        Garantizar(await _userManager.ResetPasswordAsync(user, token, newPassword));
    }

    public async Task SetUserActiveAsync(
        Guid userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("Usuario", userId);

        user.IsActive = isActive;
        Garantizar(await _userManager.UpdateAsync(user));
    }

    /// <summary>
    /// Traduce el resultado de Identity a nuestras excepciones. Identity no tira:
    /// devuelve un objeto con errores que, si nadie mira, deja pasar en silencio una
    /// contraseña débil o un alta que nunca ocurrió.
    /// </summary>
    private static void Garantizar(IdentityResult result)
    {
        if (result.Succeeded) return;

        var mensaje = string.Join(" | ", result.Errors.Select(e => e.Description));
        throw new BadRequestException(mensaje);
    }
}
