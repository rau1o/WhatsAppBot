using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

// Fase 1: primer contacto del cliente. Envía saludo, ubicación
// de la tienda y foto de la fachada.
public class NewConversationStateHandler : IStateHandler
{
    private readonly IWhatsAppMessageSender _sender;

    public NewConversationStateHandler(IWhatsAppMessageSender sender)
    {
        _sender = sender;
    }

    public ConversationState State => ConversationState.New;

    public async Task<StateResult> HandleAsync(
        Tenant tenant,
        Conversation conversation,
        IncomingMessage message,
        CancellationToken ct)
    {
        var to = conversation.CustomerPhoneNumber;
        var phoneNumberId = tenant.WhatsAppPhoneNumberId;

        await _sender.SendTextAsync(
            phoneNumberId, to,
            $"¡Hola! Gracias por escribirnos a {tenant.Name} 👋\n" +
            "Te compartimos nuestra ubicación y cómo nos vas a encontrar:",
            ct);

        await _sender.SendLocationAsync(
            phoneNumberId, to,
            tenant.LocationLatitude, tenant.LocationLongitude,
            tenant.LocationName, tenant.LocationAddress,
            ct);

        await _sender.SendImageByUrlAsync(
            phoneNumberId, to,
            tenant.FacadePhotoUrl,
            "Así se ve nuestro local desde la calle",
            ct);

        // Fase 1 termina acá. En fase 2, sigue directo a BrowsingCatalog.
        return new StateResult(ConversationState.BrowsingCatalog, ContinueImmediately: true);

    }
}
