using DSharpPlus.Entities;
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

        private SheetBackedPuzzleList(string id, SheetsService sheetsService)
        {
            this.Id = id;
            this.SheetsService = sheetsService;
        }

        private string Range { get; } = "A:F";
        private string Id { get; }
        private SheetsService SheetsService { get; }

        public static SheetBackedPuzzleList FromSheet(string id, SheetsService sheetsService)
        {
            return new SheetBackedPuzzleList(id, sheetsService);
        }

        public async Task<PuzzleRecord?> GetPuzzle(string puzzleName)
        {
            // "1jGgQ8WAgFjXFBpgNySdUefkh4P2GCn8IOcr_-gWySJI"
            var sheetReq = SheetsService.Spreadsheets.Values.Get(Id, Range);
            var data = await sheetReq.ExecuteAsync();
            var row = data.Values.FirstOrDefault(v => v.Contains(puzzleName));

            if (row == null)
            {
                return null;
            }
            else
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
        }
    }
}
