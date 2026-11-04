# AI usage log

This project was built with an AI coding assistant (Claude Code). This log records where AI wrote
SQL or documents, what was asked, what came back, and what was checked or corrected afterwards.
Every entry has a "Human review" line: AI output is a draft until the developer has read and understood it.

## 1. Admin insight SQL (audit triggers, KPI view, usage procedure)

**Prompt (summary):** Write SQL Server objects for an electric-utility admin back office, added through EF Core
migrations with `migrationBuilder.Sql(...)`:
- an `AuditLogs` table and AFTER UPDATE triggers on `Bills`, `Customers` and `Outages` that write one row per changed
  `Status` (and `Customers.RatePlanId`), comparing the `inserted` and `deleted` tables;
- a view `vw_AdminKpis` (active customers, unpaid balance, overdue balance, revenue collected this month,
  open outages, average kWh last full month);
- a stored procedure `usp_GetMonthlyUsage @Months int = 12` (total kWh and total billed per month).

**Output summary:** Three triggers using `IF UPDATE(col)` and a join of `inserted` to `deleted` on `Id`; a single-row
view built from scalar subqueries; a procedure that groups `Bills` by the first day of the `PeriodStart` month.

**What was checked or changed after the first draft:**
- Enums are stored as text with the C# names (`Unpaid`, `InProgress`), not the snake_case JSON names, so every
  comparison in the SQL uses the stored names.
- The database runs in UTC, but the rest of the app stores Eastern wall-clock time. The triggers, the view and the
  procedure convert with `SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'Eastern Standard Time'`, which handles
  daylight saving time. (`GETDATE()` would have given UTC on Azure.)
- Triggers only insert a row when the value really changed (`ISNULL(i.col,'') <> ISNULL(d.col,'')`), so saving an
  unchanged row writes nothing.
- EF Core must be told about triggers (`ToTable(tb => tb.HasTrigger(...))`), otherwise `SaveChanges` fails because EF
  uses an `OUTPUT` clause.
- `ChangedBy` reads `SESSION_CONTEXT(N'AppUser')`; a connection interceptor sets it each time a connection opens.
  It is always set (to NULL when nobody is signed in) so a pooled connection never keeps the previous user.
- The month window in the procedure ends at the current month, and `@Months` is validated (1 to 60) in the API.
- Tested against a local SQL Server (LocalDB) copy of the database: paying a bill as a customer, changing a rate plan
  and suspending an account as an admin each produced the expected audit rows with the right user; the KPI view and
  usage procedure returned plausible numbers for the seeded bills (still to be cross-checked by hand);
  `usage-trend?months=0` returns 400.

**Human review:** Pending. Isaiah to read each SQL object in `Migrations/*_AdminInsight.cs` and be able to explain it.

## 2. Release documentation (README, architecture map, BRD, demo script)

**Prompt (summary):** Write the v2.0 README (live links, demo logins, architecture diagram, tech stack, security model,
setup, testing, deployment, skills demonstrated), a file-by-file architecture document with the request flow and a data
model diagram, a business requirements document, and a five-minute client demo script.

**Output summary:** Four Markdown documents with Mermaid diagrams, written from the code as it stands at v2.0.

**What was checked or changed:**
- Facts were taken from the repository (endpoint list, file names, settings) rather than from memory.
- The demo script was checked against the live data: Jane has one unpaid bill and Robert Chen's outage is in the
  "reported" state, as the script says.
- Anything the demo changes permanently (paying a bill) is called out in `docs/DEMO.md`.
- The BRD is a draft written after the system was built. It describes what v2.0 does; it is not a record of requirements
  gathered from the client.

**Human review:** Pending. Isaiah to read the documents and correct anything he would not say himself.

## 3. Chat assistant (code and instructions)

**Prompt (summary):** Add a chat assistant that knows the signed-in customer, gives visitors a general helper, files outage reports only after a confirmation click, and never lets the model see another customer's data. Add a corner button, per-caller rate limits and tests.

**Output summary:** A chat service with two prompts (customer and visitor), an account snapshot builder, a fixed tool list, a rate limiter, an Angular corner widget, and unit tests for the snapshot builder and the tool executor.

**What was checked or changed:**
- Tests cover the snapshot (including a failing service and text flattening), the tool list, and the confirmation rules; all pass.
- The assistant was tried by hand as a visitor and as a customer, including a request for another customer's data, which it refused.
- The model, endpoint and key are the team's own deployment; no secret is in git.

**Human review:** Pending. Isaiah to read `CustomerPrompt.cs`, `VisitorPrompt.cs` and `Program.cs` in the chat service and be able to explain the two kinds of caller and the limits.

## 4. Project 3 documentation (README, architecture, demo script, next steps)

**Prompt (summary):** Rewrite the README and docs for the microservices version on Kubernetes: short and readable, with a list of what is left to do.

**Output summary:** A shorter README, a new architecture document, a new demo script, a next-steps page, and small fixes to the Kubernetes notes, chat notes and BRD.

**What was checked or changed:** Service names, URLs, ports, image names and test counts were taken from the repository and the running project, not from memory.

**Human review:** Pending. Isaiah to read the documents and correct anything the team would not say.

## 5. Chat fixes after a friend tested it

**Prompt (summary):** A tester found the outage flow asked for a typed "I confirm" and then showed a confirmation button too, the button text came through as "undefined", and the assistant sounded blunt about payments.

**Output summary:** The confirmation details are now sent in the format the browser reads. Tapping "Yes, send it" files the report directly, so it no longer depends on the model repeating exact words. The outage instructions and tool description now say the button is the only confirmation. The payment reply and the energy-saving tips were softened and limited.

**What was checked or changed:** Server and front-end tests still pass. The outage flow needs one more hand test on the cluster after the new images are deployed.

**Human review:** Pending. Isaiah to read the changed outage steps in `CustomerPrompt.cs` and the confirmed-report block in `Program.cs`.
