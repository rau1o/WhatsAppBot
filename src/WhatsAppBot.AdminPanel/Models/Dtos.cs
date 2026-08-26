namespace WhatsAppBot.AdminPanel.Models;

public record LoginRequest(string Email, string Password);
public record LoginResponse(string Token, DateTime ExpiresAtUtc, Guid TenantId, string Role);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ProductDto(Guid Id, string Name, string? Description, decimal Price, string? ImageUrl, bool IsActive);
public record UpsertProductRequest(string Name, string? Description, decimal Price, string? ImageUrl, bool IsActive);

public record ConversationSummaryDto(Guid Id, string CustomerPhoneNumber, string State, DateTime LastMessageAt);
public record UserDto(Guid Id, string Email, string DisplayName, string Role, bool IsActive);
public record InviteUserRequest(string Email, string DisplayName, string Role);
public record InviteUserResponse(UserDto User, string TemporaryPassword);
public record ChangeRoleRequest(string Role);

public record TenantSettingsDto(Guid Id, string Name, string WhatsAppPhoneNumberId, double LocationLatitude, double LocationLongitude, string LocationName, string LocationAddress, string FacadePhotoUrl, string? PaymentQrImageUrl);

public record UpdateTenantSettingsRequest(string Name, double LocationLatitude, double LocationLongitude, string LocationName, string LocationAddress);
public record PaymentProofDto(Guid Id, Guid OrderId, string CustomerPhoneNumber, decimal OrderTotal, string Status, DateTime CreatedAt);
public record OrderItemLineDto(string ProductName, int Quantity, decimal UnitPrice);
public record FulfillmentOrderDto(Guid Id, string CustomerPhoneNumber, decimal Total, string FulfillmentStatus, DateTime CreatedAt, List<OrderItemLineDto> Items);