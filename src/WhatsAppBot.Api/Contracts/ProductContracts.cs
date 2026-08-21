using System.ComponentModel.DataAnnotations;

namespace WhatsAppBot.Api.Contracts;

public record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    decimal Price,
    string? ImageUrl,
    bool IsActive
);

public record UpsertProductRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")] decimal Price,
    string? ImageUrl,
    bool IsActive
);
