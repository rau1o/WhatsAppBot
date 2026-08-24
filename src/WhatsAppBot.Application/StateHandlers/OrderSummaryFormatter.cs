using System.Text;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Application.StateHandlers;

internal static class OrderSummaryFormatter
{
    public static string BuildSummary(Order order)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📋 *Resumen de tu pedido*");

        foreach (var item in order.Items)
            sb.AppendLine($"• {item.Quantity}x {item.ProductName} — Bs {item.UnitPrice * item.Quantity:N2}");

        sb.AppendLine();
        sb.Append($"*Total: Bs {order.Total:N2}*");

        return sb.ToString();
    }
}
