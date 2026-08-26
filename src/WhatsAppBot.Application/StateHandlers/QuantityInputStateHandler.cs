using System.Globalization;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

// Después de que el cliente elige un producto de la lista, este handler
// espera que escriba la cantidad como texto libre — con todas las
// validaciones antes de tocar el pedido.
public class QuantityInputStateHandler : IStateHandler
{
    // Tope para frenar errores de tipeo (ej. un "0" de más sin querer) —
    // no es un límite de negocio real, es una red de seguridad.
    private const int MaxQuantity = 500;

    private readonly IWhatsAppMessageSender _sender;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;

    public QuantityInputStateHandler(IWhatsAppMessageSender sender, IProductRepository products, IOrderRepository orders)
    {
        _sender = sender;
        _products = products;
        _orders = orders;
    }

    public ConversationState State => ConversationState.AwaitingQuantity;

    public async Task<StateResult> HandleAsync(
        Tenant tenant,
        Conversation conversation,
        IncomingMessage message,
        CancellationToken ct)
    {
        var to = conversation.CustomerPhoneNumber;
        var phoneNumberId = tenant.WhatsAppPhoneNumberId;

        if (message.InteractiveButtonId == CatalogInteractionIds.QuantityCancel)
        {
            conversation.PendingProductId = null;
            await _sender.SendTextAsync(phoneNumberId, to, "Sin problema, volvamos al catálogo:", ct);
            return new StateResult(ConversationState.BrowsingCatalog, ContinueImmediately: true);
        }

        if (conversation.PendingProductId is null)
        {
            // No debería pasar en un flujo normal (implica llegar a este
            // estado sin haber pasado por CatalogStateHandler) — lo tratamos
            // como recuperación defensiva en vez de romper la conversación.
            return new StateResult(ConversationState.BrowsingCatalog, ContinueImmediately: true);
        }

        var product = await _products.GetByIdAsync(conversation.PendingProductId.Value, ct);
        if (product is null || !product.IsActive)
        {
            conversation.PendingProductId = null;
            await _sender.SendTextAsync(phoneNumberId, to,
                "Ese producto ya no está disponible. Te muestro el catálogo de nuevo:", ct);
            return new StateResult(ConversationState.BrowsingCatalog, ContinueImmediately: true);
        }

        var (isValid, quantity, errorMessage) = ValidateQuantity(message.Text);
        if (!isValid)
        {
            // Se queda en AwaitingQuantity (no se pierde el producto elegido)
            // para que pueda reintentar sin volver a elegirlo de la lista.
            await _sender.SendTextAsync(phoneNumberId, to, errorMessage!, ct);
            return new StateResult(ConversationState.AwaitingQuantity);
        }

        var order = await _orders.GetOrCreateDraftAsync(conversation.Id, ct);
        order.AddOrIncrementItem(product, quantity);
        var saved = await _orders.SaveAsync(order, ct);

        conversation.PendingProductId = null;

        if (!saved)
        {
            // Nunca confirmarle al cliente algo que en realidad no se guardó —
            // mejor pedirle que reintente que mentirle sobre el estado de su pedido.
            await _sender.SendTextAsync(phoneNumberId, to,
                "Uy, tuvimos un problema agregando ese producto. ¿Podés elegirlo de nuevo del catálogo?", ct);
            return new StateResult(ConversationState.BrowsingCatalog, ContinueImmediately: true);
        }

        await _sender.SendTextAsync(phoneNumberId, to,
            $"Agregado: {quantity}x {product.Name} (Bs {product.Price:N2} c/u) ✅", ct);

        await CatalogPostAddPrompt.SendAsync(_sender, phoneNumberId, to, ct);

        return new StateResult(ConversationState.BrowsingCatalog);
    }

    private static (bool IsValid, int Quantity, string? ErrorMessage) ValidateQuantity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (false, 0, "Escribí la cantidad que querés, solo el número (por ejemplo: 5).");

        var trimmed = text.Trim();

        // NumberStyles.None: solo dígitos, nada de "+5", "5.0", "5,000" ni
        // espacios internos — la forma más simple posible de escribir un
        // número entero.
        if (!int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity))
            return (false, 0, "No entendí esa cantidad 🤔 Escribí solo el número, sin letras ni símbolos (por ejemplo: 5).");

        if (quantity <= 0)
            return (false, 0, "La cantidad tiene que ser al menos 1.");

        if (quantity > MaxQuantity)
            return (false, 0,
                $"Esa cantidad es muy alta para pedir por acá (máximo {MaxQuantity}). Si necesitás más, escribinos directamente y te ayudamos.");

        return (true, quantity, null);
    }
}
