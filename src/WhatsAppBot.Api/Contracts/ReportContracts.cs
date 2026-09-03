namespace WhatsAppBot.Api.Contracts;

public record SalesSummaryDto(decimal TotalRevenue, int OrderCount, decimal AverageOrderValue);

public record TopProductDto(string ProductName, int QuantitySold, decimal Revenue);

public record DailySalesDto(DateOnly Date, decimal Total, int OrderCount);

public record SalesReportDto(
    DateOnly From,
    DateOnly To,
    SalesSummaryDto Summary,
    IReadOnlyList<TopProductDto> TopProducts,
    IReadOnlyList<DailySalesDto> DailySales
);
