You are a senior product designer specializing in enterprise logistics software, control-center dashboards, data visualization, and interactive 3D experiences.

Design a premium product called:

# LogiSphere

## 3D Warehouse & Global Logistics Network

Your responsibility in this task is DESIGN ONLY.

Do not generate implementation code.
Do not focus on React components.
Do not make technical architecture decisions unless they directly affect the UX.

## Product concept

LogiSphere is a logistics operations control center that lets an operator progressively drill down through three levels:

Global Logistics Network
→ Vietnam Logistics Operations
→ Individual Order Journey

The experience must feel like one continuous logistics world instead of three unrelated dashboard pages.

The 3D visualization is functional, not decorative.

It must help users understand:

* where shipments are moving
* which hubs are active
* which routes are delayed
* warehouse utilization
* current shipment position
* fulfillment progress
* estimated arrival
* operational problems

---

# Target viewport

Design desktop-first for:

1440 × 900

The primary experience will be used on large desktop monitors in a logistics operations environment.

The application should remain usable down to approximately 1280px width.

---

# Main application shell

Keep the following layout persistent while the 3D context changes.

## Left sidebar

Approximately 210px wide.

Navigation:

Overview
Shipments
Warehouses
Network
Fleet
Orders
Analytics
Alerts
Settings

Use minimal icons with concise labels.

Overview is selected by default.

---

## Top navigation

Approximately 64px high.

Include:

* LogiSphere logo
* global shipment/order search
* region selector
* notifications
* user profile

Search must support:

Order ID
Tracking ID
Customer
Warehouse
Hub

---

# Main information hierarchy

The 3D scene must always remain the dominant visual element.

Do not make this look like a conventional analytics dashboard containing dozens of cards.

Prioritize:

1. 3D operational visualization
2. Current operational context
3. Critical KPIs
4. Alerts
5. Detailed historical data

---

# Global Overview

The default state displays an interactive dark 3D globe.

Place compact KPI cards above the scene.

KPIs:

Active Shipments
Delivered Today
Delayed Orders
Warehouse Utilization
On-time Delivery
Active Hubs

The globe must contain logistics hubs including:

Ho Chi Minh City
Hanoi
Singapore
Shanghai
Tokyo
Dubai
Rotterdam
Los Angeles

Represent:

Hub
Airport
Seaport
Warehouse

using a coherent visual language.

Connect hubs using curved shipping routes.

Use animated particles to communicate shipment movement.

Route states:

Normal
Potential Delay
Delayed

Active hubs should gently pulse.

Hovering a hub displays a compact information tooltip.

Example:

Ho Chi Minh City Hub

Active Shipments: 2,842
Inbound: 624
Outbound: 712
Delayed: 31
Capacity: 82%

Include a contextual action:

Enter Vietnam Network

---

# Global right panel

Create a contextual operations panel approximately 320px wide.

Display:

Global Network Status
Active Hubs
Delayed Routes
Operational Alerts
Top Logistics Corridors

Avoid displaying excessive charts.

Focus on information requiring immediate operational attention.

---

# Global to Vietnam transition

Clicking Vietnam or Ho Chi Minh City must feel like drilling into the network.

Design the experience around this transition:

Global globe
→ camera focuses on Vietnam
→ globe scale increases
→ global environment fades
→ Vietnam logistics network appears
→ HCM warehouse becomes the primary visual anchor

Avoid a hard page transition.

The user should feel that they entered the country from the global network.

Include:

Back to Global Network

but keep it visually subtle.

---

# Vietnam Operations View

Transform the main visualization into an isometric logistics digital twin.

Show:

HCM Central Warehouse
Da Nang Sorting Hub
Hanoi Distribution Hub
Airport
Seaport
Domestic logistics routes

Include moving trucks between important nodes.

The HCM warehouse should contain visible operational areas:

Receiving
Storage
Picking
Packing
Outbound

Use simplified low-poly or premium miniature-style 3D geometry.

Do not make the experience look like a video game.

---

# Warehouse interactions

Each warehouse zone must support:

Default state
Hover state
Selected state
Alert state

Hovering a zone highlights the relevant physical area.

