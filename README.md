# TS SE Tool

Truck Simulator Save Editor Tool for **Euro Truck Simulator 2** and **American Truck Simulator**.

This fork continues the original project with a forward-compatible save-file layer. New fields and data blocks introduced by later game versions are preserved instead of being deleted or causing the writer to crash.

## Compatibility update 0.4

The updated save engine now:

- preserves unknown top-level blocks such as `player_vehicles`;
- preserves new or unknown fields inside known blocks;
- accepts `game_time_initial: nil` without stopping economy parsing;
- writes blocks in their original order;
- patches only fields that were actually changed in the editor;
- falls back to the untouched original block when a serializer fails;
- avoids the former `KeyNotFoundException` during save writing;
- handles structural braces and quoted values more safely.

The compatibility layer is intentionally format-tolerant. A newly added game field does not automatically become editable in the UI, but it remains present in the resulting save file.

## System requirements

- Windows x64
- .NET Framework 4.7.2

## Features

- Add custom paths for save files.
- Edit local and Steam save files.
- Edit player level and skills.
- Edit and share saved user colors for trucks and trailers.
- Edit account balance.
- Discover cities and unlock cargo locations.
- Buy or upgrade garages.
- Repair and refuel trucks.
- Share truck paint jobs and positions.
- Repair trailers.
- Create custom Freight Market jobs.
- Make basic Cargo Market edits.
- Share GPS paths.
- Share multiple truck positions as a Convoy Control pack.

## Building

1. Install Visual Studio with the **.NET desktop development** workload.
2. Restore NuGet packages for `TS SE Tool.sln`.
3. Build the solution in `Release | Any CPU`.

The repository also contains a Windows GitHub Actions workflow that restores packages and builds the solution automatically.

## Safe testing

Always test editor builds on a copied profile or save folder. Keep the game's original autosaves until the edited save has loaded successfully in-game.

## License and credits

The project remains licensed under Apache License 2.0. Original project by LIPtoH; compatibility work maintained in this fork by contributors.
