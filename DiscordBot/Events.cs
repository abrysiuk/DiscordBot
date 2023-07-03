using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot
{
	public partial class Program
	{
		public async Task Ready()
		{
			if (_client == null)
			{
				return;
			}
			await BuildCommands();
			await SetStatus();
			return;
		}

		private async Task MembersDownloaded(SocketGuild guild)
		{
			List<SocketGuildUser> members;

			if (guild == null) { return; }

			members = guild.Users.ToList();

			var _db = new AppDBContext();
			var discordMembers = _db.GuildUsers.Where(x => x.GuildId == guild.Id).ToList();

			foreach (var member in members)
			{
				var temp = discordMembers.Find(x => x.Id == member.Id);
				if (temp != null)
				{
					temp = member;
				}
				else
				{
					_db.GuildUsers.Add(member);
				}


			}
			await _db.SaveChangesAsync();
			_ = ProcessGuild(guild.Id);
			return;
		}

		private async Task MemberDownloaded(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser user)
		{
			await MemberJoined(user);
			return;
		}

		private async Task MemberJoined(SocketGuildUser user)
		{
			var _db = new AppDBContext();
			var member = await _db.GuildUsers.FindAsync(user.Id, user.Guild.Id);
			if (member == null)
			{
				_db.GuildUsers.Add(user);
			}
			else
			{
				member = user;
			}
			_db.SaveChanges();
			return;
		}

		private async Task LatencyUpdated(int a, int b)
		{
			var _db = new AppDBContext();
			var birthdays = _db.BirthdayDefs.ToList().Where(x => x.Date.Date == TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.Today, x.TimeZone).Date);
			foreach (var birthday in birthdays)
			{
				var guild = _client.GetGuild(birthday.GuildId);
				if (guild == null) { return; }

				var user = guild.GetUser(birthday.UserId);
				if (user == null) { return; }
				birthday.Date = birthday.Date.AddYears(1);
				_db.SaveChanges();
				await guild.SystemChannel.SendMessageAsync($"Happy Birthday {user.Mention}!");
			}

		}
		private async Task ReceiveMessage(IMessage message)
		{
			if (message is IUserMessage iuMessage && message.Channel is IGuildChannel igChannel)
			{

				if ((message.Flags & MessageFlags.Ephemeral) == MessageFlags.Ephemeral) { return; }
				var _db = new AppDBContext();
				DiscordMessage msg = (DiscordMessage)(SocketUserMessage)message;
				msg.GuildId = igChannel.Guild.Id;
				msg.DiscordShames = new List<DiscordShame>();
				await React(iuMessage, msg.DiscordShames);
				_db.UserMessages.Add(msg);
				_db.SaveChanges();
			}
		}
		private async Task UpdateMessage(Cacheable<IMessage, ulong> oldMessage, IMessage newMessage, IMessageChannel channel)
		{
			if (newMessage is IUserMessage suMessage && newMessage != null)
			{
				if ((newMessage.Flags & MessageFlags.Ephemeral) == MessageFlags.Ephemeral) { return; }
				var _db = new AppDBContext();
				DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordShames).FirstOrDefault(s => s.Id == newMessage.Id);
				string oldMsg = oldMessage.Value?.CleanContent ?? String.Empty;
				if (msg != null && string.IsNullOrEmpty(oldMsg)) oldMsg = msg.CleanContent;

				if (oldMsg == string.Empty) { oldMsg = "Unknown"; }

				if (oldMsg == newMessage.CleanContent)
				{
					return;
				}

				var guild = (IGuildChannel)channel;

				if (msg == null)
				{
					msg = (DiscordMessage)suMessage;
					msg.GuildId = ((IGuildChannel)suMessage.Channel).Guild.Id;
					msg.DiscordShames = new List<DiscordShame>();
					_db.UserMessages.Add(msg);
				}
				else
				{
					_db.Entry(msg).CurrentValues.SetValues((DiscordMessage)(SocketUserMessage)suMessage);
					msg.GuildId = ((IGuildChannel)suMessage.Channel).Guild.Id;
				}
				await React(suMessage, msg.DiscordShames);

				if (!msg.DiscordShames.Any(x => x.MessageId == newMessage.Id && x.Type == "Edit"))
				{
					msg.DiscordShames.Add(new DiscordShame() { Message = msg, MessageId = msg.Id, Type = "Edit", Date = msg.EditedTimestamp ?? DateTimeOffset.Now });
				}

				_db.SaveChanges();
				await SetStatus();

				var reactChannel = (await ((IGuildChannel)(newMessage).Channel).Guild.GetTextChannelsAsync()).FirstOrDefault(x => x.Name.Contains("shame") || x.Name.Contains("the-log"));

				var author = (IGuildUser)newMessage.Author;
				if (reactChannel != null && oldMsg != newMessage.CleanContent)
				{
					ChannelPermissions channelPerms = (await ((IGuild)guild).GetCurrentUserAsync()).GetPermissions(reactChannel);

					if (!channelPerms.ViewChannel || !channelPerms.SendMessages)
					{
						await Log(LogSeverity.Verbose, "Commands", $"Can't send log messages in #{reactChannel.Name}");
						return;
					}

					EmbedBuilder embed = new();
					embed.AddField("Original Message", oldMsg)
						.AddField("Edited Message", newMessage.CleanContent)
						.WithFooter(footer => footer.Text = $"In #{newMessage.Channel.Name}")
						.WithColor(Color.Orange);
					if (author != null) embed.Author = new EmbedAuthorBuilder().WithName(GetUserName(author)).WithIconUrl(author.GetDisplayAvatarUrl() ?? author.GetDefaultAvatarUrl());
					await reactChannel.SendMessageAsync(embed: embed.Build());
				}
			}
		}
		private async Task DeleteMessage(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
		{
			var _db = new AppDBContext();
			DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordShames).FirstOrDefault(s => s.Id == message.Id);
			IUserMessage? imsg = (IUserMessage)message.Value;
			IGuildChannel? ichannel = (IGuildChannel)channel.Value;

			if (msg == null && imsg != null)
			{
				msg = (DiscordMessage)imsg;
				msg.GuildId = ((IGuildChannel)imsg.Channel).Guild.Id;
				msg.Deleted = DateTimeOffset.Now;
				msg.DiscordShames = new List<DiscordShame>();
				_db.UserMessages.Add(msg);
			}

			if (ichannel == null && imsg != null)
			{
				ichannel = (IGuildChannel)imsg.Channel;
			}

			ichannel ??= (IGuildChannel)_client.GetChannel(channel.Id);

			var recentLogs = await ichannel.Guild.GetAuditLogsAsync(1);

			if (recentLogs.Any(x => x.User.Id != msg?.AuthorId && x.Data is MessageDeleteAuditLogData data && data.ChannelId == channel.Id))
			{
				_db.SaveChanges();
				return;
			}

			if (msg != null)
			{
				if (!msg.DiscordShames.Any(x => x.MessageId == msg.Id && x.Type == "Edit"))
				{
					msg.DiscordShames.Add(new DiscordShame() { Message = msg, MessageId = msg.Id, Type = "Edit", Date = msg.Deleted ?? DateTimeOffset.Now });
				}
				_db.SaveChanges();
			}

			ITextChannel? reactChannel = null;
			IGuild guild;

			if (ichannel != null || msg?.GuildId != null)
			{
				guild = ichannel?.Guild ?? _client.GetGuild(msg.GuildId);
				reactChannel = (await guild.GetTextChannelsAsync()).FirstOrDefault(x => x.Name.Contains("shame") || x.Name.Contains("the-log"));
			}
			else
			{
				return;
			}

			if (reactChannel == null) { return; }

			ChannelPermissions channelPerms = (await ((IGuild)guild).GetCurrentUserAsync()).GetPermissions(reactChannel);

			if (!channelPerms.ViewChannel || !channelPerms.SendMessages)
			{
				await Log(LogSeverity.Verbose, "Commands", $"Can't send log messages in #{reactChannel.Name}");
				return;
			}

			EmbedBuilder embed = new();

			if (msg == null)
			{
				embed.AddField("Deleted Message", "Unknown Original Content")
					.WithColor(Color.Red)
					.WithFooter(footer => footer.Text = $"In #{ichannel?.Name ?? "Unknown"}");
			}
			else
			{
				var author = (IGuildUser?)imsg?.Author ?? _client.Guilds.FirstOrDefault(x => x.Id == msg.GuildId)?.GetUser(msg.AuthorId ?? 0);
				if (author != null) embed.Author = new EmbedAuthorBuilder().WithName(GetUserName(author)).WithIconUrl(author.GetDisplayAvatarUrl() ?? author.GetDefaultAvatarUrl());
				embed.AddField("Deleted Message", $"{(String.IsNullOrEmpty(msg.CleanContent) ? "<No normal text>" : msg.CleanContent)}")
					.WithColor(Color.Red)
					.WithFooter(footer => footer.Text = $"In #{ichannel?.Name ?? "Unknown"}");
			}
			await reactChannel.SendMessageAsync(embed: embed.Build());
			await SetStatus();
		}
		private async Task SlashCommandHandler(ISlashCommandInteraction command)
		{
			switch (command.Data.Name)
			{
				case "leaderboard":
					await LeaderboardCommand(command);
					return;

				case "birthday":
					await BirthdayCommand(command);
					return;
			}
		}
		private Task Log(LogSeverity logSeverity, string source, string message)
		{
			return Log(new LogMessage(logSeverity, source, message));
		}
		private Task Log(LogMessage msg)
		{
			if (logLevel < (int)msg.Severity) { return Task.CompletedTask; }
			switch (msg.Severity)
			{
				case LogSeverity.Critical:
					Console.ForegroundColor = ConsoleColor.Black;
					Console.BackgroundColor = ConsoleColor.Red;
					break;
				case LogSeverity.Error:
					Console.ForegroundColor = ConsoleColor.Red;
					break;
				case LogSeverity.Warning:
					Console.ForegroundColor = ConsoleColor.Yellow;
					break;
				case LogSeverity.Info:
					Console.ForegroundColor = ConsoleColor.White;
					break;
				case LogSeverity.Verbose:
					Console.ForegroundColor = ConsoleColor.Cyan;
					break;
				case LogSeverity.Debug:
					Console.ForegroundColor = ConsoleColor.Magenta;
					break;
			}
			Console.WriteLine(msg.ToString());
			Console.ResetColor();
			return Task.CompletedTask;
		}
	}


}
