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
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Se busca por nombre de usuario. El chofer entra con "jperez" desde el
        // celular todas las mañanas: pedirle un email seria peor, y ademas puede no
        // tener uno.
        var user = await _userManager.FindByNameAsync(userName);

        // Mismo mensaje si el usuario no existe o si la contraseña está mal:
        // distinguirlos le confirma a quien prueba credenciales cuáles son cuentas
        // reales.
        if (user is null)
            throw new UnauthorizedException("Usuario o contraseña incorrectos.");

        if (!user.IsActive)
            throw new ForbiddenException("La cuenta está desactivada.");

        // No se cuentan los intentos fallidos ni se bloquea la cuenta; los bloqueos que
        // hayan quedado de antes se ignoran.
        if (!await _userManager.CheckPasswordAsync(user, password))
            throw new UnauthorizedException("Usuario o contraseña incorrectos.");

        var roles = (await _userManager.GetRolesAsync(user)).ToList();

        // Si el usuario es chofer, el token lleva su DriverId para que los endpoints
        // del reparto no tengan que buscarlo en cada request.
        var driverId = await _context.Drivers
            .Where(d => d.ApplicationUserId == user.Id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var token = _jwtTokenService.GenerateToken(
            new AuthUserData(user.Id, user.UserName!, user.Email, roles, driverId, user.SecurityStamp));

        return new AuthResult(
            Token:    token,
            UserId:   user.Id,
            UserName: user.UserName!,
            Email:    user.Email,
            FullName: $"{user.FirstName} {user.LastName}".Trim(),
            Roles:    roles,
            DriverId: driverId);
    }

    public async Task<Guid> CreateUserAsync(
        string userName,
        string? email,
        string password,
        string firstName,
        string lastName,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (await _userManager.FindByNameAsync(userName) is not null)
            throw new ConflictException($"Ya existe una cuenta con el usuario '{userName}'.");

        var user = new ApplicationUser
        {
            UserName       = userName,
            Email          = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            // Sin email no hay nada que confirmar; con email, el alta la hace el admin
            // en persona, asi que no se manda mail de verificacion.
            EmailConfirmed = true,
            FirstName      = firstName,
            LastName       = lastName,
            IsActive       = true
        };

        Garantizar(await _userManager.CreateAsync(user, password));
        Garantizar(await _userManager.AddToRoleAsync(user, role));

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
        // Sin token de restablecimiento: AddIdentityCore no registra proveedores de
        // tokens, y GeneratePasswordResetTokenAsync fallaba con un 500. Se valida con
        // las mismas reglas de Identity y se guarda el hash en una sola actualización.
        var errores = new List<IdentityError>();
        foreach (var validador in _userManager.PasswordValidators)
        {
            var resultado = await validador.ValidateAsync(_userManager, user, newPassword);
            if (!resultado.Succeeded) errores.AddRange(resultado.Errors);
        }
        if (errores.Count > 0) Garantizar(IdentityResult.Failed([.. errores]));

        user.PasswordHash = _userManager.PasswordHasher.HashPassword(user, newPassword);
        // Cambia el sello de seguridad y guarda el hash en el mismo UpdateAsync.
        Garantizar(await _userManager.UpdateSecurityStampAsync(user));
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

    public Task<bool> IsTokenValidAsync(Guid userId, string? securityStamp, CancellationToken cancellationToken = default)
        // Sin sello: token emitido antes de que existiera el claim; vale hasta vencer.
        => _userManager.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.IsActive
            && (securityStamp == null || u.SecurityStamp == securityStamp), cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, string>();

        return await _userManager.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.UserName != null)
            .ToDictionaryAsync(u => u.Id, u => u.UserName!, cancellationToken);
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
