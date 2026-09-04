using FluentAssertions;
using WhatsAppBot.Infrastructure.Persistence.Repositories;
using Xunit;

namespace WhatsAppBot.Infrastructure.Tests;

[Collection("Postgres")]
public class EfWebhookDeduplicationServiceTests
{
    private readonly PostgresFixture _fixture;

    public EfWebhookDeduplicationServiceTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // El mecanismo entero de deduplicación depende de que la PK (el
    // message_id de WhatsApp) rechace un segundo INSERT con el mismo
    // valor — eso es una constraint real de Postgres, no algo que un
    // repo en memoria pueda demostrar de verdad.
    [Fact]
    public async Task El_mismo_message_id_solo_se_marca_como_procesado_la_primera_vez()
    {
        var messageId = $"wamid.TEST_{Guid.NewGuid()}";

        await using var db1 = _fixture.CreateContext();
        var service1 = new EfWebhookDeduplicationService(db1);

        var firstAttempt = await service1.TryMarkAsProcessedAsync(messageId, CancellationToken.None);
        firstAttempt.Should().BeTrue("es la primera vez que se ve este message_id");

        // Simula el reintento de Meta — un DbContext totalmente distinto,
        // igual que un segundo job de Hangfire procesando la reentrega.
        await using var db2 = _fixture.CreateContext();
        var service2 = new EfWebhookDeduplicationService(db2);

        var secondAttempt = await service2.TryMarkAsProcessedAsync(messageId, CancellationToken.None);
        secondAttempt.Should().BeFalse("ya se había procesado este mismo message_id antes");
    }

    [Fact]
    public async Task Message_ids_distintos_se_procesan_ambos_normalmente()
    {
        await using var db = _fixture.CreateContext();
        var service = new EfWebhookDeduplicationService(db);

        var first = await service.TryMarkAsProcessedAsync($"wamid.A_{Guid.NewGuid()}", CancellationToken.None);
        var second = await service.TryMarkAsProcessedAsync($"wamid.B_{Guid.NewGuid()}", CancellationToken.None);

        first.Should().BeTrue();
        second.Should().BeTrue();
    }
}
