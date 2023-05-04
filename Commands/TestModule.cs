using DSharpPlus.SlashCommands;

namespace HuntBot.Commands
{
    internal class TestModule : ApplicationCommandModule
    {
        [SlashCommand("test", "for testing")]
        public async Task HelpCommand(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync("Test goes here");
        }
    }
}
