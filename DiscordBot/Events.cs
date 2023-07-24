using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using GrammarCheck;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using UnitsNet;

namespace DiscordBot
{
    /// <summary>
    /// Main program executable
    /// </summary>
    public partial class Program
    {
        /// <summary>
        /// Task fired when all guilds have connected and the app is ready to work.
        /// </summary>
        /// <returns></returns>
        public async Task Ready()
        {
            if (_client == null)
            {
                return;
            }
            await BuildCommands();
            await SetStatus();
            return;
        }
        /// <summary>
        /// Task fired when Members are downloaded from a guild at connection.
        /// </summary>
        /// <param name="guild">The SocketGuild passed from the SocketConnection</param>
        /// <returns></returns>
        private async Task MembersDownloaded(SocketGuild guild)
        {
            List<SocketGuildUser> members;

            if (guild == null) { return; }

            members = guild.Users.ToList();

            var _db = new AppDBContext();
            var discordMembers = _db.GuildUsers.Where(x => x.GuildId == guild.Id).ToList();

            foreach (var member in members)
            {
                var temp = discordMembers.Find(x => x.Id == member.Id);
                if (temp != null)
                {
                    temp.Update(member);
                }
                else
                {
                    _db.GuildUsers.Add(member);
                }
            }
            await _db.SaveChangesAsync();
#if !DEBUG
			_ = ProcessGuild(guild.Id);
#endif
            return;
        }

        /// <summary>
        /// Fired when a new or single user has been downloaded, usually on a change to the member's properties.
        /// </summary>
        /// <param name="cacheable">Cached old version of the user, or their ID</param>
        /// <param name="user">New version of the user.</param>
        /// <returns></returns>
        private async Task MemberDownloaded(Cacheable<SocketGuildUser, ulong> cacheable, SocketGuildUser user)
        {
            await MemberJoined(user);
            return;
        }
        /// <summary>
        /// Fired when a new user joins a guild.
        /// </summary>
        /// <param name="user">SocketGuildUser of the user who joined.</param>
        /// <returns></returns>
        private async Task MemberJoined(SocketGuildUser user)
        {
            var _db = new AppDBContext();
            var member = await _db.GuildUsers.FindAsync(user.Id, user.Guild.Id);
            if (member == null)
            {
                _db.GuildUsers.Add(user);
            }
            else
            {
                member.Update(user);
            }
            _db.SaveChanges();
            return;
        }
        /// <summary>
        /// Heartbeat function
        /// </summary>
        /// <param name="a">Old Latency</param>
        /// <param name="b">New Latency</param>
        /// <returns></returns>
        private async Task LatencyUpdated(int a, int b)
        {
            var _db = new AppDBContext();
            var birthdays = _db.BirthdayDefs.ToList().Where(x => x.Date.Date == TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.Now, x.TimeZone).Date);

            foreach (var birthday in birthdays)
            {
                var guild = _client.GetGuild(birthday.GuildId);
                if (guild == null)
                {
                    _db.Remove(birthday);
                    return;
                }
                var user = guild.GetUser(birthday.UserId);
                if (user == null)
                {
                    _db.Remove(birthday);
                    return;
                }

                var channel = guild.SystemChannel ?? guild.DefaultChannel;
                var channelPerms = guild.CurrentUser.GetPermissions(channel);

                if (!channelPerms.SendMessages)
                {
                    if (channel == guild.SystemChannel && guild.SystemChannel != guild.DefaultChannel)
                    {
                        channel = guild.DefaultChannel;
                        channelPerms = guild.CurrentUser.GetPermissions(channel);

                        if (!channelPerms.SendMessages)
                        {
                            await Log(LogSeverity.Error, "Birthday", $"Can't wish {user.DisplayName} a happy birthday in #{guild.Name}");
                            continue;
                        }
                    }
                }
                try
                {
                    await channel.SendMessageAsync($"Happy Birthday {user.Mention}!");
                }
                catch (Exception e)
                {

                    await Log(LogSeverity.Error, "Birthday", e.Message);
                }

                birthday.Date = birthday.Date.AddYears(1);
            }

            await Currency.DownloadCurrency(_db);

