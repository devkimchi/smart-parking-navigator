# Product Requirements Document: Smart Parking Navigator

| Field | Value |
| --- | --- |
| Status | Approved |
| Version | 1.0 |
| Last updated | 2026-08-11 |
| Product owner | TBD |
| Target market | Drivers using HDB car parks in Singapore |
| Source | [IDEATION.md](IDEATION.md) |
| Approval | Product requirements signed off on 2026-08-11 |

## 1. Executive Summary

Smart Parking Navigator helps drivers find a suitable HDB car park near their
destination using real-time availability, operating conditions, and vehicle
type. Rather than showing every nearby car park equally, the product
recommends options that the driver can actually use and immediately presents
alternatives when a selected car park is full.

The MVP will combine destination search, a map and results list, real-time
availability, car park details, compatibility filters, and ranked
recommendations. It will explicitly communicate when data is stale or
unavailable so users can make informed decisions.

## 2. Problem and Opportunity

### Problem statement

Drivers can locate car parks on a map, but location alone does not answer the
questions that determine whether a car park is useful:

- Is a lot available now?
- Does this car park support the user's vehicle type?
- Is parking permitted at the intended time?
- How current is the availability information?
- What is the best nearby alternative if the first choice is full?

Answering these questions currently requires checking multiple data points or
traveling to a car park without confidence that it will be usable. This creates
search time, unnecessary driving, and avoidable uncertainty near the
destination.

### Opportunity

HDB static car park information and real-time availability data can be combined
into a single decision-oriented experience. The product can reduce the effort
required to choose a car park by filtering incompatible options, ranking viable
ones, and exposing data quality at the point of decision.

## 3. Target Users and Jobs to Be Done

### Primary persona: Destination-bound driver

A driver who knows where they are going and wants to choose a nearby car park
before or during the trip.

**Job to be done:** When I am traveling to a destination, help me quickly choose
a nearby car park that has available space and supports my vehicle and intended
parking time, so I can avoid searching after I arrive.

### Secondary persona: Constraint-sensitive driver

A driver of a car, heavy vehicle, or motorcycle, or a driver who needs night,
short-term, or free parking.

**Job to be done:** When I select my vehicle type or operating requirements,
show only options I can use and explain why other options are excluded.

### Core use cases

1. Search for a destination and compare nearby car parks.
2. Find an available car park near the current location.
3. Filter results by vehicle type and operating requirements.
4. Inspect availability, restrictions, and data freshness before selecting.
5. Find a nearby alternative when the preferred car park is full.

## 4. Current and Future User Journey

### Current journey

1. Search for car parks near a destination.
2. Open individual listings or separate data sources.
3. Manually compare distance, availability, and restrictions.
4. Choose a car park despite uncertainty about freshness or compatibility.
5. Search again after arrival if the selected car park is full or unsuitable.

### Intended journey

1. Enter a destination or use the current location.
2. View nearby car parks on a synchronized map and results list.
3. Select a vehicle type and apply parking-condition filters.
4. Compare ranked, compatible options with current availability and update time.
5. Select a car park or immediately choose a recommended alternative.

## 5. Product Proposal

Smart Parking Navigator will provide:

1. **Decision-ready availability:** Total lots, available lots, occupancy, lot
   type, update time, and data status in one view.
2. **Compatible recommendations:** Results filtered and ranked using destination
   distance, availability, occupancy, vehicle type, and operating
   conditions.
3. **Resilient alternatives:** Nearby viable options when a car park is full,
   incompatible, or supported by stale or unavailable data.

## 6. Product Goals and Success Metrics

The initial targets below are launch criteria and should be reviewed after
baseline usability and telemetry data are available.

| Goal | Metric | MVP target |
| --- | --- | --- |
| Reduce parking search effort | Median time from destination results loading to selecting a compatible car park in usability tests | 30 seconds or less |
| Make recommendations usable | Participants who select an available, compatible car park without external information | At least 80% |
| Prevent incompatible recommendations | Recommended car parks that do not support the selected vehicle type or violate active operating constraints in the acceptance dataset | 0 |
| Communicate data quality | Availability views that display update time and correct fresh, stale, or unavailable state | 100% |
| Recover from full car parks | Full car park selections that display at least one viable nearby alternative when one exists | 100% |
| Keep core interactions responsive | 95th-percentile time to display nearby results after a valid destination is resolved | 3 seconds or less, excluding third-party outages |

