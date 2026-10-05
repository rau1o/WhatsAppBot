namespace WhatsAppBot.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string WhatsAppPhoneNumberId { get; set; } = default!;
    public double LocationLatitude { get; set; }
    public double LocationLongitude { get; set; }
    public string LocationName { get; set; } = default!;
    public string LocationAddress { get; set; } = default!;
    public string FacadePhotoUrl { get; set; } = default!;
    // URL de la imagen del QR para transferencias/pagos — nullable porque
    // un tenant recién dado de alta puede no tenerlo cargado todavía.
    public string? PaymentQrImageUrl { get; set; }
    // ID de la WhatsApp Business Account (WABA) de Meta — se obtiene del
    // callback de Embedded Signup o se puede ingresar manualmente.
    // Nullable porque los tenants existentes no lo tienen todavía.
    public string? WabaId { get; set; }
}
