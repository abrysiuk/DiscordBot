using Discord.Net;
using Discord.Rest;
using Discord;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System;
using Discord.WebSocket;

namespace DiscordBot
{
	partial class Program
	{
        #region Console Commands
        /// <summary>
        /// Temporary function to delete any messages (and reaction logs) which do not exist on the discord to account for buggy message deletion.
        /// </summary>
        /// <param name="since">Date to start cleanup check from. Earlier = slower</param>
        /// <returns></returns>
        private async Task DeletedCleanup(DateTime since)
		{
			var _db = new AppDBContext();

			var dbMessages = _db.UserMessages.Where(x => x.CreatedAt >= since);

            var groups = dbMessages.GroupBy(x => new { x.ChannelId, x.GuildId }).Select(x=>new {x.Key.ChannelId, x.Key.GuildId, Messages = x.Count() }).ToList();

			foreach (var group in groups)
			{
				var guild = _client.GetGuild(group.GuildId);
				var channel = (SocketTextChannel)guild.GetChannel(group.ChannelId);

                await Log(LogSeverity.Verbose, "Commands", $"Processing deletions from {channel.Name}.");
                ChannelPermissions channelPerms = (await ((IGuild)guild).GetCurrentUserAsync()).GetPermissions(channel);
                if (!channelPerms.ViewChannel || !channelPerms.ReadMessageHistory)
                {
                    await Log(LogSeverity.Verbose, "Commands", $"I do not have permission to read {channel.Name}");
                    continue;
                }

                var discordMessages = await channel.GetMessagesAsync(group.Messages).Flatten().Where(x => x is RestUserMessage).ToListAsync();
				foreach (var dbMessage in dbMessages.Where(x=>x.ChannelId == channel.Id))
				{
					if (!discordMessages.Exists(x=> x.Id == dbMessage.Id))
					{
						_db.Remove(dbMessage);
					}
				}
            }
			_db.SaveChanges();

		}
		/// <summary>
		/// Registers global slash commands with Discord.
		/// </summary>
		/// <returns></returns>
		private async Task BuildCommands()
		{
			if (_client == null)
			{
				await Log(LogSeverity.Error, "Commands", "Command requested while client is not connected.");
				return;
			}
			var leaderboardCommand = new SlashCommandBuilder();
			leaderboardCommand.WithName("leaderboard");
			leaderboardCommand.WithDescription($"Display a leaderboard of messages tracked by {_client.CurrentUser.Username}");
			leaderboardCommand.AddOption(new SlashCommandOptionBuilder()
				.WithName("stat")
				.WithDescription("What stat would you like to report on?")
				.WithRequired(true)
				.AddChoice("I mean", 1)
				.AddChoice("Game Pass", 2)
				.AddChoice("Regrets", 3)
				.AddChoice("Messages", 4)
				.WithType(ApplicationCommandOptionType.Integer)
				);
			leaderboardCommand.AddOption(new SlashCommandOptionBuilder()
				.WithName("days")
				.WithDescription("How many days back would you like to go? (Optional, default all-time)")
				.WithRequired(false)
				.WithType(ApplicationCommandOptionType.Integer)
				.WithMinValue(0)
				);
			leaderboardCommand.AddOption(new SlashCommandOptionBuilder()
				.WithName("normalize")
				.WithDescription("Show stat as a function of messages/time?")
				.WithRequired(false)
				.WithType(ApplicationCommandOptionType.Boolean)
				);
			leaderboardCommand.WithDMPermission(false);

			var BirthdayCommand = new SlashCommandBuilder();
			BirthdayCommand.WithName("birthday").WithDescription("Add a birthday to the database").WithDefaultMemberPermissions(GuildPermission.Administrator).WithDMPermission(false).AddOption(new SlashCommandOptionBuilder()
				.WithName("user")
				.WithDescription("Who's birthday do you want to set?")
				.WithRequired(true)
				.WithType(ApplicationCommandOptionType.User));
			BirthdayCommand.AddOption(new SlashCommandOptionBuilder()
				.WithName("month")
				.WithDescription("What month is their birthday?")
				.WithRequired(true)
				.WithType(ApplicationCommandOptionType.Integer)
				.WithMinValue(1)
				.WithMaxValue(12)
				);
			BirthdayCommand.AddOption(new SlashCommandOptionBuilder()
				.WithName("day")
				.WithDescription("What day is their birthday?")
				.WithRequired(true)
				.WithType(ApplicationCommandOptionType.Integer)
				.WithMinValue(1)
				.WithMaxValue(31)
				);
			SlashCommandOptionBuilder TimeZoneOption = new SlashCommandOptionBuilder()
				.WithName("timezone")
				.WithDescription("What timezone is the user in?")
				.WithRequired(true)
				.WithType(ApplicationCommandOptionType.String)
				.WithAutocomplete(true);

			TimeZoneOption.AddChoice($"Hawaiian Standard Time (-10:00:00", "Hawaiian Standard Time");
			TimeZoneOption.AddChoice($"Alaskan Standard Time (-09:00:00)", "Alaskan Standard Time");
			TimeZoneOption.AddChoice($"Pacific Standard Time (-08:00:00)", "Pacific Standard Time");
			TimeZoneOption.AddChoice($"US Mountain Standard Time (-07:00:00)", "US Mountain Standard");
			TimeZoneOption.AddChoice($"Mountain Standard Time (-07:00:00)", "Mountain Standard Time");
			TimeZoneOption.AddChoice($"Central Standard Time (-06:00:00)", "Central Standard Time");
			TimeZoneOption.AddChoice($"Canada Central Standard Time (-06:00:00)", "Canada Central Standard Time");
			TimeZoneOption.AddChoice($"Eastern Standard Time (-05:00:00)", "Eastern Standard Time");
			TimeZoneOption.AddChoice($"US Eastern Standard Time (-05:00:00)", "US Eastern Standard Time");
			TimeZoneOption.AddChoice($"Atlantic Standard Time (-04:00:00)", "Atlantic Standard Time");
			BirthdayCommand.AddOption(TimeZoneOption);

			try
			{
				await _client.CreateGlobalApplicationCommandAsync(leaderboardCommand.Build());
				await _client.CreateGlobalApplicationCommandAsync(BirthdayCommand.Build());
			}
			catch (HttpException exception)
			{
				await Log(LogSeverity.Error, "Command Builder", $"Error occured while building commands: {exception.Message}");
			}
			return;
		}
		/// <summary>
		/// Iterates through a guild's text channels and downloads and processes <c>cnt</c> messages from each guild with ID <c>guildId</c>
		/// </summary>
		/// <param name="guildId">ID of the Guild to download the messages from.</param>
		/// <param name="cnt"># of messages per channel to download. 0 will download everything, null will automatically check for the last message in the DB from each channel and download from there forward.</param>
		/// <returns></returns>
		private async Task ProcessGuild(ulong guildId, int? cnt = null)
		{
			if (cnt != null && cnt == 0) { cnt = int.MaxValue; }
			var guild = _client.GetGuild(guildId);
			if (guild == null)
			{
				await Log(LogSeverity.Warning, "Process Messages", $"Cannot find guild {guildId}");
				return;
			}

			var channels = guild.Channels.Where(x => x.GetChannelType() == ChannelType.Text || x.GetChannelType() == ChannelType.PublicThread || x.GetChannelType() == ChannelType.PrivateThread || x.GetChannelType() == ChannelType.Voice);

			if (!channels.Any())
			{
				await Log(LogSeverity.Warning, "Process Messages", $"{guild.Name} has no text channels available.");
				return;
			}

			await Log(LogSeverity.Verbose, "Commands", $"Found {channels.Count()} channels");

			var _db = new AppDBContext();

			foreach (ITextChannel channel in channels.Cast<ITextChannel>())
			{
				try
				{
					await Log(LogSeverity.Verbose, "Commands", $"Processing {channel.Name}.");
					ChannelPermissions channelPerms = (await ((IGuild)guild).GetCurrentUserAsync()).GetPermissions(channel);
					if (!channelPerms.ViewChannel || !channelPerms.ReadMessageHistory)
					{
						await Log(LogSeverity.Verbose, "Commands", $"I do not have permission to read {channel.Name}");
						continue;
					}
					List<IMessage> messages;
					int count;
					if (cnt == null)
					{
						var lastMessage = await channel.GetMessagesAsync(1).FlattenAsync();
						var lastDBmessages = _db.UserMessages.Where(x => x.ChannelId == channel.Id);
						var lastDBmessage = lastDBmessages.Any() ? lastDBmessages.OrderByDescending(x => x.CreatedAt).First() : null;

						if (!lastMessage.Any()) { continue; }

						if (lastDBmessage != null)
						{
							if (lastMessage.First().Id == lastDBmessage.Id)
							{
								continue;
							}

							messages = await channel.GetMessagesAsync(lastMessage.First(), Direction.After, int.MaxValue).Flatten().Where(x => x is RestUserMessage).ToListAsync();
						}

						else
						{
							messages = await channel.GetMessagesAsync(int.MaxValue).Flatten().Where(x => x is RestUserMessage).ToListAsync();
						}
					}
					else
					{
						count = cnt.Value;
						messages = await channel.GetMessagesAsync(count).Flatten().Where(x => x is RestUserMessage).ToListAsync();
					}
					await Log(LogSeverity.Verbose, "Commands", $"Processing {messages.Count} messages.");
					foreach (RestUserMessage message in messages.Cast<RestUserMessage>())
					{
						try
						{
							await ProcessMessage(message, channel, _db);
						}
						catch (Exception e)
						{
							await Log(LogSeverity.Error, "GenHistory", $"{e.Message}");
						}
						
					}
				}
				catch (Exception e)
				{
					await Log(LogSeverity.Error, "GenHistory", $"{e.ToString()}");
				}
			}

			_db.SaveChanges();
			await SetStatus();

			return;
		}
		/// <summary>
		/// Manually process a message, edits it or adds it to the DB, and adds reactions.
		/// </summary>
		/// <param name="Message">The <c>IUserMessage</c> to process.</param>
		/// <param name="channel">The <c>IGuildChannel</c> the message came from.</param>
		/// <param name="_db">An <c>AppDBContext</c> passed from the previous function, to streamline the number of calls to the database.</param>
		/// <seealso cref="ProcessGuild(ulong, int?)"/>
		/// <returns></returns>
		private async Task ProcessMessage(IUserMessage Message, IGuildChannel channel, AppDBContext _db)
		{
			if (Message is RestUserMessage suMessage && Message != null)
			{
				DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordLogs).FirstOrDefault(s => s.Id == Message.Id);
				if (msg == null)
				{
					msg = suMessage;
					msg.GuildId = channel.Guild.Id;
					msg.DiscordLogs = new List<DiscordLog>();
					_db.UserMessages.Add(msg);
				}
				else
				{
					_db.Entry(msg).CurrentValues.SetValues((DiscordMessage)suMessage);
					msg.GuildId = channel.Guild.Id;
				}
				await React(suMessage, msg.DiscordLogs);

				if (msg.EditedTimestamp != null && !msg.DiscordLogs.Any(x => x.MessageId == msg.Id && x.Type == "Edit"))
				{
					msg.DiscordLogs.Add(new DiscordLog() { Message = msg, MessageId = msg.Id, Type = "Edit", Date = msg.EditedTimestamp ?? DateTimeOffset.Now });
				}

			}
		}
		#endregion
		#region Slash Commands
		/// <summary>
		/// Receives a slash command from a text channel and adds a birthday entry to the database.
		/// </summary>
		/// <param name="command">Command passed from the Slash Command Handler</param>
		/// <returns></returns>
		private static async Task BirthdayCommand(ISlashCommandInteraction command)
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
				await command.RespondAsync(text: $"You know there aren't {day} days in {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName((int)month)}.", ephemeral: true);
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
        /// <summary>
        /// Receives a slash command from a text channel and responds with stats from the database.
        /// </summary>
        /// <param name="command">Command passed from the Slash Command Handler</param>
        /// <returns></returns>
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
					var logMessages = _db.DiscordLog
						.Include(x => x.Message)
						.Where(x => x.Type == type && (days == null || x.Date >= DateTimeOffset.Now.AddDays(-(double)days)))
						.Where(x => x.Message.GuildId == command.GuildId)
						.GroupBy(x => x.Message.AuthorId)
						.Select(x => new { authorId = x.Key, logs = x.Count() });

