My reaction is that Codex moved in the right direction but overshot into a "web dashboard" style UI and lost some of what made the original application effective.

The biggest issue isn't the missing sparkline.

The biggest issue is that it lost the feeling of an explorer.

Original
Companies
  -> Company
      -> Garages
          -> Garage
              -> Drivers
                  -> Driver
                      -> Jobs

The user always knows:

where they are
how they got there
what parent object they're looking at

It's basically Windows Explorer.

New Version

The Company screen and Garage screen are visually very similar.

Everything is:

Cards
Buttons
List

The hierarchy is gone.

The blank Explorer pane is evidence Codex was probably heading toward the correct solution but never finished it.

The Empty Explorer Is The Most Valuable Part

I would actually focus there first.

Imagine:

Explorer

▼ Companies
   ▼ tgcitw
      ▼ Garages
         Phoenix
         Seattle
         Tulsa
      ▼ Drivers
         Martina N.
         Katie W.
      ▼ Trucks
      ▼ Trailers

▼ Analysis
   Garage Rankings
   Driver Rankings
   Expansion Opportunities

Now when I click Phoenix:

Explorer                       Detail

Companies
  tgcitw
    Garages
      > Phoenix              Phoenix Garage
      Seattle                Profit
      Tulsa                  Drivers

This feels like a management tool.

The Second Issue: Too Much White Space

Look at Phoenix.

You have:

1920x1080

and the actual useful content occupies maybe:

450x350

in the center.

That's a mobile design centered on a desktop.

I would use the width.

What I'd Build

Company screen:

+------------------------------------------------+
| Explorer | Company Detail                      |
|          |                                     |
|          | Profit Card                         |
|          | Drivers Card                        |
|          | Trucks Card                         |
|          |                                     |
|          | [Overview][Garages][Drivers]        |
|          |                                     |
|          | Sortable Table                      |
|          |                                     |
+------------------------------------------------+

The table should fill the entire right pane.

The Third Issue: Cards Are Replacing Data

The old system showed:

Garage          Profit      Avg/Day
------------------------------------
Phoenix         $3.3M       $17,911
Seattle         $1.9M       $10,520

Very information dense.

The new version:

Phoenix
5 drivers
5 trucks
5 trailers

$3.3M

repeated down a giant list.

That's a lot of scrolling.

For ATS managers, density is valuable.

You have:

179 drivers
180 trucks
39 garages
7000+ jobs

Users need comparison views.

I Actually Like The Purple Tabs

Those are okay.

The concept:

Overview
Garages
Drivers
Trucks
Trailers
Jobs
Cities

works.

I'd keep them.

I would probably restyle them to look more like:

[ Overview ]
[ Garages ]
[ Drivers ]

instead of pill buttons.

Sorting Should Move Back To Headers

Current:

[Name] [Profit] [Meta]

as separate buttons.

I don't love that.

Users expect:

Name ▲
Profit ▼
Avg/Day

inside the header row.

Your original web app did this correctly.

The Missing Sparklines Matter More Than You Think

Your old UI had tiny trend charts.

Those provided:

garage making money?
garage losing money?
driver improving?
driver declining?

at a glance.

Without them, you have to drill into detail pages.

I would absolutely bring them back.

LiveCharts2 can do this.

Even a tiny:

/\/\/\/\

sparkline column adds tremendous value.

What I Would Tell Codex

If you continue with this direction, I'd give it something like:

The application should behave like Windows Explorer,
SQL Server Management Studio, or Azure Storage Explorer.

The left explorer pane is the primary navigation model.

The right pane is a detail view.

Entity lists should favor information density over card layouts.

Use table/grid layouts for garages, drivers, trucks, trailers,
cities and jobs.

Retain drill-down navigation.

Retain tabs for child collections.

Add sparkline trend columns.

Sorting should be performed by clicking column headers rather
than dedicated sort buttons.

Optimize for desktop mouse and keyboard usage, not mobile.

Looking at the screenshots, I'd rate the current UI:

Visual polish: 7/10
Information density: 4/10
ATS management usefulness: 5/10
Navigation model: 3/10 (because the explorer isn't implemented)

The original web app is actually stronger from a usability standpoint despite looking older. The goal should be to modernize it, not replace the explorer paradigm that made it effective.