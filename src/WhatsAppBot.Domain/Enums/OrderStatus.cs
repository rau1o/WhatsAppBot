namespace WhatsAppBot.Domain.Enums;

public enum OrderStatus
{
    Draft,      // el cliente todavía está agregando productos
    Submitted,  // cliente finalizó — fase 3 agrega AwaitingPayment/PaymentInReview/Confirmed a partir de acá
    Abandoned   // quedó sin comprobante por demasiado tiempo — ver ConversationTimeoutOptions

}
