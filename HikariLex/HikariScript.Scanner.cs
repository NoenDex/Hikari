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

namespace HikariLex
{
    internal partial class HikariScriptScanner
    {
        private static readonly Logger log = LogManager.GetLogger("Scanner");

        private bool hasError { get; set; }

        void GetDrive()
        {
            yylval.drive = yytext;
            yylval.s = yytext;
        }

        void GetUNC()
        {
            yylval.unc = yytext;
            yylval.s = yytext;
        }

        void GetString()
        {
            yylval.s = yytext;
            yylval.result = false;
        }

        public override void yyerror(string format, params object[] args)
        {
            base.yyerror(format, args);
            string logMessage = $"ERROR at line: {yyline} col: {yycol} unexpected text: \"{yytext}\"";
            throw new HikariParserException(logMessage);
        }

        public void FlagError()
        {
            hasError = true;
            string logMessage = $"ERROR at line: {yyline} col: {yycol} unexpected text: \"{yytext}\"";
            throw new HikariParserException(logMessage);
        }
    }
}
