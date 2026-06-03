Bug 1 - Explorer Tree Does Not Match Overview Visibility
Problem

After application startup and save-game discovery, the explorer tree is collapsed and only shows:

Games

The user must manually expand multiple levels of hierarchy before discovering the companies that are already visible in the main content area.

This creates a mismatch between:

Navigation visibility (left side)
Content visibility (right side)

The right-side "All Trucking Companies" view immediately shows all companies, but the left-side explorer hides them.

Expected Behavior

On startup, the explorer should automatically expand enough nodes to expose all companies currently visible in the "All Trucking Companies" overview.

Example:

Games
  ATS
    Save Locations
      <Save Location>
        Companies
          JheredParnell
          tgcitw

  ETS2
    Save Locations
      <Save Location>
        Companies
          EuroHaul

The user should immediately see the same companies in both:

Explorer tree
Company overview list

without additional clicks.

Acceptance Criteria
Startup Expansion

Given:

Companies Overview

shows:

tgcitw
JheredParnell
JheredParnell

Then:

Explorer

should already expose those company nodes.

Visibility Parity

Every company visible in:

All Trucking Companies

must also be visible in:

Explorer Tree

without additional expansion.

Notes

The explorer should be treated as a primary navigation surface, not secondary navigation.

Bug 2 - Company Nodes Do Not Expand To Show Child Collections
Problem

When a company node is selected:

Games
  ATS
    Save Location
      Companies
        tgcitw

the detail pane shows tabs:

Overview
Garages
Drivers
Trucks
Trailers
Jobs
Cities

However:

The company node cannot expand.
No expansion arrow exists.
No child collections are visible.

The explorer hierarchy does not reflect the same structure displayed by the detail screen.

Expected Behavior

Company nodes should expand.

Example:

tgcitw
  Garages
  Drivers
  Trucks
  Trailers
  Jobs
  Cities
Future Expansion

Each category node should eventually be expandable.

Example:

tgcitw
  Garages
    Phoenix
    Seattle
    Tulsa

  Drivers
    Martina N.
    Katie W.

  Trucks
    International 9900I
    Kenworth W900

  Trailers
    Flatbed
    Reefer

  Jobs

  Cities
Acceptance Criteria

Selecting a company should expose:

Garages
Drivers
Trucks
Trailers
Jobs
Cities

under the company node.

Navigation Parity

Every tab visible in the detail pane must have a corresponding node in the explorer.

Current:

Overview
Garages
Drivers
Trucks
Trailers
Jobs
Cities

Expected explorer:

Company
  Garages
  Drivers
  Trucks
  Trailers
  Jobs
  Cities
Bug 3 - Profit By Day Chart Ends With Artificial Collapse
Problem

The Company Overview charts:

Profit by Day
Jobs by Day

show a severe drop-off at the final data point.

The decline appears artificial and likely indicates:

incorrect final-day aggregation
missing data
partial import
charting logic issue
interpolation issue
default value insertion

rather than actual gameplay behavior.

Investigation Required

Determine why the final point collapses.

Possible causes:

Missing Final Snapshot

Example:

Day 186 = 900,000
Day 187 = null

being rendered as:

Day 187 = 0
Empty Aggregation Bucket

Example:

No jobs exist for final day

and aggregation defaults to zero.

Incorrect Historical Import Ordering

Example:

Newest record imported first

or:

Final record duplicated
Trend Calculation Bug

The trend series may be calculating:

Delta

instead of:

Total

for the final bucket.

Expected Behavior

The final chart point should accurately represent the final imported game day.

There should be no artificial collapse unless the underlying data genuinely contains such a decline.

Acceptance Criteria
Validation

For the last plotted point:

Game Day
Profit
Job Count

must be traceable back to imported source data.

Chart Consistency

The final chart point should match:

Daily profit table

and

Daily job table

for the same day.

No Artificial Zeros

Null or missing values must not be rendered as:

0

unless zero is the actual value.

I would have Codex fix these three items before adding more overview dashboards. The explorer hierarchy and chart correctness are foundational navigation and data-trust issues. If users don't trust the trend lines or can't discover data through the tree, every future analytics feature becomes harder to validate.