﻿using System.Reflection;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;
using DSharpPlus.Interactivity.Extensions;
using DSharpPlus.SlashCommands;
using Echo.Commands;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Echo.Services;

/// <summary>
///     Represents a service which manages the bot's Discord connection.
/// </summary>
internal sealed class BotService : BackgroundService
{
    private readonly ILogger<BotService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly DiscordClient _discordClient;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BotService" /> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="discordClient">The Discord client.</param>
    public BotService(ILogger<BotService> logger, IServiceProvider serviceProvider, DiscordClient discordClient)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _discordClient = discordClient;

        var attribute = typeof(BotService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        Version = attribute?.InformationalVersion ?? "Unknown";
    }

    /// <summary>
    ///     Gets the date and time at which the bot was started.
    /// </summary>
    /// <value>The start timestamp.</value>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>
    ///     Gets the bot version.
    /// </summary>
    /// <value>The bot version.</value>
    public string Version { get; }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.WhenAll(_discordClient.DisconnectAsync(), base.StopAsync(cancellationToken));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StartedAt = DateTimeOffset.UtcNow;
        _logger.LogInformation("Echo v{Version} is starting...", Version);

        _discordClient.UseInteractivity();

        SlashCommandsExtension slashCommands = _discordClient.UseSlashCommands(new SlashCommandsConfiguration
        {
            Services = _serviceProvider
        });

        _logger.LogInformation("Registering commands...");
        slashCommands.RegisterCommands<InfoCommand>();

        _logger.LogInformation("Connecting to Discord...");
        _discordClient.Ready += OnReady;

        RegisterEvents(slashCommands);

        await _discordClient.ConnectAsync();
    }

    private Task OnReady(DiscordClient sender, ReadyEventArgs e)
    {
        _logger.LogInformation("Discord client ready");
        return Task.CompletedTask;
    }

    private void RegisterEvents(SlashCommandsExtension slashCommands)
    {
        slashCommands.AutocompleteErrored += (_, args) =>
        {
            _logger.LogError(args.Exception, "An exception was thrown when performing autocomplete");
            if (args.Exception is DiscordException discordException)
            {
                _logger.LogError("API response: {Message}", discordException.JsonMessage);
            }

            return Task.CompletedTask;
        };

        slashCommands.SlashCommandInvoked += (_, args) =>
        {
            var optionsString = "";
            if (args.Context.Interaction?.Data?.Options is { } options)
            {
                optionsString = $" {string.Join(" ", options.Select(o => $"{o?.Name}: '{o?.Value}'"))}";
            }

            _logger.LogInformation("{User} ran slash command /{Name}{OptionsString}", args.Context.User, args.Context.CommandName,
                optionsString);
            return Task.CompletedTask;
        };

        slashCommands.ContextMenuInvoked += (_, args) =>
        {
            DiscordInteractionResolvedCollection? resolved = args.Context.Interaction?.Data?.Resolved;
            var properties = new List<string>();
            if (resolved?.Attachments?.Count > 0)
            {
                properties.Add($"attachments: {string.Join(", ", resolved.Attachments.Select(a => a.Value.Url))}");
            }

            if (resolved?.Channels?.Count > 0)
            {
                properties.Add($"channels: {string.Join(", ", resolved.Channels.Select(c => c.Value.Name))}");
            }

            if (resolved?.Members?.Count > 0)
            {
                properties.Add($"members: {string.Join(", ", resolved.Members.Select(m => m.Value.Id))}");
            }

            if (resolved?.Messages?.Count > 0)
            {
                properties.Add($"messages: {string.Join(", ", resolved.Messages.Select(m => m.Value.Id))}");
            }

            if (resolved?.Roles?.Count > 0)
            {
                properties.Add($"roles: {string.Join(", ", resolved.Roles.Select(r => r.Value.Id))}");
            }

            if (resolved?.Users?.Count > 0)
            {
                properties.Add($"users: {string.Join(", ", resolved.Users.Select(r => r.Value.Id))}");
            }

            _logger.LogInformation("{User} invoked context menu '{Name}' with resolved {Properties}", args.Context.User,
                args.Context.CommandName, string.Join("; ", properties));

            return Task.CompletedTask;
        };

        slashCommands.ContextMenuErrored += (_, args) =>
        {
            ContextMenuContext context = args.Context;
            if (args.Exception is ContextMenuExecutionChecksFailedException)
            {
                context.CreateResponseAsync("You do not have permission to use this command.", true);
                return Task.CompletedTask; // no need to log ChecksFailedException
            }

            string? name = context.Interaction.Data.Name;
            _logger.LogError(args.Exception, "An exception was thrown when executing context menu '{Name}'", name);
            if (args.Exception is DiscordException discordException)
            {
                _logger.LogError("API response: {Message}", discordException.JsonMessage);
            }

            return Task.CompletedTask;
        };

        slashCommands.SlashCommandErrored += (_, args) =>
        {
            InteractionContext context = args.Context;
            if (args.Exception is SlashExecutionChecksFailedException)
            {
                context.CreateResponseAsync("You do not have permission to use this command.", true);
                return Task.CompletedTask; // no need to log SlashExecutionChecksFailedException
            }

            string? name = context.Interaction.Data.Name;
            _logger.LogError(args.Exception, "An exception was thrown when executing slash command '{Name}'", name);
            if (args.Exception is DiscordException discordException)
            {
                _logger.LogError("API response: {Message}", discordException.JsonMessage);
            }

            return Task.CompletedTask;
        };
    }
}
