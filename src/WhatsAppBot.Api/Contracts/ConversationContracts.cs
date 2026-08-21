namespace WhatsAppBot.Api.Contracts
{
    public record ConversationSummary(
    Guid Id,
    string CustomerPhoneNumber,
    string State,
    DateTime LastMessageAt
    );
}
