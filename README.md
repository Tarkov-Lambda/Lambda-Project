## Credits
- **ifp**: Core • Networking • SFX • Steam Audio • Map SDK
- **tarkin**: UI • VFX • Map SDK  
---

## Features

### Gameplay
- Custom Preset Buy Menu
- First Person Spectator Mode  
- Gamemode Framework  
- Search and Destroy, AWP Only, Duel, King Of The Hill
- Headless Server Support
- Respawning Mechanic
- Preemptive Server-Side Deaths
- Mid-Raid Spawning
- Molotovs

### UI / UX
- Buy Menu
- Hideout Weapon Build Selector for Buy Menu
- Teammate Nameplates
- Scoreboard
- Top Faction Score
- Quick Access UI Rework
- Round Result
- Chat Menu

### Audio
- Steam Audio Integration (Occlusion, Transmission, Distance Attentuation)

---

## Reworks

- Sound Occlusion & Transmission overhaul  
- Arena-like movement
- Accurate Pistols  
- No Unequip Animations  
- Blindfire movement enabled
- Flash and Smoke changes

---

## For Nerds

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
| **BetterSource.SteamAudioIntegration** | EFT AudioSource system                                 |

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