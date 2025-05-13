using System.Collections.Concurrent;
using System.Timers;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace Echo.Services;

/// <summary>
///     Represents a service which manages user-created voice channels.
/// </summary>
internal sealed class VoiceService : BackgroundService
{
    private readonly ILogger<VoiceService> _logger;
    private readonly IConfiguration _configuration;
    private readonly DiscordClient _discordClient;
    private readonly Timer _cleanupTimer;
    private readonly ConcurrentDictionary<ulong, ulong> _userCreatedChannels = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="VoiceService" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="discordClient">The Discord client.</param>
    public VoiceService(ILogger<VoiceService> logger, IConfiguration configuration, DiscordClient discordClient)
    {
        _logger = logger;
        _configuration = configuration;
        _discordClient = discordClient;

        int cleanupTimer = configuration.GetSection("global").GetSection("cleanup").Get<int>();
        _cleanupTimer = new(TimeSpan.FromSeconds(cleanupTimer));
    }

    /// <summary>
    ///     Creates a new voice channel for the specified user.
    /// </summary>
    /// <param name="member">The user for whom to create the channel.</param>
    /// <returns>The newly-created voice channel.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="member" /> is <see langword="null" />.</exception>
    public async Task<DiscordChannel> CreateUserChannelAsync(DiscordMember member)
    {
        if (member is null)
        {
            throw new ArgumentNullException(nameof(member));
        }


        DiscordGuild guild = member.Guild;
        DiscordChannel? parent = GetCategory(guild);
        int userLimit = _configuration.GetSection(guild.Id.ToString()).GetSection("user_limit").Get<int>();

        DiscordChannel channel = await guild.CreateVoiceChannelAsync(member.Username, parent, user_limit: userLimit);
        _logger.LogInformation("Created voice channel {Channel} for user {User}", channel.Name, member.Username);
        _userCreatedChannels.TryAdd(channel.Id, member.Id);
        return channel;
    }

    /// <summary>
    ///     Gets the voice category in which user-created channel will be placed for the specified guild.
    /// </summary>
    /// <param name="guild">The guild whose category to retrieve.</param>
    /// <returns>The category channel, or <see langword="null" /> if it does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="guild" /> is <see langword="null" />.</exception>
    public DiscordChannel? GetCategory(DiscordGuild guild)
    {
        if (guild is null)
        {
            throw new ArgumentNullException(nameof(guild));
        }

        ulong categoryId = _configuration.GetSection(guild.Id.ToString()).GetSection("category").Get<ulong>();
        if (guild.Channels.TryGetValue(categoryId, out var category) && category.Type == ChannelType.Category)
        {
            return category;
        }

        return guild.GetChannel(categoryId);
    }

    /// <summary>
    ///     Returns a value indicating whether the specified voice channel is an automatic channel.
    /// </summary>
    /// <param name="channel">The voice channel to check.</param>
    /// <returns><see langword="true" /> if the channel is an automatic channel; otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    public bool IsAutomaticChannel(DiscordChannel channel)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (channel.Type != ChannelType.Voice)
        {
            return false;
        }

        ulong autoChannelId = _configuration.GetSection(channel.Guild.Id.ToString()).GetSection("channel").Get<ulong>();
        return channel.Id == autoChannelId;
    }

    /// <summary>
    ///     Returns a value indicating whether the specified voice channel is empty.
    /// </summary>
    /// <param name="channel">The voice channel to check.</param>
    /// <returns><see langword="true" /> if the channel is empty; otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    public bool IsChannelEmpty(DiscordChannel channel)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (channel.Type != ChannelType.Voice)
        {
            return false;
        }

        return channel.Users.Count == 0;
    }

    /// <summary>
    ///     Returns a value indicating whether the specified voice channel was created by a user.
    /// </summary>
    /// <param name="channel">The voice channel to check.</param>
    /// <returns><see langword="true" /> if the channel was created by a user; otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel" /> is <see langword="null" />.</exception>
    public bool IsUserCreatedChannel(DiscordChannel channel)
    {
        if (channel is null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        return _userCreatedChannels.ContainsKey(channel.Id);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _cleanupTimer.Elapsed -= OnCleanupTimerElapsed;
        _discordClient.VoiceStateUpdated -= OnVoiceStateUpdated;
        _cleanupTimer.Stop();
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _cleanupTimer.Elapsed += OnCleanupTimerElapsed;
        _discordClient.VoiceStateUpdated += OnVoiceStateUpdated;
        _cleanupTimer.Start();
        return Task.CompletedTask;
    }

    private async void OnCleanupTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            ulong[] channelIds = _userCreatedChannels.Keys.ToArray(); // defensively copy of keys
            foreach (var channelId in channelIds)
            {
                try
                {
                    var channel = await _discordClient.GetChannelAsync(channelId);
                    if (channel is null)
                    {
                        continue;
                    }

                    if (IsChannelEmpty(channel))
                    {
                        _logger.LogInformation("Deleting empty user-created voice channel {Channel}", channel);
                        await channel.DeleteAsync();
                        _userCreatedChannels.TryRemove(channel.Id, out _);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred trying to clean up {Id}", channelId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during cleanup");
        }
    }

    private async Task OnVoiceStateUpdated(DiscordClient sender, VoiceStateUpdateEventArgs args)
    {
        DiscordVoiceState before = args.Before;
        DiscordVoiceState after = args.After;

        DiscordChannel? channelBefore = before?.Channel;
        DiscordChannel? channelAfter = after?.Channel;

        if (channelBefore == channelAfter)
        {
            return;
        }

        if (channelAfter is null)
        {
            await HandleChannelLeaveAsync(before!.Member, channelBefore!);
        }
        else
        {
            await HandleChannelJoinAsync(after!.Member, channelAfter);
        }
    }

    private async Task HandleChannelJoinAsync(DiscordMember member, DiscordChannel channel)
    {
        _logger.LogInformation("{User} joined {Channel}", member, channel);

        if (IsAutomaticChannel(channel))
        {
            var userChannel = await CreateUserChannelAsync(member);
            _logger.LogInformation("Creating user channel {Channel} for {User}", userChannel, member);
            await member.ModifyAsync(model => model.VoiceChannel = userChannel);
        }
    }

    private async Task HandleChannelLeaveAsync(DiscordUser user, DiscordChannel channel)
    {
        _logger.LogInformation("{User} left {Channel}", user, channel.Name);

        if (!IsUserCreatedChannel(channel))
        {
            return;
        }

        if (IsChannelEmpty(channel))
        {
            _logger.LogInformation("Deleting empty user-created voice channel {Channel}", channel);
            await channel.DeleteAsync();
            _userCreatedChannels.TryRemove(channel.Id, out _);
        }
    }
}
