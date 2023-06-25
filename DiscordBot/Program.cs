using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Discord.Net;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
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
            logLevel = 3;
            foreach (var arg in args)
            {
                if (arg.Contains("loglevel"))
                {
                    if (int.TryParse(arg.Split('=')[1], out var logTry) && logTry < 6)
                    {
                        logLevel = logTry;
                    }
                }
            }
            await Log(LogSeverity.Info, "Log", $"Log level set to {(LogSeverity)logLevel}");
            _db = new AppDBContext();
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = (LogSeverity)logLevel,
                MessageCacheSize = 1024,
                DefaultRetryMode = RetryMode.RetryRatelimit,
                GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.Guilds
            });
            string? apiKey = Configuration["Discord:APIKey"];
            _client.Log += Log;
            _client.Ready += Ready;
            await _client.LoginAsync(TokenType.Bot, apiKey);
            await _client.StartAsync();
            while (true)
            {
                switch (Console.ReadLine())
                {
                    case "exit" or "quit":
                        await _client.StopAsync();
                        await _client.LogoutAsync();
                        return;
                    case "buildcommands":
                        await BuildCommands();
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
            return Task.CompletedTask;
        }
        private async Task BuildCommands()
        {
            if (_client == null)
            {
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
        private async Task SlashCommandHandler(SocketSlashCommand command)
        {
            switch (command.Data.Name)
            {
                case "leaderboard":
                    await command.RespondAsync("Uhh... Todo...", ephemeral: true);
                    break;
            }
        }
        private async Task ReceiveMessage(SocketMessage message)
        {
            if (message is SocketUserMessage)
            {
                DiscordMessage msg = (SocketUserMessage)message;
                msg.GuildId = ((SocketGuildChannel)((SocketUserMessage)message).Channel).Guild.Id;
                msg.DiscordShames = new List<DiscordShame>();
                await Shame((SocketUserMessage)message, msg.DiscordShames);
                _db.UserMessages.Add(msg);
                _db.SaveChanges();
            }
        }
        private async Task UpdateMessage(Cacheable<IMessage, ulong> oldMessage, SocketMessage newMessage, ISocketMessageChannel channel)
        {
            if (newMessage is SocketUserMessage suMessage && newMessage is not null)
            {
                DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordShames).FirstOrDefault(s => s.Id == newMessage.Id);
                if (msg == null)
                {
                    msg = suMessage;
                    msg.GuildId = ((SocketGuildChannel)suMessage.Channel).Guild.Id;
                    msg.DiscordShames = new List<DiscordShame>();
                    _db.UserMessages.Add(msg);
                }
                else
                {
                    _db.Entry(msg).CurrentValues.SetValues((DiscordMessage)suMessage);
                }
                await Shame(suMessage, msg.DiscordShames);
                _db.SaveChanges();
            }
        }
        private async Task Shame(SocketUserMessage msg, ICollection<DiscordShame> discordShames)
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
                discordShames.Add(new DiscordShame() { Type = reaction.Name });
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
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;
            }
            Console.WriteLine(msg.ToString());
            Console.ResetColor();
            return Task.CompletedTask;
        }
    }
}