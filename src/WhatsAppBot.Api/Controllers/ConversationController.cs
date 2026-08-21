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
        public async Task<IActionResult> ListRecent(CancellationToken ct)
        {
            var conversations = await _conversations.ListRecentAsync(ct);

            var response = conversations.Select(c => new ConversationSummary(
                c.Id, c.CustomerPhoneNumber, c.State.ToString(), c.LastMessageAt));

            return Ok(response);
        }
    }
}
