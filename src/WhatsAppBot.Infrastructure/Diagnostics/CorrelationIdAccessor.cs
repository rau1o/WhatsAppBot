using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Infrastructure.Diagnostics;

public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    public string? CorrelationId { get; private set; }

    public void SetCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
    }
}
