# tsb-savestate-multitool

A small collection of "tools" built from shared code that can be used on a save state for the Nintendo game Tecmo Super Bowl.

## Description

I've written similar things in Perl over the years, but I wanted to try to build something in a more modern framework with GUI. This was written in C# 12.0 using Visual Studio 2022.

### TSB RosterRipper 
Creates a text file with the roster for a given TSB ROM file. No GUI.

### TSB StatExtractor
Extracts roster information from given ROM file, stat information from given save state file, and creates a text file that lists the players and their game stats.
Windows Presentation Foundation (WPF) GUI.

### TSB ConditionChecker
Extracts roster information from given ROM file, stat information from given save state file, and displays current game state information.
Windows Presentation Foundation (WPF) GUI.

## Authors

@tundrayeti

## Version History

* 0.1
    * Initial commit

## License

This project is licensed under the [Mozilla Public License Version 2.0] License - see the LICENSE.md file for details

## Acknowledgments

Standing on the shoulders of giants of many in the ROM hacking and Tecmo Super Bowl communinties, but in particular @bruddog and drake for their generous help, inspiration, and contributions at large
