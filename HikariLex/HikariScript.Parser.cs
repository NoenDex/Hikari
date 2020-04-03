/*
Copyright (c) 2020, Stefan Bazelkov
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HikariLex
{
    internal partial class HikariScriptParser
    {
        private static readonly Logger log = LogManager.GetLogger("Parser");

        private IEnumerable<string> groups;

        private Queue<Tuple<string, string>> queue;

        public string UserName { get; set; }

        public HikariModel Model { get; set; }

        public HikariScriptParser(IEnumerable<string> Groups) : base(null)
        {
            groups = Groups;
        }

        public void PushUNC(string Groups, string UNC)
        {
            queue.Enqueue(new Tuple<string, string>(Groups, StripQuotes(UNC)));
        }

        public void PopDriveUNCs(string DriveLetter)
        {
            while (queue.Count > 0)
            {
                Tuple<string, string> tuple = queue.Dequeue();
                string expression = tuple.Item1;
                string unc = tuple.Item2;
                Model.AddDriveExpressionUNC(DriveLetter, expression, unc);
            }
        }

        public void PopPrinterUNCs()
        {
            while (queue.Count > 0)
            {
                Tuple<string, string> tuple = queue.Dequeue();
                string expression = tuple.Item1;
                string unc = tuple.Item2;
                Model.AddPrinterUNC(expression, unc);
            }
        }

        private string StripQuotes(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            return text.Replace("\"", string.Empty).Replace("'", string.Empty).Trim();
        }

        public bool IsMemberOf(string group)
        {
            group = StripQuotes(group);

            if (string.IsNullOrWhiteSpace(group))
            {
                log.Error("Group is empty!");
                return false;
            }

            bool result = groups.Contains(group.ToUpperInvariant());
            return result;
        }

        public bool Contains(string group)
        {
            group = StripQuotes(group);

            if (string.IsNullOrWhiteSpace(group))
            {
                log.Error("Group is empty!");
                return false;
            }

            group = group.ToUpper();

            foreach (string g in groups.Select(gr => gr.ToUpper()))
            {
                if (g.Contains(group))
                    return true;
            }
            return false;
        }

        public void AddDrive(string Drive, string Expression, string UNC)
        {
            UNC = StripQuotes(UNC);
            if (string.IsNullOrWhiteSpace(UNC))
            {
                log.Error($"UNC path provided for HOME drive is empty! Drive {Drive} for [{Expression} = {UNC}] skipped!");
                return;
            }
            Model.AddDriveExpressionUNC(Drive, Expression, UNC);
        }

        public void AddHomeDrive(string Drive, string UNC)
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                log.Error($"User name (SamAccountName) is not set. Home drive skipped!");
                return;
            }

            UNC = StripQuotes(UNC);

            if (string.IsNullOrWhiteSpace(UNC))
            {
                log.Error("UNC path provided for HOME drive is empty! Home drive skipped!");
                return;
            }

            if (UNC[UNC.Length - 1] != '\\')
            {
                UNC += '\\';
            }

            UNC = $"{StripQuotes(UNC)}{UserName}";
            Model.AddDriveExpressionUNC(Drive, "HOME", UNC);
        }

        public void Parse(string s)
        {
            Model = new HikariModel();
            queue = new Queue<Tuple<string, string>>();

            byte[] inputBuffer = System.Text.Encoding.Default.GetBytes(s);
            MemoryStream stream = new MemoryStream(inputBuffer);
            Scanner = new HikariScriptScanner(stream);
            Parse();
            queue.Clear();
        }
    }
}
