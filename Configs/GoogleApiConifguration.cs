using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntBot.Configs
{
    internal class GoogleApiConifguration
    {
        public const string SectionName = "GoogleApi";
        public string AccountId { get; set; }
        public string PuzzleSheetId { get; set; }
        public string HuntDirectoryId { get; set; }
    }
}
