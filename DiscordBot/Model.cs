using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics.CodeAnalysis;
namespace DiscordBot
{
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
		//public virtual DiscordGuildUser? Author { get; set; }
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
				//Author = (DiscordGuildUser)message.Author,
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
	}
	[Table("DiscordLog")]
	[PrimaryKey(nameof(Type), nameof(MessageId))]
	public class DiscordLog
	{
		public string Type { get; set; } = default!;
		public virtual DiscordMessage Message { get; set; } = default!;
		public ulong MessageId { get; set; }
		public DateTimeOffset? Date { get; set; }
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
	[PrimaryKey(nameof(Id), nameof(GuildId))]
	public class DiscordGuildUser
	{
		[DatabaseGenerated(DatabaseGeneratedOption.None)]
		public ulong Id { get; set; }
		public string Username { get; set; }
		public string? Nickname { get; set; }
		public string Displayname => Nickname ?? Username;
		public ulong GuildId { get; set; }
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
}