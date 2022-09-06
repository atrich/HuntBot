using DSharpPlus;
using DSharpPlus.CommandsNext;
using HuntBot.Commands;
using HuntBot.Configs;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace HuntBot
{
    public class Worker : BackgroundService
    {
        private ILogger<Worker> logger;
        private IConfiguration configuration;
        private DiscordClient discordClient;

        public Worker(ILogger<Worker> logger, IConfiguration configuration)
        {
            this.logger = logger;
            this.configuration = configuration;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                logger.LogInformation("Starting discord bot");

                var secret = configuration["KeyVaultSecret"];

                var kvConfig = new KeyVaultConfiguration();
                configuration.GetSection(KeyVaultConfiguration.SectionName).Bind(kvConfig);
                var kvUri = new Uri(kvConfig.Uri);
                var kvCred = new ClientSecretCredential(kvConfig.TenantId, kvConfig.AppId, secret);

                var config = new ConfigurationBuilder();
                config.AddConfiguration(configuration);
                config.AddAzureKeyVault(new SecretClient(kvUri, kvCred), new KeyVaultSecretManager());

                var builtConfig = config.Build();

                string discordBotToken = builtConfig["DiscordBotToken"];
                discordClient = new DiscordClient(new DiscordConfiguration()
                {
                    Token = discordBotToken,
                    TokenType = TokenType.Bot,
                    Intents = DiscordIntents.Guilds | DiscordIntents.AllUnprivileged
                });

                var commands = discordClient.UseCommandsNext(new CommandsNextConfiguration()
                {
                    StringPrefixes = new[] { "!" }
                });

                commands.RegisterCommands<HelpModule>();

                await discordClient.ConnectAsync();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error during startup");
            }
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await discordClient.DisconnectAsync();
            discordClient.Dispose();
            logger.LogInformation("Discord bot stopped");
        }
    }
}