### Guardrail metrics

- Search failure rate
- Percentage of car parks with unmatched static and real-time records
- Percentage of availability records classified as stale or unavailable
- Recommendation result sets with no viable option
- User location permission denial rate

## 7. Scope and Priorities

Priority definitions:

- **P0:** Required for MVP launch.
- **P1:** Important follow-up after MVP validation.
- **P2:** Future enhancement that is not required for initial adoption.

### P0: MVP

- Destination, address, and current-location search
- Google Maps and a synchronized result list
- Real-time availability and occupancy
- Car park details and operating conditions
- Vehicle type, availability, and car park type filters
- Ranked compatible recommendations
- Alternatives for full car parks
- Data freshness and failure states

### P1: Post-MVP

- Favorite car parks and availability alerts
- Saved vehicle type with automatic compatibility filtering
- Current and upcoming free-parking discovery, only if the official data sources
  define the relevant field semantics

### P2: Future

- Historical occupancy collection and forecasting
- Traffic and weather integration
- Operations and data-quality dashboard

### Out of scope for MVP

- Parking reservations or guaranteed lots
- Payments, coupon purchases, or enforcement information
- Turn-by-turn driving navigation
- User accounts, except where later required for favorites or a saved vehicle type
- Predictive availability
- Non-HDB car parks without equivalent data coverage
- Operator tools and analytics dashboards

## 8. Functional Requirements

### 8.1 Search and discovery

| ID | Priority | Requirement | Acceptance criteria |
| --- | --- | --- | --- |
| FR-01 | P0 | Users can search by destination or address. | A valid search resolves to a map location and displays car parks within a 500-metre geodesic radius of the destination; an unresolved or ambiguous search presents a clear recovery message. |
| FR-02 | P0 | Users can search around their current location. | With permission, the product centers results on the current location; without permission, destination search remains usable and the denial is explained without repeated prompts. |
| FR-03 | P0 | The map and result list remain synchronized. | Moving the map offers or performs a search for the visible area; selecting a map marker highlights the corresponding list item and vice versa. |
| FR-04 | P0 | Each result communicates its proximity to the destination. | Results show straight-line geodesic distance from the resolved destination in metres and are recalculated when the destination or search area changes. |

### 8.2 Availability and car park details

| ID | Priority | Requirement | Acceptance criteria |
| --- | --- | --- | --- |
| FR-05 | P0 | Users can see total and available lots by supported lot type. | Car, heavy-vehicle, motorcycle, and other returned lot types are labeled separately; numeric strings are displayed as validated numbers. |
| FR-06 | P0 | Users can understand occupancy at a glance. | The product shows available lots and an occupancy indicator derived from valid total and available values; invalid or missing values are not presented as zero. |
| FR-07 | P0 | Users can inspect car park operating details. | The detail view shows all available fields for address, car park type, parking system, night parking, decks, height restriction, and basement status. Short-term and free-parking conditions are shown only when their interpretation is documented by an approved official source. Missing or unverified fields are labeled as unavailable rather than inferred. |
| FR-08 | P0 | Users can assess data freshness. | Every availability display includes the source update time and a fresh, stale, or unavailable state. Stale and unavailable states are conveyed with text or icons, not color alone. |

### 8.3 Filters and compatibility

| ID | Priority | Requirement | Acceptance criteria |
| --- | --- | --- | --- |
| FR-09 | P0 | Users can show only car parks with available lots. | Enabling the filter excludes car parks with zero valid available lots for the selected lot type; unknown availability is shown separately and is not treated as available. |
| FR-10 | P0 | Users can filter by parking conditions. | Users can filter by night parking, selected vehicle type, and surface, underground, or multi-storey type; active filters remain visible and can be cleared. Free-parking filters are unavailable unless the official field semantics have been verified. |
| FR-11 | P0 | Vehicle type compatibility excludes unusable recommendations. | A car park without a lot type compatible with the selected car, heavy vehicle, or motorcycle type does not appear as a recommendation. The exclusion reason is available to the user. |
| FR-12 | P1 | Users can save a vehicle type. | A saved car, heavy vehicle, or motorcycle type is applied automatically to future searches and can be changed or disabled. |

### 8.4 Recommendations and alternatives

