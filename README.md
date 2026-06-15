# ALPHA NOTICE
The project is currently in private testing phase and is not fully released yet.  
This repository contains code and only a portion of required asset bundles for gameplay.  
Issues and PRs are currently disabled while the project is in Alpha.  
We're selectively looking for contributors. If you'd like to get involved - please message **ifp** on discord  

## On the shoulders of giants we stand
This project wouldn't be possible without:  
**Battlestate Games** - Developing Escape From Tarkov for over 10 years.  
**SPT Crew** - Creating and maintaining an exuberantly ambitious ecosystem.  
**Lacyway** - Spearheading Fika, Dedicated Servers, and doing all the hard multiplayer work this project depends on.  

## Credits
**ifp**: Core • Gamemodes • Equipment • Networking • Maps • Steam Audio  
**tarkin**: Core • UI/UX • Maps • Mentor

## Building
Rename `Directory.Build.props.EXAMPLE` and point `SPTPath` to your SPT Installation.  
Use `dotnet build -c Debug`
Debug Building puts `Lambda.Core` assembly in `BepInEx/scripts/` and supports hot-reloading (do not hot-reload during raid loading).  
Warning: `Debug Build` requires [BepInEx ScriptEngine](https://github.com/BepInEx/BepInEx.Debug) in SPT runtime.

## Features

### Gameplay
- Personalized Buy Menu Equipment
- Extensible Gamemode Framework
- Kill Trading, Respawning, Looting
- Mid-Session match joining
- Molotovs

### UI / UX
- Buy Menu
- Teammate Nameplates
- Hideout Weapon Build Selector for Buy Menu
- Scoreboard
- Top Faction Score
- Quick Access UI Rework
- Round Result
- Chat Menu

### Audio
- Steam Audio Integration
- In Game Music Kit

## Reworks

- Sound Occlusion & Transmission overhaul  
- Arena-like movement
- Accurate Pistols
- Lighter Sniper/Marksman rifle feel
- No Unequip Animations
- Blindfire in movement
- Flash and Smoke changes
- Health System simplification

## For Nerds

- Headless Server Support
- Full hot-reload support via ScriptEngine
- Unity Spatializer Engine bypass
- PacketWarden
  - Network Backend Agnostic
  - Time Synchronization
  - MemoryPack Automatic De/Serialization
  - MemoryPack Formatters For EFT Classes
  - Approval / Rejection System
  - Optimistic Approval (Client-prediction)
  - Server-Timestamped Packet Interface
  - Authored Packet Interface (For Anti-Spoofing)
  - Server-Side Packet-Specific Validation
  - Packet Authority (Anyone, Admin, Server Only)
  - Admin Authentication
  - Rate limiting

## Modules

| Project                                | Description                                            |         
|-----------------------------------     |--------------------------------------------------------|
| **Lambda.Core**                        | Core framework and base systems                        |
| **Lambda.Shared**                      | Shared classes, types, and data structures             |
| **Lambda.UI**                          | All UI components and interfaces                       |
| **Lambda.Audio**                       | All Runtime Steam Audio assemblies for EFT             |
| **PacketWarden**                       | Custom network framework for all transactions          |
| **PacketWarden.FikaIntegration**       | Fika/LiteNetLib Integration for PacketWarden           |
| **SteamAudioUnity**                    | Official Steam Audio Unity Integration Library         |
| **PhononSpatializerProxy**             | C# Rewrite of audiophonon_plugin for Unity             |
| **PhononSpatializerProxy.BepInEx**     | PhononSpatializer Proxy integration for BepInEx        |
| **BetterSource.SteamAudioIntegration** | EFT AudioSource system integration with SteamAudio     |

## Licenses

[<img src="https://mirrors.creativecommons.org/presskit/buttons/88x31/svg/by-nc-sa.svg" alt="cc by-nc-sa" width="180" height="63" align="right">](https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode.en)

This project is licensed under [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode.en).

### Dependencies

| Project        | License                                                                                |
|----------------|----------------------------------------------------------------------------------------|
| SPT.Modules    | [NCSA](https://dev.sp-tarkov.com/SPT/Modules/src/branch/master/LICENSE.md)             |
| Fika           | [CC BY-NC-SA 4.0](https://github.com/project-fika/Fika-Plugin/blob/main/LICENSE.md)    |
| Steam Audio    | [Apache 2.0](https://github.com/ValveSoftware/steam-audio/blob/master/LICENSE.md)      |
| LiteNetLib     | [MIT](https://github.com/RevenantX/LiteNetLib/blob/master/LICENSE.txt)                 |
| MemoryPack     | [MIT](https://github.com/Cysharp/MemoryPack/blob/main/LICENSE.txt)                     |
| UniTask        | [MIT](https://github.com/Cysharp/MemoryPack/blob/main/LICENSE.txt)                     |