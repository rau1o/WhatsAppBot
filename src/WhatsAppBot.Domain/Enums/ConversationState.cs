namespace WhatsAppBot.Domain.Enums;

public enum ConversationState
{
    New,              // primer mensaje del cliente, todavía no se le respondió nada
    Greeted,          // ya recibió ubicación + foto de fachada (fin de fase 1)
    BrowsingCatalog,  // fase 2
    BuildingOrder,    // fase 2
    AwaitingPayment,  // fase 3
    PaymentInReview,  // fase 3
    Confirmed         // fase 3
}
