using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Infrastructure.Persistence.Repositories
{
    public class EfConversationRepository : IConversationRepository
    { 
        private readonly WhatsAppBotDbContext _db;
        private readonly ICurrentTenantAccessor _currentTenant;
        private readonly ILogger<EfConversationRepository> _logger;
        public EfConversationRepository(WhatsAppBotDbContext db, ICurrentTenantAccessor currentTenant, ILogger<EfConversationRepository> logger)
        {
            _db = db;
            _currentTenant = currentTenant;
            _logger = logger;
        }

        public async Task<Conversation> GetOrCreateAsync(string customerPhoneNumber, CancellationToken ct)
        {
            var tenantId = RequireTenantId();

            // El global query filter del DbContext ya acota esto al tenant actual,
            // pero comparamos TenantId explícitamente igual por claridad del código.
            var existing = await _db.Conversations.FirstOrDefaultAsync(
                c => c.TenantId == tenantId && c.CustomerPhoneNumber == customerPhoneNumber, ct);

            if (existing is not null) return existing;

            var conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CustomerPhoneNumber = customerPhoneNumber,
                State = ConversationState.New,
                LastMessageAt = DateTime.UtcNow
            };

            _db.Conversations.Add(conversation);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Dos mensajes casi simultáneos del mismo cliente podrían chocar acá
                // contra el índice único (TenantId, CustomerPhoneNumber). En ese caso
                // alguien más ya la creó — la recuperamos en vez de fallar el request.
                _db.Entry(conversation).State = EntityState.Detached;
                existing = await _db.Conversations.FirstAsync(
                    c => c.TenantId == tenantId && c.CustomerPhoneNumber == customerPhoneNumber, ct);
                return existing;
            }

            return conversation;
        }

        public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);

        public async Task SaveAsync(Conversation conversation, CancellationToken ct)
        {
            var tenantId = RequireTenantId();

            // Nunca confiar ciegamente en el TenantId que ya trae la entidad en memoria:
            // si viniera de otro tenant por error de programación, esto lo corta acá
            // en vez de dejar que se guarde cruzado.
            if (conversation.TenantId != tenantId)
                throw new InvalidOperationException(
                    $"Se intentó guardar una conversación del tenant {conversation.TenantId} " +
                    $"desde un contexto con tenant actual {tenantId}.");

            _db.Conversations.Update(conversation);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Mismo motivo que en EfOrderRepository: puede pasar si la
                // conversación se borró/modificó por fuera de este DbContext
                // (ej. un reintento tardío de Hangfire sobre datos que un job
                // más nuevo ya cambió, o una limpieza manual de datos de prueba).
                // No tumbamos el job por esto — y limpiamos el change tracker
                // para no arrastrar un estado inconsistente al resto del proceso.
                _logger.LogWarning(ex,
                    "Concurrencia al guardar la conversación {ConversationId} — se descarta este intento.",
                    conversation.Id);
                _db.ChangeTracker.Clear();
            }

        }

        public async Task<PagedResult<Conversation>> ListRecentAsync(int page, int pageSize, CancellationToken ct)
        {
            // Sin esto, si no hay tenant seteado el global query filter devuelve
            // una lista vacía silenciosa (porque compara contra null) — preferimos
            // fallar fuerte para que el bug de "olvidé setear el tenant" no se
            // disfrace de "este tenant no tiene conversaciones".
            RequireTenantId();

            var query = _db.Conversations.OrderByDescending(c => c.LastMessageAt);

            var totalCount = await query.CountAsync(ct);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return new PagedResult<Conversation>(items, page, pageSize, totalCount);
        }

        private Guid RequireTenantId()
            => _currentTenant.TenantId
               ?? throw new InvalidOperationException(
                   "No hay un tenant actual seteado en este scope. " +
                   "ICurrentTenantAccessor.SetTenant(...) debe llamarse antes de usar este repositorio.");
    }
}
