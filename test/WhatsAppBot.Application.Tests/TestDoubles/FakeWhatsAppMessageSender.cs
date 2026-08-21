using WhatsAppBot.Application.Abstractions;
using WhatsAppBot.Application.Messaging;

namespace WhatsAppBot.Application.Tests.TestDoubles;

public record SentText(string To, string Text);
public record SentButtons(string To, string BodyText, IReadOnlyList<InteractiveButton> Buttons);
public record SentList(string To, string BodyText, IReadOnlyList<InteractiveListSection> Sections);

public class FakeWhatsAppMessageSender : IWhatsAppMessageSender
{
    public List<SentText> Texts { get; } = new();
    public List<SentButtons> ButtonMessages { get; } = new();
    public List<SentList> ListMessages { get; } = new();

    public Task SendTextAsync(string tenantPhoneNumberId, string toPhoneNumber, string text, CancellationToken ct)
    {
        Texts.Add(new SentText(toPhoneNumber, text));
        return Task.CompletedTask;
    }

    public Task SendLocationAsync(string tenantPhoneNumberId, string toPhoneNumber,
        double latitude, double longitude, string name, string address, CancellationToken ct)
        => Task.CompletedTask;

    public Task SendImageByUrlAsync(string tenantPhoneNumberId, string toPhoneNumber, string imageUrl, string? caption, CancellationToken ct)
        => Task.CompletedTask;

    public Task SendInteractiveButtonsAsync(string tenantPhoneNumberId, string toPhoneNumber,
        string bodyText, IReadOnlyList<InteractiveButton> buttons, CancellationToken ct)
    {
        ButtonMessages.Add(new SentButtons(toPhoneNumber, bodyText, buttons));
        return Task.CompletedTask;
    }

    public Task SendInteractiveListAsync(string tenantPhoneNumberId, string toPhoneNumber,
        string bodyText, string buttonText, IReadOnlyList<InteractiveListSection> sections, CancellationToken ct)
    {
        ListMessages.Add(new SentList(toPhoneNumber, bodyText, sections));
        return Task.CompletedTask;
    }
}
