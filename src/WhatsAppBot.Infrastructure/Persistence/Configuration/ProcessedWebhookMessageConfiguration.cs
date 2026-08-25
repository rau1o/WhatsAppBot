using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace WhatsAppBot.Infrastructure.Persistence.Configurations;

public class ProcessedWebhookMessageConfiguration : IEntityTypeConfiguration<ProcessedWebhookMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookMessage> builder)
    {
        builder.ToTable("processed_webhook_messages");

        // El message_id de WhatsApp ES la clave — no necesitamos un Guid
        // aparte, y usarlo como PK nos da la constraint de unicidad gratis
        // (es lo que hace que el segundo intento de guardar el mismo
        // mensaje falle, y ese fallo es justamente cómo detectamos el duplicado).
        builder.HasKey(p => p.WhatsAppMessageId);
        builder.Property(p => p.WhatsAppMessageId).HasMaxLength(128);
    }
}
