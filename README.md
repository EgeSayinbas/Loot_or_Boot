Cinematic Trailer: https://www.youtube.com/watch?v=Bf7uq9-B2pc

Gameplay Trailer: https://www.youtube.com/watch?v=e4cN7flZe8w

🎴 Multiplayer 3D Card Game (Unity – Netcode for GameObjects)

This repository contains an ongoing multiplayer tabletop card game project developed in Unity using Netcode for GameObjects (NGO).

The core goal of the project is to translate a real world team based card game experience into a networked 3D digital environment, preserving player interaction, signaling, and spatial awareness.

🎯 Project Vision

The game focuses on non verbal player communication in a multiplayer card game setting.
Players sit around a virtual table and interact through:

Eye direction & head movement

Card placement and swapping

Team-based visual signaling

Real time synchronized interactions

Rather than relying on chat or explicit UI cues, the project experiments with physical and spatial signaling mechanics inspired by real tabletop play.

🧩 Core Features (Current)

✅ Multiplayer architecture using Netcode for GameObjects

✅ Server-authoritative card spawning

✅ Fully synchronized 3D card objects (visible to all players)

✅ Player seating system (Seat 0–3)

✅ Team system (Red / Blue)

✅ Lobby flow with ready states

✅ Network-safe input handling via ServerRPC

✅ Modular code structure for future expansion

🛠️ Tech Stack

Engine: Unity

Engine Version: 6000.2.15f1

Networking: Netcode for GameObjects

Gameplay: 3D card objects (not UI-based cards)

Architecture: Server-authoritative logic

Input: Mouse based interaction (expandable)

Version Control: Git / GitHub

🚧 Project Status

This project is actively under development.

Systems are being implemented incrementally with a strong focus on:

Network correctness

Clean synchronization

Scalability for future mechanics

This repository serves as a living development branch, not a final or stable release.

🔮 Planned Features

Player head & camera direction synchronization

Card swapping mechanics

Round system & scoring

Anti-cheat validation

Polished game state transitions

Visual feedback for team signaling

UX & interaction refinements

📂 Repository Purpose

This repo acts as:

A long term project foundation

A development checkpoint for experimentation

A reference implementation for multiplayer tabletop mechanics in Unity

📌 Notes

The project prioritizes clarity over speed

Systems are designed to be testable and replaceable

Some features may be temporarily disabled or refactored during development
