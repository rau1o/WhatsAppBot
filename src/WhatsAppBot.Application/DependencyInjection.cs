using Microsoft.Extensions.DependencyInjection;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.StateHandlers;

namespace WhatsAppBot.Application;

public static class DependencyInjection
{
    // Api solo llama a este método — no necesita saber qué handlers existen.
    // Cada fase nueva agrega su AddScoped<IStateHandler, X>() acá adentro,
    // no en el Program.cs del Api.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IStateHandler, NewConversationStateHandler>();
        services.AddScoped<IStateHandler, CatalogStateHandler>();
        services.AddScoped<IStateHandler, OrderReviewStateHandler>();
        services.AddScoped<IStateHandler, PaymentProofStateHandler>();
        services.AddScoped<IStateHandler, PaymentInReviewStateHandler>();
        services.AddScoped<IStateHandler, ConfirmedStateHandler>();

        services.AddScoped<StateHandlerResolver>();
        services.AddScoped<IMessageProcessor, MessageProcessor>();

        return services;
    }
}
