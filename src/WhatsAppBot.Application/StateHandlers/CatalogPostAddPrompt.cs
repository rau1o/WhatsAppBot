using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;

namespace WhatsAppBot.Application.StateHandlers;

internal static class CatalogPostAddPrompt
{
    public static Task SendAsync(IWhatsAppMessageSender sender, string phoneNumberId, string to, CancellationToken ct)
        => sender.SendInteractiveButtonsAsync(phoneNumberId, to,
            "¿Qué querés hacer ahora?",
            new[]
            {
                new InteractiveButton(CatalogInteractionIds.AddMore, "Agregar otro"),
                new InteractiveButton(CatalogInteractionIds.ViewOrder, "Ver pedido"),
                new InteractiveButton(CatalogInteractionIds.FinishOrder, "Finalizar pedido")
            }, ct);
}
