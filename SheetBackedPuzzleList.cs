using DSharpPlus;
using DSharpPlus.Entities;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace HuntBot
{
    internal class SheetBackedPuzzleList
    {
        internal class PuzzleRecord
        {
            public string? Name { get; set; }
            public string? Answer { get; set; }
            public string? SheetId { get; set; }
            public string? DocId { get; set; }
            public string? DiscordChannelId { get; set; }
            public string? DiscordVoiceChannelId { get; set; }
        }

        internal enum DocType
        {
            Sheet,
            Doc
        }

        private SheetBackedPuzzleList(
            string id,
            string huntDirectoryId,
            DriveService driveService,
            SheetsService sheetsService,
            DiscordChannel puzzleChatGroup,
            DiscordChannel solvedPuzzleChatGroup,
            DiscordChannel voiceChatGroup)
        {
            Id = id;
            HuntDirectoryId = huntDirectoryId;
            DriveService = driveService;
            SheetsService = sheetsService;
            PuzzleChatGroup = puzzleChatGroup;
            SolvedPuzzleChatGroup = solvedPuzzleChatGroup;
            VoiceChatGroup = voiceChatGroup;
        }

        private string Range { get; } = "A:F";
        private string Id { get; }
        private string HuntDirectoryId { get; }
        private DriveService DriveService { get; }
        private SheetsService SheetsService { get; }

        private DiscordChannel PuzzleChatGroup { get; }
        private DiscordChannel SolvedPuzzleChatGroup { get; }
        private DiscordChannel VoiceChatGroup { get; }

        public static SheetBackedPuzzleList FromSheet(
            string id,
            string huntDirectoryId,
            DriveService driveService,
            SheetsService sheetsService,
            DiscordChannel puzzleChatGroup,
            DiscordChannel solvedPuzzleChatGroup,
            DiscordChannel voiceChatGroup)
        {
            return new SheetBackedPuzzleList(
                id,
                huntDirectoryId,
                driveService,
                sheetsService,
                puzzleChatGroup,
                solvedPuzzleChatGroup,
                voiceChatGroup);
        }

        public async Task<PuzzleRecord?> GetPuzzle(string puzzleName)
        {
            var sheetReq = SheetsService.Spreadsheets.Values.Get(Id, Range);
            var data = await sheetReq.ExecuteAsync();
            var row = data.Values.FirstOrDefault(v => v.Any(x => x is not null && x is string && x.ToString().ToLower().Contains(puzzleName.ToLower())));

            if (row == null)
            {
                return null;
            }
            else
            {
                return BuildPuzzleRecord(row);
            }
        }

        public async Task<PuzzleRecord> NewPuzzle(string puzzleName)
        {
            var sheetReq = SheetsService.Spreadsheets.Values.Get(Id, Range);
            var data = await sheetReq.ExecuteAsync();
            var row = data.Values.FirstOrDefault(v => v.Any(x => x is not null && x is string && x.ToString().ToLower().Contains(puzzleName.ToLower())));

            if (row is not null)
            {
                return BuildPuzzleRecord(row);
            }
            else
            {
                // make the discord channel
                var newChannel = await PuzzleChatGroup.Guild.CreateTextChannelAsync(puzzleName, PuzzleChatGroup);

                var requestBody = new ValueRange();
                requestBody.Values = new List<IList<object>> { new List<object> { puzzleName, null, null, null, $"{newChannel.Id}" } };
                var valueInputOpt = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                var insertDataOpt = SpreadsheetsResource.ValuesResource.AppendRequest.InsertDataOptionEnum.INSERTROWS;
                var appendReq = SheetsService.Spreadsheets.Values.Append(requestBody, Id, Range);
                appendReq.ValueInputOption = valueInputOpt;
                appendReq.InsertDataOption = insertDataOpt;
                var response = await appendReq.ExecuteAsync();

                return BuildPuzzleRecord(requestBody.Values.First());
            }
        }

        public async Task<PuzzleRecord?> AddItem(DocType type, ulong discordChannelId)
        {
            var sheetReq = SheetsService.Spreadsheets.Values.Get(Id, Range);
            var data = await sheetReq.ExecuteAsync();
            var row = data.Values.FirstOrDefault(v => v.Contains(discordChannelId.ToString()));

            if (row is null)
            {
                return null;
            }
            else
            {
                var record = BuildPuzzleRecord(row);
                var docId = string.Empty;
                var mimeType = string.Empty;

                switch (type)
                {
                    case DocType.Sheet:
                        docId = record.SheetId;
                        mimeType = "application/vnd.google-apps.spreadsheet";
                        break;

                    case DocType.Doc:
                        docId = record.DocId;
                        mimeType = "application/vnd.google-apps.document";
                        break;
                }

                if (!string.IsNullOrEmpty(docId))
                {
                    return record;
                }
                else
                {
                    // create new sheet
                    var docMetadata = new Google.Apis.Drive.v3.Data.File()
                    {
                        Name = record.Name,
                        Parents = new List<string> { HuntDirectoryId },
                        MimeType = mimeType
                    };

                    var request = DriveService.Files.Create(docMetadata);
                    request.Fields = "id";
                    var result = await request.ExecuteAsync();

                    // now add the item Id to the master puzzle sheet
                    switch (type)
                    {
                        case DocType.Sheet:
                            record.SheetId = result.Id;
                            break;

                        case DocType.Doc:
                            record.DocId = result.Id;
                            break;
                    }

                    await UpdatePuzzleRecord(record);
                    return record;
                }
            }
        }

        private static PuzzleRecord BuildPuzzleRecord(IList<object> row)
        {
            var record = new PuzzleRecord();

            try
            {
                record.Name = row[0] as string;
                record.Answer = row[1] as string;
                record.SheetId = row[2] as string;
                record.DocId = row[3] as string;
                record.DiscordChannelId = row[4] as string;
                record.DiscordVoiceChannelId = row[5] as string;
            }
            catch (System.ArgumentOutOfRangeException)
            {
                // this is not unexpected, the column item collection is truncated as soon as there are no more values
                // easier to just eat this exception
            }

            return record;
        }

        private async Task UpdatePuzzleRecord(PuzzleRecord record)
        {
            // calculate the range that needs to be updated
            var sheetReq = SheetsService.Spreadsheets.Values.Get(Id, Range);
            var data = await sheetReq.ExecuteAsync();

            int rowNum = 0;
            for(int i = 0; i < data.Values.Count; i++)
            {
                var row = data.Values[i];
                if (row.Contains(record.Name))
                {
                    rowNum = i + 1;
                    break;
                }
            }

            if (rowNum == 0)
            {
                return; // we couldn't find the row to update, give up
            }

            var updateRange = new ValueRange()
            {
                Values = new List<IList<object>> { BuildSheetRow(record) }
            };

            var updateReq = SheetsService.Spreadsheets.Values.Update(updateRange, Id, $"{rowNum}:{rowNum}");
            updateReq.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
            await updateReq.ExecuteAsync();
        }

        private static IList<object> BuildSheetRow(PuzzleRecord record)
        {
            var row = new List<object>();
            row.Add(record.Name);
            row.Add(record.Answer);
            row.Add(record.SheetId);
            row.Add(record.DocId);
            row.Add(record.DiscordChannelId);
            row.Add(record.DiscordVoiceChannelId);
            return row;
        }

        public async Task<IEnumerable<PuzzleRecord>> FindPuzzles(string query)
        {
            var sheetReq = SheetsService.Spreadsheets.Values.Get(Id, Range);
            var data = await sheetReq.ExecuteAsync();
            var rows = data.Values.Where(v => v.Any(x => x is not null && x is string && x.ToString().ToLower().Contains(query)));
            return rows.Select(row => BuildPuzzleRecord(row));
        }
    }
}
