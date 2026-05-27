Design and implement and improved information architecture, navigation model, and visualization framework for an American Truck Simulator analytics platform that processes save-game data into a medallion architecture (bronze/silver/gold) and provides drill-down analytics for trucking companies, drivers, garages, trucks, trailers, jobs, and cities.

Context:
- This project extracts ATS save-game data and localization data into SQLite.
- Bronze layer contains near-raw extracted save-game and localization data.
- Silver layer contains canonical normalized entities and relationships.
- Gold layer contains aggregates, analytics projections, trends, and visualization-ready models.
- The application is both:
  1. A local desktop/self-hosted analytics experience.
  2. A future cloud analytics platform with anonymized multi-user aggregation.
- The UI pattern is:
  - List view -> Detail view -> Child tabs -> Nested list/detail navigation.
- The system must support historical time-series analysis using ATS in-game time progression.
- Save games span years of real-world play and many in-game days.
- ALL manual and autosaves included
- do not reload save or SCS data files already loaded

Primary Objectives:
1. Refine and formalize the information architecture.
2. Refine the medallion data architecture.
3. Design gold-layer aggregates and visualization models.
4. Improve UX with embedded trend visualizations and drill-down reports.
5. Prepare the architecture for future community/cloud aggregation.

Requirements:

# Bronze Layer
Implement and/or validate extraction and storage of:
- Raw save-game structures.
- Localization file mappings.
- Driver data.
  - friendly game game and id
- Trucking company/player data.
- Truck data.
- Trailer data.
- Garage data.
- Job data.
- Route/city data.
  - from job data
- Financial data.
- Skill progression data.
- Truck assignment history.
- Garage assignment history.
- Time-series snapshots.
- ATS in-game timestamps/days.
- Real-world save timestamps

Ensure:
- Historical snapshots are preserved.
- No destructive overwrites occur.
- Relationships are traceable historically.
- Data lineage from bronze -> silver -> gold is clear.

# Silver Layer
Model canonical entities including:
- TruckingCompany
- Driver
- Truck
- Trailer
- Garage
- Job
- City
- Route
- SkillProgression
- ProfitSnapshot
- DriverTruckAssignment
- DriverGarageAssignment

Model relationships:
- Drivers historically assigned to many garages.
- Drivers historically assigned to many trucks.
- Trucks historically associated with many garages/drivers/jobs.
- Garages associated with cities.
- Jobs linked to origin city and destination city.
- Jobs linked to trucks, trailers, garages, and drivers.
- Jobs represented both as:
  - One-way ATS jobs.
  - Logical round-trip chains (outbound + return).

Support:
- Historical assignment tracking.
- Time-series analytics.
- Future cross-user aggregation.

# City Modeling
Track all cities encountered through jobs.

Annotate:
- Cities with owned garages.
- Cities eligible for garages.
- Cities frequently visited.
- Cities with strong outbound profitability.
- Cities with strong bidirectional profitability.

Enable future analytics:
- Recommended garage expansion opportunities.
- Strong route hubs.
- Deadhead reduction analysis.
- Route profitability heatmaps.

# Gold Layer
Design aggregates and analytics projections for:
- Profit trends by driver.
- Profit trends by garage.
- Profit trends by trucking company.
- Profit trends by truck.
- Profit trends by trailer.
- Skill progression vs profitability.
- Route profitability.
- Deadhead frequency.
- Bidirectional route efficiency.
- Trailer utilization.
- Truck utilization.
- Garage productivity.
- City profitability.
- Expansion opportunity scoring.

Design gold-layer read models optimized for:
- Charts.
- Reports.
- Dashboards.
- Drill-down navigation.
- Inline list-view visualizations.

# User Interface / Information Architecture

Top-Level Navigation:
- Trucking Companies list view.

Trucking Company Detail View:
Tabs:
- Details
- Drivers
- Garages
- Trucks
- Trailers
- Jobs
- Cities

Garage Detail View:
Tabs:
- Details
- Drivers
- Trucks
- Trailers
- Jobs
- Routes

Driver Detail View:
Tabs:
- Details
- Trucks
- Garages
- Jobs
- Skill Progression

Truck Detail View:
Tabs:
- Details
- Drivers
- Garages
- Jobs
- Trailers

Trailer Detail View:
Tabs:
- Details
- Trucks
- Jobs
- Route Analytics

Job Detail View:
Tabs:
- Details
- Route
- Truck
- Trailer
- Driver
- Garage
- Profitability

City Detail View:
Tabs:
- Details
- Garages
- Routes
- Profitability
- Expansion Potential

# Visualization Requirements

List Views:
- Add lightweight inline sparklines.
- Sparklines should show trends over the currently selected time window.
- Example:
  - Driver daily profit trend.
  - Garage productivity trend.
  - Truck utilization trend.

Detail Views:
- Add larger breakout charts and reports.
- Support:
  - Line charts.
  - Bar charts.
  - Comparative overlays.
  - Heatmaps.
  - Trend analysis.
  - Time filtering.

Time Filters:
- Support at minimum:
  - 7 day
  - 14 day
- Architect for future extensibility.

# Comparative Analytics
Prepare architecture for:
- Personal analytics.
- Community analytics.
- Anonymous benchmarking.
- Comparison of:
  - Driver profitability.
  - Garage profitability.
  - Route efficiency.
  - Trailer utilization.
  - Expansion strategies.

# Cloud Architecture Preparation
Prepare extraction tooling to become:
- Standalone executable.
- Local SQLite-based analytics tool.
- Optional cloud upload client.

Future cloud workflow:
1. Local extraction.
2. Local SQLite generation.
3. Local analytics and visualizations.
4. Optional anonymized upload.
5. Cloud aggregation and benchmarking.

# Constraints
- Preserve existing navigation concepts where reasonable.
- Keep UX drill-down oriented.
- Avoid cluttering list views.
- Use inline sparklines for compact trends.
- Use detail views for deep analytics.
- Design for incremental iteration.
- Prefer modular architecture.
- Keep SQLite compatibility.

# Deliverables
Produce:
1. Refined information architecture.
2. Navigation hierarchy diagrams.
3. Entity relationship model recommendations.
4. Bronze/silver/gold architectural recommendations.
5. Gold-layer aggregate definitions.
6. UI layout recommendations.
7. Visualization recommendations by screen type.
8. Suggested DTO/read-model structures.
9. Suggested API/query patterns.
10. Incremental implementation roadmap.

Verification:
- Ensure navigation hierarchy is internally consistent.
- Ensure all entities can drill into related entities.
- Ensure historical assignment tracking works.
- Ensure gold aggregates can support intended charts.
- Ensure visualization recommendations fit the information architecture.
- Ensure future cloud aggregation remains possible without redesign.

Stop Conditions:
- Stop when:
  - The information architecture is fully documented.
  - The medallion architecture is fully mapped.
  - Gold aggregates are defined.
  - UI/navigation structure is coherent.
  - Visualization strategy is documented.
  - Incremental implementation plan is complete.