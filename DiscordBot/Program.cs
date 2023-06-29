using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Discord.Net;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using Discord.Rest;
namespace DiscordBot
{
	public class Program
	{
		public static IConfiguration Configuration => new ConfigurationBuilder().AddUserSecrets<Program>().Build();
		private DiscordSocketClient _client = default!;
		private AppDBContext _db = default!;
		public static Task Main(string[] args) => new Program().MainAsync(args);
		private int logLevel;
		public async Task MainAsync(string[] args)
		{
			logLevel = args.Where(arg => arg.Contains("loglevel")).Where(arg => int.TryParse(arg.Split('=')[1], out int logTry) && logTry < 6).Select(arg => int.Parse(arg.Split('=')[1])).FirstOrDefault();

			await Log(LogSeverity.Verbose, "Log", $"Log level set to {(LogSeverity)logLevel}");
			_db = new AppDBContext();
			_client = new DiscordSocketClient(new DiscordSocketConfig
			{
				LogLevel = (LogSeverity)logLevel,
				MessageCacheSize = 1024,
				DefaultRetryMode = RetryMode.RetryRatelimit,
				GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.Guilds,
				UseInteractionSnowflakeDate = false
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
						await ProcessGuild(guildid);
						break;
					default:
						await Log(LogSeverity.Error, "User Input", "Command Unknown");
						break;
				}
			}
		}
		public Task Ready()
		{
			if (_client == null)
			{
				return Task.CompletedTask;
			}
			_client.SlashCommandExecuted += SlashCommandHandler;
			_client.MessageReceived += ReceiveMessage;
			_client.MessageUpdated += UpdateMessage;
			_client.MessageDeleted += DeleteMessage;
			return Task.CompletedTask;
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
			leaderboardCommand.WithDescription("Display a leaderboard of messages tracked by Ditto.NET");
			leaderboardCommand.AddOption(new SlashCommandOptionBuilder()
				.WithName("stat")
				.WithDescription("What stat would you like to report on?")
				.WithRequired(true)
				.AddChoice("I mean", 1)
				.AddChoice("Game Pass", 2)
				.AddChoice("Regrets", 3)
				.WithType(ApplicationCommandOptionType.Integer)
				);
			try
			{
				await _client.CreateGlobalApplicationCommandAsync(leaderboardCommand.Build());
			}
			catch (HttpException exception)
			{
				var json = JsonConvert.SerializeObject(exception.Errors);
				await Log(LogSeverity.Error, "Command Builder", $"Error occured while building commands: {json}");
			}
			return;
		}
		private async Task SlashCommandHandler(ISlashCommandInteraction command)
		{
			switch (command.Data.Name)
			{
				case "leaderboard":
					switch ((long)command.Data.Options.First().Value)
					{
						case 1:
							await command.RespondAsync(embed: LeaderboardGen("I mean", "It's mean, but I mean is a crutch", "Times a statement started with 'I mean'.", command), ephemeral: true);
							break;
						case 2:
							await command.RespondAsync(embed: LeaderboardGen("Game Pass", "Is alcohol free at an all-inclusive?", "Times a game was declared free on game pass.", command), ephemeral: true);
							break;
						case 3:
							await command.RespondAsync(embed: LeaderboardGen("Edit", "Ninjas are slower than robots", "Times edited or deleted messages", command), ephemeral: true);
							break;
					}
					break;
			}
			await Log(LogSeverity.Verbose, "Discord Command", $"{command.User.Username} issued {command.Data.Name}.");
		}
		private Embed LeaderboardGen(string type, string footerText, string description, ISlashCommandInteraction command)
		{
			if (command.GuildId is null)
			{
				return new EmbedBuilder().WithDescription("I can only tell you leaderboard stats within a server.").Build();
			}

			string strBuilder;
			EmbedBuilder embed = new();

			var messages = _db.UserMessages
				.Include(x => x.DiscordShames)
				.Where(x => x.GuildId == command.GuildId && x.DiscordShames.Any(y => y.Type == type))
				.GroupBy(x => x.AuthorId)
				.Select(x => new
				{
					name = _client.GetGuild((ulong)command.GuildId).GetUser(x.Key).Nickname ?? _client.GetGuild((ulong)command.GuildId).GetUser(x.Key).Username ?? $"Deleted #{x.Key}",
					count = x.Count()
				}).
				OrderByDescending(x => x.count).ToList();

			if (!messages.Any())
			{
				return new EmbedBuilder().WithDescription("That stat has no tracked instances").Build();
			}

			int len = messages.Aggregate(4, (x, y) => y.name.Length > x ? y.name.Length : x);

			strBuilder =
				$"+{new String('-', len + 2)}+-------+\n" +
				$"  {"User".PadRight(len)}   Count  \n" +
				$"+{new String('-', len + 2)}+-------+\n";

			foreach (var item in messages)
			{
				strBuilder += "| " + item.name.PadRight(len) + " | " + item.count.ToString().PadLeft(5) + " |\n";
			}

			strBuilder += $"+{new String('-', len + 2)}+-------+";
			Console.WriteLine(strBuilder);

			embed.WithTitle("Stat Leaderboard")
				.WithDescription(description)
				.WithFooter(footer => footer.Text = footerText)
				.AddField("All-Time", "```" + strBuilder + "```")
				.WithColor(Color.DarkPurple);

			return embed.Build();

		}
		private async Task ReceiveMessage(IMessage message)
		{
			if (message is IUserMessage iuMessage && message.Channel is IGuildChannel igChannel)
			{
				DiscordMessage msg = (DiscordMessage)(SocketUserMessage)message;
				msg.GuildId = igChannel.Guild.Id;
				msg.DiscordShames = new List<DiscordShame>();
				await Shame(iuMessage, msg.DiscordShames);
				_db.UserMessages.Add(msg);
				_db.SaveChanges();
			}
		}

