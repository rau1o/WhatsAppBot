using System.Text.Json.Serialization;

namespace WhatsAppBot.Api.Contracts;

// Estructura mínima del payload que manda Meta — solo lo que usamos hoy.
// El JSON real trae bastante más anidamiento (statuses, contacts, etc.)
// que se puede ir sumando a medida que se necesite.
public record WhatsAppWebhookPayload(
    [property: JsonPropertyName("entry")] List<Entry> Entry
);

public record Entry(
    [property: JsonPropertyName("changes")] List<Change> Changes
);

public record Change(
    [property: JsonPropertyName("value")] ChangeValue Value
);

public record ChangeValue(
    [property: JsonPropertyName("metadata")] Metadata Metadata,
    [property: JsonPropertyName("messages")] List<Message>? Messages
);

public record Metadata(
    [property: JsonPropertyName("phone_number_id")] string PhoneNumberId
);

public record Message(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("text")] MessageText? Text,
    [property: JsonPropertyName("interactive")] MessageInteractive? Interactive,
    [property: JsonPropertyName("image")] MessageMedia? Image
);

public record MessageText([property: JsonPropertyName("body")] string Body);
public record MessageInteractive(
    [property: JsonPropertyName("button_reply")] ButtonReply? ButtonReply,
    [property: JsonPropertyName("list_reply")] ListReply? ListReply
);
public record ButtonReply([property: JsonPropertyName("id")] string Id);
public record ListReply([property: JsonPropertyName("id")] string Id);
public record MessageMedia([property: JsonPropertyName("id")] string Id);
