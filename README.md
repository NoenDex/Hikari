# Hikari

- Content
    - [Download](#download)
    - [What is Hikari?](#what-is-hikari)
    - [Why?](#why?)
    - [Used NuGet packages and sources](#used-nuget-packages-and-sources)
    - [Running Hikari](#running-hikari)
    - [Features](#features)
    - [Planned features](#planned-features)
    - [Advantages](#advantages)
    - [Small example](#small-example)
    - [Administrator's test example](#administrators-test-example)
    - [Rules](#rules)
    - [Responsibility and license](#responsibility-and-license)

![.NET Core](https://github.com/NoenDex/Hikari/workflows/.NET%20Core/badge.svg?branch=master)

## Download
Download [lates release from here](https://github.com/NoenDex/Hikari/releases/).

## What is Hikari?
Hikari (ひかり) is Japanese word for "Light". There is no particular reason why I chose it.
It is a custom logon script engine for Microsoft Windows OS, executed during user's logon in Microsoft Windows session.
The main goal is to parse set of rules described in text file (usually with extension '.hikari'), query current logon user Active Directory group membership (not recursive) and produce network drives mapping solution with automatic conflict resolution.

## Why?
Well, I was looking for similar solution and used many years VB script, KiX and more, but I was't satisfied. None of them offered human readable, easy-to-understand set of rules to help to offload daily SysAdmin's burden :-)
Over time in dynamic organizations the logon script responsible to map the network shares becomes over cluttered and very hard to maintain. Building Hikari is the answer to that. I had especially in mind the possibility to test user's mappings before deploying and without the requirement to execute it within user's account. Actually it is second itteration and built from scratch custom tool used internally in production.

## Used NuGet packages and sources
- [FluentCommandLineParser](https://github.com/fclp/fluent-command-line-parser) - A simple, strongly typed .NET C# command line parser library using a fluent easy to use interface.
- [NLog](https://github.com/NLog/NLog) - NLog is a free logging platform for .NET with rich log routing and management capabilities.
- [YaccLexTools](https://github.com/ernstc/YaccLexTools) - This package includes GPPG and GPLEX tools for compiling YACC and LEX source files in your C# project.
- [Forker class](https://stackoverflow.com/a/540380) from StackOverflow.com by Marc Gravell
- [InvokeWindowsNetworking class](https://www.pinvoke.net) - MPR.DLL

## Running Hikari
_Command line syntax_:
```
Hikari.exe /s <script> [/a] [/h] [/u <sAMAccountName>]
```
- Required:
    - /s or /script <file.hikari> - Set script file to be parsed.
- Optional:
    - /u or /user <sAMAccountName> - Active Directory sAMAccountName of the user.
        - Used for checking what network mappings particular user would have.
        - If provided script will be run against givven sAMAccountName group membership and no local network mapping will be executed.
    - /h or /hide - Hide terminal window on execute. (Prevents users of killing the script until finished)
    - /a or /alert - Show user if error occurs

_Example run_:
```
Hikari.exe /s net_drives.hikari /h
```
Above example line runs Hikari engine setting "net_drives.hikari" as a script to be parsed and hides terminal window on start.

_Real-life example_:
- Copy:
    - Hikari.exe
    - FluentCommandLineParser.dll
    - NLog.config
    - NLog.dll
    - net_drives.hikari
    to your \\DOMAIN\SYSVOL\DOMAIN.INT\Scripts\DOMAIN\Hikari folder
- Create net_connect.bat file with:
```
   @ECHO OFF
   %logonserver%\NETLOGON\DOMAIN\Hikari\Hikari.exe /s %logonserver%\NETLOGON\DOMAIN\Hikari\net_drives.hikari /h /a
```
- Create GPO to execute that .bat file on user logon
- Apply it to required Active Directory OU
- Update or wait until GPO is propagated
- Test it!
- Say "Thank you" :-)

## Features
- Runs under current logon user credentials.
- Boolean rules expressions (OR, AND, NOT)
- [Drive rules](#drive-rule)
- Built-in [HOME](#home-directive) drive directive.
- Built-in [ALL](#all-directive) drive(s) directive.
- [Automatic conflict resolution](#automatic-conflict-resolution) of duplicated drive mappings.
- Test network mappings for any other user without mapping network drives.
- C-style line and block comments
- Requires .NET 4.5
- Built to run fast!
## Planned features
- Network printer support based on group membership
## Advantages
- Single script file for the entire Active Directory domain
- Single GPO required.
- [Automatic conflict resolution](#automatic-conflict-resolution)
- Test user's mappings without having logged in his/her account to check!
- Human-readable.
- Open-source.
- Fast!

## Small example
```
// H: -> "\\SERVER1\HomeFolders\sAMAccountName"
HOME H: = "\\SERVER1\HomeFolders"

// L: -> "\\SERVER.DOMAIN.ORG\LibShare"
ALL L: = "\\SERVER.DOMAIN.ORG\LibShare"

E: {
    // "GROUP 1" members -> "\\SERVER2\MapForGroup1"
    "GROUP 1" = "\\SERVER2\MapForGroup1"
    "ADGROUP TEST" = "\\SERVER7\TestFolder"
    "DOMAIN USERS" AND NOT "TEST USERS" = "\\SERVER\Folder4"
}

F: {
    "DIVISION 4" = "\\SERVER\\Division 4\Forms"
}

G: {
    // Using brackets is fine :-)
    "Group 3" OR ("Group6" AND NOT "Test users") = "\\SomeServer\Share"
}

R: {
    "FIN GROUP" AND ("FIN MANAGEMENT" OR "FIN Users") = "\\FINANCE SERVER\Payslips"
    "R&D GROUP" OR "DEV Management" = "\\RESARCH SERVER\Projects"
    /* 
        Following "DEV Management" will have 2 R: drives, which will result moving
        UNC "\\Research Server\Guidelines" to the first available drive letter.
    */
    "DEV Management" = "\\Research Server\Guidelines"
}

Y: {
    // $ shares are supported also
    "System admins" = "\\StorageServer\D$"
}
```
## Administrator's test example
```
Hikari.exe /s net_drives.hikari /u UserTe
```
Takes AD group membership of UserTe and runs it against the rules described in net_drives.hikari script and displays the outcome in the terminal and in log file.
## Rules
#### Drive rule:
_SYNTAX_:
```
<DRIVE LETTER>: { 
    <BLOCK OF EXPERSSIONS = UNC PATH>
}
```
#### Block of expression(s) and UNC path
_SYNTAX_:
```
<BOOLEAN CONDITIONS> = <UNC PATH>
```
#### Boolean conditions
_SYNTAX_:
```
"<AD GROUP NAME1>" [[OR|AND] [[NOT] "<AD GROUP NAME2>" ...]
```
Expressions between '[' ']' are optional
#### UNC path
_SYNTAX_:
```
"\\SERVER NAME\Shared folder"
```
#### Home directive
_SYNTAX_:
```
HOME <DRIVE LETTER>: = <UNC PATH>
```
#### ALL directive
_SYNTAX_:
```
ALL <DRIVE LETTER>: = <UNC PATH>
```
#### Comments
- // is a single line comment
- /* */ is a multi-line block comment
#### Automatic conflict resolution
- On normal run Hikari will get all local fixed drives and remove them from the available drives list, which initially is set to D: to Z: drive letters (A: to C: are presumed to be taken by default).
- After the parsing step check for more then one UNC is added to each drive letter.
- If there are no conflicts Hikari will:
    - Disconnect any existing network shared drives
    - Start connecting the network drives after checking UNC path exist and then exits.
- Otherwise for each conflict every second UNC will be moved to the first available drive letter and that letter will marked as used.
- The step above will be repeated until all drive letters contain only one UNC to be mapped to or until no more available letters.
- If the last occurs then emergency "flattening" will be executed, all remaining unresolved drives will take the first UNC and the rest will be discarded.
- All information is logged into User's profile folder, aka %USERPROFILE%\Hikari.log
- On every execution the log file is overwritten!
## Responsibility and license
- This is not commercial product, use it on your own responsibility.
- Licensed under following:
```
BSD 2-Clause License

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
```