		private async Task DeleteMessage(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
		{
			DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordShames).FirstOrDefault(s => s.Id == message.Id);
			IUserMessage? imsg = (IUserMessage)message.Value;
			IGuildChannel? ichannel = (IGuildChannel)channel.Value;

			if (msg is null && imsg is not null)
			{
				msg = (DiscordMessage)imsg;
				msg.GuildId = ((IGuildChannel)imsg.Channel).Guild.Id;
				msg.Deleted = DateTimeOffset.Now;
				msg.DiscordShames = new List<DiscordShame>();
				_db.UserMessages.Add(msg);
			}

			if (msg is not null)
			{
				if (!msg.DiscordShames.Any(x => x.MessageId == msg.Id && x.Type == "Edit"))
				{
					msg.DiscordShames.Add(new DiscordShame() { Message = msg, MessageId = msg.Id, Type = "Edit", Date = msg.Deleted ?? DateTimeOffset.Now });
				}
				_db.SaveChanges();
			}

			if (ichannel is null && imsg is not null)
			{
				ichannel = (IGuildChannel)imsg.Channel;
			}

			if (ichannel is null)
			{
				_client.GetChannel(channel.Id);
			}

			ITextChannel? shameChannel = null;

			if (ichannel is not null)
			{
				shameChannel = (await ichannel.Guild.GetTextChannelsAsync()).FirstOrDefault(x => x.Name.Contains("shame"));
			}

			if (shameChannel is null) return;

			EmbedBuilder embed = new();

			if (msg is null)
			{
				embed.AddField("Deleted Message", "Unknown Original Content")
					.WithColor(Color.Red)
					.WithFooter(footer => footer.Text = $"#{ichannel?.Name ?? "Unknown"}");
			}
			else
			{
				var author = (IGuildUser?)imsg?.Author ?? _client.Guilds.FirstOrDefault(x => x.Id == msg.GuildId)?.GetUser(msg.AuthorId);
				if (author is not null) embed.WithAuthor(author);
				embed.AddField("Deleted Message", $"{(String.IsNullOrEmpty(msg.CleanContent) ? "<No normal text>" : msg.CleanContent)}")
					.WithColor(Color.Red)
					.WithFooter(footer => footer.Text = $"In #{ichannel?.Name ?? "Unknown"} by {author?.Nickname ?? author?.Username ?? "Unknown"}");
			}
			await shameChannel.SendMessageAsync(embed: embed.Build());
		}

