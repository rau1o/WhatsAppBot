using FluentAssertions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Application.StateHandlers;
using WhatsAppBot.Application.Tests.TestDoubles;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;
using Xunit;

namespace WhatsAppBot.Application.Tests.StateHandlers;

public class ConfirmedStateHandlerTests
{
    [Fact]
    public async Task Un_mensaje_nuevo_arranca_un_pedido_nuevo_en_vez_de_quedar_trabado()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Name = "Tienda", WhatsAppPhoneNumberId = "123456",
            LocationLatitude = 0, LocationLongitude = 0, LocationName = "x", LocationAddress = "x", FacadePhotoUrl = "x"
        };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            CustomerPhoneNumber = "59170000000", State = ConversationState.Confirmed
        };

        var sender = new FakeWhatsAppMessageSender();
        var handler = new ConfirmedStateHandler(sender);

        var message = new IncomingMessage(
            conversation.CustomerPhoneNumber, tenant.WhatsAppPhoneNumberId,
            Text: "Hola, quiero hacer otro pedido", InteractiveButtonId: null, ListReplyId: null, MediaId: null);

        var result = await handler.HandleAsync(tenant, conversation, message, CancellationToken.None);

        // No se queda repitiendo "tu pedido ya está confirmado" para
        // siempre — deja la puerta abierta a un pedido nuevo, y
        // ContinueImmediately hace que el catálogo se muestre ya mismo,
        // sin necesitar un mensaje más del cliente.
        result.NextState.Should().Be(ConversationState.BrowsingCatalog);
        result.ContinueImmediately.Should().BeTrue();
    }
}