Display floating labels such as:

PICKING
43 active orders
Avg. 8.4 min

Clicking a zone updates the contextual right panel.

---

# Vietnam right panel

When HCM Central Warehouse is selected, display:

Operational status
Warehouse utilization
Active orders
Inbound today
Outbound today
Average processing time
Alerts

Example:

Warehouse Utilization
82%

Active Orders
3,420

Inbound
1,280

Outbound
1,145

Alerts:

Packing Zone
91% utilization

Dock #04
Truck delayed by 24 minutes

---

# Shipment selection

Users must be able to select a shipment through:

3D shipment marker
Search
Shipment list
Warehouse activity

Selecting a shipment transforms the visualization into Order Journey mode.

---

# Order Journey

Visualize the complete lifecycle of one shipment.

Example route:

Singapore
→ HCM Warehouse
→ Da Nang Sorting Hub
→ Hanoi Hub
→ Last-mile Vehicle
→ Customer

Represent each logistics stage physically.

Use:

Completed state
Current state
Upcoming state

The current shipment position must be visually obvious.

Animate a parcel or shipment indicator moving through the route.

The visualization should answer:

Where is my shipment?
Where did it come from?
Where is it going next?
How much of the journey is complete?

---

# Order details panel

Display:

Order ID
Status
Origin
Destination
Delivery progress
Current location
Carrier
ETA
Delay risk
Distance remaining

Example:

ORD-2026-00184

IN TRANSIT

Singapore
→ Hanoi, Vietnam

Progress
68%

Current Location
Da Nang Sorting Hub

Carrier
GHN

ETA
Aug 15 · 14:30

Delay Risk
Low

Distance Remaining
748 km

---

# Bottom operational timeline

Create a collapsible bottom panel.

Default global state:

show recent logistics events.

Selected order state:

show order tracking timeline.

Example:

Order Created
→ Packed
→ Dispatched
→ Sorting Hub
→ Out for Delivery
→ Delivered

Clearly indicate:

completed
current
future

Clicking a timeline step should reveal event metadata.

---

# Visual direction

Use a sophisticated enterprise logistics aesthetic.

Primary direction:

Dark neutral environment
Near-black surfaces
Subtle elevated panels
Light transparency
Soft glass effects
High contrast typography
Muted borders
Restrained blue/cyan operational accent

Use warning colors only when semantically necessary.

Do not cover the UI in neon.

Avoid excessive gradients.

Avoid generic AI-generated SaaS aesthetics.

Avoid rounded cards everywhere.

Use radius selectively.

The result should feel somewhere between:

a modern logistics operations center
a digital twin system
an enterprise command center
a premium transportation technology product

Do not copy an existing company.

---

# Typography

Use strong information hierarchy.

Dashboard titles should be concise.

Numbers and KPIs should be easy to scan.

Technical labels can use small uppercase typography where appropriate.

Avoid oversized marketing-page typography inside the operational dashboard.

---

# Motion

Motion should communicate logistics activity.

Use animation for:

Shipment movement
Route activity
Hub pulses
Vehicle movement
Camera navigation
Panel state changes
Order journey progress

Do not animate elements simply for decoration.

Transitions should be smooth and controlled.

Global → Vietnam:
approximately 1.2–1.8 seconds.

Panel state changes:
approximately 200–350ms.

---

# UX principles

The user should always know:

Where am I?
What am I looking at?
What is selected?
What requires attention?
How do I return to the previous level?

Do not hide critical navigation inside mysterious gestures.

Maintain breadcrumbs/context such as:

Global Network

>

Vietnam

>

ORD-2026-00184

when appropriate.

---

# Required design states

Produce clear designs for:

1. Global Overview
2. Global hub hover
3. Vietnam Operations
4. Warehouse selected
5. Warehouse zone selected
6. Order selected
7. Order Journey
8. Order timeline expanded
9. Operational alert
10. Empty/loading/error states

---

# Final objective

The final experience should visually communicate:

Global scale
→ operational infrastructure
→ individual package

The strongest impression should be:

“I can see an entire logistics network and drill down all the way to one package without leaving the same operational environment.”
