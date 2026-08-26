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

        // Cliente terminó de elegir productos. ContinueImmediately: true
        // porque acá no mandamos nada nosotros — es OrderReviewStateHandler
        // el que arma el resumen + QR + pedido de comprobante.
        if (message.InteractiveButtonId == CatalogInteractionIds.FinishOrder)
        {
            return new StateResult(ConversationState.BuildingOrder, ContinueImmediately: true);
        }

        // Cliente quiere ver qué lleva hasta ahora, sin finalizar todavía.
        if (message.InteractiveButtonId == CatalogInteractionIds.ViewOrder)
        {
            await ShowOrderSoFarAsync(tenant, conversation, phoneNumberId, to, ct);
            return new StateResult(ConversationState.BrowsingCatalog);
        }

        // Cliente eligió un producto de la lista — a partir de acá le
        // pedimos que ESCRIBA la cantidad (fuera de este handler, en
        // QuantityInputStateHandler).
        if (message.ListReplyId is not null && message.ListReplyId.StartsWith(CatalogInteractionIds.ProductRowPrefix))
        {
            var handled = await TryAskQuantityAsync(conversation, message.ListReplyId, phoneNumberId, to, ct);
            if (handled) return new StateResult(ConversationState.AwaitingQuantity);
        }       

        // Cliente tocó "Ver más productos" — es una fila de la lista, no un botón.
        if (message.ListReplyId is not null && message.ListReplyId.StartsWith(CatalogInteractionIds.PagePrefix))
        {
            var pageRaw = message.ListReplyId[CatalogInteractionIds.PagePrefix.Length..];
            var page = int.TryParse(pageRaw, out var parsedPage) ? parsedPage : 0;
            await SendCatalogAsync(tenant, phoneNumberId, to, ct, page);
            return new StateResult(ConversationState.BrowsingCatalog);
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
        await CatalogPostAddPrompt.SendAsync(_sender, phoneNumberId, to, ct);
    }

    // Guarda qué producto quedó pendiente y le pide al cliente que escriba
    // la cantidad — la validación y el agregado real al pedido pasan en
    // QuantityInputStateHandler, en el próximo mensaje.
    private async Task<bool> TryAskQuantityAsync(Conversation conversation, string listReplyId, string phoneNumberId, string to, CancellationToken ct)
    {
        var rawProductId = listReplyId[CatalogInteractionIds.ProductRowPrefix.Length..];
        if (!Guid.TryParse(rawProductId, out var productId)) return false;

        var product = await _products.GetByIdAsync(productId, ct);
        if (product is null || !product.IsActive) return false;

        conversation.PendingProductId = productId;

        await _sender.SendInteractiveButtonsAsync(phoneNumberId, to,
            $"¿Cuántas unidades de *{product.Name}* querés? Escribí el número (por ejemplo: 5).",
            new[] { new InteractiveButton(CatalogInteractionIds.QuantityCancel, "Cancelar") }, ct);

        return true;
    }
   
    // Se deja 1 fila libre para "Ver más productos" cuando hace falta —
    // por eso 9, no 10 (el límite real de WhatsApp es 10 filas por lista).
    private const int ProductsPerPage = 9;
    private async Task SendCatalogAsync(Tenant tenant, string phoneNumberId, string to, CancellationToken ct, int page = 0)
    {
        var allProducts = await _products.ListActiveAsync(ct);

        if (allProducts.Count == 0)
        {
            await _sender.SendTextAsync(phoneNumberId, to,
                "Todavía no tenemos productos cargados. Un asesor te va a contactar en breve para ayudarte 🙌", ct);
            return;
        }

        var pageProducts = allProducts.Skip(page * ProductsPerPage).Take(ProductsPerPage).ToList();
        var hasMorePages = allProducts.Count > (page + 1) * ProductsPerPage;

        var rows = pageProducts
            .Take(10)
            .Select(p => new InteractiveListRow(
                Id: $"{CatalogInteractionIds.ProductRowPrefix}{p.Id}",
                Title: Truncate(p.Name, 24),
                Description: $"Bs {p.Price:N2}"))
            .ToList();

        if (hasMorePages)
        {
            rows.Add(new InteractiveListRow(
                Id: $"{CatalogInteractionIds.PagePrefix}{page + 1}",
                Title: "Ver más productos"));
        }

        var bodyText = page == 0
            ? "Elegí un producto de nuestro catálogo:"
            : $"Más productos (página {page + 1}):";

        await _sender.SendInteractiveListAsync(
            phoneNumberId, to,
            bodyText: bodyText,
            buttonText: "Ver productos",
            sections: new[] { new InteractiveListSection("Productos disponibles", rows) },
            ct);

    }

    // WhatsApp trunca (o rechaza) títulos de fila más largos que 24 caracteres.
    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 1)] + "…";
}
