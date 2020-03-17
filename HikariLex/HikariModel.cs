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
using System.Text;

namespace HikariLex
{
    public class HikariModel
    {
        private static readonly Logger log = LogManager.GetLogger("Model");
        public Dictionary<string, List<Tuple<string, string>>> Drives { get; }

        public HikariModel()
        {
            Drives = new Dictionary<string, List<Tuple<string, string>>>();
        }

        public void AddDriveExpressionUNC(string Drive, string Expression, string UNC)
        {
            if (Drives.ContainsKey(Drive))
            {
                Drives[Drive].Add(new Tuple<string, string>(Expression, UNC));
            }
            else
            {
                Drives.Add(Drive, new List<Tuple<string, string>>() { new Tuple<string, string>(Expression, UNC) });
            }
        }

        /// <summary>
        /// Emergency reduce all drives to just 1st UNC path
        /// Used in case conflicts could not be resolved
        /// </summary>
        public void ReduceUNCs()
        {
            foreach (var item in Drives)
            {
                if (item.Value.Count > 1)
                {
                    item.Value.RemoveRange(1, item.Value.Count - 1);
                }
            }
        }

        public List<string> UsedDriveLetters()
        {
            List<string> letters = new List<string>();
            foreach (string key in Drives.Keys)
            {
                letters.Add(key);
            }
            return letters;
        }

        public string GetNextConflictDriveIndex()
        {
            foreach (var item in Drives)
            {
                if (item.Value.Count > 1)
                    return item.Key;
            }
            return string.Empty;
        }

        public void MoveFirstUNCConflict(string FromDrive, string ToDrive)
        {
            if (!Drives.ContainsKey(FromDrive))
            {
                log.Error($"{FromDrive} doesn't exist!");
                return;
            }
            if (Drives.ContainsKey(ToDrive))
            {
                log.Error($"{ToDrive} is already in use!");
                return;
            }

            List<Tuple<string, string>> uncs = Drives[FromDrive];
            Tuple<string, string> unc = uncs[1];
            Drives.Add(ToDrive, new List<Tuple<string, string>>() { unc });
            uncs.RemoveAt(1);

            log.Debug($"Moved \"{FromDrive}\" [{unc.Item1} = {unc.Item2}] to \"{ToDrive}\"");
        }

        public uint TotalConflicts()
        {
            int conflicts = 0;
            foreach (var item in Drives)
            {
                conflicts += item.Value.Count > 1 ? item.Value.Count - 1 : 0;
            }
            return (uint)conflicts;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in Drives)
            {
                sb.AppendLine();
                sb.AppendLine($" {item.Key}");
                foreach (Tuple<string, string> val in item.Value)
                {
                    sb.AppendLine($"  {val.Item1} = \"{val.Item2}\"");
                }
            }
            return sb.ToString();
        }
    }
}
