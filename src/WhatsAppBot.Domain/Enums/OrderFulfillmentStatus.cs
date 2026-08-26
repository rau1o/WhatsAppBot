namespace WhatsAppBot.Domain.Enums;

// Independiente de OrderStatus a propósito: OrderStatus es "en qué parte
// del flujo del bot está el pedido" (Draft/Submitted/Abandoned).
// OrderFulfillmentStatus es "en qué parte de la preparación física está" —
// solo tiene sentido una vez que el pago ya se aprobó.
public enum OrderFulfillmentStatus
{
    Pending,        // pago aprobado, todavía sin preparar
    ReadyForPickup, // preparado, esperando que el cliente lo recoja
    Completed       // el cliente ya se lo llevó
}
