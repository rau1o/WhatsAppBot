using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = "RequireManagerOrAbove")]
public class ReportsController : ControllerBase
{
    private const int MaxRangeDays = 366; // un año — evita que alguien pida un rango absurdo por error y tumbe el server agregando todo

    private readonly IOrderRepository _orders;

    public ReportsController(IOrderRepository orders)
    {
        _orders = orders;
    }

    [HttpGet("sales")]
    public async Task<IActionResult> GetSalesReport([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var effectiveTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveFrom = from ?? effectiveTo.AddDays(-29); // últimos 30 días por default

        if (effectiveFrom > effectiveTo)
            return BadRequest(new { message = "La fecha 'from' no puede ser posterior a 'to'." });

        if (effectiveTo.DayNumber - effectiveFrom.DayNumber > MaxRangeDays)
            return BadRequest(new { message = $"El rango no puede superar los {MaxRangeDays} días." });

        // CreatedAt es UTC — tomamos el rango completo del día en UTC. Para
        // un negocio en un solo huso horario (Bolivia, UTC-4) esto puede
        // correr los reportes unas horas respecto a "medianoche local", pero
        // no vale la pena la complejidad de manejar zonas horarias por
        // tenant todavía para un reporte de este tamaño.
        var fromUtc = effectiveFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = effectiveTo.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var orders = await _orders.ListPaidOrdersInRangeAsync(fromUtc, toUtc, ct);

        var summary = new SalesSummaryDto(
            TotalRevenue: orders.Sum(o => o.Total),
            OrderCount: orders.Count,
            AverageOrderValue: orders.Count > 0 ? orders.Sum(o => o.Total) / orders.Count : 0);

        var topProducts = orders
            .SelectMany(o => o.Items)
            .GroupBy(i => i.ProductName)
            .Select(g => new TopProductDto(g.Key, g.Sum(i => i.Quantity), g.Sum(i => i.Quantity * i.UnitPrice)))
            .OrderByDescending(p => p.QuantitySold)
            .Take(10)
            .ToList();

        var dailySales = orders
            .GroupBy(o => DateOnly.FromDateTime(o.CreatedAt))
            .Select(g => new DailySalesDto(g.Key, g.Sum(o => o.Total), g.Count()))
            .OrderBy(d => d.Date)
            .ToList();

        return Ok(new SalesReportDto(effectiveFrom, effectiveTo, summary, topProducts, dailySales));
    }
}
