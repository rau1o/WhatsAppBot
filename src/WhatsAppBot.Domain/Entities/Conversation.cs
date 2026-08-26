using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Domain.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CustomerPhoneNumber { get; set; } = default!;
    public ConversationState State { get; set; } = ConversationState.New;
    public DateTime LastMessageAt { get; set; }

    // Solo tiene valor mientras State == AwaitingQuantity — recuerda para
    // qué producto el cliente tiene que escribir la cantidad, ya que
    // WhatsApp no tiene forma nativa de "esperar texto libre para esto
    // puntual" entre un mensaje y el siguiente.
    public Guid? PendingProductId { get; set; }
}
