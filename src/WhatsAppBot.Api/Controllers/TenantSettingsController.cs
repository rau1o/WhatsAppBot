using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Domain.Entities;

namespace WhatsAppBot.Api.Controllers
{
    [ApiController]
    [Route("api/tenant-settings")]
    [Authorize]
    public class TenantSettingsController : ControllerBase
    {
        private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };
        private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5 MB

        private readonly ITenantRepository _tenants;
        private readonly IFileStorage _fileStorage;
        private readonly ICurrentTenantAccessor _currentTenant;

        public TenantSettingsController(ITenantRepository tenants, IFileStorage fileStorage, ICurrentTenantAccessor currentTenant)
        {
            _tenants = tenants;
            _fileStorage = fileStorage;
            _currentTenant = currentTenant;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken ct)
        {
            var tenant = await GetCurrentTenantAsync(ct);
            return Ok(ToDto(tenant));
        }

        // Restringido a Owner a propósito: esto cambia info que el bot le
        // muestra a cualquier cliente que escriba (ubicación, fachada, QR de
        // cobro) — no es algo que cualquier empleado debería poder tocar.
        [HttpPut]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Update([FromBody] UpdateTenantSettingsRequest request, CancellationToken ct)
        {
            var tenant = await GetCurrentTenantAsync(ct);

            tenant.Name = request.Name;
            tenant.LocationLatitude = request.LocationLatitude;
            tenant.LocationLongitude = request.LocationLongitude;
            tenant.LocationName = request.LocationName;
            tenant.LocationAddress = request.LocationAddress;

            await _tenants.UpdateAsync(tenant, ct);

            return Ok(ToDto(tenant));
        }

        [HttpPost("facade-photo")]
        [Authorize(Roles = "Owner")]
        public Task<IActionResult> UploadFacadePhoto(IFormFile file, CancellationToken ct)
            => UploadImageAsync(file, "facade", (tenant, url) => tenant.FacadePhotoUrl = url, ct);

        [HttpPost("payment-qr")]
        [Authorize(Roles = "Owner")]
        public Task<IActionResult> UploadPaymentQr(IFormFile file, CancellationToken ct)
            => UploadImageAsync(file, "payment-qr", (tenant, url) => tenant.PaymentQrImageUrl = url, ct);

        private async Task<IActionResult> UploadImageAsync(
            IFormFile file, string slot, Action<Tenant, string> assignUrl, CancellationToken ct)
        {
            if (file.Length == 0) return BadRequest(new { message = "El archivo está vacío." });
            if (file.Length > MaxImageSizeBytes) return BadRequest(new { message = "La imagen no puede superar los 5 MB." });
            if (!AllowedImageContentTypes.Contains(file.ContentType))
                return BadRequest(new { message = "Formato no soportado. Usá JPG, PNG o WEBP." });

            var tenant = await GetCurrentTenantAsync(ct);

            // Nombre fijo por tenant+slot: cada nueva subida pisa la anterior en
            // vez de acumular archivos huérfanos en el storage.
            var extension = file.ContentType switch
            {
                "image/png" => "png",
                "image/webp" => "webp",
                _ => "jpg"
            };
            var fileName = $"{tenant.Id}-{slot}.{extension}";

            await using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, ct);

            var url = await _fileStorage.SaveAsync(memoryStream.ToArray(), fileName, file.ContentType, ct);

            assignUrl(tenant, url);
            await _tenants.UpdateAsync(tenant, ct);

            return Ok(ToDto(tenant));
        }

        private async Task<Tenant> GetCurrentTenantAsync(CancellationToken ct)
            => await _tenants.GetByIdAsync(_currentTenant.TenantId!.Value, ct);

        private static TenantSettingsDto ToDto(Tenant t) => new(
            t.Id, t.Name, t.WhatsAppPhoneNumberId,
            t.LocationLatitude, t.LocationLongitude, t.LocationName, t.LocationAddress,
            t.FacadePhotoUrl, t.PaymentQrImageUrl);
    }
}
