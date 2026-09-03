namespace WhatsAppBot.Infrastructure.Identity;

// Guardamos el HASH del token, nunca el valor real — mismo principio que
// las contraseñas: si alguien accede a la base, no puede usar lo que ve acá
// directamente. A diferencia de las contraseñas, el hash es rápido (SHA-256,
// no PBKDF2) porque el token en sí ya es aleatorio de alta entropía — no
// hace falta protegerlo contra fuerza bruta como a una contraseña elegida
// por una persona.
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }

    // Si este token fue reemplazado por uno nuevo (rotación en cada
    // refresh), guardamos el hash del reemplazo — permite detectar reuso:
    // si alguien presenta un token ya usado, es señal de que se filtró.
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
