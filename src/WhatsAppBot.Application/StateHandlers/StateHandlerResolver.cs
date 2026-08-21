using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Application.StateHandlers;

public class StateHandlerResolver
{
    private readonly Dictionary<ConversationState, IStateHandler> _handlers;

    public StateHandlerResolver(IEnumerable<IStateHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.State);
    }

    public IStateHandler Resolve(ConversationState state)
    {
        if (!_handlers.TryGetValue(state, out var handler))
            throw new InvalidOperationException($"No hay handler registrado para el estado {state}");

        return handler;
    }
}
