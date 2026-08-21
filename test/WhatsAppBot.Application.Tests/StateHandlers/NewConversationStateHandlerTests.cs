using FluentAssertions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Application.StateHandlers;
using WhatsAppBot.Application.Tests.TestDoubles;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;
using Xunit;

namespace WhatsAppBot.Application.Tests.StateHandlers;

public class NewConversationStateHandlerTests
{
    private static Tenant MakeTenant() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Tienda de prueba",
        WhatsAppPhoneNumberId = "123456",
        LocationLatitude = -17.78,
        LocationLongitude = -63.18,
        LocationName = "Tienda de prueba",
        LocationAddress = "Av. Ejemplo 123",
        FacadePhotoUrl = "https://example.com/foto.jpg"
    };

    [Fact]
    public async Task Envia_saludo_ubicacion_y_foto_y_pasa_a_BrowsingCatalog()
    {
        var sender = new FakeWhatsAppMessageSender();
        var handler = new NewConversationStateHandler(sender);

        var tenant = MakeTenant();
        var conversation = new Conversation { Id = Guid.NewGuid(), TenantId = tenant.Id, CustomerPhoneNumber = "59170000000", State = ConversationState.New };
        var message = new IncomingMessage(conversation.CustomerPhoneNumber, tenant.WhatsAppPhoneNumberId, "Hola", null, null, null);

        var result = await handler.HandleAsync(tenant, conversation, message, CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BrowsingCatalog);
        sender.Texts.Should().ContainSingle(t => t.Text.Contains(tenant.Name));
    }
}
