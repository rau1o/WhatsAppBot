using WhatsAppBot.Application.Messaging;

namespace WhatsAppBot.Application.Abstractions;

// Caso de uso principal: "procesar un mensaje entrante de WhatsApp".
// El Api/Worker (Hangfire) solo conoce esta interfaz.
public interface IMessageProcessor
{
    Task ProcessAsync(Guid tenantId, IncomingMessage message, string correlationId, CancellationToken ct = default);
}
