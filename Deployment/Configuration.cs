using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Deployment
{
    public sealed class Configuration
    {
        public string? Host { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
        public string? SguiPath { get; set; }
        public string? SguiAsiPath { get; set; }
        public string? SguiAsiSourcePath { get; set; }
        public string? MySqlUsername { get; set; }
        public string? MySqlPassword { get; set; }
    }
}
