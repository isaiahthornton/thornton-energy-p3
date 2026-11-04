# Business Requirements Document: Thornton Energy Management Platform

| | |
|---|---|
| **Project** | Thornton Energy: customer portal and admin back office (v2.0) |
| **Client** | Demonstration for ConEd, business unit P&R |
| **Developer** | Isaiah Thornton |
| **Status** | v2.0 requirements, delivered. Project 3 now runs the same application as microservices on Kubernetes (see the addendum at the end) |
| **Note** | Drafted with AI assistance (see `docs/AI-LOG.md`); to be reviewed by the developer before use with a client |

## 1. Purpose

Thornton Energy is a fictional electric utility used to demonstrate a modern, secure, cloud-hosted management platform. The platform gives customers self-service access to their account and gives utility staff one place to run billing, meters, outages and customer care, with the operational insight a manager needs.

## 2. Business problem and goals

Utilities often handle billing questions, outage reports and account changes through phone calls and spreadsheets. This is slow, hard to audit, and gives managers little visibility.

| # | Goal | How we will know |
|---|---|---|
| G1 | Reduce routine contact-centre calls by letting customers self-serve | Customers can see and pay bills, view usage, switch plans and report outages online |
| G2 | Make billing accurate and consistent | Bills are priced by the server from the customer's rate plan; the browser cannot set an amount |
| G3 | Give operations a live picture | A dashboard shows balances, overdue money, revenue, open outages and usage trends |
| G4 | Make changes accountable | Every status or rate-plan change records what changed, when and by whom |
| G5 | Respond to outages faster | Outages are queued by status and shown on a map |
| G6 | Protect customer data | A customer can only ever reach their own records; staff actions are role-restricted |
| G7 | Keep running cost at zero for the demonstration | Only free-tier cloud services are used |

## 3. Stakeholders and users

| Role | Description | Main needs |
|---|---|---|
| **Customer** | A residential account holder with an online login | View and pay bills, see usage, compare and switch rate plans, report and follow outages |
| **Utility administrator** | Customer-care and operations staff | Manage accounts, meters, readings, rate plans, billing, the outage queue and inbox; see KPIs and audit history |
| **Prospective customer / visitor** | Anyone on the public website | See rate plans, contact the utility, register for online access |
| **Client sponsor** | The organisation evaluating the platform | See a secure, understandable, maintainable system |

## 4. Scope

**In scope**

- Public website: landing page with rate plans, about page, contact form, login, and registration against an existing account number
- Customer portal and admin back office (section 5)
- Web API, relational database, cloud hosting, automated tests, documentation

**Out of scope for v2.0**

- Real payment processing (paying a bill marks it paid; no card data is collected)
- Real-time smart-meter data feeds
- Email or SMS notifications
- Time-of-use pricing by hour of day (the estimate uses a flat rate)
- Multi-language support and a native mobile app

## 5. Functional requirements

### 5.1 Accounts and access

| ID | Requirement |
|---|---|
| FR-1 | A person with an existing account number and matching email can register a login with a password of at least 6 characters |
| FR-2 | Users sign in with email and password and receive a time-limited token (60 minutes); wrong credentials are refused |
| FR-3 | Suspended or closed accounts cannot sign in, and the refusal is enforced on the server |
| FR-4 | Two roles exist: customer and administrator; each sees only its own portal |
| FR-5 | Users are signed out automatically when the token expires |

### 5.2 Customer portal

| ID | Requirement |
|---|---|
| FR-10 | The dashboard shows balance due, next due date, an overdue warning, current-month usage versus last month, any open outage, and a map of the service location |
| FR-11 | The usage page shows a 12-month chart of kWh calculated from meter readings |
| FR-12 | The billing page lists the customer's bills, filterable by paid, unpaid and overdue, and opens a bill with its line items |
| FR-13 | A customer can pay their own unpaid bill once; the payment date is set by the server |
| FR-14 | A customer can report an outage (address pre-filled) and follow its status |
| FR-15 | A customer can compare rate plans using an estimate based on their own average usage, and switch plan |

### 5.3 Admin back office

| ID | Requirement |
|---|---|
| FR-20 | The dashboard shows active customers, unpaid balance, overdue balance, revenue collected this month, open outages, and average usage last month, plus a monthly kWh and billing chart |
| FR-21 | Admins can search customers and change an account's status |
| FR-22 | Admins can run billing for a month; each bill is priced from the customer's rate plan and due 21 days after the period ends; only one bill per customer per period |
| FR-23 | Admins can record meter readings; a reading must be later than, and not lower than, the previous reading for that meter |
| FR-24 | Admins can create, edit and delete rate plans; a plan with customers on it cannot be deleted |
| FR-25 | Admins work an outage queue (reported, in progress, resolved); the system stamps the resolution time |
| FR-26 | An outage map shows each outage at the customer's service location, coloured by status, with a popup of details |
| FR-27 | An activity page lists the latest status and rate-plan changes with the old value, new value, time and user |
| FR-28 | Admins can read contact-form messages and mark them read |