            _db.SaveChanges();

        }
        /// <summary>
        /// Event fired when a reaction is added to a message. Used to request unit conversion.
        /// </summary>
        /// <param name="cacheable1">Cached message or its ID</param>
        /// <param name="cacheable2">Cached channel or its ID</param>
        /// <param name="reaction">Reaction added</param>
        /// <returns></returns>
        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> cacheable1, Cacheable<IMessageChannel, ulong> cacheable2, SocketReaction reaction)
        {
            string country;

            switch (reaction.Emote.Name)
            {
                case "🇺🇸":
                    country = "USD";
                    break;
                case "🇨🇦":
                    country = "CAD";
                    break;
                default:
                    return;
            }

            if (reaction.UserId == _client.CurrentUser.Id) { return; }

            var message = reaction.Message.IsSpecified ? reaction.Message.Value : await cacheable1.GetOrDownloadAsync();
            if (!(message.Reactions.Any(x=>x.Key.Name == reaction.Emote.Name && x.Value.IsMe))) { return; }

            var db = new AppDBContext();
            var msg = db.UserMessages.Include(x=>x.QuantityParses).FirstOrDefault(x=>x.Id == cacheable1.Id);
            if (msg == null || !(msg.QuantityParses.Any())) { return; }

            var igChannel = await cacheable2.GetOrDownloadAsync();

            var channelPerms = ((SocketGuild)((IGuildChannel)igChannel).Guild).CurrentUser.GetPermissions(((IGuildChannel)igChannel));

            if (!channelPerms.SendMessages) { return; }

            var units = await ConvertUnits(msg, country);
            if (igChannel is ITextChannel itchannel && units.Any())
            {
                await itchannel.SendMessageAsync(text: string.Join("\n", units.Select(x => $"> {x.OldString ?? x.OldQuantity?.ToString() ?? "NULL"} = {x.NewString ?? x.NewQuantity?.ToString() ?? "NULL"}")), messageReference: new MessageReference(message.Id), allowedMentions: AllowedMentions.None);
                await message.RemoveReactionAsync(reaction.Emote, _client.CurrentUser);
            }

        }

        /// <summary>
        /// Fired when a message is received
        /// </summary>
        /// <param name="message">IMessage object received.</param>
        /// <returns></returns>
        private async Task ReceiveMessage(IMessage message)
        {
            await Task.Run(async () =>
            {

                if (message is IUserMessage iuMessage && message.Channel is IGuildChannel igChannel)
                {

                    if ((message.Flags & MessageFlags.Ephemeral) == MessageFlags.Ephemeral || message.Author.IsBot || _client.CurrentUser.Id == message.Author.Id) { return; }
                    var _db = new AppDBContext();
                    DiscordMessage msg = (DiscordMessage)(SocketUserMessage)message;
                    msg.GuildId = igChannel.Guild.Id;
                    msg.DiscordLogs = new List<DiscordLog>();
                    await React(iuMessage, msg.DiscordLogs);
                    _db.UserMessages.Add(msg);

                    try
                    {
                        await QuantulumParse(msg);

                        if (msg.QuantityParses.Any())
                        {
                            var emotes = new List<Emoji>();
                            if (msg.QuantityParses.Any(x => x.CurrencyCode != "CAD"))
                            {
                                emotes.Add(new Emoji("🇨🇦"));
                            }
                            if (msg.QuantityParses.Any(x => x.CurrencyCode != "USD"))
                            {
                                emotes.Add(new Emoji("🇺🇸"));
                            }
                            var channelPerms = ((SocketGuild)igChannel.Guild).CurrentUser.GetPermissions(igChannel);

                            if (channelPerms.AddReactions)
                            {
                                foreach (var emote in emotes)
                                {
                                    await iuMessage.AddReactionAsync(emote);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {

                        await Log(LogSeverity.Warning, "Python", e.ToString());
                    }

                    _db.SaveChanges();

                    if (!_db.GuildUsers.Find(msg.AuthorId, msg.GuildId)?.TrackGrammar ?? true) { return; }
                    var corrections = await Check.ProcessText(msg.CleanContent);

                    if (corrections != null && corrections.matches.Any())
                    {
                        foreach (var match in corrections.matches)
                        {
                            var embedBuilder = new EmbedBuilder();
                            embedBuilder
                                .WithTitle(match.message)
                                .WithDescription($"{match.context.text.Insert(match.context.offset + match.context.length, "__").Insert(match.context.offset, "__")}")
                                .WithFooter(new EmbedFooterBuilder() { Text = $"Posted in #{igChannel.Name} on {igChannel.Guild.Name}", IconUrl = igChannel.Guild.IconUrl })
                                .WithCurrentTimestamp();
                            var components = new ComponentBuilder();

                            if (!string.IsNullOrEmpty(message.GetJumpUrl()))
                            {
                                components.WithButton("Go to message", url: message.GetJumpUrl(), style: ButtonStyle.Link);
                            }

                            if (match.rule?.urls?.Any() ?? false)
                            {
                                components.WithButton("Learn more", url: match.rule?.urls?.FirstOrDefault()?.value ?? "", style: ButtonStyle.Link);
                            }

                            try
                            {
                                await message.Author.SendMessageAsync(components: components.Build(), embed: embedBuilder.Build());
                            }
                            catch (HttpException e)
                            {
                                if (e.DiscordCode == (DiscordErrorCode)50007)
                                {
                                    await Log(LogSeverity.Info, "Grammar", "Tried to DM but got blocked");
                                }
                                else
                                {
                                    throw;
                                }
                            }

                        }
                    }
                }
            });
        }
        /// <summary>
        /// Fired when an existing message is updated
        /// </summary>
        /// <param name="oldMessage">IMessage of the old message if cached, otherwise its ID.</param>
        /// <param name="newMessage">IMessage representing the new message.</param>
        /// <param name="channel">The IMessageChannel the message was received on.</param>
        /// <returns></returns>
        private async Task UpdateMessage(Cacheable<IMessage, ulong> oldMessage, IMessage newMessage, IMessageChannel channel)
        {
            if (newMessage is IUserMessage suMessage && newMessage != null)
            {
                if ((newMessage.Flags & MessageFlags.Ephemeral) == MessageFlags.Ephemeral || newMessage.Author.IsBot || _client.CurrentUser.Id == newMessage.Author.Id) { return; }
                var _db = new AppDBContext();
                DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordLogs).FirstOrDefault(s => s.Id == newMessage.Id);
                string oldMsg = oldMessage.Value?.CleanContent ?? String.Empty;
                if (msg != null && string.IsNullOrEmpty(oldMsg)) oldMsg = msg.CleanContent;

                if (oldMsg == string.Empty) { oldMsg = "Unknown"; }

                if (oldMsg == newMessage.CleanContent)
                {
                    return;
                }

                var guild = ((IGuildChannel)channel).Guild;

                if (msg == null)
                {
                    msg = (DiscordMessage)suMessage;
                    msg.GuildId = ((IGuildChannel)suMessage.Channel).Guild.Id;
                    msg.DiscordLogs = new List<DiscordLog>();
                    _db.UserMessages.Add(msg);
                }
                else
                {
                    _db.Entry(msg).CurrentValues.SetValues((DiscordMessage)(SocketUserMessage)suMessage);
                    msg.GuildId = ((IGuildChannel)suMessage.Channel).Guild.Id;
                }
                await React(suMessage, msg.DiscordLogs);

                if (!msg.DiscordLogs.Any(x => x.MessageId == newMessage.Id && x.Type == "Edit"))
                {
                    msg.DiscordLogs.Add(new DiscordLog() { Message = msg, MessageId = msg.Id, Type = "Edit", Date = msg.EditedTimestamp ?? DateTimeOffset.Now });
                }

                _db.SaveChanges();
                await SetStatus();

                var reactChannel = (await ((IGuildChannel)(newMessage).Channel).Guild.GetTextChannelsAsync()).FirstOrDefault(x => x.Name.Contains("shame") || x.Name.Contains("the-log"));

                var author = (IGuildUser)newMessage.Author;
                if (reactChannel != null && oldMsg != newMessage.CleanContent)
                {
                    ChannelPermissions channelPerms = (await guild.GetCurrentUserAsync()).GetPermissions(reactChannel);

                    if (!channelPerms.ViewChannel || !channelPerms.SendMessages)
                    {
                        await Log(LogSeverity.Verbose, "Commands", $"Can't send log messages in #{reactChannel.Name}");
                        return;
                    }

                    EmbedBuilder embed = new();
                    embed.AddField("Original Message", oldMsg)
                        .AddField("Edited Message", newMessage.CleanContent)
                        .WithFooter(footer => footer.Text = $"In #{newMessage.Channel.Name}")
                        .WithColor(Color.Orange);
                    if (author != null) embed.Author = new EmbedAuthorBuilder().WithName(GetUserName(author)).WithIconUrl(author.GetDisplayAvatarUrl() ?? author.GetDefaultAvatarUrl());
                    await reactChannel.SendMessageAsync(embed: embed.Build());
                }
            }
        }
        /// <summary>
        /// Fired when a message is deleted.
        /// </summary>
        /// <param name="message">IMessage representing the cached message, or it's Id if not cached.</param>
        /// <param name="channel">IMessageChannel representing where the message came from, or it's ID if the channel has been deleted too.</param>
        /// <returns></returns>
        private async Task DeleteMessage(Cacheable<IMessage, ulong> message, Cacheable<IMessageChannel, ulong> channel)
        {
            var _db = new AppDBContext();
            DiscordMessage? msg = _db.UserMessages.Include(x => x.DiscordLogs).FirstOrDefault(s => s.Id == message.Id);
            IUserMessage? imsg = (IUserMessage)message.Value;
            IGuildChannel? ichannel = (IGuildChannel)channel.Value;

            if (ichannel == null && imsg != null)
            {
                ichannel = (IGuildChannel)imsg.Channel;
            }

            ichannel ??= (IGuildChannel)_client.GetChannel(channel.Id);

            var recentLogs = await ichannel.Guild.GetAuditLogsAsync(1);

            if (recentLogs.Any(x => x.User.Id != msg?.AuthorId && x.Data is MessageDeleteAuditLogData data && data.Target.Id == msg?.AuthorId && data.ChannelId == channel.Id))
            {
                return;
            }

            if (msg != null)
            {
                _db.Remove(msg);
                _db.SaveChanges();
            }

            IGuild guild;
            if (ichannel?.Guild != null)
            {
                guild = ichannel.Guild;
            }
            else if (msg?.GuildId != null)
            {
                guild = _client.GetGuild(msg.GuildId);
            }
            else
            {
                return;
            }

            ITextChannel? reactChannel = (await guild.GetTextChannelsAsync()).FirstOrDefault(x => x.Name.Contains("shame") || x.Name.Contains("the-log"));

            if (reactChannel == null) { return; }

            ChannelPermissions channelPerms = (await guild.GetCurrentUserAsync()).GetPermissions(reactChannel);

            if (!channelPerms.ViewChannel || !channelPerms.SendMessages)
            {
                await Log(LogSeverity.Verbose, "Commands", $"Can't send log messages in #{reactChannel.Name}");
                return;
            }

            EmbedBuilder embed = new();

            if (msg == null)
            {
                embed.AddField("Deleted Message", "Unknown Original Content")
                    .WithColor(Color.Red)
                    .WithFooter(footer => footer.Text = $"In #{ichannel?.Name ?? "Unknown"}");
            }
            else
            {
                var author = (IGuildUser?)imsg?.Author ?? _client.Guilds.FirstOrDefault(x => x.Id == msg.GuildId)?.GetUser(msg.AuthorId ?? 0);
                if (author != null) embed.Author = new EmbedAuthorBuilder().WithName(GetUserName(author)).WithIconUrl(author.GetDisplayAvatarUrl() ?? author.GetDefaultAvatarUrl());
                embed.AddField("Deleted Message", $"{(String.IsNullOrEmpty(msg.CleanContent) ? "<No normal text>" : msg.CleanContent)}")
                    .WithColor(Color.Red)
                    .WithFooter(footer => footer.Text = $"In #{ichannel?.Name ?? "Unknown"}");
            }
            await reactChannel.SendMessageAsync(embed: embed.Build());
            await SetStatus();
        }
        /// <summary>
        /// Function to return auto complete options
        /// </summary>
        /// <param name="interaction">AutoComplete Interaction object passed by the client</param>
        /// <returns></returns>
        private async Task AutoCompleteExecuted(SocketAutocompleteInteraction interaction)
        {
            var focusedOption = interaction.Data.Options.FirstOrDefault(x => x.Focused);
            if (focusedOption == null) { return; }
            var foValue = focusedOption.Value.ToString()?.ToLower() ?? "";
            List<AutocompleteResult> results = new();
            switch (interaction.Data.CommandName)
            {
                case "birthday":
                    if (focusedOption.Name == "timezone")
                    {
                        foreach (TimeZoneInfo z in TimeZoneInfo.GetSystemTimeZones().Where(x => x.DisplayName.ToLower().Contains(foValue) || x.Id.ToLower().Contains(foValue)).OrderBy(q => q.BaseUtcOffset))
                        {
                            results.Add(new AutocompleteResult($"{z.DisplayName} {(z.SupportsDaylightSavingTime ? "(DST)" : "(No DST)")}", z.Id));
                            await Log(LogSeverity.Debug, "Auto", $"{z.DisplayName}");
                        }
                    }
                    break;

                case "convert":
                    switch (focusedOption.Name)
                    {
                        case "unit-type":
                            results = Quantity.Infos.Where(q => q.Name.ToLower().Contains(foValue)).Select(q => new AutocompleteResult(Regex.Replace(q.Name, @"\B([A-Z])", " $1"), q.Name)).ToList();
                            results.Add(new AutocompleteResult("Currency", "Currency"));
                            results = results.OrderBy(q => q.Name).ToList();
                            break;
                        case "from-unit":
                        case "to-unit":
                            if ((interaction.Data.Options.FirstOrDefault(x => x.Name == "unit-type")?.Value.ToString() ?? "") == "Currency")
                            {
                                var db = new AppDBContext();
                                results = db.Currencies.Where(q => q.Name.ToLower().Contains(foValue) || q.CurrencyCode.ToLower().Contains(foValue)).ToList().Select(q => new AutocompleteResult(q.Name, q.CurrencyCode)).OrderBy(q => q.Name).ToList();
                                break;
                            }
                            var quant = Quantity.Infos.FirstOrDefault(q => q.Name == (interaction.Data.Options.FirstOrDefault(x => x.Name == "unit-type")?.Value.ToString() ?? ""));
                            if (quant == null) { break; }
                            results = quant.UnitInfos.Where(q => q.Name.ToLower().Contains(focusedOption.Value.ToString()?.ToLower() ?? "")).Select(q => new AutocompleteResult(Regex.Replace(q.Name, @"\B([A-Z])", " $1"), q.Name)).ToList();
                            break;
                    }
                    break;
            }
            await interaction.RespondAsync(results.Take(25));
        }
        /// <summary>
        /// Fired when Discord sends on a slash command.
        /// </summary>
        /// <param name="command">Command received.</param>
        /// <returns></returns>
        private async Task SlashCommandHandler(ISlashCommandInteraction command)
        {
            switch (command.Data.Name)
            {
                case "leaderboard":
                    await LeaderboardCommand(command);
                    return;

                case "birthday":
                    await BirthdayCommand(command);
                    return;

                case "grammar":
                    await GrammarToggle(command);
                    return;
                case "convert":
                    await Convert(command);
                    return;
            }
        }
        /// <summary>
        /// Fired when a loggable event occurs, based on severity.
        /// </summary>
        /// <param name="logSeverity">The severity of the log.</param>
        /// <param name="source">The function that generated the log.</param>
        /// <param name="message">The message to send</param>
        /// <returns></returns>
        public static Task Log(LogSeverity logSeverity, string source, string message)
        {
            return Log(new LogMessage(logSeverity, source, message));
        }
        /// <summary>
        /// Fired when a loggable event occurs, based on severity.
        /// </summary>
        /// <param name="msg">The LogMessage received.</param>
        /// <returns></returns>
        public static Task Log(LogMessage msg)
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
