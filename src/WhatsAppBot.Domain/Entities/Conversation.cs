using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Domain.Entities;

public class Conversation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string CustomerPhoneNumber { get; set; } = default!;
    public ConversationState State { get; set; } = ConversationState.New;
    public DateTime LastMessageAt { get; set; }
}
