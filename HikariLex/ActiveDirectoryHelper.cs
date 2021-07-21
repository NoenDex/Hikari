/*
Copyright (c) 2020-2021, Stefan Bazelkov
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
using System.Diagnostics;
using System.DirectoryServices.AccountManagement;
using System.Linq;

namespace HikariLex
{
    public static class ActiveDirectoryHelper
    {
        private static readonly Logger log = LogManager.GetLogger("AD");

        private static readonly List<string> GroupMembershipResult = new List<string>();
        private static readonly List<string> GroupNamesAndDescriptions = new List<string>();

        public static UserPrincipal FindPrincipal(string samAccountName)
        {
            try
            {
                PrincipalContext directory_context = new PrincipalContext(ContextType.Domain, Environment.UserDomainName);
                UserPrincipal usr = UserPrincipal.FindByIdentity(directory_context, IdentityType.SamAccountName, samAccountName);
                if (usr != null)
                {
                    log.Info($"Found: {usr.SamAccountName} -> {usr.DisplayName}; [{usr.Description}]\n");
                }
                else
                {
                    log.Error($"User: \"{samAccountName}\" NOT found in AD.");
                    return null;
                }
                return usr;
            }
            catch (Exception x)
            {
                log.Error($"Error getting PricipalContext for \"{samAccountName}\": {x.Message}");
                throw new Exception("samAccountName");
            }
        }

        public static IEnumerable<string> GetMembership(UserPrincipal user)
        {
            GroupMembershipResult.Clear();
            GroupNamesAndDescriptions.Clear();

            if (user == null)
            {
                log.Error("UserPrincipal object is NULL!");
                return new List<string>();
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                using (PrincipalSearchResult<Principal> groups = UserPrincipal.Current.GetAuthorizationGroups())
                {
                    GroupMembershipResult.AddRange(
                        groups.Where(x => x != null && x.SamAccountName != null && x.SamAccountName != string.Empty)
                        .Select(x => x.SamAccountName.ToUpper())
                        .OrderBy(x => x)
                        .Distinct());

                    GroupNamesAndDescriptions.AddRange(
                        groups.Where(x => x != null && x.SamAccountName != null && x.SamAccountName != string.Empty)
                        .OrderBy(g => g.SamAccountName)
                        .Select(x => $"{x.SamAccountName.ToUpper()} - ({x.Description})")
                        .Distinct());
                }

                stopwatch.Stop();
                log.Info($"Group membership for {user.SamAccountName} -> {user.DisplayName}; [{user.Description}]");
                log.Info("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
                foreach (string group in GroupNamesAndDescriptions)
                {
                    log.Info(group);
                }
                log.Info("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
                log.Info($"Active Directory info gathering time elapsed: {stopwatch.Elapsed.TotalSeconds:00.00}s");

                return GroupMembershipResult;

            }
            catch (Exception x)
            {
                if (stopwatch.IsRunning)
                    stopwatch.Stop();
                log.Error($"Get membership failed: {x.Message}");
                return new List<string>();
            }
        }

    }
}
