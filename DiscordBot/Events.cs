using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;

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
                    temp = member;
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
                member = user;
            }
            _db.SaveChanges();
            return;
        }
        /// <summary>
        /// Heartbeat function
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
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

                ChannelPermissions channelPerms;
                ChannelPermissions userChannelPerms;

                if (user.Id == 221340610953609218)
                {
                    var channels = guild.TextChannels;

                    foreach (var achannel in channels)
                    {
                        channelPerms = guild.CurrentUser.GetPermissions(achannel);
                        userChannelPerms = user.GetPermissions(achannel);

                        if (!channelPerms.ViewChannel || !channelPerms.SendMessages || !userChannelPerms.ViewChannel)
                        {
                            continue;
                        }
                        try
                        {
                            await achannel.SendMessageAsync($"Happy Birthday {user.Mention}! \n https://youtu.be/Sv-OYkGWOhE");
                        }
                        catch (Exception e)
                        {

                            await Log(LogSeverity.Error, "Birthday", e.Message);
                        }
                    }
                    birthday.Date = birthday.Date.AddYears(1);
                    _db.SaveChanges();
                    return;
                }

                var channel = guild.SystemChannel ?? guild.DefaultChannel;
                channelPerms = guild.CurrentUser.GetPermissions(channel);

                if (!channelPerms.ViewChannel || !channelPerms.SendMessages)
                {
                    if (channel == guild.SystemChannel && guild.SystemChannel != guild.DefaultChannel)
                    {
                        channel = guild.DefaultChannel;
                        channelPerms = guild.CurrentUser.GetPermissions(channel);

                        if (!channelPerms.ViewChannel || !channelPerms.SendMessages)
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
                _db.SaveChanges();
            }

        }
        /// <summary>
        /// Fired when a message is received
        /// </summary>
        /// <param name="message">IMessage object received.</param>
        /// <returns></returns>
        private async Task ReceiveMessage(IMessage message)
        {
            if (message is IUserMessage iuMessage && message.Channel is IGuildChannel igChannel)
            {

                if ((message.Flags & MessageFlags.Ephemeral) == MessageFlags.Ephemeral) { return; }
                var _db = new AppDBContext();
                DiscordMessage msg = (DiscordMessage)(SocketUserMessage)message;
                msg.GuildId = igChannel.Guild.Id;
                msg.DiscordLogs = new List<DiscordLog>();
                await React(iuMessage, msg.DiscordLogs);
                _db.UserMessages.Add(msg);
                _db.SaveChanges();
            }
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
                if ((newMessage.Flags & MessageFlags.Ephemeral) == MessageFlags.Ephemeral) { return; }
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

            if (recentLogs.Any(x => x.User.Id != msg?.AuthorId && x.Data is MessageDeleteAuditLogData data && data.ChannelId == channel.Id))
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
            await Log(LogSeverity.Debug, "AutoComplete", "Autocomplete fired");
            if (interaction.Data.CommandName != "birthday") { return; }
            var focusedOption = interaction.Data.Options.First(x => x.Focused);
            if (focusedOption == null) { return; }

            await Log(LogSeverity.Debug, "Auto", $"{interaction.Data.CommandName}: {focusedOption.Value}");

            List<AutocompleteResult> results = new();
            if (focusedOption.Name == "timezone")
            {
                foreach (TimeZoneInfo z in TimeZoneInfo.GetSystemTimeZones().Where(x => x.DisplayName.Contains(focusedOption.Value.ToString() ?? "") || x.Id.Contains(focusedOption.Value.ToString() ?? "")).Take(25))
                {
                    results.Add(new AutocompleteResult($"{z.DisplayName} {(z.SupportsDaylightSavingTime ? "(DST)" : "(No DST)")}", z.Id));
                    await Log(LogSeverity.Debug, "Auto", $"{z.DisplayName}");
                }
                await interaction.RespondAsync(results);
            }
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
            }
        }
        /// <summary>
        /// Fired when a loggable event occurs, based on severity.
        /// </summary>
        /// <param name="logSeverity">The severity of the log.</param>
        /// <param name="source">The function that generated the log.</param>
        /// <param name="message">The message to send</param>
        /// <returns></returns>
        private Task Log(LogSeverity logSeverity, string source, string message)
        {
            return Log(new LogMessage(logSeverity, source, message));
        }
        /// <summary>
        /// Fired when a loggable event occurs, based on severity.
        /// </summary>
        /// <param name="msg">The LogMessage received.</param>
        /// <returns></returns>
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
