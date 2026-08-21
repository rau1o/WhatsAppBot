namespace WhatsAppBot.Application.Messaging;

// Mensaje entrante ya parseado desde el payload crudo de Meta.
// El parseo del JSON real vive en Infrastructure — acá solo llega
// la forma limpia que necesita la lógica de negocio.
public record IncomingMessage(
    string CustomerPhoneNumber,
    string TenantPhoneNumberId,
    string? Text,
    string? InteractiveButtonId,
    string? MediaId,
    string? ListReplyId

);
