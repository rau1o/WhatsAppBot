namespace WhatsAppBot.Api.Contracts;

public record PaymentProofDto(
    Guid Id,
    Guid OrderId,
    string CustomerPhoneNumber,
    decimal OrderTotal,
    string Status,
    DateTime CreatedAt
);
