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
%namespace HikariLex
%scannertype HikariScriptScanner
%visibility internal
%tokentype Token

%option stack, minimize, parser, verbose, persistbuffer, noembedbuffers 
%x C_BLOCKCOMMENT
%x COMMENT_SINGLE

Eol             (\r\n?|\n|\n\r)
CR				\r
NotWh           [^ \t\r\n]
Space           [ \t\n]
PBlockOpen		\{
PBlockClose		\}
POpen			\(
PClose			\)
DriveLetter		[d-zD-Z]:
Assign			=
HomeOp			[Hh][Oo][Mm][Ee]
AllOp			[Aa][Ll][Ll]
NotOp			[Nn][Oo][Tt]
OrOp			[Oo][Rr]
AndOp			[Aa][Nn][Dd]
Group			\"([^\"][A-Za-z0-9_\. \-&]*)\"
UNC				\"([^\"][\\\\][A-Za-z0-9_\.\\ \-&]*\$?)\"
ContainsOp      [Cc][Oo][Nn][Tt][Aa][Ii][Nn][Ss]

%{

%}


%%

{POpen}			{ return (int)Token.OPEN_BRACKET; }
{PClose}		{ return (int)Token.CLOSE_BRACKET; }
{PBlockOpen}	{ return (int)Token.BLOCKOPEN; }
{PBlockClose}	{ return (int)Token.BLOCKCLOSE; }
{OrOp}			{ return (int)Token.OR; }
{AndOp}			{ return (int)Token.AND; }
{DriveLetter}	{ GetDrive(); return (int)Token.DRIVELETTER; }
{Assign}		{ return (int)Token.ASSIGN; }
{Group}			{ GetString(); return (int)Token.GROUP; }
{UNC}			{ GetUNC(); return (int)Token.UNC; }
{NotOp}			{ return (int)Token.NOT; }
{HomeOp}		{ return (int)Token.HOME; }
{AllOp}			{ return (int)Token.ALL; }
{ContainsOp}    { return (int)Token.CONTAINS; }

"/*"					{ BEGIN(C_BLOCKCOMMENT); }
<C_BLOCKCOMMENT>"*/"	{ BEGIN(INITIAL); }
<C_BLOCKCOMMENT>.		{ }
<C_BLOCKCOMMENT>\n		{ }

<INITIAL>"//"			{ BEGIN(COMMENT_SINGLE); }
<COMMENT_SINGLE>\n      { BEGIN(INITIAL); }
<COMMENT_SINGLE>[^\n]+  { }

{CR}			{}
{Space}+		{} /* skip */
.               { FlagError(); } /* anything else */
%%