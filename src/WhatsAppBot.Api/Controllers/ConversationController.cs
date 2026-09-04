using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppBot.Api.Contracts;
using WhatsAppBot.Application.Abstractions;

namespace WhatsAppBot.Api.Controllers
{

    [ApiController]
    [Route("api/conversations")]
    [Authorize] // requiere JWT válido — el TenantContextMiddleware ya seteó el tenant antes de llegar acá
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationRepository _conversations;

        public ConversationsController(IConversationRepository conversations)
        {
            _conversations = conversations;
        }

        [HttpGet]
        public async Task<IActionResult> ListRecent([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var result = await _conversations.ListRecentAsync(page, pageSize, ct);

            var items = result.Items.Select(c => new ConversationSummary(
                c.Id, c.CustomerPhoneNumber, c.State.ToString(), c.LastMessageAt)).ToList();

            return Ok(new PagedResponse<ConversationSummary>(items, result.Page, result.PageSize, result.TotalCount, result.TotalPages));

        }
    }
}
