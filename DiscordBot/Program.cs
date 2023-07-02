using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Discord.Net;
using Microsoft.EntityFrameworkCore;
using Discord.Rest;
using System;

namespace DiscordBot
{
	public partial class Program
	{
		public static IConfiguration Configuration => new ConfigurationBuilder().AddUserSecrets<Program>().Build();
		private DiscordSocketClient _client = default!;
		public static Task Main(string[] args) => new Program().MainAsync(args);
		private int logLevel;
		public async Task MainAsync(string[] args)
		{
			var logLevels = args.Where(arg => arg.Contains("loglevel")).Where(arg => int.TryParse(arg.Split('=')[1], out int logTry) && logTry < 6).Select(arg => int.Parse(arg.Split('=')[1]));
			logLevel = logLevels.Any() ? logLevels.First() : 3;
			await Log(LogSeverity.Verbose, "Log", $"Log level set to {(LogSeverity)logLevel}");
			_client = new DiscordSocketClient(new DiscordSocketConfig
			{
				LogLevel = (LogSeverity)logLevel,
				MessageCacheSize = 1024,
				DefaultRetryMode = RetryMode.RetryRatelimit,
				GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.Guilds | GatewayIntents.GuildMembers,
				UseInteractionSnowflakeDate = false,
				AlwaysDownloadUsers = true
			});
			string? apiKey = Configuration["Discord:APIKey"];
			_client.Log += Log;
			_client.Ready += Ready;
			await _client.LoginAsync(TokenType.Bot, apiKey);
			await _client.StartAsync();

			while (true)
			{
				switch (Console.ReadLine()?.ToLower())
				{
					case "exit" or "quit":
						await _client.StopAsync();
						await _client.LogoutAsync();
						return;
					case "buildcommands":
						await BuildCommands();
						break;
					case "genhistory":
						Console.WriteLine("Enter Guild ID: ");
						if (!ulong.TryParse(Console.ReadLine(), out ulong guildid))
						{
							await Log(LogSeverity.Error, "User Input", "Invalid GuildId format");
							break;
						}
						Console.WriteLine("Enter # of Messages per Channel (0 is no limit): ");
						if (!int.TryParse(Console.ReadLine(), out int cnt))
						{
							await Log(LogSeverity.Error, "User Input", "Invalid Number format");
							break;
						}
						await ProcessGuild(guildid, cnt);
						break;
					case "status":
						await _client.SetGameAsync($" {Console.ReadLine()}", type: ActivityType.Playing);
						break;
					default:
						await Log(LogSeverity.Error, "User Input", "Command Unknown");
						break;
				}
			}
		}
		private async Task BuildCommands()
		{
			if (_client == null)
			{
				await Log(LogSeverity.Error, "Commands", "Command requested while client is not connected.");
				return;
			}
			var leaderboardCommand = new SlashCommandBuilder();
			leaderboardCommand.WithName("leaderboard");
			leaderboardCommand.WithDescription("Display a leaderboard of messages tracked by ShameBot");
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
		private async Task ProcessGuild(ulong guildId, int cnt)
		{
			if (cnt == 0) { cnt = int.MaxValue; }
			var guild = _client.GetGuild(guildId);
			if (guild == null)
			{
				await Log(LogSeverity.Warning, "Process Messages", $"Cannot find guild {guildId}");
				return;
			}

			var channels = guild.Channels.Where(x => x.GetChannelType() == ChannelType.Text || x.GetChannelType() == ChannelType.PublicThread || x.GetChannelType() == ChannelType.PrivateThread || x.GetChannelType() == ChannelType.Voice);

			if (!channels.Any())
			{
				await Log(LogSeverity.Warning, "Process Messages", $"Guild {guildId} has no text channels available.");
				return;
			}

			await Log(LogSeverity.Verbose, "Commands", $"Found {channels.Count()} channels");

			var _db = new AppDBContext();

			foreach (ITextChannel channel in channels.Cast<ITextChannel>())
			{
				await Log(LogSeverity.Verbose, "Commands", $"Processing {channel.Name}.");
				try
				{
					var messages = await channel.GetMessagesAsync(cnt).Flatten().Where(x => x is RestUserMessage).ToListAsync();
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
					await Log(LogSeverity.Error, "GenHistory", $"{e.Message}");
				}
			}

			_db.SaveChanges();
			_ = SetStatus();

			return;
		}
		private async Task ProcessMessage(IUserMessage Message, IGuildChannel channel, AppDBContext _db)
		{
			if (Message is RestUserMessage suMessage && Message != null)
			{
				DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordShames).FirstOrDefault(s => s.Id == Message.Id);
				if (msg == null)
				{
					msg = suMessage;
					msg.GuildId = channel.Guild.Id;
					msg.DiscordShames = new List<DiscordShame>();
					_db.UserMessages.Add(msg);
				}
				else
				{
					_db.Entry(msg).CurrentValues.SetValues((DiscordMessage)suMessage);
					msg.GuildId = channel.Guild.Id;
				}
				await Shame(suMessage, msg.DiscordShames);

				if (msg.EditedTimestamp != null && !msg.DiscordShames.Any(x => x.MessageId == msg.Id && x.Type == "Edit"))
				{
					msg.DiscordShames.Add(new DiscordShame() { Message = msg, MessageId = msg.Id, Type = "Edit", Date = msg.EditedTimestamp ?? DateTimeOffset.Now });
				}

			}
		}
		private async Task Shame(IUserMessage msg, ICollection<DiscordShame> discordShames)
		{
			var Reactions = new List<ReactionDef>
			{
				new ReactionDef("I mean", @"(^|[.?!;,:-])\s*i\s+mean\b", @"<a:ditto:1075842464415494214>"),
				new ReactionDef("Game Pass", @"free\b.*game\s*pass|game\s*pass\b.*free","\uD83D\uDCB0")
			};

			foreach (ReactionDef reaction in Reactions)
			{
				if (!Regex.IsMatch(msg.Content.ToString(), reaction.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase)) { continue; }
				if (discordShames.Where(x => x.Type == reaction.Name).Any()) { continue; }
				discordShames.Add(new DiscordShame() { Type = reaction.Name, Date = msg.CreatedAt });
				_ = SetStatus();
				if (Emote.TryParse(reaction.Emote, out var emote))
				{
					await msg.AddReactionAsync(emote);
					await Log(LogSeverity.Verbose, "Bot", $"{reaction.Name} {msg.Author.Username}:{msg.Content}");
				}
				else if (Emoji.TryParse(reaction.Emote, out var emoji))
				{
					await msg.AddReactionAsync(emoji);
					await Log(LogSeverity.Verbose, "Bot", $"{reaction.Name} {msg.Author.Username}:{msg.Content}");
				}
			}
		}
		private string? GetUserName(IGuildUser? user)
		{
			return user == null ? null : GetUserName(user.Id, user.GuildId);
		}
		private string? GetUserName(ulong id, ulong guildid)
		{
			var guild = _client.GetGuild(guildid);
			var user = guild.Users.FirstOrDefault(x => x.Id == id);
			if (user != null) { return user.DisplayName; }

			var _db = new AppDBContext();
			var member = _db.GuildUsers.Find(id, guildid);

			return member?.Displayname ?? $"Deleted #{id}";

		}

		private async Task SetStatus()
		{
			var db = new AppDBContext();

			await _client.SetGameAsync($" Shame: {db.DiscordShame.Count()}",type: ActivityType.Playing);
		}
	}
}