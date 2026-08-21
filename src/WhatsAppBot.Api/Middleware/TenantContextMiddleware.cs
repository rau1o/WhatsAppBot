using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Api.Middleware
{
    public class TenantContextMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantContextMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICurrentTenantAccessor currentTenant)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;

            if (tenantClaim is not null && Guid.TryParse(tenantClaim, out var tenantId))
            {
                currentTenant.SetTenant(tenantId);
            }

            await _next(context);
        }
    }
}
