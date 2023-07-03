using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace DiscordBot
{
	/// <summary>
	/// Main DiscordBot class
	/// </summary>
	public partial class Program
	{
		public static IConfiguration Configuration => new ConfigurationBuilder().AddUserSecrets<Program>().Build();
		private DiscordSocketClient _client = default!;
		/// <summary>
		/// Initial subroutine fired, immediately passed on to an asynchronous version.
		/// </summary>
		/// <param name="args">Command line arguements</param>
		/// <returns></returns>
		public static Task Main(string[] args) => new Program().MainAsync(args);
		private int logLevel;
		private bool pauseStatus = false;
		/// <summary>
		/// Main function which reads command line parameters and configures and starts the connection to discord, then listens for console commands.
		/// </summary>
		/// <param name="args">Command line arguments</param>
		/// <returns></returns>
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
			_client.AutocompleteExecuted += AutoCompleteExecuted;
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
					case "delete":
						Console.WriteLine($"Enter start date {CultureInfo.CurrentCulture.DateTimeFormat.UniversalSortableDateTimePattern}: ");
						if (DateTime.TryParse(Console.ReadLine(), CultureInfo.CurrentCulture.DateTimeFormat, out DateTime since))
						{
							await DeletedCleanup(since);
							break;
						}
                        await Log(LogSeverity.Error, "User Input", "Malformed date");
                        break;
					default:
						await Log(LogSeverity.Error, "User Input", "Command Unknown");
						break;
				}
			}
		}

        /// <summary>
        /// Search a message for certain text and respond with a reaction if found.
        /// </summary>
        /// <param name="msg">Message received to evaluate</param>
        /// <param name="discordLogs">Collection of DiscordLogs to save the result back to.</param>
        /// <returns></returns>
        private async Task React(IUserMessage msg, ICollection<DiscordLog> discordLogs)
		{
			var Reactions = new List<ReactionDef>
			{
				new ReactionDef("I mean", @"(^|[.?!;,:-])\s*i\s+mean\b", @"<a:ditto:1075842464415494214>"),
				new ReactionDef("Game Pass", @"free\b.*game\s*pass|game\s*pass\b.*free","\uD83D\uDCB0")
			};
			foreach (ReactionDef reaction in Reactions)
			{
				if (!Regex.IsMatch(msg.Content.ToString(), reaction.Regex, RegexOptions.Multiline | RegexOptions.IgnoreCase)) { continue; }
				if (!discordLogs.Where(x => x.Type == reaction.Name).Any())
				{
					discordLogs.Add(new DiscordLog() { Type = reaction.Name, Date = msg.CreatedAt });
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
					if (msg.Reactions.Any(x => ((Emote)x.Key)?.Id == emote.Id && x.Value.IsMe))
					{
						continue;
					}
					await msg.AddReactionAsync(emote);
					await Log(LogSeverity.Debug, "Bot", $"{reaction.Name} {msg.Author.Username}:{msg.Content}");
				}
				else if (Emoji.TryParse(reaction.Emote, out var emoji))
				{
					if (msg.Reactions.Any(x => ((Emoji)x.Key)?.Name == emoji.Name && x.Value.IsMe))
					{
						continue;
					}
					await msg.AddReactionAsync(emoji);
					await Log(LogSeverity.Debug, "Bot", $"{reaction.Name} {msg.Author.Username}:{msg.Content}");
				}
			}
		}
		/// <summary>
		/// Get the nickname, username, or deleted notation for a user that may not be in guild anymore.
		/// </summary>
		/// <param name="user">IGuildUser to pass on.</param>
		/// <returns></returns>
		private string? GetUserName(IGuildUser? user)
		{
			return user == null ? null : GetUserName(user.Id, user.GuildId);
		}
        /// <summary>
        /// Get the nickname, username, or deleted notation for a user that may not be in guild anymore.
        /// </summary>
        /// <param name="id">User's ID</param>
        /// <param name="guildid">Guild's ID (nicknames only exist in guilds)</param>
        /// <returns></returns>
        private string? GetUserName(ulong id, ulong guildid)
		{
			var guild = _client.GetGuild(guildid);
			var user = guild.Users.FirstOrDefault(x => x.Id == id);
			if (user != null) { return user.DisplayName; }

			var _db = new AppDBContext();
			var member = _db.GuildUsers.Find(id, guildid);

			return member?.Displayname ?? $"Deleted #{id}";

		}
		/// <summary>
		/// Set the Discord status to Playing Log: #
		/// </summary>
		/// <returns></returns>
		private async Task SetStatus()
		{
			if (pauseStatus) { return; }
			var db = new AppDBContext();

			await _client.SetGameAsync($" Log: {db.DiscordLog.Count()}",type: ActivityType.Playing);
		}
	}
}