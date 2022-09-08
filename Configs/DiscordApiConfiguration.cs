using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntBot.Configs
{
    internal class DiscordApiConfiguration
    {
        public const string SectionName = "Discord";
        public ulong PuzzleChatGroupId { get; set; }
        public ulong VoiceGroupId { get; set; }
        public ulong SolvedPuzzleChatGroupId { get; set; }
    }
}
