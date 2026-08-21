using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatsAppBot.Domain.Enums;

namespace WhatsAppBot.Domain.Entities
{
    public class PaymentProof
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid OrderId { get; set; }
        // Referencia al comprobante, no la imagen en sí — la imagen queda en
        // WhatsApp (en el teléfono del cliente y en la app de WhatsApp Business
        // del negocio). El staff la revisa ahí directamente; el panel solo
        // rastrea que llegó y su estado.
        public string WhatsAppMediaId { get; set; } = default!;

        public PaymentProofStatus Status { get; set; } = PaymentProofStatus.Pending;
        public DateTime CreatedAt { get; set; }

        // Quién y cuándo lo validó — null mientras está Pending.
        public Guid? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }
}