| ID | Priority | Requirement | Acceptance criteria |
| --- | --- | --- | --- |
| FR-13 | P0 | The product ranks viable car parks for the selected destination. | Recommended results satisfy active hard constraints and are ordered using distance, available lots, and occupancy. The UI identifies the recommended option and summarizes the factors supporting it. |
| FR-14 | P0 | Full car parks have actionable alternatives. | Selecting a car park with zero availability displays compatible options with valid availability within 500 metres of the destination; if none exist, the product states that no verified alternative was found and allows filters or area to be changed. |
| FR-15 | P0 | Uncertain data does not appear certain. | Results with stale or unavailable availability are labeled and are not ranked above otherwise comparable results with fresh, verified availability. |

### 8.5 Favorites and alerts

| ID | Priority | Requirement | Acceptance criteria |
| --- | --- | --- | --- |
| FR-16 | P1 | Users can favorite a car park. | A user can add or remove a favorite and view the saved list on subsequent visits when persistence is available. |
| FR-17 | P1 | Users can define an availability alert threshold. | The user can set a minimum available-lot count for a favorite, receives at most one notification per threshold crossing, and can disable the alert. |
| FR-18 | P1 | Users can discover free parking by time when official field semantics are available. | If the approved official sources define the free-parking values and boundary behavior, results distinguish car parks with free parking now from those where a free period begins soon, evaluated using the current Singapore Standard Time. Otherwise, this feature is not implemented. The product does not accept an intended arrival time. |

## 9. Data and Business Rules

### Source data

