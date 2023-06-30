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
			BuildCommands();
			return Task.CompletedTask;
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
				await Log(LogSeverity.Debug, "Debug", $"Birthday Event {user.Mention} {x.Date}");
				//await guild.SystemChannel.SendMessageAsync($"Happy Birthday {user.Mention}!", allowedMentions: AllowedMentions.All);
				x.Date = x.Date.AddYears(1);
				await Log(LogSeverity.Debug, "Debug", $"Birthday Follow up {user.Mention} {x.Date}");
			});

			await _db.SaveChangesAsync();
			_db.Dispose();
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
				_db.Dispose();
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

				if (shameChannel != null && oldMsg != newMessage.CleanContent)
				{
					EmbedBuilder embed = new();
					embed.AddField("Original Message", oldMsg)
						.AddField("Edited Message", newMessage.CleanContent)
						.WithFooter(footer => footer.Text = $"In #{newMessage.Channel.Name} by {((IGuildUser)newMessage.Author).Nickname ?? newMessage.Author.Username}")
						.WithColor(Color.Orange);
					if (newMessage.Author != null) embed.WithAuthor(newMessage.Author);
					await shameChannel.SendMessageAsync(embed: embed.Build());
				}

				_db.SaveChanges();
				_db.Dispose();
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
			_db.Dispose();
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
				var author = (IGuildUser?)imsg?.Author ?? _client.Guilds.FirstOrDefault(x => x.Id == msg.GuildId)?.GetUser(msg.AuthorId);
				if (author != null) embed.WithAuthor(author);
				embed.AddField("Deleted Message", $"{(String.IsNullOrEmpty(msg.CleanContent) ? "<No normal text>" : msg.CleanContent)}")
					.WithColor(Color.Red)
					.WithFooter(footer => footer.Text = $"In #{ichannel?.Name ?? "Unknown"} by {author?.Nickname ?? author?.Username ?? "Unknown"}");
			}
			await shameChannel.SendMessageAsync(embed: embed.Build());
		}
		private async Task SlashCommandHandler(ISlashCommandInteraction command)
		{
			switch (command.Data.Name)
			{
				case "leaderboard":
					var stat = (long)command.Data.Options.First(x => x.Name == "stat").Value;
					var days = (long?)command.Data.Options.FirstOrDefault(x => x.Name == "days")?.Value;

					if (days == 0) { days = null; }
					switch (stat)
					{
						case 1:
							await command.RespondAsync(embed: await LeaderboardGen("I mean", "It's mean, but I mean is a crutch", "Times a statement started with 'I mean'.", command, days), ephemeral: true);
							break;
						case 2:
							await command.RespondAsync(embed: await LeaderboardGen("Game Pass", "Is alcohol free at an all-inclusive?", "Times a game was declared free on game pass.", command, days), ephemeral: true);
							break;
						case 3:
							await command.RespondAsync(embed: await LeaderboardGen("Edit", "Robots are faster than ninjas", "Times edited or deleted messages", command, days), ephemeral: true);
							break;
					}
					break;
				case "birthday":
					var user = (IUser)command.Data.Options.First(x => x.Name == "user").Value;
					if (user == null)
					{
						await command.RespondAsync("Sorry, I couldn't find that user", ephemeral: true);
						break;
					}
					var month = (long?)command.Data.Options.First(x => x.Name == "month").Value;
					var day = (long?)command.Data.Options.First(x => x.Name == "day").Value;

					if (month == null || day == null)
					{
						await command.RespondAsync("Missing day or month (which Discord shouldn't allow)", ephemeral: true);
						break;
					}

					if (month == 2 && day == 29)
					{
						await command.RespondAsync($"No one has a birthday on February 29. Fuck off and quit testing my bot.", ephemeral: true);
						break;
					}

					if (day > DateTime.DaysInMonth(DateTime.Now.Year, (int)month))
					{
						await command.RespondAsync($"You know there aren't {day} days in {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName((int)month)}.", ephemeral: true);
						break;
					}
					if (command.GuildId == null)
					{
						await command.RespondAsync("Please don't DM me. We're not friends.", ephemeral: true);
						break;
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
					_db.Dispose();
					await command.RespondAsync("Birthday List Updated!", ephemeral: true);
					break;
			}
			await Log(LogSeverity.Verbose, "Discord Command", $"{command.User.Username} issued {command.Data.Name}.");
		}
		private async Task<Embed> LeaderboardGen(string type, string footerText, string description, ISlashCommandInteraction command, long? days)
		{
			if (command.GuildId == null)
			{
				return new EmbedBuilder().WithDescription("I can only tell you leaderboard stats within a server.").Build();
			}

			var guild = _client.GetGuild((ulong)command.GuildId);

			var guildusers = await guild.GetUsersAsync().Flatten().ToListAsync();

			string strBuilder;
			EmbedBuilder embed = new();
			var _db = new AppDBContext();
			var messages = _db.DiscordShame
				.Include(x => x.Message)
				.Where(x=> x.Type == type && (days == null || x.Date >=DateTimeOffset.Now.AddDays(-(double)days)))
				.Where(x => x.Message.GuildId == command.GuildId)
				.GroupBy(x=>x.Message.AuthorId)
				.ToList()
				.Select(x=> new
				{
					name = guildusers.Find(y => y.Id == x.Key)?.Nickname ?? guildusers.Find(y => y.Id == x.Key)?.Username ?? $"Deleted #{x.Key}",
					count = x.Count()
				})
				.OrderByDescending(x => x.count).ToList();

			//var messages = _db.UserMessages
			//	.Include(x => x.DiscordShames)
			//	.Where(x => x.GuildId == command.GuildId && x.DiscordShames.Any(y => y.Type == type && (days == null || y.Date >= DateTimeOffset.Now.AddDays(-(double)days))))
			//	.GroupBy(x => x.AuthorId)
			//	.ToList()
			//	.Select(x =>
			//	new
			//	{
			//		name = guildusers.Find(y => y.Id == x.Key)?.Nickname ?? guildusers.Find(y => y.Id == x.Key)?.Username ?? $"Deleted #{x.Key}",
			//		count = x.Count()
			//	}).
			//	OrderByDescending(x => x.count).ToList();

			if (!messages.Any())
			{
				return new EmbedBuilder().WithDescription("That stat has no tracked instances").Build();
			}

			int len = messages.Aggregate(4, (x, y) => y.name.Length > x ? y.name.Length : x);

			strBuilder =
				$"+{new String('-', len + 2)}+-------+\n" +
				$"| {"User".PadRight(len)} | Count |\n" +
				$"+{new String('-', len + 2)}+-------+\n";

			foreach (var item in messages)
			{
				strBuilder += "| " + item.name.PadRight(len) + " | " + item.count.ToString().PadLeft(5) + " |\n";
			}

			strBuilder += $"+{new String('-', len + 2)}+-------+";
			embed.WithTitle("Stat Leaderboard")
				.WithDescription(description)
				.WithFooter(footer => footer.Text = footerText)
				.AddField(days == null ? "All-Time" : $"Last {days} Days", "```\n" + strBuilder + "```")
				.WithColor(Color.DarkPurple);

			return embed.Build();

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
