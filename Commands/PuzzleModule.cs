using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.SlashCommands;
using Google.Apis.Drive.v3;
using System.Text;

namespace HuntBot.Commands
{

    internal class PuzzleModule : ApplicationCommandModule
    {
        public SheetBackedPuzzleList? PuzzleList { private get; set; }
        public DriveService? DriveService { private get; set; }

        string DriveRequestFields { get; } = "id, name, mimeType, webViewLink";

        readonly DiscordWebhookBuilder NoPuzzleListResponse = new DiscordWebhookBuilder().WithContent("error: unable to load the puzzle list sheet");
        readonly DiscordWebhookBuilder NoDriveServiceResponse = new DiscordWebhookBuilder().WithContent("error: google drive service is unavailable");
        readonly DiscordWebhookBuilder CantFindPuzzleResponse = new DiscordWebhookBuilder().WithContent("error: could not find puzzle record for this channel");

        [SlashCommand("puzzle", "Get the puzzle for this channel")]
        public async Task GetPuzzleCommand(InteractionContext ctx)
        {
            await EnsureContextAsync(ctx);

            var puzzle = await PuzzleList.GetPuzzleByChannelId(ctx.Channel.Id);
            if (puzzle is null)
            {
                await ctx.EditResponseAsync(CantFindPuzzleResponse);
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await ctx.EditResponseAsync(message);
        }

        [SlashCommand("get", "Get a specific puzzle by name")]
        public async Task GetPuzzleCommand(
            InteractionContext ctx,
            [Option("name", "puzzle name"), Autocomplete(typeof(PuzzleNameProvider))] string name)
        {
            await EnsureContextAsync(ctx);

            name = name.Trim();

            var puzzle = await PuzzleList.GetPuzzleByName(name);
            if (puzzle is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"error: no puzzle named '{name}'"));
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await ctx.EditResponseAsync(message);
        }

        [SlashCommand("find", "Find all puzzles by substring match")]
        public async Task FindPuzzleCommand(InteractionContext ctx, [Option("query", "search string")] string query)
        {
            await EnsureContextAsync(ctx);

            query = query.Trim();
            var puzzles = await PuzzleList.FindPuzzles(query);
            if (puzzles is null || puzzles.Count() == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"error: no puzzles match query '{query}'"));
                return;
            }

