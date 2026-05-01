[![Latest release](https://img.shields.io/github/v/release/AemiliusXIV/OrchestrionPlugin)](https://github.com/AemiliusXIV/OrchestrionPlugin/releases)

> **Based on [OrchestrionPlugin by perchbird & Meli](https://github.com/perchbirdd/OrchestrionPlugin).**
> This repository adds local PC audio support (MP3/WAV) on top of the original plugin.

# Orchestrion Aria
A plugin for [XIVLauncher](https://github.com/goaaats/FFXIVQuickLauncher) that adds a music player interface to control the in-game BGM,
allowing you to set it to any in-game track you want — or play your own **local MP3/WAV files** as replacements.
The BGM will persist through **most** changes of zone/instance/etc, and usually will stay active until you change it or click Stop.
You can search for tracks by name or by assorted metadata, such as zone, instance or boss name where the track is played.

## Installation
In-game, go to **ESC → Dalamud Settings → Experimental → Custom Plugin Repositories** and add:
```
https://aemiliusxiv.github.io/DalamudPlugins/pluginmaster.json
```
Orchestrion Aria will then appear in your Plugin Installer and update automatically when new releases are published.

> **Note:** This is a fork of the original [Orchestrion plugin](https://github.com/perchbirdd/OrchestrionPlugin). Both cannot run at the same time — please disable the original Orchestrion before enabling Orchestrion Aria. Your existing settings and song replacements will be imported automatically on first load.

![Usage](https://github.com/ff-meli/OrchestrionPlugin/raw/master/gh/orch.gif)

_Note that this gif is very old, and is not representative of the current version of the plugin_

## FAQ
### Why are the song numbers skipping around?  They don't even start at 1!
Those numbers are the internal ids used by the game.  Many numbers do not correspond to playable tracks, and so I don't display them in the player.

### It's so hard to find certain tracks!  Can you add/change/remove (some specific info)?
All the song information in the player is auto-updated from [this spreadsheet](https://docs.google.com/spreadsheets/d/1s-xJjxqp6pwS7oewNy1aOQnr3gaJbewvIBbyYchZ6No).
Feel free to comment in the document if you find any inconsistencies.

### Some new in-game music is out and I can't find it!
If the tracks are new, it is possible that either the spreadsheet has not been updated yet.

### I have a suggestion/issue/concern!
Open an issue on this repository, or find AemiliusXIV in the XIVLauncher Discord.

## Credits
* [perchbird](https://github.com/perchbirdd) & [ff-meli](https://github.com/ff-meli), for the original OrchestrionPlugin this fork is based on
* goat, for the launcher and dalamud, without which none of this would be possible.
* MagowDeath#1763 for maintaining [the previous spreadsheet](https://docs.google.com/spreadsheets/d/14yjTMHYmuB1m5-aJO8CkMferRT9sNzgasYq02oJENWs/edit#gid=0) with all of the song data that is used in this plugin.
* Many thanks to [Caraxi](https://github.com/Caraxi/) for keeping things working and updated while I (meli) was away!
* [Luna](https://github.com/LunaRyuko) for adding history and replacing columns with tables in the song list UI
* Too many discord people to name, for helping out with things and offering suggestions.
