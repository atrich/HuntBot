using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using DSharpPlus;
using DSharpPlus.SlashCommands;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using HuntBot.Commands;
using HuntBot.Configs;
using System.Text;

namespace HuntBot
{
    public class Worker : BackgroundService
    {
        private ILogger<Worker> logger;
        private IConfiguration configuration;
        private DiscordClient discordClient;
        private DriveService driveService;
        private SheetsService sheetsService;

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

                var googleConfig = new GoogleApiConifguration();
                builtConfig.GetSection(GoogleApiConifguration.SectionName).Bind(googleConfig);

                byte[] apiKeyBytes = Convert.FromBase64String(builtConfig["GoogleApiServiceAccountKey"]);
                string apiKey = Encoding.UTF8.GetString(apiKeyBytes);
                await ConnectToGoogleCloudApi(googleConfig.AccountId, apiKey);

                var discordConfig = new DiscordApiConfiguration();
                builtConfig.GetSection(DiscordApiConfiguration.SectionName).Bind(discordConfig);

                var puzzleChatGroupChannel = await discordClient.GetChannelAsync(discordConfig.PuzzleChatGroupId);
                var solvedPuzzleChatGroupChannel = await discordClient.GetChannelAsync(discordConfig.SolvedPuzzleChatGroupId);
                var voiceChatGroupChannel = await discordClient.GetChannelAsync(discordConfig.VoiceGroupId);

                var puzzleList = SheetBackedPuzzleList.FromSheet(
                    googleConfig.PuzzleSheetId,
                    googleConfig.HuntDirectoryId,
                    driveService,
                    sheetsService,
                    puzzleChatGroupChannel,
                    solvedPuzzleChatGroupChannel,
                    voiceChatGroupChannel);

                await puzzleList.BuildRoundCache();

                var services = new ServiceCollection()
                    .AddSingleton(puzzleList)
                    .AddSingleton(driveService)
                    .BuildServiceProvider();

                var slash = discordClient.UseSlashCommands(new SlashCommandsConfiguration()
                {
                    Services = services
                });
                slash.RegisterCommands<PuzzleModule>();

                await discordClient.ConnectAsync();
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error during startup");
            }
        }

        private async Task ConnectToGoogleCloudApi(string accountId, string apiServiceKey)
        {
            var credentialInitializer = new ServiceAccountCredential.Initializer(accountId)
            {
                Scopes = new[] { DriveService.Scope.Drive }
            };

            var credential = new ServiceAccountCredential(credentialInitializer
                .FromPrivateKey(apiServiceKey));

            driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "HuntBot"
            });

            sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "HuntBot"
            });
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