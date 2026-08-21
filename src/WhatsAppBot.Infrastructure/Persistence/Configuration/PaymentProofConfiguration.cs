using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Infrastructure.Persistence.Configurations;

public class PaymentProofConfiguration : IEntityTypeConfiguration<PaymentProof>
{
    public void Configure(EntityTypeBuilder<PaymentProof> builder)
    {
        builder.ToTable("payment_proofs");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.WhatsAppMediaId).HasMaxLength(128).IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.Status });
    }
}
