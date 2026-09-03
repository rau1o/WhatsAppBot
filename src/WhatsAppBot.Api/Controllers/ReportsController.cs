using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;

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
        var rangeResult = ValidateRange(from, to);
        if (rangeResult.Error is not null) return BadRequest(new { message = rangeResult.Error });

        var report = await BuildReportAsync(rangeResult.From, rangeResult.To, ct);
        return Ok(report);
    }

    [HttpGet("sales/export")]
    public async Task<IActionResult> ExportSalesReport([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var rangeResult = ValidateRange(from, to);
        if (rangeResult.Error is not null) return BadRequest(new { message = rangeResult.Error });

        var report = await BuildReportAsync(rangeResult.From, rangeResult.To, ct);

        using var workbook = new XLWorkbook();

        var summarySheet = workbook.Worksheets.Add("Resumen");
        summarySheet.Cell(1, 1).Value = "Reporte de ventas";
        summarySheet.Cell(1, 1).Style.Font.Bold = true;
        summarySheet.Cell(2, 1).Value = $"Período: {report.From:dd/MM/yyyy} - {report.To:dd/MM/yyyy}";

        summarySheet.Cell(4, 1).Value = "";
        summarySheet.Cell(4, 2).Value = "Este período";
        summarySheet.Cell(4, 3).Value = "Período anterior";
        summarySheet.Range(4, 1, 4, 3).Style.Font.Bold = true;

        summarySheet.Cell(5, 1).Value = "Total vendido (Bs)";
        summarySheet.Cell(5, 2).Value = report.Summary.TotalRevenue;
        summarySheet.Cell(5, 3).Value = report.PreviousPeriodSummary.TotalRevenue;

        summarySheet.Cell(6, 1).Value = "Cantidad de pedidos";
        summarySheet.Cell(6, 2).Value = report.Summary.OrderCount;
        summarySheet.Cell(6, 3).Value = report.PreviousPeriodSummary.OrderCount;

        summarySheet.Cell(7, 1).Value = "Ticket promedio (Bs)";
        summarySheet.Cell(7, 2).Value = report.Summary.AverageOrderValue;
        summarySheet.Cell(7, 3).Value = report.PreviousPeriodSummary.AverageOrderValue;

        summarySheet.Columns().AdjustToContents();

        var productsSheet = workbook.Worksheets.Add("Productos más vendidos");
        productsSheet.Cell(1, 1).Value = "Producto";
        productsSheet.Cell(1, 2).Value = "Unidades vendidas";
        productsSheet.Cell(1, 3).Value = "Ingresos (Bs)";
        productsSheet.Range(1, 1, 1, 3).Style.Font.Bold = true;

        var productRow = 2;
        foreach (var product in report.TopProducts)
        {
            productsSheet.Cell(productRow, 1).Value = product.ProductName;
            productsSheet.Cell(productRow, 2).Value = product.QuantitySold;
            productsSheet.Cell(productRow, 3).Value = product.Revenue;
            productRow++;
        }
        productsSheet.Columns().AdjustToContents();

        var dailySheet = workbook.Worksheets.Add("Ventas por día");
        dailySheet.Cell(1, 1).Value = "Fecha";
        dailySheet.Cell(1, 2).Value = "Total (Bs)";
        dailySheet.Cell(1, 3).Value = "Pedidos";
        dailySheet.Range(1, 1, 1, 3).Style.Font.Bold = true;

        var dailyRow = 2;
        foreach (var day in report.DailySales)
        {
            dailySheet.Cell(dailyRow, 1).Value = day.Date.ToString("dd/MM/yyyy");
            dailySheet.Cell(dailyRow, 2).Value = day.Total;
            dailySheet.Cell(dailyRow, 3).Value = day.OrderCount;
            dailyRow++;
        }
        dailySheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var fileName = $"reporte-ventas-{report.From:yyyyMMdd}-{report.To:yyyyMMdd}.xlsx";
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    private static (DateOnly From, DateOnly To, string? Error) ValidateRange(DateOnly? from, DateOnly? to)
    {
        var effectiveTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveFrom = from ?? effectiveTo.AddDays(-29); // últimos 30 días por default

        if (effectiveFrom > effectiveTo)
            return (effectiveFrom, effectiveTo, "La fecha 'from' no puede ser posterior a 'to'.");

        if (effectiveTo.DayNumber - effectiveFrom.DayNumber > MaxRangeDays)
            return (effectiveFrom, effectiveTo, $"El rango no puede superar los {MaxRangeDays} días.");

        return (effectiveFrom, effectiveTo, null);
    }

    private async Task<SalesReportDto> BuildReportAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        // CreatedAt es UTC — tomamos el rango completo del día en UTC. Para
        // un negocio en un solo huso horario (Bolivia, UTC-4) esto puede
        // correr los reportes unas horas respecto a "medianoche local", pero
        // no vale la pena la complejidad de manejar zonas horarias por
        // tenant todavía para un reporte de este tamaño.
        var orders = await _orders.ListPaidOrdersInRangeAsync(
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
            ct);

        var summary = Summarize(orders);

        // Mismo largo de días, inmediatamente antes — no el mes calendario
        // anterior completo, para que comparar "lo que va del mes" contra
        // "el mismo tramo del mes pasado" sea una comparación pareja.
        var periodLengthDays = to.DayNumber - from.DayNumber + 1;
        var previousTo = from.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(periodLengthDays - 1));

        var previousOrders = await _orders.ListPaidOrdersInRangeAsync(
            previousFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            previousTo.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc),
            ct);

        var previousSummary = Summarize(previousOrders);

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

        return new SalesReportDto(from, to, summary, previousSummary, topProducts, dailySales);
    }

    private static SalesSummaryDto Summarize(IReadOnlyList<Order> orders) => new(
        TotalRevenue: orders.Sum(o => o.Total),
        OrderCount: orders.Count,
        AverageOrderValue: orders.Count > 0 ? orders.Sum(o => o.Total) / orders.Count : 0);
}
