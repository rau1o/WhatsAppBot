namespace WhatsAppBot.Application.StateHandlers;

internal static class CatalogInteractionIds
{
    public const string AddMore = "catalog:add_more";
    public const string FinishOrder = "catalog:finish";
    public const string ViewOrder = "catalog:view_order";
    public const string ProductRowPrefix = "product:";
    //public const string QuantityPrefix = "qty:"; // formato: qty:{productId}:{cantidad}
    public const string PagePrefix = "catalog:page:"; // formato: catalog:page:{número de página}
    public const string QuantityCancel = "catalog:quantity_cancel";
}
