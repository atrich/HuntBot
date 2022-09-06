using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HuntBot.Configs
{
    internal class KeyVaultConfiguration
    {
        public const string SectionName = "KeyVault";
        public string Uri { get; set; }
        public string TenantId { get; set; }
        public string AppId { get; set; }
    }
}
