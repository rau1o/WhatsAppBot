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

public class CatalogStateHandlerTests
{
    private record Sut(
        Tenant Tenant,
        Conversation Conversation,
        InMemoryProductRepository Products,
        InMemoryOrderRepository Orders,
        FakeWhatsAppMessageSender Sender,
        CatalogStateHandler Handler);

    private static Sut MakeSut()
    {
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(), Name = "Tienda", WhatsAppPhoneNumberId = "123456",
            LocationLatitude = 0, LocationLongitude = 0, LocationName = "x", LocationAddress = "x", FacadePhotoUrl = "x"
        };
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id,
            CustomerPhoneNumber = "59170000000", State = ConversationState.BrowsingCatalog
        };

        var currentTenant = new CurrentTenantAccessor();
        currentTenant.SetTenant(tenant.Id);

        var products = new InMemoryProductRepository(currentTenant);
        var orders = new InMemoryOrderRepository();
        var sender = new FakeWhatsAppMessageSender();
        var handler = new CatalogStateHandler(sender, products, orders);

        return new Sut(tenant, conversation, products, orders, sender, handler);
    }

    private static Product MakeProduct(Guid tenantId, string name, decimal price) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, Name = name, Price = price, IsActive = true
    };

    [Fact]
    public async Task Primer_mensaje_en_el_estado_muestra_la_lista_de_productos()
    {
        var sut = MakeSut();
        var product = MakeProduct(sut.Tenant.Id, "Router TP-Link", 350);
        sut.Products.Seed(product);

        var message = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId,
            Text: null, InteractiveButtonId: null, ListReplyId: null, MediaId: null);

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, message, CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BrowsingCatalog);
        sut.Sender.ListMessages.Should().ContainSingle();
        sut.Sender.ListMessages[0].Sections.Single().Rows.Should().ContainSingle(r => r.Title == product.Name);
    }

    [Fact]
    public async Task Elegir_un_producto_de_la_lista_pregunta_la_cantidad_sin_agregarlo_todavia()
    {
        var sut = MakeSut();
        var product = MakeProduct(sut.Tenant.Id, "Switch 8 puertos", 280);
        sut.Products.Seed(product);

        var message = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId,
            Text: null, InteractiveButtonId: null, ListReplyId: $"product:{product.Id}", MediaId: null);

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, message, CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BrowsingCatalog);

        sut.Sender.ButtonMessages.Should().ContainSingle();
        sut.Sender.ButtonMessages[0].Buttons.Should().HaveCount(3)
            .And.OnlyContain(b => b.Id.StartsWith($"qty:{product.Id}:"));

        var order = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        order.Items.Should().BeEmpty(); // todavía no eligió cantidad
    }

    [Fact]
    public async Task Elegir_una_cantidad_agrega_el_producto_con_esa_cantidad_y_pregunta_si_seguir()
    {
        var sut = MakeSut();
        var product = MakeProduct(sut.Tenant.Id, "Cable de red", 620);
        sut.Products.Seed(product);

        var message = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId,
            null, InteractiveButtonId: $"qty:{product.Id}:2", ListReplyId: null, MediaId: null);

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, message, CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BrowsingCatalog);

        var order = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        order.Items.Should().ContainSingle(i => i.ProductId == product.Id && i.Quantity == 2);

        sut.Sender.ButtonMessages.Should().ContainSingle();
        sut.Sender.ButtonMessages[0].Buttons.Should().Contain(b => b.Id == "catalog:finish");
    }

    [Fact]
    public async Task Elegir_cantidad_del_mismo_producto_dos_veces_suma_en_vez_de_duplicar()
    {
        var sut = MakeSut();
        var product = MakeProduct(sut.Tenant.Id, "Conector RJ45", 15);
        sut.Products.Seed(product);

        var message = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId,
            null, InteractiveButtonId: $"qty:{product.Id}:2", ListReplyId: null, MediaId: null);

        await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, message, CancellationToken.None);

        var afterFirst = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        afterFirst.Items.Should().ContainSingle();
        afterFirst.Items.Single().Quantity.Should().Be(2, "el primer tap debería dejar cantidad 2");

        await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, message, CancellationToken.None);

        var afterSecond = await sut.Orders.GetOrCreateDraftAsync(sut.Conversation.Id, CancellationToken.None);
        afterSecond.Items.Should().ContainSingle();
        afterSecond.Items.Single().Quantity.Should().Be(4, "el segundo tap debería sumar otras 2 unidades, 2+2");
    }

    [Fact]
    public async Task Boton_finalizar_pasa_a_BuildingOrder()
    {
        var sut = MakeSut();

        var message = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId,
            null, InteractiveButtonId: "catalog:finish", ListReplyId: null, MediaId: null);

        var result = await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, message, CancellationToken.None);

        result.NextState.Should().Be(ConversationState.BuildingOrder);
    }

    [Fact]
    public async Task Con_10_productos_o_menos_no_hay_fila_de_ver_mas()
    {
        var sut = MakeSut();
        for (var i = 0; i < 9; i++)
            sut.Products.Seed(MakeProduct(sut.Tenant.Id, $"Producto {i}", 10));

        var message = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId, null, null, null, null);

        await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, message, CancellationToken.None);

        sut.Sender.ListMessages[0].Sections.Single().Rows.Should().HaveCount(9);
        sut.Sender.ListMessages[0].Sections.Single().Rows.Should().NotContain(r => r.Id.StartsWith("catalog:page:"));
    }

    [Fact]
    public async Task Con_mas_de_9_productos_aparece_Ver_mas_productos_y_pagina_correctamente()
    {
        var sut = MakeSut();
        for (var i = 0; i < 12; i++)
            sut.Products.Seed(MakeProduct(sut.Tenant.Id, $"Producto {i:D2}", 10));

        var firstMessage = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId, null, null, null, null);
        await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, firstMessage, CancellationToken.None);

        // Página 1: 9 productos + la fila de "Ver más productos" = 10 filas, dentro del límite de WhatsApp.
        var page1Rows = sut.Sender.ListMessages[0].Sections.Single().Rows;
        page1Rows.Should().HaveCount(10);
        page1Rows.Last().Id.Should().Be("catalog:page:1");

        var nextPageMessage = new IncomingMessage(
            sut.Conversation.CustomerPhoneNumber, sut.Tenant.WhatsAppPhoneNumberId,
            null, InteractiveButtonId: null, ListReplyId: "catalog:page:1", MediaId: null);
        await sut.Handler.HandleAsync(sut.Tenant, sut.Conversation, nextPageMessage, CancellationToken.None);

        // Página 2: quedan 3 productos (12 - 9), sin fila de "Ver más" porque ya no hay más.
        var page2Rows = sut.Sender.ListMessages[1].Sections.Single().Rows;
        page2Rows.Should().HaveCount(3);
        page2Rows.Should().NotContain(r => r.Id.StartsWith("catalog:page:"));
    }
}
