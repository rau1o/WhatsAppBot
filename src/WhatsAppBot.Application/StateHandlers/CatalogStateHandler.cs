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

        // Cliente quiere ver qué lleva hasta ahora, sin finalizar todavía.
        if (message.InteractiveButtonId == CatalogInteractionIds.ViewOrder)
        {
            await ShowOrderSoFarAsync(tenant, conversation, phoneNumberId, to, ct);
            return new StateResult(ConversationState.BrowsingCatalog);
        }

        // Cliente ya eligió cuántas unidades quiere — acá recién se agrega al pedido.
        if (message.InteractiveButtonId is not null && message.InteractiveButtonId.StartsWith(CatalogInteractionIds.QuantityPrefix))
        {
            var handled = await TryAddChosenQuantityAsync(conversation, message.InteractiveButtonId, phoneNumberId, to, ct);
            if (handled) return new StateResult(ConversationState.BrowsingCatalog);
        }

        // Cliente eligió un producto de la lista — preguntamos cantidad antes de agregarlo.
        if (message.ListReplyId is not null && message.ListReplyId.StartsWith(CatalogInteractionIds.ProductRowPrefix))
        {
            var handled = await TryAskQuantityAsync(message.ListReplyId, phoneNumberId, to, ct);
            if (handled) return new StateResult(ConversationState.BrowsingCatalog);
        }

        // Primer mensaje en este estado, pidió ver más productos, o no
        // entendimos lo que mandó — en todos los casos, mostramos el catálogo.
        await SendCatalogAsync(tenant, phoneNumberId, to, ct);
        return new StateResult(ConversationState.BrowsingCatalog);

    }
    private async Task ShowOrderSoFarAsync(Tenant tenant, Conversation conversation, string phoneNumberId, string to, CancellationToken ct)
    {
        var order = await _orders.GetOrCreateDraftAsync(conversation.Id, ct);

        if (order.Items.Count == 0)
        {
            await _sender.SendTextAsync(phoneNumberId, to,
                "Todavía no agregaste ningún producto. Elegí algo del catálogo:", ct);
            await SendCatalogAsync(tenant, phoneNumberId, to, ct);
            return;
        }

        await _sender.SendTextAsync(phoneNumberId, to, OrderSummaryFormatter.BuildSummary(order), ct);
        await SendPostAddButtonsAsync(phoneNumberId, to, ct);
    }
    // Máximo 3 botones (límite de WhatsApp) — para más de 3 unidades, el
    // cliente vuelve a elegir el mismo producto y se suma sobre lo que ya tenía.
    private async Task<bool> TryAskQuantityAsync(string listReplyId, string phoneNumberId, string to, CancellationToken ct)
    {
        var rawProductId = listReplyId[CatalogInteractionIds.ProductRowPrefix.Length..];
        if (!Guid.TryParse(rawProductId, out var productId)) return false;

        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null || !product.IsActive) return false;

        await _sender.SendInteractiveButtonsAsync(phoneNumberId, to,
            $"¿Cuántas unidades de {product.Name} querés?",
            new[]
            {
                new InteractiveButton($"{CatalogInteractionIds.QuantityPrefix}{productId}:1", "1"),
                new InteractiveButton($"{CatalogInteractionIds.QuantityPrefix}{productId}:2", "2"),
                new InteractiveButton($"{CatalogInteractionIds.QuantityPrefix}{productId}:3", "3")
            }, ct);

        return true;
    }
    private async Task<bool> TryAddChosenQuantityAsync(
        Conversation conversation, string buttonId, string phoneNumberId, string to, CancellationToken ct)
    {
        // buttonId tiene la forma "qty:{productId}:{cantidad}"
        var raw = buttonId[CatalogInteractionIds.QuantityPrefix.Length..];
        var parts = raw.Split(':');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var productId) || !int.TryParse(parts[1], out var quantity))
            return false;

        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null || !product.IsActive) return false;

        var order = await _orders.GetOrCreateDraftAsync(conversation.Id, ct);
        order.AddOrIncrementItem(product, quantity);
        await _orders.SaveAsync(order, ct);

        await _sender.SendTextAsync(phoneNumberId, to,
            $"Agregado: {quantity}x {product.Name} (Bs {product.Price:N2} c/u) ✅", ct);

        await SendPostAddButtonsAsync(phoneNumberId, to, ct);

        return true;
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

        await SendPostAddButtonsAsync(phoneNumberId, to, ct);

        return true;

    }

    private Task SendPostAddButtonsAsync(string phoneNumberId, string to, CancellationToken ct)
       => _sender.SendInteractiveButtonsAsync(phoneNumberId, to,
           "¿Qué querés hacer ahora?",
           new[]
           {
                new InteractiveButton(CatalogInteractionIds.AddMore, "Agregar otro"),
                new InteractiveButton(CatalogInteractionIds.ViewOrder, "Ver pedido"),
                new InteractiveButton(CatalogInteractionIds.FinishOrder, "Finalizar pedido")
           }, ct);

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
