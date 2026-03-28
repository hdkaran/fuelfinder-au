# ADR 001 — Use polling + manual refresh instead of SignalR for MVP

**Status:** Accepted
**Date:** 2026-03-28
**Deciders:** FuelFinder AU core team

---

## Context

During a fuel shortage crisis, drivers need near-real-time information about which petrol stations have fuel available. Ideally the app would push live updates to every connected client the moment a new crowdsourced report comes in.

SignalR (ASP.NET Core) would enable server-push WebSocket connections for true real-time delivery. However:

- SignalR requires a persistent connection layer and scale-out backplane (typically Azure SignalR Service — ~$50–200/month).
- It adds meaningful operational complexity: connection state management, backplane configuration, client reconnect logic, and fallback transports.
- During a crisis, **time-to-launch matters more than perfect real-time UX**. Every hour of delay building non-critical infrastructure is an hour the app isn't helping drivers.
- RTK Query (our frontend data layer) has built-in `pollingInterval` support that covers the gap acceptably for an MVP.

---

## Decision

We will **not** use SignalR in the MVP. Instead:

- **Station list / nearby query:** RTK Query `pollingInterval: 120_000` (2-minute refresh)
- **Summary stats:** RTK Query `pollingInterval: 60_000` (1-minute refresh)
- **Manual refresh:** A `Refresh` button in the UI calls RTK Query's `refetch()` for immediate update on demand.

SignalR will be added post-launch as a well-scoped enhancement once the app is stable and user demand justifies it.

---

## Consequences

**Positive:**
- Simpler infrastructure — no Azure SignalR Service required.
- Faster time-to-launch.
- Less client-side connection management code.
- No scale-out backplane needed for MVP traffic levels.

**Negative / Trade-offs:**
- Data can be up to 2 minutes stale in normal operation.
- Users who don't notice the auto-refresh or manual button may act on slightly old data.
- Poll-based approach creates predictable but consistent load on the API (every connected client polls every 2 minutes).

**Mitigation:**
- The `lastReportMinutesAgo` field on `StationDto` gives users a visible freshness signal.
- The `Refresh` button is prominent in the UI so users know they can force an immediate update.
- API responses are cached in Redis, so polling load is absorbed without hitting the database on every request.

---

## Review trigger

Revisit this decision when: (a) average concurrent users exceed 5,000, (b) user feedback consistently calls out stale data as a problem, or (c) Azure SignalR Service becomes available at lower cost tiers.
