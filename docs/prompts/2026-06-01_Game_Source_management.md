Implement Game Source Management.

Add support for both ATS and ETS2.

Create discovery services that locate:

- Game installation paths
- Profile paths
- Save game paths

Create a Settings screen where users can:

- Review discovered paths
- Override paths
- Add custom locations
- Enable or disable game sources

Introduce domain concepts:

- GameInstallation
- SaveGame
- GameType

Refactor all ingestion and exploration data to partition by:

Game
Profile
Save

Prevent cross-save contamination of:

- Drivers
- Trucks
- Garages
- Jobs
- Time series

Update explorer hierarchy:

Games
    ATS
        Saves
            Companies

    ETS2
        Saves
            Companies

Persist discovered configuration locally.

All future analytics must operate within a specific Game + Save context unless explicitly aggregated.