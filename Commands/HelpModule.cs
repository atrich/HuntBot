using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace HuntBot.Commands
{
    internal class HelpModule : BaseCommandModule
    {
        [Command("test")]
        public async Task HelpCommand(CommandContext ctx)
        {
            await ctx.RespondAsync("Test goes here");
        }
    }
}
