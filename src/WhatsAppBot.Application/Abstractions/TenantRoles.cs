namespace WhatsAppBot.Application.Abstractions;

// Los dos únicos roles que existen hoy. Vive en Application (no en
// Infrastructure/Identity) porque "qué roles son válidos" es una regla
// de negocio, no un detalle de cómo se implementa la autenticación.
public static class TenantRoles
{
    public const string Owner = "Owner";
    public const string Staff = "Staff";

    public static readonly IReadOnlyList<string> All = new[] { Owner, Staff };

    public static bool IsValid(string role) => All.Contains(role);
}
