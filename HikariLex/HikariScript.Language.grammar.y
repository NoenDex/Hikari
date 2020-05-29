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
%partial
%parsertype HikariScriptParser
%visibility internal
%tokentype Token

%union {
			public string s;
			public string drive;
			public string unc;
			public bool result;
	   }

%start main

%token OPEN_BRACKET, CLOSE_BRACKET, BLOCKOPEN, BLOCKCLOSE, DRIVELETTER, ASSIGN, OR, AND, GROUP, UNC, NOT, HOME, ALL, CONTAINS, PRINTER

%%

main		: // empty script || home drive || drives mapped to all || block of drives
			| home all drives printer
			;

home		: // empty script or block of drives
			| HOME DRIVELETTER ASSIGN UNC					{ AddHomeDrive($2.drive, $4.unc); }
			;

all			: // null or one or more 
			| alldrive
			| all alldrive
			;

alldrive	: ALL DRIVELETTER ASSIGN UNC					{ AddDrive($2.drive, "ALL", $4.unc); }
			;

drives		: drive											
			| drives drive
			;

drive		: DRIVELETTER BLOCKOPEN conditions BLOCKCLOSE	{ $$.result = $3.result; PopDriveUNCs($1.drive); }
			;

printer		:
			| PRINTER BLOCKOPEN conditions BLOCKCLOSE		{ $$.result = $3.result; PopPrinterUNCs(); }
			;

conditions	: 
			| condition										
			| conditions condition							
			;

condition	: expressions ASSIGN UNC						    { if($1.result) PushUNC($1.s, $3.unc); }
			;

expressions	: notexp
			| OPEN_BRACKET expressions CLOSE_BRACKET		    { $$.result = $2.result; $$.s = $2.s; }
			;

notexp		: orexp
			| NOT OPEN_BRACKET group CLOSE_BRACKET		         { $$.result = !$3.result; $$.s = "NOT(" + $3.s + ")"; }
			| NOT OPEN_BRACKET expressions CLOSE_BRACKET         { $$.result = !$3.result; $$.s = "NOT(" + $3.s + ")"; }
			;

orexp		: andexp
			| orexp OR orexp									{ $$.result = $1.result || $3.result; $$.s = "(" + $1.s + " OR " + $3.s + ")"; }
			| orexp OR expressions								{ $$.result = $1.result || $3.result; $$.s = "(" + $1.s + " OR " + $3.s + ")"; }
			| expressions OR orexp								{ $$.result = $1.result || $3.result; $$.s = "(" + $1.s + " OR " + $3.s + ")"; }
			| expressions OR expressions						{ $$.result = $1.result || $3.result; $$.s = "(" + $1.s + " OR " + $3.s + ")"; }
			;

andexp		: group 											{ $$.result = $1.result; $$.s = $1.s; }
			| andexp AND andexp									{ $$.result = $1.result && $3.result; $$.s = "(" + $1.s + " AND " + $3.s + ")"; }
			| andexp AND expressions							{ $$.result = $1.result && $3.result; $$.s = "(" + $1.s + " AND " + $3.s + ")"; }
			| expressions AND andexp							{ $$.result = $1.result && $3.result; $$.s = "(" + $1.s + " AND " + $3.s + ")"; }
			| expressions AND expressions						{ $$.result = $1.result && $3.result; $$.s = "(" + $1.s + " AND " + $3.s + ")"; }
			;

group		: GROUP											    { $$.result = IsMemberOf($1.s); $$.s = $1.s; }
			| OPEN_BRACKET GROUP CLOSE_BRACKET					{ $$.result = IsMemberOf($1.s); $$.s = $1.s; }
			| CONTAINS OPEN_BRACKET GROUP CLOSE_BRACKET		    { $$.result = Contains($3.s); $$.s = "CONTAINS(" + $3.s + ")"; }
			;

%%