using Hangfire;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;


namespace WhatsAppBot.Infrastructure.BackgroundJobs
{
    public class HangfireBackgroundJobEnqueuer : IBackgroundJobEnqueuer
    {
        private readonly IBackgroundJobClient _cliet;

        public HangfireBackgroundJobEnqueuer(IBackgroundJobClient client)
        {
            _cliet = client;
        }
        public void EnqueueProcessMessage(Guid tenantId, IncomingMessage message)
        {
            // Hangfire serializa los argumentos (Newtonsoft.Json) y los persiste
            // en el storage — por eso IncomingMessage viaja como datos simples,
            // no como algo con lógica adentro.
            _cliet.Enqueue<IMessageProcessor>(x => x.ProcessAsync(tenantId, message, CancellationToken.None));
        }
    }
}
