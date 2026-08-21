namespace WhatsAppBot.Application.Messaging;

public record InteractiveButton(string Id, string Title);

public record InteractiveListRow(string Id, string Title, string? Description = null);

public record InteractiveListSection(string Title, IReadOnlyList<InteractiveListRow> Rows);
