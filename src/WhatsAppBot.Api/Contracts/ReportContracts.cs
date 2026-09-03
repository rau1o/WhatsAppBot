namespace WhatsAppBot.Api.Contracts;

public record SalesSummaryDto(decimal TotalRevenue, int OrderCount, decimal AverageOrderValue);

public record TopProductDto(string ProductName, int QuantitySold, decimal Revenue);

public record DailySalesDto(DateOnly Date, decimal Total, int OrderCount);

public record SalesReportDto(
    DateOnly From,
    DateOnly To,
    SalesSummaryDto Summary,
    // Mismo largo de días, inmediatamente antes del período pedido — ej. si
    // pedís "este mes" (parcial, hasta hoy), el anterior es la misma
    // cantidad de días del mes pasado, no el mes calendario completo. Así
    // la comparación es pareja incluso a mitad de mes.
    SalesSummaryDto PreviousPeriodSummary,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<DailySalesDto> DailySales
);
