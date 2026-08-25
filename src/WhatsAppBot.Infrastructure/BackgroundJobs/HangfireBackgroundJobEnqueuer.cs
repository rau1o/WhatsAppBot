using Hangfire;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;


namespace WhatsAppBot.Infrastructure.BackgroundJobs
{
    public class HangfireBackgroundJobEnqueuer : IBackgroundJobEnqueuer
    {
        private readonly IBackgroundJobClient _client;

        public HangfireBackgroundJobEnqueuer(IBackgroundJobClient client)
        {
            _client = client;
        }

        public void EnqueueProcessMessage(Guid tenantId, IncomingMessage message, string correlationId)
        {
            // Hangfire serializa los argumentos (Newtonsoft.Json) y los persiste
            // en el storage — por eso IncomingMessage viaja como datos simples,
            // no como algo con lógica adentro.
            _client.Enqueue<IMessageProcessor>(p => p.ProcessAsync(tenantId, message, correlationId, CancellationToken.None));
        }
    }
}
