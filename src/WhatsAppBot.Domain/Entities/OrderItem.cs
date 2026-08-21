namespace WhatsAppBot.Domain.Entities;

public class OrderItem
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }

    // Snapshot del nombre/precio al momento de agregarlo — si el producto
    // cambia de precio después, el pedido ya hecho no se altera solo.
    public string ProductName { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
}
