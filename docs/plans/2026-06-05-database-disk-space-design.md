# Database Disk Space Confirmation Design

## Context

The original local database was `2,725,462,016` bytes. The local ATS and ETS2
`game.sii` files across all document backup folders total `1,361,411,048` bytes:

- ATS: `882,703,768` bytes
- ETS2: `478,707,280` bytes

The actual rebuilt database loaded `258` Steam userdata save files totaling
`156,804,016` bytes into bronze. That produced a `2,725,462,016` byte database,
for an observed `17.3813x` database-to-loaded-save ratio. `bronze_sii_units` and
its unique index account for about `2.64 GB` of the file because the app stores
millions of parsed SII unit rows. The app uses `19.2x`, which is the observed
ratio plus about 10% rounded up to one decimal.

## Design

When the user clicks `Finish` in Game Source Setup, the WPF presenter estimates the
Employee Database storage needed for the selected save roots. It sums non-backup
`game.sii` files under the selected roots and multiplies that by `19.2`.

If free space is sufficient, the user sees a confirmation dialog showing the
estimated database space and current free space. Cancel leaves the wizard open and
does not save the configuration. Continue runs the existing validated save flow.

If available free space is below the additional space required, the presenter
blocks saving and returns a status telling the user to free space or remove old
save games. Additional space required is `max(0, projected database size - current
ats-employee-stats.db size)`, because an existing database file already occupies
part of the projected final footprint.

## Boundaries

The application use case still owns save-root validation and persistence. Disk
measurement and user confirmation stay in WPF-facing services so the application
layer does not depend on UI dialogs or local disk APIs directly.

## Testing

Tests cover the estimator math, backup-save exclusion, insufficient-space blocking,
cancelled confirmation, and successful confirmation before persistence.
