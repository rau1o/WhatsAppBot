using FluentAssertions;
using WhatsAppBot.Application.Messaging;
using WhatsAppBot.Application.StateHandlers;
using WhatsAppBot.Application.Tests.TestDoubles;
using WhatsAppBot.Domain.Entities;
using WhatsAppBot.Domain.Enums;
using WhatsAppBot.Infrastructure.MultiTenancy;
using WhatsAppBot.Infrastructure.Persistence;
using Xunit;

namespace WhatsAppBot.Application.Tests.StateHandlers;

public class QuantityInputStateHandlerTests
{
    private record Sut(
        Tenant Tenant,
        Conversation Conversation,
        Product Product,
        InMemoryOrderRepository Orders,
        FakeWhatsAppMessageSender Sender,
        QuantityInputStateHandler Handler);

    private static Sut MakeSut()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Name = "Tienda", WhatsAppPhoneNumberId = "123456",
            LocationLatitude = 0, LocationLongitude = 0, LocationName = "x", LocationAddress = "x", FacadePhotoUrl = "x"
        };

        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Name = "Cable de red", Price = 620, IsActive = true
        };

        var conversation = new Conversation
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            CustomerPhoneNumber = "59170000000", State = ConversationState.AwaitingQuantity,
            PendingProductId = product.Id
        };

        var currentTenant = new CurrentTenantAccessor();
        currentTenant.SetTenant(tenant.Id);

        var products = new InMemoryProductRepository(currentTenant);
        products.Seed(product);

        var orders = new InMemoryOrderRepository();
        var sender = new FakeWhatsAppMessageSender();
        var handler = new QuantityInputStateHandler(sender, products, orders);

        return new Sut(tenant, conversation, product, orders, sender, handler);
    }

    private static IncomingMessage TextMessage(Sut sut, string text) => new(
        sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId,
        Text: text, InteractiveButtonId: null, ListReplyId: null, MediaId: null);

    [Fact]
    public async Task Cantidad_valida_agrega_el_producto_y_limpia_el_pendiente()
    {
        var sut = MakeSut();

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, TextMessage(sut, "5"), CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BrowsingCatalog);
        sut.Conversation.PendingProductId.Should().BeNull();

        var order = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        order.Items.Should().ContainSingle(i => i.ProductId == sut.Product.Id && i.Quantity == 5);

        sut.Sender.Texts.Should().Contain(t => t.Text.Contains("5x"));
        sut.Sender.ButtonMessages.Should().ContainSingle(b => b.Buttons.Any(btn => btn.Id == "catalog:finish"));
    }

    [Theory]
    [InlineData("cinco")]      // letras
    [InlineData("5.5")]        // decimal
    [InlineData("5,000")]      // separador de miles
    [InlineData("+5")]         // signo
    [InlineData("5 unidades")] // texto extra
    [InlineData("")]           // vacío
    public async Task Texto_no_numerico_pide_reintentar_sin_perder_el_producto_pendiente(string invalidInput)
    {
        var sut = MakeSut();

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, TextMessage(sut, invalidInput), CancellationToken.None);

        result.NextState.Should().Be(ConversationState.AwaitingQuantity);
        sut.Conversation.PendingProductId.Should().Be(sut.Product.Id); // no se pierde, puede reintentar

        var order = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        order.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    public async Task Cantidad_cero_o_negativa_se_rechaza(string invalidInput)
    {
        var sut = MakeSut();

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, TextMessage(sut, invalidInput), CancellationToken.None);

        result.NextState.Should().Be(ConversationState.AwaitingQuantity);

        var order = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Cantidad_por_encima_del_tope_se_rechaza_con_mensaje_explicito()
    {
        var sut = MakeSut();

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, TextMessage(sut, "501"), CancellationToken.None);

        result.NextState.Should().Be(ConversationState.AwaitingQuantity);
        sut.Sender.Texts.Should().Contain(t => t.Text.Contains("500"));

        var order = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Boton_cancelar_vuelve_al_catalogo_sin_agregar_nada()
    {
        var sut = MakeSut();

        var message = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId,
            null, InteractiveButtonId: "catalog:quantity_cancel", ListReplyId: null, MediaId: null);

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, message, CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BrowsingCatalog);
        result.ContinueImmediately.Should().BeTrue();
        sut.Conversation.PendingProductId.Should().BeNull();

        var order = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        order.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Elegir_la_misma_cantidad_dos_veces_suma_en_vez_de_duplicar()
    {
        var sut = MakeSut();

        await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, TextMessage(sut, "2"), CancellationToken.None);

        // Simula que el cliente volvió a elegir el mismo producto del catálogo.
        sut.Conversation.PendingProductId = sut.Product.Id;
        sut.Conversation.State = ConversationState.AwaitingQuantity;

        await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, TextMessage(sut, "2"), CancellationToken.None);

        var order = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        order.Items.Should().ContainSingle();
        order.Items.Single().Quantity.Should().Be(4);
    }

    [Fact]
    public async Task Sin_producto_pendiente_vuelve_al_catalogo_como_recuperacion_defensiva()
    {
        var sut = MakeSut();
        sut.Conversation.PendingProductId = null;

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, TextMessage(sut, "5"), CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BrowsingCatalog);
        result.ContinueImmediately.Should().BeTrue();
    }
}
