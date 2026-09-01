using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace SecureApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DocumentsController : ControllerBase
{
    private static readonly List<Document> Db = new()
    {
        new Document(1, "usr_alice", "Nomina_Confidencial.pdf", "Datos sensibles de Alice"),
        new Document(2, "usr_bob", "Estrategia_Precios_2026.pdf", "Datos confidenciales de Bob"),
        new Document(3, "usr_charlie", "Historia_Clinica.pdf", "Datos médicos de Charlie")
    };

    // VULNERABLE: No valida si el documento pertenece al usuario autenticado (BOLA) Broken Object Level Authorization, (IDOR) Insecure Direct Object Reference
    [HttpGet("vulnerable/{id}")]
    public IActionResult GetVulnerable(int id)
    {
        var doc = Db.FirstOrDefault(d => d.Id == id);
        if (doc == null) return NotFound(new { message = "Documento no encontrado" });

        // 2. FALLA CRÍTICA DE SEGURIDAD:
        // Retorna el recurso directamente sin verificar si el usuario autenticado
        // coincide con 'document.OwnerId'.
        return Ok(doc);
    }

    // MITIGADO: Validación explícita de autorización a nivel de objeto (ABAC) Ausencia de Control de Acceso Basado en Atributos
    [HttpGet("secure/{id}")]
    public IActionResult GetSecure(int id)
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "usr_alice"; // Simulado
        var doc = Db.FirstOrDefault(d => d.Id == id);

        if (doc == null) return NotFound(new { message = "Documento no encontrado" });

        // Control de Acceso ABAC / BOLA Check
        if (doc.OwnerId != currentUserId)
        {
            return StatusCode(403, new { message = "Acceso denegado: No posee privilegios sobre este recurso." });
        }

        return Ok(doc);
    }
}

public record Document(int Id, string OwnerId, string Title, string Content);