		private async Task ProcessGuild(ulong guildId)
		{
			var guild = _client.GetGuild(guildId);
			if (guild == null)
			{
				await Log(LogSeverity.Warning, "Process Messages", $"Cannot find guild {guildId}");
				return;
			}

			var channels = guild.Channels.Where(x => x.GetChannelType() == ChannelType.Text || x.GetChannelType() == ChannelType.PublicThread || x.GetChannelType() == ChannelType.PrivateThread);

			if (!channels.Any())
			{
				await Log(LogSeverity.Warning, "Process Messages", $"Guild {guildId} has no text channels available.");
				return;
			}

			await Log(LogSeverity.Verbose, "Commands", $"Found {channels.Count()} channels");

			foreach (ITextChannel channel in channels.Cast<ITextChannel>())
			{
				await Log(LogSeverity.Verbose, "Commands", $"Processing {channel.Name}.");
				var messages = await channel.GetMessagesAsync(int.MaxValue).Flatten().ToListAsync();
				await Log(LogSeverity.Verbose, "Commands", $"Processing {messages.Count} messages.");
				foreach (RestUserMessage message in messages.Cast<RestUserMessage>())
				{
					await ProcessMessage(message, channel);
				}
			}


			return;
		}
		private async Task ProcessMessage(IUserMessage Message, IGuildChannel channel)
		{
			if (Message is RestUserMessage suMessage && Message is not null)
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

				if (msg.EditedTimestamp is not null && !msg.DiscordShames.Any(x => x.MessageId == msg.Id && x.Type == "Edit"))
				{
					msg.DiscordShames.Add(new DiscordShame() { Message = msg, MessageId = msg.Id, Type = "Edit", Date = msg.EditedTimestamp ?? DateTimeOffset.Now });
				}

				_db.SaveChanges();
			}
		}
		private async Task UpdateMessage(Cacheable<IMessage, ulong> oldMessage, IMessage newMessage, IMessageChannel channel)
		{
			if (newMessage is IUserMessage suMessage && newMessage is not null)
			{
				DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordShames).FirstOrDefault(s => s.Id == newMessage.Id);
				string oldMsg = oldMessage.Value?.CleanContent ?? String.Empty;
				if (msg is not null && string.IsNullOrEmpty(oldMsg)) oldMsg = msg.CleanContent;

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

				if (shameChannel is not null)
				{
					EmbedBuilder embed = new();
					embed.AddField("Original Message", oldMsg)
						.AddField("Edited Message", newMessage.CleanContent)
						.WithFooter(footer => footer.Text = $"In #{newMessage.Channel.Name} by {((IGuildUser)newMessage.Author).Nickname ?? newMessage.Author.Username}")
						.WithColor(Color.Orange);
					if (newMessage.Author is not null) embed.WithAuthor(newMessage.Author);
					await shameChannel.SendMessageAsync(embed: embed.Build());
				}

				_db.SaveChanges();
			}
		}
		private async Task Shame(IUserMessage msg, ICollection<DiscordShame> discordShames)
		{
			var Reactions = new List<ReactionDef>
			{
				//<:ditto:1075842464415494214>"
				new ReactionDef("I mean", @"(^|[.?!;,:-])\s*i\s+mean\b", @"<:belfaris:1122028010808287292>"),
				new ReactionDef("Game Pass", @"free\b.*game\s*pass|game\s*pass\b.*free","\uD83D\uDCB0")
			};

			foreach (ReactionDef reaction in Reactions)
			{
				if (!Regex.IsMatch(msg.Content.ToString(), reaction.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase)) { continue; }
				if (discordShames.Where(x => x.Type == reaction.Name).Any()) { continue; }
				discordShames.Add(new DiscordShame() { Type = reaction.Name, Date = msg.CreatedAt });
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