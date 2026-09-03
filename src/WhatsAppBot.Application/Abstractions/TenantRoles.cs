namespace WhatsAppBot.Application.Abstractions;

// Vive en Application (no en Infrastructure/Identity) porque "qué roles
// son válidos" es una regla de negocio, no un detalle de cómo se
// implementa la autenticación.
public static class TenantRoles
{
    public const string Owner = "Owner";

    // Todo lo operativo (Catálogo, Comprobantes, Pedidos) más lo que sea de
    // "ver el rendimiento del negocio" (Reportes, cuando se construyan) —
    // sin llegar a gestionar usuarios ni la configuración de la cuenta,
    // que sigue siendo exclusivo de Owner.
    public const string Manager = "Manager";

    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = new[] { Owner, Manager, Staff };

    public static bool IsValid(string role) => All.Contains(role);
}
