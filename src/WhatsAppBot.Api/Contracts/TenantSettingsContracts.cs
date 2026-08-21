using System.ComponentModel.DataAnnotations;

namespace WhatsAppBot.Api.Contracts
{
    public record TenantSettingsDto(
     Guid Id,
     string Name,
     string WhatsAppPhoneNumberId,
     double LocationLatitude,
     double LocationLongitude,
     string LocationName,
     string LocationAddress,
     string FacadePhotoUrl,
     string? PaymentQrImageUrl
 );

    public record UpdateTenantSettingsRequest(
        [Required, MaxLength(200)] string Name,
        double LocationLatitude,
        double LocationLongitude,
        [Required, MaxLength(200)] string LocationName,
        [Required, MaxLength(300)] string LocationAddress
    );

}