- Use `carpark_number` from the
  [Car Park Availability](https://data.gov.sg/datasets?formats=API&sort=relevancy&resultId=d_ca933a644e55d34fe21f28b8052fac63)
  API as the real-time car park identifier.
- Join real-time data to
  [HDB Car Park Information](https://data.gov.sg/datasets/d_23f946fa557947f93a8043bbef41dd09/view)
  using `car_park_no`.
- Use `carpark_info` for total and available lots by lot type.
- Use `update_datetime` as the availability source timestamp.
- Convert HDB SVY21 `x_coord` and `y_coord` values to WGS84 latitude and
  longitude for map input.

### Availability states

- **Fresh:** The latest successful source timestamp is no more than two expected
  refresh intervals old.
- **Stale:** Data exists but its timestamp is older than the fresh threshold.
- **Unavailable:** No valid availability value exists or no successful response
  has been received.

The source's recommended one-minute polling interval is the initial expected
refresh interval. The threshold must be configurable if source behavior changes.

### Distance and time rules

- "Nearby" and recommendation distance use straight-line geodesic distance
  between WGS84 coordinates, not walking or driving-route distance.
- Officially documented time-aware conditions are evaluated using the current
  Singapore Standard Time (SGT, UTC+08:00). The MVP does not accept a
  user-specified arrival time.

### Observed parking-condition values

The bundled HDB dataset contains `short_term_parking` and `free_parking` fields.
Observed `short_term_parking` values are `WHOLE DAY`, `7AM-7PM`,
`7AM-10.30PM`, and `NO`. Observed `free_parking` values are `NO`,
`SUN & PH FR 7AM-10.30PM`, and `SUN & PH FR 1PM-10.30PM`.

These labels provide operating-condition content but not a complete data
dictionary. Only the linked Car Park Availability and HDB Car Park Information
sources, including official documentation linked from those pages, are approved
to define their meaning. If those sources do not define public-holiday behavior,
boundary times, or other required semantics, the product must not use the
affected fields for filtering, recommendation, eligibility, or calculated
status.

### Data contract and discrepancies

- Treat the published OpenAPI specification as the primary contract.
- Validate representative live responses against the contract.
- Accept unknown optional fields that do not change existing semantics.
- Treat missing required fields, incompatible types, and structural changes as
  schema validation errors.
- For a discrepancy, retain a sanitized response example, document the affected
  fields and user impact, and confirm the behavior before updating the parser,
  tests, and local schema together.
- Report discrepancies to data.gov.sg as the owner responsible for the API and
  its specification, and track each discrepancy until the upstream
  specification is corrected.
- Do not silently substitute invalid or missing numeric values with zero.

### Partial and failed data

- A car park with unmatched static or real-time data remains processable but is
  labeled as incomplete and is excluded from recommendations when required
  compatibility or availability cannot be verified.
- After an API failure, the last successful data may remain visible only with
  its original update time and current freshness state.

## 10. Experience Requirements

### Information hierarchy

Each result should prioritize:

1. Car park name or address
2. Available lots and lot type
3. Distance to destination
4. Compatibility or restriction status
5. Data update time and freshness
6. Relevant operating conditions

### Required states

The experience must define and test:

- Loading
- No destination match
- No nearby car parks
- No results after filters
- Full car park
- Incompatible car park
- Stale availability
- Unavailable availability
- Partial static or real-time record
- Map or upstream API failure
- Location permission denied

### Accessibility

- Target WCAG 2.2 Level AA for web experiences.
- Do not communicate availability, occupancy, or freshness by color alone.
- Support keyboard navigation, visible focus, accessible map alternatives, and
  screen-reader labels for controls and status changes.

## 11. Non-Functional Requirements

Implementation and validation of these acceptance criteria will be tracked as a
separate cross-cutting workstream from the P0 functional requirements. NFR-01,
NFR-06, and NFR-08 are intentionally deferred until the P0 functional
implementation is complete. The remaining non-functional requirements must be
implemented alongside P0. All criteria must pass before the MVP is released
unless an exception is explicitly approved.

| ID | Area | Delivery timing | Requirement | Acceptance criteria |
| --- | --- | --- | --- | --- |
| NFR-01 | Performance | Post-P0, pre-MVP | Meet the result-loading target in Section 6 under expected load and use progressive loading when map or detail data arrives separately. | Under expected launch load, 95% of valid searches display nearby results within 3 seconds, excluding confirmed third-party outages. Filter and sort interactions update visible results within 500 milliseconds. |
| NFR-02 | Reliability | During P0 | Preserve the last successful availability response through transient failures and never present cached data without its timestamp and freshness state. | During transient API failures, the last successful response remains available with its original timestamp and correct stale status. No cached response is labeled fresh after exceeding the configured freshness threshold. |
| NFR-03 | Data quality | During P0 | Monitor API validation failures, stale records, unmatched identifiers, invalid coordinates, and impossible lot counts such as available lots exceeding total lots. | Every API response is schema-validated. Invalid lot counts and coordinates are excluded from recommendations and recorded for monitoring. Alerts trigger when matching rates fall below the approved baseline or schema validation failures exceed 1% over 15 minutes. |
| NFR-04 | Privacy | During P0 | Request precise location only in response to a user action, explain its purpose, and do not retain it beyond the active experience without explicit consent. | Location access is requested only after a user action. Precise location is not persisted after the active session without explicit consent and does not appear in application logs or analytics events. |
| NFR-05 | Security | During P0 | Use supported authentication for upstream services, keep credentials out of clients and logs, and sanitize recorded discrepancy samples. | All network traffic uses HTTPS with TLS 1.2 or later. Credentials remain server-side and are absent from client bundles and logs. No unresolved critical or high-severity vulnerability is permitted at release without documented approval. |
| NFR-06 | Compatibility | Post-P0, pre-MVP | Support Chromium-family browsers for the MVP. | All P0 journeys pass in the latest two stable versions of Google Chrome and Microsoft Edge on desktop and the latest stable Chrome on Android. The interface remains usable from a viewport width of 360 pixels upward. |
| NFR-07 | Observability | During P0 | Record search outcomes, upstream failures, response freshness, filter usage, recommendation availability, and alternative selection without collecting unnecessary personal data. | Search failures, upstream failures, stale data, schema validation errors, and unmatched identifiers emit structured telemetry. Alerts identify the affected dependency and environment, and telemetry contains no unnecessary personal or precise-location data. |
| NFR-08 | Accessibility | Post-P0, pre-MVP | Provide an inclusive experience that meets the accessibility requirements in Section 10. | All P0 journeys meet WCAG 2.2 Level AA, support keyboard-only operation, expose meaningful screen-reader labels, and never communicate availability or freshness through color alone. |

## 12. Analytics and Validation Plan

### Product events

- Destination search submitted, resolved, failed, or ambiguous
- Current location requested, granted, or denied
- Search results displayed
- Filter applied or cleared
- Car park detail viewed
- Recommendation selected
- Full car park viewed
- Alternative displayed and selected
- Fresh, stale, or unavailable data displayed
- No-results state displayed

### Pre-launch validation

1. Conduct task-based usability testing for destination search, vehicle-type
   filtering, stale-data interpretation, and full-car-park recovery.
2. Validate recommendation constraints against a curated acceptance dataset.
3. Run contract tests using sanitized representative API responses.
4. Test coordinate conversion against known HDB car park locations.
5. Test all required empty, partial, stale, failure, and permission states.

## 13. Assumptions and Dependencies

### Assumptions

- HDB identifiers remain sufficiently stable to join static and real-time data.
- The majority of real-time records continue to match HDB information; the
  current sample matches 2,008 of 2,016 records.
- Availability updates are generally published near the recommended one-minute
  polling interval.
- Map and geocoding providers accept WGS84 latitude and longitude.
- Google Maps has sufficient budget and quota for expected MVP usage.
- For the MVP, "nearby" means a car park whose WGS84 coordinate is within a
  500-metre geodesic radius of the resolved destination. When current location
  is used as the search origin, the same radius applies around that location.
- Users will select a vehicle type of car, heavy vehicle, or motorcycle when it
  affects parking compatibility; the product will not request vehicle height,
  weight, or other physical attributes.

### Dependencies

- [Car Park Availability](https://data.gov.sg/datasets?formats=API&sort=relevancy&resultId=d_ca933a644e55d34fe21f28b8052fac63)
  API and its OpenAPI specification
- [HDB Car Park Information](https://data.gov.sg/datasets/d_23f946fa557947f93a8043bbef41dd09/view)
  dataset
- Google Maps for maps and geocoding, plus browser geolocation for optional
  current-location search
- SVY21-to-WGS84 conversion with verified reference points
- Notification capability and persistence for P1 alerts
- Product analytics and operational monitoring

## 14. Risks and Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| Availability data is delayed or unavailable. | Users may drive toward a full car park. | Show source time and freshness prominently, lower the rank of uncertain data, preserve last-known data honestly, and present alternatives. |
| OpenAPI and live responses diverge. | Parsing or availability display may fail. | Run contract validation, alert on breaking differences, retain sanitized examples, and update schema, parser, and tests together. |
| Static and real-time records do not match. | Details or availability may be incomplete. | Monitor match rate, label incomplete records, and exclude unverifiable options from recommendations. |
| Coordinate conversion is incorrect. | Car parks appear in the wrong location. | Verify known reference points and reject coordinates outside the expected geographic bounds. |
| Ranking favors proximity over usability. | A nearby but impractical option may be recommended. | Apply hard compatibility rules first and validate rankings with realistic scenarios and user testing. |
| Location data creates privacy concerns. | Users may deny access or lose trust. | Make location optional, request it contextually, minimize retention, and provide destination search as a complete fallback. |

## 15. Release Plan and Gates

### Phase 1: Data foundation

- Verify identifier matching and coordinate conversion.
- Implement source validation, freshness classification, and failure handling.
- Establish data-quality monitoring.

### Phase 2: Search and decision experience

- Deliver map, destination and current-location search, result list, availability,
  details, and required states.
- Deliver filters, compatibility rules, recommendations, and alternatives.

### Phase 3: Validation and MVP launch

- Complete accessibility, usability, acceptance-dataset, contract, performance,
  and failure-state validation.
- Confirm that all P0 requirements and Section 6 launch targets are met or have
  an explicitly approved exception.

### MVP release gates

- All P0 acceptance criteria pass.
- No known issue can recommend a car park that violates an active hard
  constraint.
- Freshness and unavailable states are present across every availability view.
- Contract and coordinate-conversion tests pass against approved fixtures.
- Monitoring exists for upstream failures, stale data, validation errors, and
  unmatched identifiers.
- Product owner, design, engineering, QA, privacy, and accessibility reviewers
  approve release.

## 16. Appendix

### Supporting documents

- [Smart Parking Navigator Idea](IDEATION.md)
- [PRD Document Template in 2025: How to Write Effective Product Requirements](https://www.kuse.ai/blog/tutorials/prd-document-template-in-2025-how-to-write-effective-product-requirements)

### PRD maintenance

This PRD is the cross-functional source of truth for product intent, scope, and
outcomes. It should be updated when user research, source-data behavior, scope,
or launch targets change. Implementation architecture, API client design, and
detailed ranking algorithms should be maintained in separate product and
technical specifications.
