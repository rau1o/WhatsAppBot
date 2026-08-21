using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Infrastructure.Persistence.Configuration
{
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("tenants");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
            builder.Property(t => t.WhatsAppPhoneNumberId).HasMaxLength(64).IsRequired();
            builder.Property(t => t.LocationName).HasMaxLength(200).IsRequired();
            builder.Property(t => t.LocationAddress).HasMaxLength(300).IsRequired();
            builder.Property(t => t.FacadePhotoUrl).HasMaxLength(500).IsRequired();
            builder.Property(t => t.PaymentQrImageUrl).HasMaxLength(500);
            // El webhook resuelve el tenant por este campo en cada mensaje entrante —
            // tiene que ser único y estar indexado.
            builder.HasIndex(t => t.WhatsAppPhoneNumberId).IsUnique();
        }
    }
}
