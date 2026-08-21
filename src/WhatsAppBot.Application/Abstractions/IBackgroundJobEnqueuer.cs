using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Application.Messaging;

namespace WhatsAppBot.Application.Abstractions
{
    // Puerto: Application solo sabe que puede "encolar el procesamiento de un
    // mensaje". No conoce Hangfire ni ninguna otra tecnología de colas —
    // eso lo decide Infrastructure. Así el día de mañana se puede cambiar
    // Hangfire por Azure Queues o lo que sea sin tocar Application ni Api.
    public interface IBackgroundJobEnqueuer
    {
        void EnqueueProcessMessage(Guid tenantId, IncomingMessage message);
    }
}