### 5.4 Business rules

| ID | Rule |
|---|---|
| BR-1 | Energy charge = kWh used x the plan's price per kWh; service charge = the plan's monthly fee; each amount is rounded to cents (half rounds away from zero); the total is their sum |
| BR-2 | A bill is **overdue** when it is unpaid and its due date has passed. Overdue is calculated, never stored |
| BR-3 | A bill can be paid only once |
| BR-4 | A customer can only see, pay or change their own data |
| BR-5 | Only administrators can create bills, record readings, manage rate plans, change account status, resolve outages and read the inbox |
| BR-6 | A customer's outage report is always saved under their own account and always starts as "reported" |
| BR-7 | Timestamps (reported, resolved, sent, paid) come from the server clock in the utility's time zone (Eastern), not from the browser |

## 6. Non-functional requirements

| Area | Requirement |
|---|---|
| **Security** | Passwords stored as salted hashes; signed tokens validated on every request; role and ownership checks on the server; login, register and contact form rate limited; secrets kept out of source control; HTTPS only |
| **Auditability** | Status and rate-plan changes recorded by the database itself, including the signed-in user |
| **Reliability** | Unexpected errors return a clean message and a correlation id; two people editing the same bill cannot silently overwrite each other |
| **Performance** | Typical pages load within a few seconds once the database is awake (the free database tier pauses when idle and can take 30 seconds or more on the first request) |
| **Usability** | Works on a laptop browser; plain, consistent layout; clear error messages |
| **Maintainability** | Layered code (controllers, services, repository), automated unit and integration tests, documented architecture |
| **Cost** | $0: free tiers only |
| **Compatibility** | Current versions of Chrome, Edge, Firefox and Safari |

## 7. Data requirements

Customers, rate plans, meters, meter readings, bills, outages, contact messages, user logins and an audit log, as described in `docs/ARCHITECTURE.md`. Demo data is seeded into an empty database. No real personal data is used or stored.

## 8. Assumptions and constraints

- Free-tier hosting only; the database pauses when idle
- Each customer has one online login and one rate plan at a time
- The utility operates in one time zone (Eastern)
- Demo data only; the platform is not intended to hold real customer data without further hardening

## 9. Risks

| Risk | Impact | Mitigation |
|---|---|---|
| Free database is asleep when the demo starts | First page is slow | Open the site a few minutes beforehand; connection timeout is set high |
| Stolen token | Account access until expiry | Short lifetime, HTTPS, token kept in session storage only |
| Demo actions change data (for example paying a bill) | Next demo shows different numbers | `docs/DEMO.md` lists which steps are permanent and how to avoid or reverse them |
| Free-tier limits (quota, cold starts) | Slow or unavailable service | Keep a recorded walkthrough as a fallback |

## 10. Acceptance criteria

The release is accepted when:

1. A customer can sign in, see their balance and usage, pay a bill once, switch rate plan and report an outage.
2. An administrator can see the KPI dashboard and the activity trail, resolve an outage and see it change colour on the map.
3. A customer is refused (403) when asking for another customer's data or an admin function, and anonymous callers are refused (401).
4. Automated server and client tests pass, and the live site and API respond.
5. Running cost is $0.

## 11. Future opportunities

- A customer-facing assistant that answers billing and outage questions from the utility's FAQ and the caller's own account data
- Email and SMS outage alerts
- Real payment integration
- Hourly time-of-use pricing and smart-meter ingestion
- Continuous integration and deployment pipeline

## Addendum: Project 3 (microservices on Kubernetes)

The requirements above still describe what the application does. Project 3 changes how it is built and hosted:

- The API is split into six services, each with its own database, behind one router. See `docs/ARCHITECTURE.md`.
- A chat assistant (Wattson) helps visitors with general questions and signed-in customers with their own account. It may only act with the customer's own login, and any outage report needs a confirmation click.
- Hosting is a small Kubernetes cluster on Azure that runs only while needed, so the "$0, free tiers only" line above no longer applies to this deployment.
- Public HTTPS is not set up yet, so the "HTTPS only" line above is not met on the cluster. See `docs/NEXT-STEPS.md`.
