namespace WhatsAppBot.Application.Abstractions;

// Mismo patrón que ICurrentTenantAccessor: un valor ambient dentro del scope
// de DI (un request HTTP, o la ejecución de un job de Hangfire) — permite
// que todos los logs de una misma operación, aunque crucen del webhook HTTP
// a un job en background, queden marcados con el mismo identificador.
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
    void SetCorrelationId(string correlationId);
}
