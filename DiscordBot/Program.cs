﻿using Discord;
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
						Console.WriteLine("Enter Guild ID:");
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
						Console.WriteLine($"Enter Custom Status:");
						await _client.SetGameAsync($" {Console.ReadLine()}", type: ActivityType.Playing);
						break;
					default:
						await Log(LogSeverity.Error, "User Input", "Command Unknown");
						break;
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