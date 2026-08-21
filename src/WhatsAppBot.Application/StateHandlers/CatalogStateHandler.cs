using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

// Fase 2: el cliente ve el catálogo, elige productos (uno por uno,
// pueden ser varios mensajes seguidos) y decide cuándo finalizar.
public class CatalogStateHandler : IStateHandler
{
    private readonly IWhatsAppMessageSender _sender;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;

    public CatalogStateHandler(IWhatsAppMessageSender sender, IProductRepository products, IOrderRepository orders)
    {
        _sender = sender;
        _products = products;
        _orders = orders;
    }

    public ConversationState State => ConversationState.BrowsingCatalog;

    public async Task<StateResult> HandleAsync(
        Tenant tenant,
        Conversation conversation,
        IncomingMessage message,
        CancellationToken ct)
    {
        var to = conversation.CustomerPhoneNumber;
        var phoneNumberId = tenant.WhatsAppPhoneNumberId;

        // Cliente terminó de elegir productos.
        if (message.InteractiveButtonId == CatalogInteractionIds.FinishOrder)
        {
            return new StateResult(ConversationState.BuildingOrder);
        }

        // Cliente eligió un producto de la lista.
        if (message.ListReplyId is not null && message.ListReplyId.StartsWith(CatalogInteractionIds.ProductRowPrefix))
        {
            var handled = await TryAddProductToOrderAsync(tenant, conversation, message.ListReplyId, phoneNumberId, to, ct);
            if (handled) return new StateResult(ConversationState.BrowsingCatalog);
        }

        // Primer mensaje en este estado, pidió ver más productos, o no
        // entendimos lo que mandó — en todos los casos, mostramos el catálogo.
        await SendCatalogAsync(tenant, phoneNumberId, to, ct);
        return new StateResult(ConversationState.BrowsingCatalog);
    }

    private async Task<bool> TryAddProductToOrderAsync(
        Tenant tenant, Conversation conversation, string listReplyId, string phoneNumberId, string to, CancellationToken ct)
    {
        var rawProductId = listReplyId[CatalogInteractionIds.ProductRowPrefix.Length..];
        if (!Guid.TryParse(rawProductId, out var productId)) return false;

        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null || !product.IsActive) return false;

        var order = await _orders.GetOrCreateDraftAsync(conversation.Id, ct);
        order.AddOrIncrementItem(product);
        await _orders.SaveAsync(order, ct);

        await _sender.SendTextAsync(phoneNumberId, to,
            $"Agregado: {product.Name} (Bs {product.Price:N2}) ✅", ct);

        await _sender.SendInteractiveButtonsAsync(phoneNumberId, to,
            "¿Querés agregar otro producto o finalizar el pedido?",
            new[]
            {
                new InteractiveButton(CatalogInteractionIds.AddMore, "Agregar otro"),
                new InteractiveButton(CatalogInteractionIds.FinishOrder, "Finalizar pedido")
            }, ct);

        return true;
    }

    private async Task SendCatalogAsync(Tenant tenant, string phoneNumberId, string to, CancellationToken ct)
    {
        var products = await _products.ListActiveAsync(ct);

        if (products.Count == 0)
        {
            await _sender.SendTextAsync(phoneNumberId, to,
                "Todavía no tenemos productos cargados. Un asesor te va a contactar en breve para ayudarte 🙌", ct);
            return;
        }

        // Límite de la Cloud API: máximo 10 filas por lista. Si el catálogo
        // crece más que eso, hay que sumar categorías/paginado — fuera de
        // alcance por ahora.
        var rows = products
            .Take(10)
            .Select(p => new InteractiveListRow(
                Id: $"{CatalogInteractionIds.ProductRowPrefix}{p.Id}",
                Title: Truncate(p.Name, 24),
                Description: $"Bs {p.Price:N2}"))
            .ToList();

        await _sender.SendInteractiveListAsync(
            phoneNumberId, to,
            bodyText: "Elegí un producto de nuestro catálogo:",
            buttonText: "Ver productos",
            sections: new[] { new InteractiveListSection("Productos disponibles", rows) },
            ct);
    }

    // WhatsApp trunca (o rechaza) títulos de fila más largos que 24 caracteres.
    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
