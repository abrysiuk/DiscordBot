using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DiscordBot
{
	public partial class Program
	{
		public Task Ready()
		{
			if (_client == null)
			{
				return Task.CompletedTask;
			}
			_client.LatencyUpdated += LatencyUpdated;
			_client.MessageReceived += ReceiveMessage;
			_client.MessageUpdated += UpdateMessage;
			_client.MessageDeleted += DeleteMessage;
			_client.SlashCommandExecuted += SlashCommandHandler;
			_client.GuildMembersDownloaded += MembersDownloaded;
			_client.GuildMemberUpdated += MemberDownloaded;
			_client.UserJoined += MemberJoined;
			_ = BuildCommands();
			_ = SetStatus();
			return Task.CompletedTask;
		}

		private async Task MembersDownloaded(SocketGuild guild)
		{
			List<SocketGuildUser> members;

			if (guild == null) { return; }

			if (guild.HasAllMembers)
			{
				members = guild.Users.ToList();
			}
			else
			{
				members = await guild.GetUsersAsync().Flatten().Cast<SocketGuildUser>().ToListAsync();
			}

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
			_db.SaveChanges();
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
			if (member == null) { _db.GuildUsers.Add(user); }
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
			var birthdays = await _db.BirthdayDefs.Where(x => x.Date.Date == DateTime.Today).ToListAsync();
			birthdays.ForEach(async x =>
			{
				var guild = _client.GetGuild(x.GuildId);
				if (guild == null) { return; }

				var user = guild.GetUser(x.UserId);
				if (user == null) { return; }
				await guild.SystemChannel.SendMessageAsync($"Happy Birthday {user.Mention}!");
				x.Date = x.Date.AddYears(1);
			});

			await _db.SaveChangesAsync();
			
		}
		private async Task ReceiveMessage(IMessage message)
		{
			if (message is IUserMessage iuMessage && message.Channel is IGuildChannel igChannel)
			{
				var _db = new AppDBContext();
				DiscordMessage msg = (DiscordMessage)(SocketUserMessage)message;
				msg.GuildId = igChannel.Guild.Id;
				msg.DiscordShames = new List<DiscordShame>();
				await Shame(iuMessage, msg.DiscordShames);
				_db.UserMessages.Add(msg);
				_db.SaveChanges();
				
			}
		}
		private async Task UpdateMessage(Cacheable<IMessage, ulong> oldMessage, IMessage newMessage, IMessageChannel channel)
		{
			if (newMessage is IUserMessage suMessage && newMessage != null)
			{
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
				await Shame(suMessage, msg.DiscordShames);

				if (!msg.DiscordShames.Any(x => x.MessageId == newMessage.Id && x.Type == "Edit"))
				{
					msg.DiscordShames.Add(new DiscordShame() { Message = msg, MessageId = msg.Id, Type = "Edit", Date = msg.EditedTimestamp ?? DateTimeOffset.Now });
				}

				var shameChannel = (await ((IGuildChannel)(newMessage).Channel).Guild.GetTextChannelsAsync()).FirstOrDefault(x => x.Name.Contains("shame")); ;
				var author = (IGuildUser)newMessage.Author;
				if (shameChannel != null && oldMsg != newMessage.CleanContent)
				{
					EmbedBuilder embed = new();
					embed.AddField("Original Message", oldMsg)
						.AddField("Edited Message", newMessage.CleanContent)
						.WithFooter(footer => footer.Text = $"In #{newMessage.Channel.Name}")
						.WithColor(Color.Orange);
					if (author != null) embed.Author = new EmbedAuthorBuilder().WithName(GetUserName(author)).WithIconUrl(author.GetDisplayAvatarUrl() ?? author.GetDefaultAvatarUrl());
					await shameChannel.SendMessageAsync(embed: embed.Build());
				}

				_db.SaveChanges();
				_ = SetStatus();
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
			
			ITextChannel? shameChannel = null;

			if (ichannel != null)
			{
				shameChannel = (await ichannel.Guild.GetTextChannelsAsync()).FirstOrDefault(x => x.Name.Contains("shame"));
			}

			if (shameChannel == null) return;

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
			await shameChannel.SendMessageAsync(embed: embed.Build());
			_ = SetStatus();
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

		private async Task BirthdayCommand(ISlashCommandInteraction command)
		{
			if (command.GuildId == null)
			{
				await command.RespondAsync("Please don't DM me. We're not friends.", ephemeral: true);
				return;
			}
			var user = (IUser)command.Data.Options.First(x => x.Name == "user").Value;
			if (user == null)
			{
				await command.RespondAsync("Sorry, I couldn't find that user", ephemeral: true);
				return;
			}
			var month = (long?)command.Data.Options.First(x => x.Name == "month").Value;
			var day = (long?)command.Data.Options.First(x => x.Name == "day").Value;

			if (month == null || day == null)
			{
				await command.RespondAsync("Missing day or month (which Discord shouldn't allow)", ephemeral: true);
				return;
			}

			if (month == 2 && day == 29)
			{
				await command.RespondAsync($"No one has a birthday on February 29. Fuck off and quit testing my bot.", ephemeral: true);
				return;
			}

			if (day > DateTime.DaysInMonth(DateTime.Now.Year, (int)month))
			{
				await command.RespondAsync($"You know there aren't {day} days in {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName((int)month)}.", ephemeral: true);
				return;
			}

			var date = new DateTime(DateTime.Now.Year, (int)month, (int)day);

			if (date < DateTime.Today) { date = date.AddYears(1); }

			var _db = new AppDBContext();
			var bd = _db.BirthdayDefs.Find(user.Id, command.GuildId) ?? _db.BirthdayDefs.Add(new BirthdayDef()).Entity;
			bd.Date = date;
			bd.CreatedBy = command.User.Username;
			bd.UserId = user.Id;
			bd.GuildId = (ulong)command.GuildId;

			_db.SaveChanges();

			await command.RespondAsync("Birthday List Updated!", ephemeral: true);
		}

		private async Task LeaderboardCommand(ISlashCommandInteraction command)
		{
			const int trunc = 17;

			var stat = (long)command.Data.Options.First(x => x.Name == "stat").Value;
			var days = (long?)command.Data.Options.FirstOrDefault(x => x.Name == "days")?.Value;
			if (days == 0) { days = null; }
			var normalize = (bool?)command.Data.Options.FirstOrDefault(x => x.Name == "normalize")?.Value ?? false;

			string type, footerText, description;
			type = footerText = description = string.Empty;
			Embed embedbuilt = new EmbedBuilder().Build();

			if (command.GuildId == null)
			{
				embedbuilt = new EmbedBuilder().WithDescription("I can only tell you leaderboard stats within a server.").Build();
				return;
			}

			var guild = _client.GetGuild((ulong)command.GuildId);
			await MembersDownloaded(guild);
			
			EmbedBuilder embed = new();
			var _db = new AppDBContext();

			switch (stat)
			{
				case 1:
					type = "I mean";
					footerText = "It's mean, but I mean is a crutch";
					description = "Times a statement started with 'I mean'.";
					break;
				case 2:
					type = "Game Pass";
					footerText = "Is alcohol free at an all-inclusive?";
					description = "Times a game was declared free on game pass.";
					break;
				case 3:
					type = "Edit";
					footerText = "Robots are faster than ninjas";
					description = "Times edited or deleted messages";
					break;
				case 4:
					type = "Messages";
					footerText = "How loud are you?";
					description = "Total messages sent";
					break;
			}
			

			switch (stat)
			{
				case 1 or 2 or 3:
				{
					var shameMessages = _db.DiscordShame
						.Include(x => x.Message)
						.Where(x => x.Type == type && (days == null || x.Date >= DateTimeOffset.Now.AddDays(-(double)days)))
						.Where(x => x.Message.GuildId == command.GuildId)
						.GroupBy(x => x.Message.AuthorId)
						.Select(x => new { authorId = x.Key, shames = x.Count() });

					var totalMessages = _db.UserMessages
						.Where(x => x.GuildId == command.GuildId)
						.GroupBy(x => x.AuthorId)
						.Select(x => new { authorId = x.Key, total = x.Count() });

					var messages = shameMessages.Join(totalMessages, x => x.authorId, y => y.authorId, (x, y) => new { x.authorId, x.shames, y.total, per = x.shames * 1000.0 / y.total })
						.Take(30)
						.ToList()
						.Select(x => new
						{
							x.authorId,
							name = GetUserName(x.authorId ?? 0,guild.Id),
							x.shames,
							x.total,
							x.per
						})
						.OrderByDescending(x => normalize ? x.per : x.shames).ToList();

					if (!messages.Any())
					{
						embedbuilt = new EmbedBuilder().WithDescription("That stat has no tracked instances").Build();
						break;
					}

					int len = Math.Min(messages.Aggregate(4, (x, y) => y.name?.Length > x ? y.name.Length : x), trunc);

					string strBuilder =
						$"+{new String('-', len + 2)}+--------+\n" +
						$"| {"User".PadRight(len)} | {(normalize ? "Per 1k" : "Count"),6} |\n" +
						$"+{new String('-', len + 2)}+--------+\n";

					foreach (var item in messages)
					{
						strBuilder += $"| {item.name?[..(item.name.Length > trunc ? trunc : item.name.Length)].PadRight(len)} | {(normalize ? item.per.ToString("0.00") : item.shames.ToString()),6} |\n";
					}

					strBuilder += $"+{new String('-', len + 2)}+--------+";
					embed.WithTitle("Stat Leaderboard")
						.WithDescription(description)
						.WithFooter(footer => footer.Text = footerText)
						.AddField(days == null ? "All-Time" : $"Last {days} Days", "```\n" + strBuilder + "```")
						.WithColor(Color.DarkPurple);

					embedbuilt = embed.Build();

					break;
				}
				case 4:
				{
					var totalMessages = _db.UserMessages
						.Where(x => x.GuildId == command.GuildId && (days == null || x.CreatedAt >= DateTimeOffset.Now.AddDays(-(double)days)))
						.GroupBy(x => x.AuthorId)
						.Select(x => new { authorId = x.Key, total = x.Count(), days = x.OrderBy(y => y.CreatedAt).Last().CreatedAt.Date - x.OrderBy(y => y.CreatedAt).First().CreatedAt.Date})
						.OrderByDescending(x=>x.total)
						.Take(30)
						.ToList();

					var messages = totalMessages.Select(x => new
						 {
							 x.authorId,
							 name = GetUserName(x.authorId ?? 0,guild.Id),
							 x.total,
							 TotalDays = x.days.TotalDays + 1
						 })
						.OrderByDescending(x => normalize ? x.total / x.TotalDays : x.total).ToList();

					if (!messages.Any())
					{
						embedbuilt = new EmbedBuilder().WithDescription("I don't have a single clue what has been said here.").Build();
						break;
					}

					int len = Math.Min(messages.Aggregate(4, (x, y) => y.name?.Length > x ? y.name.Length : x), trunc);

					string strBuilder =
						$"+{new String('-', len + 2)}+---------+\n" +
						$"| {"User".PadRight(len)} | {(normalize ? "Per Day" : "Count"),7} |\n" +
						$"+{new String('-', len + 2)}+---------+\n";

					foreach (var item in messages)
					{
						strBuilder += $"| {item.name?[..(item.name.Length > trunc ? trunc : item.name.Length)].PadRight(len)} | {(normalize ? (item.total / item.TotalDays).ToString("0.00") : item.total),7} |\n";
					}

					strBuilder += $"+{new String('-', len + 2)}+---------+";
					embed.WithTitle("Stat Leaderboard")
						.WithDescription(description)
						.WithFooter(footer => footer.Text = footerText)
						.AddField(days == null ? "All-Time" : $"Last {days} Days", "```\n" + strBuilder + "```")
						.WithColor(Color.DarkPurple);

					embedbuilt = embed.Build();
					break;
				}
			}
			
			await command.RespondAsync(embed: embedbuilt, ephemeral: true);
			return;
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
