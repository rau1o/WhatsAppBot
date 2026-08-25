using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Application.StateHandlers;
using WhatsAppBot.Application.Tests.TestDoubles;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;
using WhatsAppBot.Infrastructure.Persistence;
using Xunit;

namespace WhatsAppBot.Application.Tests.StateHandlers;

public class OrderReviewStateHandlerTests
{
    private static Tenant MakeTenant() => new()
    {
        Id = Guid.NewGuid(), Name = "Tienda", WhatsAppPhoneNumberId = "123456",
        LocationLatitude = 0, LocationLongitude = 0, LocationName = "x", LocationAddress = "x", FacadePhotoUrl = "x"
    };

    [Fact]
    public async Task Sin_items_vuelve_al_catalogo_en_vez_de_cerrar_un_pedido_vacio()
    {
        var tenant = MakeTenant();
        var conversation = new Conversation { Id = Guid.NewGuid(), TenantId = tenant.Id, CustomerPhoneNumber = "59170000000", State = ConversationState.BuildingOrder };

        var orders = new InMemoryOrderRepository();
        var sender = new FakeWhatsAppMessageSender();
        var handler = new OrderReviewStateHandler(sender, orders, NullLogger<OrderReviewStateHandler>.Instance);

        var message = new IncomingMessage(conversation.CustomerPhoneNumber, tenant.WhatsAppPhoneNumberId, null, null, null, null);

        var result = await handler.HandleAsync(tenant, conversation, message, CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BrowsingCatalog);
    }

    [Fact]
    public async Task Con_items_manda_el_resumen_con_el_total_correcto_y_marca_el_pedido_como_Submitted()
    {
        var tenant = MakeTenant();
        var conversation = new Conversation { Id = Guid.NewGuid(), TenantId = tenant.Id, CustomerPhoneNumber = "59170000000", State = ConversationState.BuildingOrder };

        var orders = new InMemoryOrderRepository();
        var draft = await orders.GetOrCreateDraftAsync(conversation.Id, CancellationToken.None);
        draft.AddOrIncrementItem(new Product { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Router", Price = 350 });
        draft.AddOrIncrementItem(new Product { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Cable", Price = 100 });
        await orders.SaveAsync(draft, CancellationToken.None);

        var sender = new FakeWhatsAppMessageSender();
        var handler = new OrderReviewStateHandler(sender, orders, NullLogger<OrderReviewStateHandler>.Instance);

        var message = new IncomingMessage(conversation.CustomerPhoneNumber, tenant.WhatsAppPhoneNumberId, null, null, null, null);

        var result = await handler.HandleAsync(tenant, conversation, message, CancellationToken.None);

        // Desde que armamos fase 3 (comprobante de pago), un pedido con items
        // pasa a AwaitingPayment — este test se había quedado con la
        // expectativa vieja de fase 2 (donde terminaba en BuildingOrder).
        result.NextState.Should().Be(ConversationState.AwaitingPayment);

        var updatedOrder = await orders.GetOrCreateDraftAsync(conversation.Id, CancellationToken.None);
        updatedOrder.Status.Should().Be(OrderStatus.Submitted);

        sender.Texts.Should().Contain(t => t.Text.Contains("450")); // 350 + 100, formateado con separador de miles
    }
}
