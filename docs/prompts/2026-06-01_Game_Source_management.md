also the app starts out with some assumptions about ats files (game install and save game locations) AND i want it to know about ETS2. so what i really want is a capability that discovers AND configures the location of each then let's you process all the data for both. this probably requires 'partitioning' things like the reference driver names etc so we know the game time in the reference units and save game units

I think this is actually more important than Overview tabs right now.

What you're describing is really the beginning of a Game Source Management capability.

Today the application assumes:

ATS
  Install Location
  Profile Location
  Save Game Location

Hard-coded.

But the real future is:

Game Sources
  ATS
    Install Path
    Profile Path
    Save Path
    Save Games

  ETS2
    Install Path
    Profile Path
    Save Path
    Save Games

Then everything downstream becomes:

Game Source
    -> Save Game
        -> Company
            -> Garage
            -> Driver
            -> Truck
I would actually add a new root explorer node
Games
    ATS
        Save 1
        Save 2

    ETS2
        Save 1
        Save 2

instead of:

Companies
    tgcitw
    JheredParnell

because eventually you'll have:

ATS
    tgcitw

ETS2
    EuroHaul

and you'll want to know where the company came from.

New Domain Concept

I would introduce:

GameInstallation
{
    GameType
    InstallPath
    ProfilePath
    SavePath
}

and:

SaveGame
{
    SaveGameId
    GameType
    ProfileName
    SaveName
    SaveDirectory
}

and:

enum GameType
{
    Ats,
    Ets2
}
Discovery Service

Codex should build:

IGameDiscoveryService

Responsibilities:

Find ATS installation
Find ETS2 installation

Find ATS profiles
Find ETS2 profiles

Find ATS saves
Find ETS2 saves

Typical Windows locations:

ATS

Documents
  American Truck Simulator

ETS2

Documents
  Euro Truck Simulator 2

Steam libraries should also be discovered.

Why This Matters

Right now your data model likely assumes:

Company

is globally unique.

It isn't.

You may eventually have:

ATS
  Acme Trucking

ETS2
  Acme Trucking

So everything should probably be partitioned by:

Game
Profile
Save
Reference Data

This is where your comment becomes really important:

reference driver names

I agree.

You need to decide whether:

Driver

means:

Driver in this save

or

Canonical driver across all saves

Those are very different.

Example

Game Day 50:

Martina N.
Profit 100k

Game Day 100:

Martina N.
Profit 250k

Same driver?

Maybe.

Maybe not.

Could be different saves.

Could be different games.

I would create:

DriverSnapshot

and

CanonicalDriver
Canonical Driver
    Martina N.

Snapshots
    ATS Save A
    ATS Save B
    ETS2 Save C
Time Is The Hard Problem

You called this out already.

ATS day:

Day 187

means absolutely nothing globally.

You need:

GameCalendar
{
    GameType
    SaveId
    GameDay
}

and all trends should be stored relative to:

Save
Game Day

not globally.

Otherwise:

ATS Day 187

and

ETS2 Day 187

will accidentally get merged.

What I'd Tell Codex Next

Not Overview tabs.

I'd tell it:

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

Because once you support both ATS and ETS2, changing the hierarchy later becomes painful. I'd get the partitioning model correct before building Overview dashboards, recommendations, ROI engines, or any other analytics. The game/save boundary is a foundational architectural decision.