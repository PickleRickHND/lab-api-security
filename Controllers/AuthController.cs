using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureApi.Security;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace SecureApi.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/[controller]")]
public sealed class AuthController(IJwtTokenService tokenService) : ControllerBase
{
    private const int PasswordIterations = 100_000;

    private static readonly DemoUser[] Users =
    [
        new(
            "usr_alice",
            "alice",
            "SbUP6OtujPXpWeSXc9foeQ==",
            "MdFuVy+uF3ZdIarXzypRBjhXGwAKUKq2n2zRdG1xceQ="),
        new(
            "usr_bob",
            "bob",
            "RNG4u8i8QX8m/FSkNc1L3Q==",
            "LqnpyJwXZEYD4liHl50IhgESM1hxwM0ThlmI4U+QcF0="),
        new(
            "usr_charlie",
            "charlie",
            "N34uHRx5wGnjhulwR9LwZg==",
            "zqNppiGUN2wQnUicRp+hBEr6KrurDbX/IqyqgjF9Plg=")
    ];

    [HttpPost("login")]
    public ActionResult<TokenResponse> Login(LoginRequest request)
    {
        var user = Users.FirstOrDefault(candidate =>
            string.Equals(candidate.Username, request.Username, StringComparison.OrdinalIgnoreCase));

        // Ejecutar el mismo trabajo criptográfico aunque el usuario no exista
        // evita revelar nombres válidos mediante diferencias evidentes de tiempo.
        var passwordIsValid = VerifyPassword(request.Password, user ?? Users[0]);
        if (user is null || !passwordIsValid)
        {
            return Unauthorized(new { message = "Usuario o contraseña incorrectos." });
        }

        return Ok(tokenService.CreateToken(user));
    }

    private static bool VerifyPassword(string password, DemoUser user)
    {
        var salt = Convert.FromBase64String(user.PasswordSalt);
        var expectedHash = Convert.FromBase64String(user.PasswordHash);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}

public sealed record LoginRequest(
    [Required, MinLength(1)] string Username,
    [Required, MinLength(1)] string Password);
