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
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace HikariLex
{
    public static class ActiveDirectoryHelper
    {
        private static readonly Logger log = LogManager.GetLogger("AD");
        public static UserPrincipal FindPrincipal(string samAccountName)
        {
            PrincipalContext ctx = new PrincipalContext(ContextType.Domain);
            UserPrincipal usr = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, samAccountName);
            if (usr != null)
            {
                log.Info("Found: {0} -> {1}; [{2}]\n", usr.SamAccountName, usr.DisplayName, usr.Description);
            }
            else
            {
                log.Error("User: \"{0}\" NOT found in AD.", samAccountName);
                return null;
            }
            return usr;
        }

        public static IEnumerable<string> GetMembership(UserPrincipal user)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            PrincipalSearchResult<Principal> groups = user.GetGroups();
            stopwatch.Stop();
            IEnumerable<string> groupNames = groups.Select(x => x.SamAccountName);
            IEnumerable<string> groupNamesAndDescriptions = groups.OrderBy(g => g.SamAccountName).Select(x => string.Format("{0} - ({1})", x.SamAccountName.ToUpper(), x.Description)).Distinct();
            groupNames = (from groupName in groupNames orderby groupName select groupName.Trim().ToUpper()).Distinct();
            log.Info("Group membership for {0} -> {1}; [{2}]", user.SamAccountName, user.DisplayName, user.Description);
            log.Info("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            foreach (string group in groupNamesAndDescriptions)
            {
               log.Info(group);
            }
            log.Info("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
            log.Info("Active Directory info gathering time elapsed: {0:00.00}s", stopwatch.Elapsed.TotalSeconds);
            return groupNames;
        }

    }
}
