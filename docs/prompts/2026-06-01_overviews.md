GOAL

Create a reusable Overview architecture for every entity in the application.

Do NOT add new analytics or recommendation engines yet.

Focus on creating a consistent framework that allows every entity to have a useful Overview tab.

The purpose of an Overview tab is:

- quick understanding
- visual validation
- trend visibility
- navigation to related data

Every entity should have an Overview tab as the first tab.

The Overview tab should answer:

"What is this thing and how is it performing?"

======================================================================
OVERVIEW STANDARD
======================================================================

Every overview page should follow the same layout.

--------------------------------------------------
Header
Summary Cards
Trend Charts
Top Related Items
Recent Activity
--------------------------------------------------

Do not leave Overview tabs empty.

======================================================================
COMPANY OVERVIEW
======================================================================

Add:

Summary Cards:
- Profit
- Drivers
- Trucks
- Trailers
- Jobs
- Cities
- Garages

Charts:
- Profit by Day
- Jobs by Day
- Drivers by Garage

Top Lists:
- Top 10 Garages
- Top 10 Drivers
- Top 10 Cities

Recent:
- Most Recent Jobs

======================================================================
GARAGE OVERVIEW
======================================================================

Summary Cards:
- Profit
- Drivers
- Trucks
- Trailers
- Jobs

Charts:
- Profit Trend
- Jobs Trend

Top Lists:
- Top Drivers
- Top Trucks
- Top Trailers

Recent:
- Recent Jobs

======================================================================
DRIVER OVERVIEW
======================================================================

Summary Cards:
- Profit
- Jobs
- Average Profit Per Job
- Current Garage
- Current Truck

Charts:
- Profit Trend
- Jobs Trend

Top Lists:
- Best Jobs
- Most Frequent Cities
- Most Common Cargo

Recent:
- Recent Jobs

======================================================================
TRUCK OVERVIEW
======================================================================

Summary Cards:
- Profit
- Jobs
- Driver Count
- Garage Count

Charts:
- Profit Trend
- Usage Trend

Top Lists:
- Top Drivers
- Top Garages
- Top Cargo

Recent:
- Recent Jobs

======================================================================
TRAILER OVERVIEW
======================================================================

Summary Cards:
- Profit
- Jobs
- Driver Count

Charts:
- Profit Trend
- Usage Trend

Top Lists:
- Top Drivers
- Top Cargo

Recent:
- Recent Jobs

======================================================================
CITY OVERVIEW
======================================================================

Summary Cards:
- Visits
- Outbound Revenue
- Inbound Revenue
- Total Revenue
- Expansion Score

Charts:
- Revenue Trend
- Visit Trend

Top Lists:
- Top Destinations
- Top Origins
- Most Common Cargo

Recent:
- Recent Jobs

======================================================================
JOB OVERVIEW
======================================================================

Summary Cards:
- Profit
- Distance
- Cargo
- Origin
- Destination

Charts:
- None required initially

Related:
- Driver
- Truck
- Trailer
- Garage

======================================================================
IMPLEMENTATION REQUIREMENTS
======================================================================

Create reusable controls:

OverviewHeaderControl
SummaryCardsControl
TrendChartControl
TopListControl
RecentActivityControl

Avoid duplicated XAML.

Create reusable ViewModels.

======================================================================
CHARTS
======================================================================

Use LiveCharts2.

All charts must be driven from ViewModels.

No hard-coded chart data.

======================================================================
CURRENT PRIORITY
======================================================================

The goal is NOT advanced analytics.

The goal is data validation.

Overview pages should help verify:

- relationships
- aggregations
- trends

The user should be able to visually confirm that the underlying data model is correct before advanced recommendation engines are added.

======================================================================
DO NOT BUILD YET
======================================================================

Do NOT build:

- Garage recommendations
- Driver coaching
- Expansion recommendations
- AI features
- ROI engines

Those come later after data accuracy is verified.