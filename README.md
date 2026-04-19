## Credits
- **ifp**: Gamemodes • Networking • SFX • Steam Audio • Patching
- **tarkin**: UI • VFX • Map SDK • Patching  
---

## Features

### Gameplay
- Custom Preset Buy Menu
- First Person Spectator Mode  
- Gamemode Framework  
- Search and Destroy, AWP Only, Duel, King Of The Hill
- Headless Support
- Respawning Mechanic  
- Preemptive Server-Side Deaths  
- Mid-Raid Spawning
- Lootable Fake Corpses  
- Molotovs

### UI / UX
- Buy Menu
- Hideout Weapon Build Selector for Buy Menu
- Teammate Nameplates
- Scoreboard
- Top Faction Score
- Quick Access UI Rework
- Round Result

### Audio
- Steam Audio Integration (Occlusion, Transmission, Distance Attentuation)

---

## Reworks

- Sound Occlusion & Transmission overhaul  
- No Inertia movement system  
- Accurate Pistols  
- No Unequip Animations  
- Blindfire movement enabled  

---

## For Nerds

- Unity Spatializer Engine bypass *(Steam Audio DSP bridge)*  
- Custom LiteNetLib Packet Handler
  - MemoryPack Automatic De/Serialization
  - MemoryPack Formatters For EFT Classes (Player)
  - Approval / Rejection System (With rejection reason)
  - Local Approval Prediction Handling
  - Timestamped Packet Interface (For Time Dependant Packets)
  - Authored Packet Interface (For Anti-Spoofing)
  - Server-Side Packet-Specific Validation
  - Packet Authority (Anyone, Admin, Server Only)
  - Admin Authentication
  - Rate limiting
- Unity Tracer  
  - Class-wide method observation via Harmony postfixing  

## Modules

| Project          | Description                                                      |
|------------------|------------------------------------------------------------------|
| **arena.bep**    | Core framework and base systems                                  |
| **arena.shared** | Shared classes, types, and data structures *(Unity Package)*     |
| **arena.ui**     | All UI components and interfaces *(Unity Package)*               |
| **arena.audio**  | Steam Audio / SFX integration *(Unity Package)*                  |
| **tracer**       | Class-wide method observation via Harmony postfixing             |

## Licenses

[<img src="https://mirrors.creativecommons.org/presskit/buttons/88x31/svg/by-nc-sa.svg" alt="cc by-nc-sa" width="180" height="63" align="right">](https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode.en)

This project is licensed under [CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/legalcode.en).

### Dependencies

| Project        | License                                                                                |
|----------------|----------------------------------------------------------------------------------------|
| SPT.Modules    | [NCSA](https://dev.sp-tarkov.com/SPT/Modules/src/branch/master/LICENSE.md)             |
| Steam Audio    | [Apache 2.0](https://github.com/ValveSoftware/steam-audio/blob/master/LICENSE.md)      |
| LiteNetLib     | [MIT](https://github.com/RevenantX/LiteNetLib/blob/master/LICENSE.txt)                 |
| MemoryPack     | [MIT](https://github.com/Cysharp/MemoryPack/blob/main/LICENSE.txt)                     |
| UniTask        | [MIT](https://github.com/Cysharp/MemoryPack/blob/main/LICENSE.txt)                     |