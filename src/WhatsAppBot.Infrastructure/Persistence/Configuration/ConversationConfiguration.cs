using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Infrastructure.Persistence.Configuration
{
    public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
    {
        public void Configure(EntityTypeBuilder<Conversation> builder)
        {
            builder.ToTable("conversations");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.CustomerPhoneNumber).HasMaxLength(32).IsRequired();

            // ConversationState es un enum — se guarda como texto en vez de int
            // para que la tabla sea legible directamente en la base y agregar
            // un estado nuevo no reordene los valores existentes.
            builder.Property(c => c.State)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            // Una conversación por (tenant, número de cliente) — así GetOrCreateAsync
            // puede confiar en esta combinación como identidad natural.
            builder.HasIndex(c => new { c.TenantId, c.CustomerPhoneNumber }).IsUnique();
        }
    }
}
