using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Emzi0767;
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
        public async Task GetPuzzleCommand(CommandContext ctx)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
                return;
            }

            var puzzle = await PuzzleList.GetPuzzleByChannelId(ctx.Channel.Id);
            if (puzzle is null)
            {
                await ctx.RespondAsync("error: could not find puzzle record for this channel");
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await message.SendAsync(ctx.Channel);
        }

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
                return;
            }

            name = name.Trim();

            var puzzle = await PuzzleList.GetPuzzleByName(name);
            if (puzzle is null)
            {
                await ctx.RespondAsync($"error: no puzzle named '{name}'");
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await message.SendAsync(ctx.Channel);
        }

        [Command("find")]
        public async Task FindPuzzleCommand(CommandContext ctx, [RemainingText] string query)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
                return;
            }

            query = query.Trim();
            var puzzles = await PuzzleList.FindPuzzles(query);
            if (puzzles is null || puzzles.Count() == 0)
            {
                await ctx.RespondAsync($"error: no puzzles match query '{query}'");
                return;
            }

            foreach(var puzzle in puzzles)
            {
                var message = await RenderMessage(ctx.Client, puzzle);
                await message.SendAsync(ctx.Channel);
            }
        }

        [Command("new")]
        public async Task NewPuzzleCommand(CommandContext ctx, [RemainingText] string name)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
                return;
            }

            name = name.Trim();
            var puzzle = await PuzzleList.NewPuzzle(name);
            var message = await RenderMessage(ctx.Client, puzzle);
            await message.SendAsync(ctx.Channel);
        }

        [Command("sheet")]
        public async Task AddSheetToPuzzleCommand(CommandContext ctx)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
                return;
            }

            var puzzle = await PuzzleList.AddItem(SheetBackedPuzzleList.DocType.Sheet, ctx.Channel.Id);
            if (puzzle is null)
            {
                await ctx.RespondAsync("error: could not find puzzle record for this channel");
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await message.SendAsync(ctx.Channel);
        }

        [Command("doc")]
        public async Task AddDocToPuzzleCommand(CommandContext ctx)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
                return;
            }

            var puzzle = await PuzzleList.AddItem(SheetBackedPuzzleList.DocType.Doc, ctx.Channel.Id);
            if (puzzle is null)
            {
                await ctx.RespondAsync("error: could not find puzzle record for this channel");
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await message.SendAsync(ctx.Channel);
        }

        [Command("voice")]
        public async Task AddVoiceChannelToPuzzleCommand(CommandContext ctx, int num)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
                return;
            }

            var voiceChannel = ctx.Guild.Channels.FirstOrDefault(c => c.Value.Name.Equals($"puzzchat{num}")).Value;

            if (voiceChannel is null)
            {
                await ctx.RespondAsync("error: no voice channel specified");
                return;
            }

            if (voiceChannel.Type != ChannelType.Voice)
            {
                await ctx.RespondAsync("error: channel specified is not a voice channel");
                return;
            }

            var puzzle = await PuzzleList.AddVoiceChannelToPuzzle(ctx.Channel.Id, voiceChannel.Id);
            if (puzzle is null)
            {
                await ctx.RespondAsync("error: could not find puzzle record for this channel");
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await message.SendAsync(ctx.Channel);
        }

        [Command("solve")]
        public async Task SolvePuzzleCommand(CommandContext ctx, [RemainingText] string answer)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
                return;
            }

            var canonAnswer = CanonicalizeAnswer(answer);
            if(string.IsNullOrEmpty(canonAnswer))
            {
                await ctx.RespondAsync($"error: canonincalized answer from `{answer}` is empty");
                return;
            }

            var puzzle = await PuzzleList.SolvePuzzle(ctx.Channel.Id, canonAnswer);
            if (puzzle is null)
            {
                await ctx.RespondAsync("error: could not find puzzle record for this channel");
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await message.SendAsync(ctx.Channel);
        }

        [Command("bulkimport")]
        public async Task BulkImportPuzzles(CommandContext ctx)
        {
            if (PuzzleList is null)
            {
                await ctx.RespondAsync("error: unable to load the puzzle list sheet");
                return;
            }

            if (DriveService is null)
            {
                await ctx.RespondAsync("error: google drive service is unavailable");
                return;
            }

            var puzzles = await PuzzleList.BulkImportPuzzles();
            if (puzzles is null || puzzles.Count == 0)
            {
                await ctx.RespondAsync("warning: found no new puzzles to import");
                return;
            }

            foreach (var puzzle in puzzles)
            {
                var message = await RenderMessage(ctx.Client, puzzle);
                await message.SendAsync(ctx.Channel);
            }
        }

        private static string CanonicalizeAnswer(string answer)
        {
            // all caps
            answer = answer.ToUpper();

            // keep only basic alphanum
            return string.Concat(answer.Where(c => c.IsBasicAlphanumeric()));
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
            if (!string.IsNullOrEmpty(puzzle.SheetLink))
            {
                buttons.Add(new DiscordLinkButtonComponent(puzzle.SheetLink, label: string.Empty, emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(client, ":bar_chart:"))));
            }

            // Create a google docs link if the puzzle has a doc
            if (!string.IsNullOrEmpty(puzzle.DocLink))
            {
                buttons.Add(new DiscordLinkButtonComponent(puzzle.DocLink, label: string.Empty, emoji: new DiscordComponentEmoji(DiscordEmoji.FromName(client, ":page_facing_up:"))));
            }

            if (buttons.Any())
            {
                message.AddComponents(buttons);
            }

            return message;
        }
    }
}
