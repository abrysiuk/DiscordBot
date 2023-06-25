using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
namespace DiscordBot
{
    public class SlashLog
    {
        public ulong Id { get; set; }
        public string Command { get; set; } = default!;
        //public discordAuthor Author { get; set; }
        public ulong AuthorID { get; set; }
        public string AuthorMention { get; set; } = default!;
        public DiscordMessageChannel Channel { get; set; } = default!;
        public ulong ThreadId { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }
    public class DiscordMessage
    {
        public MessageType Type { get; set; }
        public MessageSource Source { get; set; }
        public bool IsTTS { get; set; }
        public bool IsPinned { get; set; }
        public bool IsSuppressed { get; set; }
        public bool MentionedEveryone { get; set; }
        public string Content { get; set; } = default!;
        public string CleanContent { get; set; } = default!;
        public DateTimeOffset Timestamp { get; set; }
        public DateTimeOffset? EditedTimestamp { get; set; }
        public ulong ChannelId { get; set; }
        public string ChannelMention { get; set; } = default!;
        public ulong GuildId { get; set; }
        //public discordAuthor Author { get; set; }
        public ulong AuthorId { get; set; }
        public string AuthorMention { get; set; } = default!;
        public ulong? ThreadID { get; set; }
        public string? ThreadMention { get; set; }
        public ulong? ReferenceId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public ulong Id { get; set; }
        public static implicit operator DiscordMessage(SocketUserMessage message)
        {
            return new DiscordMessage
            {
                Id = message.Id,
                Type = message.Type,
                Source = message.Source,
                IsTTS = message.IsTTS,
                IsPinned = message.IsPinned,
                IsSuppressed = message.IsSuppressed,
                MentionedEveryone = message.MentionedEveryone,
                Content = message.Content,
                CleanContent = message.CleanContent,
                Timestamp = message.Timestamp,
                EditedTimestamp = message.EditedTimestamp,
                ThreadID = message.Thread?.Id,
                ThreadMention = message.Thread?.Mention,
                CreatedAt = message.CreatedAt,
                //Author = message.Author,
                AuthorId = message.Author.Id,
                AuthorMention = message.Author.Mention,
                ChannelId = message.Channel.Id,
                ChannelMention = message.Channel.Name,
                ReferenceId = message.Reference is null ? null : (ulong)message.Reference.MessageId
            };
        }
        public virtual ICollection<DiscordShame> DiscordShames { get; set; } = default!;
    }
    [PrimaryKey(nameof(Type), nameof(MessageId))]
    public class DiscordShame
    {
        public string Type { get; set; } = default!;
        public virtual DiscordMessage Message { get; set; } = default!;
        public ulong MessageId { get; set; }
    }
    public class DiscordAuthor
    {
        public string AvatarId { get; set; } = default!;
        public string Discriminator { get; set; } = default!;
        public ushort DiscriminatorValue { get; set; }
        public bool IsBot { get; set; }
        public bool IsWebhook { get; set; }
        public string Username { get; set; } = default!;
        public UserProperties? PublicFlags { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public ulong Id { get; set; }
        public string Mention { get; set; } = default!;
        public static implicit operator DiscordAuthor(SocketUser u)
        {
            return new DiscordAuthor()
            {
                AvatarId = u.AvatarId,
                Discriminator = u.Discriminator,
                CreatedAt = u.CreatedAt,
                Id = u.Id,
                Mention = u.Mention,
                DiscriminatorValue = u.DiscriminatorValue,
                IsBot = u.IsBot,
                IsWebhook = u.IsWebhook,
                Username = u.Username,
                PublicFlags = u.PublicFlags
            };
        }
    }
    public class DiscordMessageChannel
    {
        public string Mention { get; set; } = default!;
        public ulong? CategoryId { get; set; }
        public int Position { get; set; }
        public ChannelFlags Flags { get; set; }
        public DiscordGuild Guild { get; set; } = default!;
        public string Name { get; set; } = default!;
        public DateTimeOffset CreatedAt { get; set; }
        public ulong Id { get; set; }
    }
    public class DiscordGuild
    {
        public string Name { get; set; } = default!;
        public string IconId { get; set; } = default!;
        public string IconUrl { get; set; } = default!;
        public ulong OwnerId { get; set; }
        [NotMapped]
        public IReadOnlyCollection<GuildEmote> Emotes { get; set; } = default!;
        [NotMapped]
        public IReadOnlyCollection<ICustomSticker> Stickers { get; set; } = default!;
        public string Description { get; set; } = default!;
        public DateTimeOffset CreatedAt { get; set; }
        public ulong Id { get; set; }
    }
    public class ReactionDef
    {
        public string Name { get; set; }
        [StringSyntax(StringSyntaxAttribute.Regex)]
        public string Regex { get; set; }
        public string Emote { get; set; }
        public ReactionDef()
        {
            Name = string.Empty;
            Regex = string.Empty;
            Emote = string.Empty;
        }
        public ReactionDef(string name, [StringSyntax(StringSyntaxAttribute.Regex)] string regex, string emote)
        {
            Regex = regex;
            Emote = emote;
            Name = name;
        }
    }
}