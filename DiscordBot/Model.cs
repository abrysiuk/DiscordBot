﻿using Discord;
using Discord.Rest;
using Discord.WebSocket;
using GrammarCheck;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
using UnitsNet;

namespace DiscordBot
{
    /// <summary>
    /// A single text message in Discord, for storage in the database.
    /// </summary>
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
        public ulong? AuthorId { get; set; }
        public string AuthorMention { get; set; } = default!;
        public ulong? ThreadID { get; set; }
        public string? ThreadMention { get; set; }
        public ulong? ReferenceId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public ulong Id { get; set; }
        private static DiscordMessage FromIUM(IUserMessage message)
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
                AuthorId = message.Author.Id,
                AuthorMention = message.Author.Mention,
                ChannelId = message.Channel.Id,
                ChannelMention = message.Channel.Name,
                ReferenceId = (ulong?)message.Reference?.MessageId
            };
        }

        public static implicit operator DiscordMessage(SocketUserMessage message)
        {
            return FromIUM(message);
        }
        public static implicit operator DiscordMessage(RestUserMessage message)
        {
            return FromIUM(message);
        }
        public virtual ICollection<DiscordLog> DiscordLogs { get; set; } = default!;
        public virtual ICollection<QuantityParse> QuantityParses { get; set;} = new List<QuantityParse>();
    }
    /// <summary>
    /// A logged interaction on Discord, such as an edited message or a triggered reaction.
    /// </summary>
    [Table("DiscordLog")]
    [PrimaryKey(nameof(Type), nameof(MessageId))]
    public class DiscordLog
    {
        public string Type { get; set; } = default!;
        public virtual DiscordMessage Message { get; set; } = default!;
        public ulong MessageId { get; set; }
        public DateTimeOffset? Date { get; set; }
    }
    /// <summary>
    /// A reaction definition to define a regex pattern to apply and emote/emoji to react with.
    /// </summary>
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
    /// <summary>
    /// A discord user record, to remember names long forgotten.
    /// </summary>
    [PrimaryKey(nameof(Id), nameof(GuildId))]
    public class DiscordGuildUser
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public ulong Id { get; set; }
        public string Username { get; set; }
        public string? Nickname { get; set; }
        public string Displayname => Nickname ?? Username;
        public ulong GuildId { get; set; }
        public bool TrackGrammar { get; set; } = false;
        public Currency? Currency { get; set; }
        public DiscordGuildUser Update(SocketGuildUser x)
        {
            Id = x.Id;
            Nickname = x.Nickname;
            GuildId = x.Guild.Id;
            Username = x.Username;

            return this;
        }
        public DiscordGuildUser()
        {
            Nickname = string.Empty;
            Username = string.Empty;
        }
        public static implicit operator DiscordGuildUser(SocketGuildUser x)
        {
            return new DiscordGuildUser()
            {
                Id = x.Id,
                Nickname = x.Nickname,
                GuildId = x.Guild.Id,
                Username = x.Username
            };
        }
        public static implicit operator DiscordGuildUser(RestGuildUser x)
        {
            return new DiscordGuildUser()
            {
                Id = x.Id,
                Nickname = x.Nickname,
                GuildId = x.GuildId,
                Username = x.Username
            };
        }
    }
    /// <summary>
    /// A birthday record
    /// </summary>
    [PrimaryKey(nameof(UserId), nameof(GuildId))]
    public class BirthdayDef
    {
        public string CreatedBy { get; set; }
        public DateTime Date { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public ulong UserId { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public ulong GuildId { get; set; }
        public string TimeZone { get; set; }
        public BirthdayDef()
        {
            CreatedBy = string.Empty;
            Date = DateTime.MinValue;
            UserId = ulong.MinValue;
            GuildId = ulong.MinValue;
            TimeZone = TimeZoneInfo.Local.Id;
        }
    }
    /// <summary>
    /// Record of an acronym (unused)
    /// </summary>
    public class Acronym
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public ulong Id { get; set; }
        public string Abbrv { get; set; } = string.Empty;
        [Column(TypeName = "text")]
        public string Meaning { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }
    /// <summary>
    /// A grammar/spelling mistake
    /// </summary>
    public class GrammarMatch
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public ulong Id { get; set; }
        public virtual DiscordMessage DiscordMessage { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ShortMessage { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Length { get; set; }
        public string?[] Replacements { get; set; }
        public string Sentence { get; set; } = string.Empty;
        public GrammarRule? Rule { get; set; }
        public GrammarMatch(string message, string shortMessage, int offset, int length, string sentence)
        {
            DiscordMessage = new DiscordMessage();
            Message = message;
            ShortMessage = shortMessage;
            Offset = offset;
            Length = length;
            Replacements = Array.Empty<string?>();
            Sentence = sentence;
        }
        public GrammarMatch(DiscordMessage discordMessage, GrammarCheck.Match match)
        {
            DiscordMessage = discordMessage;
            Message = match.message;
            ShortMessage = match.shortMessage;
            Offset = match.offset;
            Length = match.length;
            Replacements = match.replacements?.Select(x => x.value).ToArray() ?? Array.Empty<string?>();
            Sentence = match.sentence;
            Rule = new GrammarRule(match.rule ?? new Rule());
        }
    }
    /// <summary>
    /// A grammar/spelling rule
    /// </summary>
    [PrimaryKey("Id", "SubId")]
    public class GrammarRule
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string Id { get; set; }
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string SubId { get; set; }
        public string Description { get; set; }
        public string?[] Urls { get; set; }
        public string? IssueType { get; set; }
        public string CategoryId { get; set; }
        public string CategoryName { get; set; }
        public GrammarRule()
        {
            Id = string.Empty;
            SubId = string.Empty;
            Description = string.Empty;
            IssueType = string.Empty;
            Urls = Array.Empty<string?>();
            CategoryId = string.Empty;
            CategoryName = string.Empty;
        }
        public GrammarRule(Rule rule)
        {
            Id = rule.id;
            SubId = rule.subId;
            Description = rule.description;
            Urls = rule.urls?.Select(x => x.value ?? null).ToArray() ?? Array.Empty<string?>();
            IssueType = rule.issueType;
            CategoryId = rule.category.id;
            CategoryName = rule.category.name;
        }
    }
    /// <summary>
    /// An object representing the source and destination objects of a unit conversion.
    /// </summary>
    public class UnitConversion
    {
        public string? OldString { get; set; }
        public string? NewString { get; set; }
        public int StartPos { get; set; }
        public int EndPos { get; set; }
        public IQuantity? NewQuantity { get; set; }
        public IQuantity? OldQuantity { get; set; }
    }
    /// <summary>
    /// An object representing measurements extracted from a message
    /// </summary>
    public class QuantityParse
    {
        public ulong Id { get; set; }
        public DiscordMessage? Message { get; set; }
        public float Value { get; set; }
        public string Entity { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public int StartPos { get; set; }
        public int EndPos { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
    }
}