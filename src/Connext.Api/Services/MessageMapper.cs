using Connext.Data.Entities;
using Connext.Shared.Chat;

namespace Connext.Api.Services;

public static class MessageMapper
{
    public const string RecalledText = "Tin nhắn đã thu hồi";
    private const int PreviewLength = 100;

    // Nội dung rút gọn của một tin (dùng cho reply và ghim)
    public static string? Preview(Message m)
    {
        if (m.IsRecalled) return RecalledText;
        return m.Type switch
        {
            "poll" => "[Bình chọn]",
            "file" => "[Tệp đính kèm]",
            _ => m.Content is { Length: > PreviewLength } c ? c[..PreviewLength] + "…" : m.Content
        };
    }

    // Cần Include: Mentions, Reactions, ReplyToMessage
    public static MessageDto ToDto(Message m, bool mine) => new()
    {
        Id = m.Id,
        ConversationId = m.ConversationId,
        SenderId = m.SenderId,
        Mine = mine,
        Type = m.IsRecalled ? "text" : m.Type,
        Content = m.IsRecalled ? RecalledText : m.Content,
        FileUrl = m.IsRecalled ? null : m.FileUrl,
        FileName = m.IsRecalled ? null : m.FileName,
        FileSize = m.IsRecalled ? null : m.FileSize,
        CreatedAt = m.CreatedAt,
        Status = m.Status,
        IsRecalled = m.IsRecalled,
        MentionedUserIds = m.Mentions.Select(x => x.UserId).ToList(),
        ReplyToMessageId = m.ReplyToMessageId,
        ReplyTo = m.ReplyToMessage == null ? null : new ReplyPreviewDto
        {
            Id = m.ReplyToMessage.Id,
            SenderId = m.ReplyToMessage.SenderId,
            Preview = Preview(m.ReplyToMessage)
        },
        Reactions = m.Reactions
            .GroupBy(r => r.Emoji)
            .Select(g => new ReactionDto { Emoji = g.Key, Count = g.Count(), UserIds = g.Select(x => x.UserId).ToList() })
            .ToList()
    };
}