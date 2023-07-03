using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace DiscordBot
{
	/// <summary>
	/// Main DiscordBot class
	/// </summary>
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
			_client.LatencyUpdated += LatencyUpdated;
			_client.MessageReceived += ReceiveMessage;
			_client.MessageUpdated += UpdateMessage;
			_client.MessageDeleted += DeleteMessage;
			_client.SlashCommandExecuted += SlashCommandHandler;
			_client.GuildMembersDownloaded += MembersDownloaded;
			_client.GuildMemberUpdated += MemberDownloaded;
			_client.UserJoined += MemberJoined;
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
						Console.WriteLine("Enter Guild ID:");
						if (!ulong.TryParse(Console.ReadLine(), out ulong guildid))
						{
							await Log(LogSeverity.Error, "User Input", "Invalid GuildId format");
							break;
						}
						Console.WriteLine("Enter # of Messages per Channel (0 is no limit): ");
						int? cnt;
						if (!int.TryParse(Console.ReadLine(), out int input))
						{
							await Log(LogSeverity.Info, "User Input", "Generating messages since last heard");
							cnt = null;
						}
						else
						{
							cnt = input;
						}
						await ProcessGuild(guildid, cnt);
						break;
					case "status":
						Console.WriteLine($"Enter Custom Status:");
						await _client.SetGameAsync($" {Console.ReadLine()}", type: ActivityType.Playing);
						break;
					default:
						await Log(LogSeverity.Error, "User Input", "Command Unknown");
						break;
				}
			}
		}
		private async Task React(IUserMessage msg, ICollection<DiscordShame> discordShames)
		{
			var Reactions = new List<ReactionDef>
			{
				new ReactionDef("I mean", @"(^|[.?!;,:-])\s*i\s+mean\b", @"<a:ditto:1075842464415494214>"),
				new ReactionDef("Game Pass", @"free\b.*game\s*pass|game\s*pass\b.*free","\uD83D\uDCB0")
			};
			foreach (ReactionDef reaction in Reactions)
			{
				if (!Regex.IsMatch(msg.Content.ToString(), reaction.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase)) { continue; }
				if (!discordShames.Where(x => x.Type == reaction.Name).Any())
				{
					discordShames.Add(new DiscordShame() { Type = reaction.Name, Date = msg.CreatedAt });
				}

				await SetStatus();

				var channel = (IGuildChannel)msg.Channel;
				IGuild guild = channel.Guild;
				ChannelPermissions channelPerms = (await guild.GetCurrentUserAsync()).GetPermissions(channel);

				if (!channelPerms.ViewChannel || !channelPerms.AddReactions)
				{
					await Log(LogSeverity.Verbose, "Bot", $"Can't react to {msg.Author.Username}:{msg.Content} in #{channel.Name}");
					continue;
				}

				if (Emote.TryParse(reaction.Emote, out var emote))
				{
					if (msg.Reactions.Any(x => ((Emote)x.Key).Id == emote.Id && x.Value.IsMe))
					{
						continue;
					}
					await msg.AddReactionAsync(emote);
					await Log(LogSeverity.Debug, "Bot", $"{reaction.Name} {msg.Author.Username}:{msg.Content}");
				}
				else if (Emoji.TryParse(reaction.Emote, out var emoji))
				{
					if (msg.Reactions.Any(x => ((Emoji)x.Key).Name == emote.Name && x.Value.IsMe))
					{
						continue;
					}
					await msg.AddReactionAsync(emoji);
					await Log(LogSeverity.Debug, "Bot", $"{reaction.Name} {msg.Author.Username}:{msg.Content}");
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