using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Google;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4.Data;
using System.Text;

namespace HuntBot.Commands
{
    internal class PuzzleModule : BaseCommandModule
    {
        public SheetBackedPuzzleList? PuzzleList { private get; set; }
        public DriveService? DriveService { private get; set; }

        string DriveRequestFields { get; } = "id, name, mimeType, webViewLink";

        [Command("puzzle")]
        public async Task GetPuzzleCommand(CommandContext ctx, [RemainingText] string name)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
            }

            name = name.Trim();

            var puzzle = await PuzzleList.GetPuzzle(name);
            if (puzzle is null)
            {
                await ctx.RespondAsync($"error: no puzzle named '{name}'");
                return;
            }

            DiscordMessageBuilder message = await RenderMessage(ctx.Client, puzzle);
            await message.SendAsync(ctx.Channel);
        }

        private async Task<DiscordMessageBuilder> RenderMessage(DiscordClient client, SheetBackedPuzzleList.PuzzleRecord? puzzle)
        {
            // build a message for linking to the puzzle assets
            var message = new DiscordMessageBuilder();
            var text = new StringBuilder();
            var buttons = new List<DiscordComponent>();

            var solved = !string.IsNullOrEmpty(puzzle.Answer);

            // Add ✅ or ❓if puzzle is solved
            if (solved)
            {
                text.Append(":white_check_mark:");
            }
            else
            {
                text.Append(":grey_question:");
            }

            // Display puzzle name
            text.Append(puzzle.Name);

            // Display answer where solved
            if (!string.IsNullOrEmpty(puzzle.Answer))
            {
                text.Append($" **[{puzzle.Answer}]**");
            }
            text.AppendLine();

            if (!string.IsNullOrEmpty(puzzle.DiscordChannelId))
            {
                text.AppendLine($"> :hash: <#{puzzle.DiscordChannelId}>");
            }

            if (!string.IsNullOrEmpty(puzzle.DiscordVoiceChannelId))
            {
                text.AppendLine($"> :sound: <#{puzzle.DiscordVoiceChannelId}>");
            }

            message.WithContent(text.ToString());

            // Create a google sheets link if the puzzle has a sheet
            if (!string.IsNullOrEmpty(puzzle.SheetId))
            {
                var sheetRequest = DriveService.Files.Get(puzzle.SheetId);
                sheetRequest.Fields = DriveRequestFields;
                var sheet = await sheetRequest.ExecuteAsync();
                if (sheet is not null)
                {
                    buttons.Add(new DiscordLinkButtonComponent(sheet.WebViewLink, label: string.Empty, emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(client, ":bar_chart:"))));
                }
            }

            // Create a google docs link if the puzzle has a doc
            if (!string.IsNullOrEmpty(puzzle.DocId))
            {
                var docRequest = DriveService.Files.Get(puzzle.DocId);
                docRequest.Fields = DriveRequestFields;
                var doc = await docRequest.ExecuteAsync();
                if (doc is not null)
                {
                    buttons.Add(new DiscordLinkButtonComponent(doc.WebViewLink, label: string.Empty, emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(client, ":page_facing_up:"))));
                }
            }

            if (buttons.Any())
            {
                message.AddComponents(buttons);
            }

            return message;
        }
    }
}