            var text = GetPuzzleListText(puzzles);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(text));
        }

        [SlashCommand("new", "Create a new puzzle")]
        public async Task NewPuzzleCommand(InteractionContext ctx,
            [Option("name", "puzzle name")] string name,
            [Option("round", "puzzle round"), Autocomplete(typeof(RoundProvider))] string round)
        {
            await EnsureContextAsync(ctx);

            name = name.Trim();
            round = round.Trim();
            var puzzle = await PuzzleList.NewPuzzle(name, round);
            var message = await RenderMessage(ctx.Client, puzzle);
            await ctx.EditResponseAsync(message);
        }

        [SlashCommand("sheet", "Add a new google sheet to this puzzle")]
        public async Task AddSheetToPuzzleCommand(InteractionContext ctx)
        {
            await EnsureContextAsync(ctx);

            var puzzle = await PuzzleList.AddItem(SheetBackedPuzzleList.DocType.Sheet, ctx.Channel.Id);
            if (puzzle is null)
            {
                await ctx.EditResponseAsync(CantFindPuzzleResponse);
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await ctx.EditResponseAsync(message);
        }

        [SlashCommand("doc", "Add a new google doc to this puzzle")]
        public async Task AddDocToPuzzleCommand(InteractionContext ctx)
        {
            await EnsureContextAsync(ctx);

            var puzzle = await PuzzleList.AddItem(SheetBackedPuzzleList.DocType.Doc, ctx.Channel.Id);
            if (puzzle is null)
            {
                await ctx.EditResponseAsync(CantFindPuzzleResponse);
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await ctx.EditResponseAsync(message);
        }

        [SlashCommand("voice", "Assign a voice channel to this puzzle")]
        public async Task AddVoiceChannelToPuzzleCommand(InteractionContext ctx,
            [Choice("puzzchat1", 1)]
            [Choice("puzzchat2", 2)]
            [Choice("puzzchat3", 3)]
            [Choice("puzzchat4", 4)]
            [Choice("puzzchat5", 5)]
            [Option("channel", "voice channel")] long num)
        {
            await EnsureContextAsync(ctx);

            var voiceChannel = ctx.Guild.Channels.FirstOrDefault(c => c.Value.Name.Equals($"puzzchat{num}")).Value;

            if (voiceChannel is null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("error: no voice channel specified"));
                return;
            }

            if (voiceChannel.Type != ChannelType.Voice)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("error: channel specified is not a voice channel"));
                return;
            }

            var puzzle = await PuzzleList.AddVoiceChannelToPuzzle(ctx.Channel.Id, voiceChannel.Id);
            if (puzzle is null)
            {
                await ctx.EditResponseAsync(CantFindPuzzleResponse);
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await ctx.EditResponseAsync(message);
        }

        [SlashCommand("solve", "Add a solution for this puzzle")]
        public async Task SolvePuzzleCommand(InteractionContext ctx, [Option("answer", "puzzle answer")] string answer)
        {
            await EnsureContextAsync(ctx);

            var canonAnswer = CanonicalizeAnswer(answer);
            if (string.IsNullOrEmpty(canonAnswer))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"error: canonincalized answer from `{answer}` is empty"));
                return;
            }

            var puzzle = await PuzzleList.SolvePuzzle(ctx.Channel.Id, canonAnswer);
            if (puzzle is null)
            {
                await ctx.EditResponseAsync(CantFindPuzzleResponse);
                return;
            }

            var message = await RenderMessage(ctx.Client, puzzle);
            await ctx.EditResponseAsync(message);
        }

        [SlashCommand("bulkimport", "Bulk import puzzles that were entered into the sheet manually")]
        public async Task BulkImportPuzzles(InteractionContext ctx)
        {
            await EnsureContextAsync(ctx);

            var puzzles = await PuzzleList.BulkImportPuzzles();
            if (puzzles is null || puzzles.Count == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("warning: found no new puzzles to import"));
                return;
            }

            var text = GetPuzzleListText(puzzles);
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(text));
        }

        private static string CanonicalizeAnswer(string answer)
        {
            // all caps
            answer = answer.ToUpper();

            // keep only basic alphanum
            return string.Concat(answer.Where(char.IsLetterOrDigit));
        }

        private async Task<DiscordWebhookBuilder> RenderMessage(DiscordClient client, SheetBackedPuzzleList.PuzzleRecord? puzzle)
        {
            // build a message for linking to the puzzle assets
            var message = new DiscordWebhookBuilder();
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

        private async Task EnsureContextAsync(InteractionContext ctx)
        {
            await ctx.CreateResponseAsync(InteractionResponseType.DeferredChannelMessageWithSource);

            if (PuzzleList is null)
            {
                await ctx.EditResponseAsync(NoPuzzleListResponse);
                return;
            }

            if (DriveService is null)
            {
                await ctx.EditResponseAsync(NoDriveServiceResponse);
                return;
            }
        }

        private static string GetPuzzleListText(IEnumerable<SheetBackedPuzzleList.PuzzleRecord> puzzles)
        {
            var text = new StringBuilder();
            foreach (var puzzle in puzzles)
            {
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

                if (!string.IsNullOrEmpty(puzzle.DiscordChannelId))
                {
                    text.Append($"  <#{puzzle.DiscordChannelId}>");
                }

                if (!string.IsNullOrEmpty(puzzle.DiscordVoiceChannelId))
                {
                    text.Append($"  <#{puzzle.DiscordVoiceChannelId}>");
                }

                text.AppendLine();
            }

            return text.ToString();
        }
    }

    internal class RoundProvider : IAutocompleteProvider
    {
        public async Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
        {
            var list = ctx.Services.GetRequiredService<SheetBackedPuzzleList>();
            var rounds = list.GetRounds();
            var options = new List<DiscordAutoCompleteChoice>();
            string input = ((string)ctx.OptionValue).ToLower();

            foreach (var round in rounds)
            {
                if (round.ToLower().Contains(input))
                {
                    options.Add(new DiscordAutoCompleteChoice(round, round));
                }
            }

            return options.ToArray();
        }
    }

    internal class PuzzleNameProvider : IAutocompleteProvider
    {
        public async Task<IEnumerable<DiscordAutoCompleteChoice>> Provider(AutocompleteContext ctx)
        {
            var list = ctx.Services.GetRequiredService<SheetBackedPuzzleList>();
            var names = list.GetPuzzleNames();
            var options = new List<DiscordAutoCompleteChoice>();
            string input = ((string)ctx.OptionValue).ToLower();

            foreach (var name in names)
            {
                if (name.ToLower().Contains(input))
                {
                    options.Add(new DiscordAutoCompleteChoice(name, name));
                }
            }

            return options.ToArray();
        }
    }

    internal class VoiceChannelProvider : ChoiceProvider
    {
        public override Task<IEnumerable<DiscordApplicationCommandOptionChoice>> Provider()
        {
            throw new NotImplementedException();
        }
    }

}
