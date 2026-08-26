using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ConversationId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public List<OrderItem> Items { get; set; } = new();

    // Nulo hasta que se aprueba el pago — recién ahí empieza a tener sentido
    // el seguimiento de preparación física del pedido.
    public OrderFulfillmentStatus? FulfillmentStatus { get; set; }
    public decimal Total => Items.Sum(i => i.UnitPrice * i.Quantity);

    // Regla de negocio simple pero que pertenece acá, no al StateHandler:
    // si el producto ya está en el pedido, suma cantidad en vez de duplicar la fila.
    public void AddOrIncrementItem(Product product, int quantity = 1)
    {
        if (quantity < 1)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad tiene que ser al menos 1.");

        var existing = Items.FirstOrDefault(i => i.ProductId == product.Id);
        if (existing is not null)
        {
            existing.Quantity += quantity;
            return;
        }

        Items.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = Id,
            ProductId = product.Id,
            ProductName = product.Name,
            UnitPrice = product.Price,
            Quantity = quantity
        });
    }
}
