using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using Python.Runtime;
using UnitsNet;
using UnitsNet.Units;
using System.Text;

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
        /// <param name="args">Command line arguments</param>
        /// <returns></returns>
        public static Task Main(string[] args) => new Program().MainAsync(args);
        private static int logLevel;
        private bool pauseStatus;
        /// <summary>
        /// Main function which reads command line arguments and configures and starts the connection to discord, then listens for console commands.
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns></returns>
        public async Task MainAsync(string[] args)
        {
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            var logLevels = args.Where(arg => arg.Contains("loglevel")).Where(arg => int.TryParse(arg.Split('=')[1], out int logTry) && logTry < 6).Select(arg => int.Parse(arg.Split('=')[1]));
            logLevel = logLevels.Any() ? logLevels.First() : 3;
            await Log(LogSeverity.Verbose, "Log", $"Log level set to {(LogSeverity)logLevel}");
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = (LogSeverity)logLevel,
                MessageCacheSize = 1024,
                DefaultRetryMode = RetryMode.RetryRatelimit,
                GatewayIntents = GatewayIntents.MessageContent | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessageReactions | GatewayIntents.DirectMessageReactions,
                UseInteractionSnowflakeDate = false,
                AlwaysDownloadUsers = true
            });
            string? apiKey = Configuration["Discord:APIKey"];
            _client.LatencyUpdated += LatencyUpdated;
            _client.MessageReceived += ReceiveMessage;
            _client.MessageUpdated += UpdateMessage;
            _client.MessageDeleted += DeleteMessage;
            _client.SlashCommandExecuted += SlashCommandHandler;
            _client.ReactionAdded += ReactionAdded;
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
                        PythonEngine.Shutdown();
                        return;
                    case "birthday":
                        await BirthdayTestFire();
                        break;
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
                    case "grammar":
                        Console.WriteLine($"Enter UserID:");
                        await GrammarStat(ulong.Parse(Console.ReadLine() ?? "0"));
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
                    case "units":
                        foreach (var str in Quantity.Infos.SelectMany(qi => qi.UnitInfos.Select(ui => $"{qi.Name}: {ui.Value}")))
                        {
                            Console.WriteLine(Regex.Replace(str, @"\B([A-Z])", " $1"));
                        }
                        break;
                    case "currency":
                        var stringBuilder = new StringBuilder();

                        foreach (var currency in CultureInfo.GetCultures(CultureTypes.SpecificCultures).Where(x => x.LCID != 4096))
                        {
                            RegionInfo regionInfo;
                            try
                            {
                                regionInfo = new RegionInfo(currency.Name.Split("-")[1]);
                            }
                            catch (Exception)
                            {
                                continue;
                            }
                            stringBuilder.AppendLine($"{currency.Name}|{regionInfo.ISOCurrencySymbol}|{regionInfo.CurrencyEnglishName}|{currency.NumberFormat.CurrencySymbol}|{currency.NumberFormat.CurrencyDecimalSeparator}|{currency.NumberFormat.CurrencyGroupSeparator}|{currency.NumberFormat.CurrencyPositivePattern}|{currency.NumberFormat.CurrencyNegativePattern}|{currency.Parent.Name}|{currency.LCID}");
                        };


                        string docPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        using (StreamWriter outputFile = new(Path.Combine(docPath, "CultureInfo.txt")))
                        {
                            await outputFile.WriteAsync(stringBuilder.ToString());
                        }
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

            await _client.SetGameAsync($" Log: {db.DiscordLog.Count()}", type: ActivityType.Playing);
        }

        /// <summary>
        /// Run external library to parse text for measurements
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        private static async Task QuantulumParse(DiscordMessage msg)
        {
            var parsed = new List<QuantityParse>();

            await Task.Run(async () =>
            {
                try
                {
                    using (Py.GIL())
                    {

                        dynamic parser = Py.Import("quantulum3.parser");
                        PyObject load = Py.Import("quantulum3.load");
                        load.InvokeMethod("load_custom_units", new PyString("units.json"));
                        load.InvokeMethod("load_custom_entities", new PyString("entities.json"));

                        var content = Regex.Replace(msg.CleanContent, @"(\d+\')(\d+"")", "$1 $2");
                        dynamic quants = parser.parse(content);

                        foreach (dynamic quant in quants)
                        {
                            if ((string)quant.unit.entity.name == "dimensionless") { continue; }

                            if ((string)quant.unit.entity.name == "unknown")
                            {
                                var dimensions = (PyObject)quant.unit.dimensions;

                                if (dimensions.Length() > 1)
                                {
                                    for (int i = 0; i < dimensions.Length(); i++)
                                    {
                                        var dimension = dimensions[i];
                                        if (dimension.GetItem("base").ToString() != "dollar")
                                        {
                                            var dimunit = load.InvokeMethod("units", new PyTuple()).GetItem(dimension.GetItem("base"));
                                            if (dimunit.GetItem("entity").ToString() == "currency")
                                            {
                                                quant.unit = dimunit;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            msg.QuantityParses.Add(new()
                            {
                                Value = (float)quant.value,
                                Entity = quant.unit.entity.name,
                                StartPos = (int)(quant.span[0] ?? 0),
                                EndPos = (int)(quant.span[1] ?? 0),
                                Unit = ((string)quant.unit.name),
                                CurrencyCode = quant.unit.currency_code ?? ""
                            });

                        }
                    }
                }
                catch (Exception e)
                {
                    await Log(LogSeverity.Error, "Quantulum", e.Message);
                }

            });
        }
        /// <summary>
        /// Convert between currencies
        /// </summary>
        /// <param name="original">Quantity Parse with at least the original value and currency code included</param>
        /// <param name="toCountry">Currency code of the destination currency</param>
        /// <returns>A unit conversion object with details of the former currency and the new currency</returns>
        public static UnitConversion? ConvertCurrency(QuantityParse original, string? toCountry = null)
        {

            var _db = new AppDBContext();

            if (string.IsNullOrEmpty(original.CurrencyCode)) { return null; }

            if (original.CurrencyCode == "NAN")
            {
                original.CurrencyCode = toCountry == "CAD" ? "USD" : "CAD";
            }


            var fromCurrency = _db.Currencies.Find(original.CurrencyCode);
            var toCurrency = _db.Currencies.Find(toCountry);

            if (toCurrency == null || fromCurrency == null) { return null; }

            var fromCulture = CultureInfo.GetCultureInfo(fromCurrency.LCID);
            var toCulture = CultureInfo.GetCultureInfo(toCurrency.LCID);

            if (toCulture == null || fromCulture == null) { return null; }

            return new UnitConversion
            {
                StartPos = original.StartPos,
                EndPos = original.EndPos,
                OldQuantity = null,
                NewQuantity = null,
                OldString = $"{(fromCulture.NumberFormat.CurrencySymbol == "$" ? original.Value.ToString("C", fromCulture).Replace("$", $"{fromCurrency.CurrencyCode[..2]} $") : original.Value.ToString("C", fromCulture))}",
                NewString = $"{(toCulture.NumberFormat.CurrencySymbol == "$" ? (fromCurrency.ToCurrency(toCurrency, original.Value)).ToString("C", toCulture).Replace("$", $"{toCurrency.CurrencyCode[..2]} $") : (fromCurrency.ToCurrency(toCurrency, original.Value)).ToString("C", toCulture))}"
            };
        }

        /// <summary>
        /// Convert units parsed out of a user message.
        /// </summary>
        /// <param name="msg">Message with a list of quantity parses.</param>
        /// <param name="toCountry">Destination currency code to represent the unit system to convert to.</param>
        /// <returns></returns>
        private static async Task<List<UnitConversion>> ConvertUnits(DiscordMessage msg, string? toCountry = null)
        {
            var units = new List<UnitConversion>();
            var parsed = msg.QuantityParses;

            if (parsed == null || parsed.Count == 0) { return units; }

            foreach (var parse in parsed)
            {
                await Log(LogSeverity.Debug, "Units", $"{parse.Value} - {parse.Entity} - {parse.Unit}");

                if (parse.Unit == "degree generic")
                {
                    parse.Unit = toCountry == "CAD" ? "degree fahrenheit" : "degree celsius";
                }

                if (parse.Entity == "currency" || parse.Entity == "unknown")
                {
                    var unit = ConvertCurrency(parse, toCountry);
                    if (unit != null) { units.Add(unit); }
                    continue;
                }

                IQuantity? newQuant = null;
                IQuantity? oldQuant;
                string? newString = null;
                oldQuant = Quantity.From(parse.Value, parse.Entity.Replace(" ", "").ToLower(), parse.Unit.Replace(" ", "").ToLower());

                switch (parse.Entity.ToLower())
                {

                    case "currency":
                    case "unknown":

                    case "length":
                        if (toCountry == "USD")
                        {
                            newQuant = oldQuant.ToUnit(LengthUnit.Mile);
                            Console.WriteLine(newQuant.ToString());
                            if (newQuant.Value < 0.25) { newQuant = newQuant.ToUnit(LengthUnit.Foot); }
                            Console.WriteLine(newQuant.ToString());
                            if (newQuant.Value < 3 && newQuant.Unit == (Enum)LengthUnit.Foot) { newQuant = newQuant.ToUnit(LengthUnit.Inch); }
                            Console.WriteLine(newQuant.ToString());
                        }
                        if (toCountry == "CAD")
                        {
                            var last = units.LastOrDefault();
                            if (last is not null && last.NewQuantity is Length newLen && last.OldQuantity?.Unit is LengthUnit.Foot && last.EndPos + 2 >= parse.StartPos)
                            {
                                last.OldQuantity = Length.FromFeetInches(((double)last.OldQuantity.Value), parse.Value);
                                last.NewQuantity = newLen + Length.FromInches(parse.Value).ToUnit(LengthUnit.Centimeter);
                                last.OldString = ((Length)last.OldQuantity).FeetInches.ToString();
                                continue;
                            }
                            newQuant = oldQuant.ToUnit(LengthUnit.Kilometer);
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(LengthUnit.Meter); }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(LengthUnit.Centimeter); }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(LengthUnit.Millimeter); }
                        }
                        break;
                    case "area":
                        if (toCountry == "USD")
                        {
                            newQuant = oldQuant.ToUnit(AreaUnit.Acre);
                            if (newQuant.Value < 0.25) { newQuant = newQuant.ToUnit(AreaUnit.SquareFoot); }
                            if (newQuant.Value < 1 && newQuant.Unit == (Enum)AreaUnit.SquareFoot) { newQuant = newQuant.ToUnit(AreaUnit.SquareInch); }
                        }
                        if (toCountry == "CAD")
                        {
                            newQuant = oldQuant.ToUnit(AreaUnit.Hectare);
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(AreaUnit.SquareMeter); }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(AreaUnit.SquareCentimeter); }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(AreaUnit.SquareMillimeter); }
                        }
                        break;
                    case "volume":
                        if (toCountry == "USD")
                        {
                            string unit = " gallon";
                            newQuant = oldQuant.ToUnit(VolumeUnit.UsGallon);
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(VolumeUnit.UsCustomaryCup); unit = "cup"; }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(VolumeUnit.UsTablespoon); unit = "tbsp"; }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(VolumeUnit.UsTeaspoon); unit = "tsp"; }
                            newString = $"{newQuant.Value:0.##} {unit}";
                        }
                        if (toCountry == "CAD")
                        {
                            newQuant = oldQuant.ToUnit(VolumeUnit.CubicMeter);
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(VolumeUnit.Liter); }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(VolumeUnit.Milliliter); }
                        }
                        break;
                    case "temperature":
                        if (toCountry == "USD")
                        {
                            newQuant = oldQuant.ToUnit(TemperatureUnit.DegreeFahrenheit);
                        }
                        if (toCountry == "CAD")
                        {
                            newQuant = oldQuant.ToUnit(TemperatureUnit.DegreeCelsius);
                        }
                        break;
                    case "speed":
                        if (toCountry == "USD")
                        {
                            newQuant = oldQuant.ToUnit(SpeedUnit.MilePerHour);
                        }
                        if (toCountry == "CAD")
                        {
                            newQuant = oldQuant.ToUnit(SpeedUnit.KilometerPerHour);
                            Console.WriteLine(newQuant.Dimensions.Time);
                        }
                        break;
                    case "mass":
                        if (toCountry == "USD")
                        {
                            newQuant = oldQuant.ToUnit(MassUnit.ShortTon);
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(MassUnit.Pound); }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(MassUnit.Ounce); }
                        }
                        if (toCountry == "CAD")
                        {
                            newQuant = oldQuant.ToUnit(MassUnit.Tonne);
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(MassUnit.Kilogram); }
                            if (newQuant.Value < 1) { newQuant = newQuant.ToUnit(MassUnit.Gram); }
                        }
                        break;
                    case "pressure":
                        if (toCountry == "USD")
                        {
                            newQuant = oldQuant.ToUnit(PressureUnit.PoundForcePerSquareInch);
                        }
                        if (toCountry == "CAD")
                        {
                            newQuant = oldQuant.ToUnit(PressureUnit.Kilopascal);
                        }
                        break;
                }
                if (newQuant == null || oldQuant == null) { continue; }

                var conversion = new UnitConversion()
                {
                    StartPos = parse.StartPos,
                    EndPos = parse.EndPos,
                    NewString = newString,
                    OldString = null,
                    NewQuantity = newQuant,
                    OldQuantity = oldQuant
                };

                units.Add(conversion);

            }

            return units;

        }

        /// <summary>
        /// Just a shortcut to title case in the currently set culture
        /// </summary>
        /// <param name="s">String to convert to title case</param>
        /// <returns>Input string in title case.</returns>
        private static string TitleCase(object s)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToString() ?? "");
        }
    }
}