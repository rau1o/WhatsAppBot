using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppBot.Domain.Enums
{
    public enum PaymentProofStatus
    {
        Pending,   // esperando que un empleado lo revise
        Approved,
        Rejected
    }
}
