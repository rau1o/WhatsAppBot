using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.Messaging;

// ContinueImmediately = true le dice a MessageProcessor que dispare el
// handler del NUEVO estado ya mismo, en vez de esperar el próximo mensaje
// real del cliente. Se usa cuando la transición necesita mostrar contenido
// que nadie más va a mandar (ej. el catálogo apenas termina el saludo, o
// el resumen+QR apenas el cliente toca "Finalizar pedido"). Default false
// porque la mayoría de los handlers ya mandan su propio mensaje de "entré
// a este estado" antes de transicionar, y re-disparar ahí sería redundante.
public record StateResult(ConversationState NextState, bool ContinueImmediately = false);
