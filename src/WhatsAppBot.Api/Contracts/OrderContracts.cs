namespace WhatsAppBot.Api.Contracts;

public record OrderItemLineDto(string ProductName, int Quantity, decimal UnitPrice);

public record FulfillmentOrderDto(
    Guid Id,
    string CustomerPhoneNumber,
    decimal Total,
    string FulfillmentStatus,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemLineDto> Items
);