					var totalMessages = _db.UserMessages
						.Where(x => x.GuildId == command.GuildId)
						.GroupBy(x => x.AuthorId)
						.Select(x => new { authorId = x.Key, total = x.Count() });

					var messages = logMessages.Join(totalMessages, x => x.authorId, y => y.authorId, (x, y) => new { x.authorId, x.logs, y.total, per = x.logs * 1000.0 / y.total })
						.Take(30)
						.ToList()
						.Select(x => new
						{
							x.authorId,
							name = GetUserName(x.authorId ?? 0, guild.Id),
							x.logs,
							x.total,
							x.per
						})
						.OrderByDescending(x => normalize ? x.per : x.logs).ToList();

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
						strBuilder += $"| {item.name?[..(item.name.Length > trunc ? trunc : item.name.Length)].PadRight(len)} | {(normalize ? item.per.ToString("0.00") : item.logs.ToString()),6} |\n";
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
						.Select(x => new { authorId = x.Key, total = x.Count(), days = x.OrderBy(y => y.CreatedAt).Last().CreatedAt.Date - x.OrderBy(y => y.CreatedAt).First().CreatedAt.Date })
						.OrderByDescending(x => x.total)
						.Take(30)
						.ToList();

					var messages = totalMessages.Select(x => new
					{
						x.authorId,
						name = GetUserName(x.authorId ?? 0, guild.Id),
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
		#endregion
	}
}
