using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Discord.Net;
using Newtonsoft.Json;

public class Program
{
    public IConfiguration configuration => new ConfigurationBuilder().AddUserSecrets<Program>().Build();
    private DiscordSocketClient _client;
    public static Task Main(string[] args) => new Program().MainAsync(args);

    public async Task MainAsync(string[] args)
    {
        var logLevel = 3;
        foreach (var arg in args)
        {
            if (arg.Contains("loglevel"))
            {
                if (int.TryParse(arg.Split("=")[1], out var logTry) && logTry < 6)
                {
                    logLevel = logTry;
                }
            }
        }
        await Log(new LogMessage(LogSeverity.Info, "Program", $"Log level set to {(LogSeverity)logLevel}"));

        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            LogLevel = (LogSeverity)logLevel,
            MessageCacheSize = 1024,
            DefaultRetryMode = RetryMode.RetryRatelimit,
            GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.GuildMessages | GatewayIntents.GuildMessageReactions | GatewayIntents.Guilds
        });

        string? apiKey = configuration["Discord:APIKey"];
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
                    Console.WriteLine("Unknown command.");
                    break;
            }
        }
    }

    public Task Ready()
    {
        _client.SlashCommandExecuted += SlashCommandHandler;
        _client.MessageReceived += ReceiveMessage;
        return Task.CompletedTask;
    }
    private async Task BuildCommands()
    {
        var leaderboardCommand = new SlashCommandBuilder();
        leaderboardCommand.WithName("leaderboard");
        leaderboardCommand.WithDescription("Display a leaderboard of messages tracked by Ditto.NET");

        leaderboardCommand.AddOption(new SlashCommandOptionBuilder()
            .WithName("stat")
            .WithDescription("What stat would you like to report on?")
            .WithRequired(true)
            .AddChoice("I mean", 1)
            .AddChoice("Xbox", 2)
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
            await Log(new LogMessage(LogSeverity.Error, "Command Builder", $"Error occured while building commands: {json}"));
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
        string regexPattern = @"(^|[.?!;,:-])\s*i\s+mean\b";
        if (Regex.IsMatch(message.Content.ToString(), regexPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
        {
            //<:ditto:1075842464415494214>"
            if (Emote.TryParse(@"<:belfaris:1122028010808287292>", out var emote))
            {
                await message.AddReactionAsync(emote);
                await Log(new LogMessage(LogSeverity.Verbose, "Bot", $"Ditto'd {message.Author.Username}:{message.Content}"));
            }

        }

    }
    private Task Log(LogMessage msg)
    {
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