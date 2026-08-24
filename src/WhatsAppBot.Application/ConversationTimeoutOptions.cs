namespace WhatsAppBot.Application;

public class ConversationTimeoutOptions
{
    public const string SectionName = "ConversationTimeout";

    // Después de este tiempo sin actividad, un pedido a medio hacer
    // (BuildingOrder/AwaitingPayment/PaymentInReview) se considera
    // abandonado — el próximo mensaje del cliente arranca de cero
    // en vez de retomar el pedido viejo.
    public int StaleAfterHours { get; set; } = 6;
}
