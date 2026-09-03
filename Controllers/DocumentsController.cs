using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace SecureApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class DocumentsController : ControllerBase
{
    private static readonly List<Document> Documents =
    [
        new(1, "usr_alice", "Nomina_Confidencial.pdf", "Datos sensibles de Alice"),
        new(2, "usr_bob", "Estrategia_Precios_2026.pdf", "Datos confidenciales de Bob"),
        new(3, "usr_charlie", "Historia_Clinica.pdf", "Datos médicos de Charlie")
    ];

    // VULNERABLE: No valida si el documento pertenece al usuario autenticado (BOLA) Broken Object Level Authorization, (IDOR) Insecure Direct Object Reference
    [HttpGet("vulnerable/{id}")]
    public IActionResult GetVulnerable(int id)
    {
        var document = Documents.FirstOrDefault(item => item.Id == id);
        if (document is null)
        {
            return NotFound(new { message = "Documento no encontrado." });
        }

        // 2. FALLA CRÍTICA DE SEGURIDAD:
        // Retorna el recurso directamente sin verificar si el usuario autenticado
        // coincide con 'document.OwnerId'.
        return Ok(document);
    }

    // MITIGADO: Validación explícita de autorización a nivel de objeto (ABAC) Ausencia de Control de Acceso Basado en Atributos
    [HttpGet("secure/{id}")]
    public IActionResult GetSecure(int id)
    {
        var document = Documents.FirstOrDefault(item => item.Id == id);
        if (document is null)
        {
            return NotFound(new { message = "Documento no encontrado." });
        }

        var currentUserId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "El token no contiene el claim requerido: 'sub'."
            });
        }

        // Control de Acceso ABAC / BOLA Check
        if (!string.Equals(document.OwnerId, currentUserId, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Acceso denegado: el documento pertenece a otro usuario."
            });
        }

        return Ok(document);
    }

    // Endpoint auxiliar para comprobar la identidad y los claims del JWT.
    [HttpGet("whoami")]
    public IActionResult WhoAmI()
    {
        return Ok(new
        {
            userIdClaim = JwtRegisteredClaimNames.Sub,
            userId = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
            claims = User.Claims.Select(claim => new { claim.Type, claim.Value })
        });
    }
}

public sealed record Document(int Id, string OwnerId, string Title, string Content);
