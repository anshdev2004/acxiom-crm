# AcxiomCRM — Enterprise Customer Relationship Management System

AcxiomCRM is a full-featured, secure, and production-ready Customer Relationship Management (CRM) application built with **C#**, **ASP.NET Core 8 MVC with Razor Views**, **ASP.NET Core Identity**, and **Entity Framework Core**.

It was built strictly adhering to the requirements in the assignment specification, with special emphasis on **Section 17 (Mandatory Requirements)**. The project features clean 3-tier service architecture, server-side data authorization, dual database provider support (SQLite for zero-friction evaluation and SQL Server for enterprise deployment), automated audit trails, Chart.js visual dashboards, and comprehensive acceptance test coverage.

---

## 🌟 Key Features

1. **Role-Based Access Control (RBAC) & Scoping**:
   - **Admin**: Complete system-wide visibility, user & role management, account unlock/password resets, append-only audit trail inspection, and organization-wide reports.
   - **Manager**: Team-level visibility scoped via hierarchical `ManagerId` relationship, team pipeline analytics, team follow-ups, and conversion reports.
   - **SalesExecutive**: Strictly scoped to assigned customers, leads, opportunities, follow-ups, and activities.
   - *Public Registration*: Strictly hard-assigned to `SalesExecutive` on the server; cannot be escalated by client tampering.

2. **Customer Management**:
   - Normalized unique email and unique phone constraints at both service and database levels.
   - Full CRUD, search across multiple fields (name, email, phone, company), filtering, sorting, and pagination.
   - 360-degree customer view detailing associated opportunities, follow-ups, and historical interaction timeline.

3. **Lead Life-Cycle & Atomic Conversion**:
   - Full status workflow: `New` &rarr; `Contacted` &rarr; `Qualified` &rarr; `Unqualified` &rarr; `Converted` &rarr; `Lost`.
   - **Atomic Transactional Lead Conversion**: Converts qualified leads into customer records and optional initial opportunities inside an isolated `IDbContextTransaction`. Prevents duplicate conversions and maintains full relationship integrity.

4. **Opportunity & Sales Pipeline**:
   - Stages: `Qualification`, `Proposal`, `Negotiation`, `Won`, `Lost`.
   - Business validations: Amount strictly $> 0$, probability $0 \le p \le 100$, expected close date cannot be in the past for active deals.
   - Automatic calculation of weighted pipeline: $\text{Weighted Amount} = \frac{\text{Amount} \times \text{Probability}}{100}$.
   - Automatic recording of `OutcomeDate` upon reaching `Won` or `Lost` stage for accurate monthly revenue analytics.

5. **Follow-ups & CRM Activities**:
   - Scheduling with client and server rejection of past dates for planned items (`FollowUpDate >= Today`).
   - Statuses: `Planned`, `Completed`, `Missed`, `Cancelled`.
   - Activities: `Call`, `Meeting`, `Email`, `Task` with durations and outcome logs.

6. **Interactive Dashboard & Reports**:
   - KPI cards for Customers, Leads, Opportunities, Pipeline value, and Pending Follow-ups.
   - **Chart.js** data visualizations:
     1. Lead Status Breakdown (Doughnut chart)
     2. Opportunity Pipeline by Stage (Bar chart)
     3. Monthly Won Sales Performance (Line chart)
   - Date range filters: `Today`, `This Week`, `This Month`, and `Custom Range`.
   - 8 comprehensive tabular reports with CSV export capabilities.

7. **Append-Only Security Audit Logging**:
   - Records user, timestamp, action (`Login`, `Logout`, `FailedLogin`, `Lockout`, `Create`, `Update`, `Delete`, `LeadConversion`, `RoleChange`, `PasswordReset`), entity name, record ID, result, client IP, and sanitized old/new values.
   - Strict sanitization filter: **Never logs passwords, password hashes, reset tokens, or auth cookies**.
   - Admin-only viewing interface.

8. **Dual Database Provider Support**:
   - Ready out of the box with zero external configuration using **SQLite** (`acxiomcrm.db`).
   - Seamlessly switches to **Microsoft SQL Server** by changing `"DatabaseProvider": "SqlServer"` in `appsettings.json`.

---

## 🛠️ Prerequisites & Environment Setup

- **Operating System**: Windows 10/11 (or macOS / Linux)
- **.NET SDK**: .NET 8.0 SDK (8.0.426 or later)
- **Tools**: Windows PowerShell

If running on a system where .NET 8 SDK was installed in a custom user location (e.g. `C:\Users\ishik\.dotnet`):

```powershell
# Set environment variables for the current PowerShell session:
$env:DOTNET_ROOT = "C:\Users\ishik\.dotnet"
$env:PATH = "C:\Users\ishik\.dotnet;" + $env:PATH

# Verify .NET SDK installation:
dotnet --version
# Expected output: 8.0.426 (or 8.0.x)
```

---

## 🚀 Quick Start (Running the Application)

### 1. Build the Solution
```powershell
cd C:\Users\ishik\.gemini\antigravity\scratch\AcxiomCRM
dotnet build
```

### 2. Run the Acceptance Tests
The solution includes a test suite covering the 14 mandatory acceptance scenarios defined in Section 17.19 of the assignment document:
```powershell
dotnet test
```
*All 15 tests pass with 100% success.*

### 3. Launch the Web Application
```powershell
cd C:\Users\ishik\.gemini\antigravity\scratch\AcxiomCRM\AcxiomCRM
dotnet run
```
The application will automatically:
1. Ensure the database is created (`EnsureCreatedAsync` / migrations).
2. Seed initial roles (`Admin`, `Manager`, `SalesExecutive`).
3. Seed default accounts, sample customers, leads, opportunities, follow-ups, and activities.
4. Host the application at: `http://localhost:5000` (or `https://localhost:5001`).

Open your web browser and navigate to `http://localhost:5000`.

---

## 👥 Seed Credentials & Role Hierarchy

For immediate evaluation, the database is pre-seeded with representative accounts for all three primary roles:

| Role | Email / Username | Password | Organizational Scope |
| :--- | :--- | :--- | :--- |
| **Admin** | `admin@acxiomcrm.local` | `Admin@12345!` | Full system access, all records, user management, audit logs, all reports |
| **Manager** | `manager@acxiomcrm.local` | `Manager@12345!` | Team CRM records (subordinates linked via `ManagerId`), team pipeline & conversion reports |
| **SalesExecutive** | `sales1@acxiomcrm.local` | `Sales@12345!` | Only assigned customers, leads, deals, follow-ups, and activities |
| **SalesExecutive** | `sales2@acxiomcrm.local` | `Sales@12345!` | Only assigned customers, leads, deals, follow-ups, and activities |

> **Pro Tip**: The Login screen (`/Account/Login`) includes one-click **"Quick Fill Demo Credentials"** buttons to instantly log in as Admin, Manager, or Sales Executive during a live presentation.

---

## 🏗️ Architecture & Code Organization

The application follows ASP.NET Core architectural best practices:

```
AcxiomCRM/
│
├── AcxiomCRM/                     # Main Web Application Project
│   ├── Controllers/               # MVC Razor Controllers
│   │   ├── AccountController.cs   # Auth, Login, Register, Lockout, Password Reset
│   │   ├── CustomersController.cs # Customer CRUD, Search, Filter, Sort, History
│   │   ├── LeadsController.cs     # Lead CRUD, Status workflow, Conversion modal
│   │   ├── OpportunitiesController.cs # Deals, Sales Pipeline, Win/Loss tracking
│   │   ├── FollowUpsController.cs # Follow-up scheduling, calendar/list, rescheduling
│   │   ├── ActivitiesController.cs# Interactions: Calls, Meetings, Emails, Tasks
│   │   ├── DashboardController.cs # KPI metrics, Chart.js feeds, date range filtering
│   │   ├── ReportsController.cs   # 8 Reports + CSV Export engine
│   │   ├── UsersController.cs     # Admin User & Role Administration, Unlock, Reset
│   │   └── AuditLogsController.cs # Admin Append-only Audit Log viewer
│   │
│   ├── Controllers/Api/           # RESTful API Controllers with DTOs
│   │   ├── AuthApiController.cs
│   │   ├── CustomersApiController.cs
│   │   ├── LeadsApiController.cs
│   │   ├── OpportunitiesApiController.cs
│   │   ├── FollowUpsApiController.cs
│   │   └── ReportsApiController.cs
│   │
│   ├── Data/                      # EF Core DbContext, Migrations & Seeding
│   │   ├── ApplicationDbContext.cs
│   │   ├── ApplicationDbContextSeed.cs
│   │   └── Migrations/
│   │
│   ├── Models/                    # Domain Entities & Value Objects
│   │   ├── ApplicationUser.cs     # Extended IdentityUser with ManagerId & IsActive
│   │   ├── ApplicationRole.cs     # Extended IdentityRole
│   │   ├── Customer.cs
│   │   ├── Lead.cs
│   │   ├── Opportunity.cs
│   │   ├── FollowUp.cs
│   │   ├── Activity.cs
│   │   ├── AuditLog.cs
│   │   └── Enums/CrmConstants.cs  # Centralized string constants for stages, statuses, roles
│   │
│   ├── Services/                  # Core Business Logic Layer (Shared by MVC & API)
│   │   ├── Interfaces/            # Service contracts
│   │   ├── UserScopeService.cs    # Authorizes record ownership & accessible user IDs
│   │   ├── AuditLogService.cs     # Append-only audit logger with secret sanitization
│   │   ├── CustomerService.cs     # Customer domain rules & unique constraint checks
│   │   ├── LeadService.cs         # Status transitions & atomic transactional conversion
│   │   ├── OpportunityService.cs  # Deal validation, weighted pipeline computation
│   │   ├── FollowUpService.cs     # Date validation & status management
│   │   ├── ActivityService.cs     # Activity interaction logging
│   │   ├── DashboardService.cs    # Aggregates KPI cards & Chart.js datasets
│   │   ├── ReportService.cs       # Multi-criteria reporting & CSV generation
│   │   └── UserService.cs         # Administrative user management
│   │
│   ├── ViewModels/                # Strongly-typed models for Razor views
│   ├── DTOs/                      # Data Transfer Objects for API contracts
│   ├── Views/                     # Razor Views styled with Bootstrap 5
│   └── wwwroot/                   # Static assets (Bootstrap, Chart.js, Icons, App JS)
│
└── AcxiomCRM.Tests/               # Automated Acceptance Tests
    ├── AcceptanceScenariosTests.cs# End-to-end acceptance tests for Section 17.19
    └── TestHelpers.cs
```

---

## 🔒 Security Architecture Highlights

1. **Defense-in-Depth Record Scoping (`UserScopeService`)**:
   - Authorization is enforced at the C# service layer before querying database records.
   - An unauthorized user attempting to access a customer or opportunity ID belonging to another sales rep receives `null` or `403 Forbidden`, regardless of whether the request comes from MVC or REST API.
2. **Server-Side Validation**:
   - Razor client-side unobtrusive validation provides immediate visual feedback.
   - Server-side validation via DataAnnotations and service layer rules guarantees that bypassed client validation (e.g., via Postman or modified DOM) is completely rejected.
3. **Transaction Safety**:
   - Lead-to-Customer conversion wraps customer creation, lead status update, optional opportunity creation, activity history recording, and audit logging into a single EF Core atomic transaction (`BeginTransactionAsync`). If any step fails, the entire transaction rolls back.
4. **Credential & Secret Protection**:
   - Passwords use ASP.NET Core Identity's default PBKDF2 password hasher with cryptographic salt.
   - Passwords and reset tokens are strictly filtered out in `AuditLogService.SanitizeValues()` to prevent any sensitive credential from appearing in audit logs.
5. **Anti-Forgery & Safe State Changes**:
   - All state-altering MVC forms include `@Html.AntiForgeryToken()`.
   - Logout is strictly handled via `HTTP POST` with anti-forgery validation to prevent cross-site logout attacks.

---

## 📡 REST API Examples

The application exposes REST APIs using DTOs, returning standard HTTP status codes (`200 OK`, `201 Created`, `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`).

### 1. Authenticate (Obtain Identity Cookie)
```powershell
$body = @{
    email = "sales1@acxiomcrm.local"
    password = "Sales@12345!"
    rememberMe = $false
} | ConvertTo-Json

Invoke-RestMethod -Method Post `
    -Uri "http://localhost:5000/api/auth/login" `
    -ContentType "application/json" `
    -Body $body `
    -SessionVariable crmSession
```

### 2. Fetch Customers (Scoped to User Role)
```powershell
Invoke-RestMethod -Method Get `
    -Uri "http://localhost:5000/api/customers?page=1&pageSize=10" `
    -WebSession $crmSession
```

### 3. Create a New Customer
```powershell
$newCust = @{
    customerName = "Apex Innovations"
    email = "contact@apexinnovations.local"
    phone = "+1-555-8833"
    company = "Apex Corp"
    status = "Active"
} | ConvertTo-Json

Invoke-RestMethod -Method Post `
    -Uri "http://localhost:5000/api/customers" `
    -ContentType "application/json" `
    -Body $newCust `
    -WebSession $crmSession
```

### 4. Fetch Pipeline Summary
```powershell
Invoke-RestMethod -Method Get `
    -Uri "http://localhost:5000/api/reports/pipeline" `
    -WebSession $crmSession
```

---

## 🎬 Project Demonstration Script (Viva / Evaluation)

Follow these steps for a live project demonstration:

### Step 1: Public Registration & Default Role Assignment (1 minute)
1. Open `http://localhost:5000` in an incognito window.
2. Click **Register** on the top right navigation bar.
3. Fill in:
   - Full Name: `Alice Jenkins`
   - Email: `alice@example.com`
   - Password: `Password@12345!`
   - Department: `Direct Sales`
4. Submit the registration form.
5. Explain to the evaluator: *“Notice there was no option to select Admin or Manager. The server strictly assigns the `SalesExecutive` role by default.”*

### Step 2: Sales Executive Scoped Access (2 minutes)
1. Log in as `sales1@acxiomcrm.local` (password: `Sales@12345!`).
2. Navigate to **Customers**. Point out that only customers owned by `sales1` or explicitly assigned to `sales1` are listed.
3. Attempt to navigate directly to `/Users` or `/AuditLogs`. Notice that access is denied (`403 Forbidden` / Access Denied).

### Step 3: Lead Life-Cycle & Atomic Conversion (3 minutes)
1. Navigate to **Leads**.
2. Click **Create New Lead**:
   - Lead Name: `Starlight Logistics`
   - Email: `info@starlightlogistics.local`
   - Phone: `+1-555-4422`
   - Source: `Website`
   - Priority: `High`
   - Status: `Qualified`
3. Click **Convert Lead**:
   - In the modal dialog, check **Create Opportunity as well?**.
   - Opportunity Name: `Starlight Enterprise Fleet Deal`
   - Amount: `$35,000`
   - Stage: `Qualification`
   - Expected Close Date: Select a date 30 days in the future.
4. Click **Confirm Conversion**.
5. Explain to the evaluator: *“This conversion executed in a single atomic database transaction. Starlight Logistics is now a Customer, the lead status is marked Converted, and an active Opportunity is created in the sales pipeline.”*

### Step 4: Opportunity & Pipeline Rules (2 minutes)
1. Navigate to **Opportunities**.
2. Point out the new opportunity `Starlight Enterprise Fleet Deal`.
3. Click **Edit**:
   - Change Probability to `120`. Show that the server rejects values over 100.
   - Change Amount to `-500`. Show that negative amounts are rejected.
   - Change Stage to `Won`.
4. Save the changes. Explain to the evaluator: *“Marking the stage as Won automatically stamped the `OutcomeDate` to today's date, which feeds into our monthly sales performance analytics.”*

### Step 5: Follow-Up Validation (1 minute)
1. Navigate to **Follow-ups**.
2. Click **Schedule Follow-Up**.
3. Choose yesterday's date for a new Planned follow-up.
4. Attempt to save. Show that both client validation and server-side validation reject dates earlier than today for planned follow-ups.
5. Choose tomorrow's date at 10:00 AM and save. The follow-up appears in the **Upcoming** tab.

### Step 6: Dashboard & Visual Charts (2 minutes)
1. Navigate to **Dashboard**.
2. Point out the real-time KPI metrics at the top:
   - Total Customers, Total Leads, Open Pipeline Value, Pending Follow-ups.
3. Point out the three dynamic **Chart.js** visualizations:
   - Lead Status breakdown doughnut chart.
   - Pipeline stages bar chart.
   - Monthly sales outcome line chart.
4. Toggle date filters: click **This Week**, then **This Month**, demonstrating instant chart and KPI re-aggregation.

### Step 7: Manager & Admin Oversight (2 minutes)
1. Log out and log in as `admin@acxiomcrm.local` (password: `Admin@12345!`).
2. Navigate to **Audit Logs**:
   - Show the detailed chronological audit trail showing the Lead Conversion, Opportunity Won update, and logins.
   - Point out that passwords and secrets are completely absent from the log values.
3. Navigate to **Users**:
   - Demonstrate managing roles, toggling account active/inactive state, and unlocking locked accounts.
4. Navigate to **Reports**:
   - Open **Sales Pipeline Report**.
   - Click **Export to CSV** to demonstrate instant CSV reporting with preserved security scoping.

---

## 🧪 Acceptance Test Suite (15 Automated Tests)

The automated test project `AcxiomCRM.Tests` validates all mandatory acceptance criteria from Section 17.19 using an in-memory/isolated ASP.NET Core test server:

| Scenario | Test Name | Tested Requirement |
| :--- | :--- | :--- |
| **01** | `Scenario01_UnauthenticatedAccessBlocked` | Unauthenticated requests redirected to login; APIs return JSON 401 |
| **02** | `Scenario02_RegisterAndLoginUser` | Registration assigns `SalesExecutive` server-side |
| **03** | `Scenario03_CustomerValidationClientAndServer` | Invalid customer email/phone format rejected |
| **04** | `Scenario04_DuplicateCustomerRejectedByServer` | Duplicate customer email or phone rejected on server |
| **05** | `Scenario05_OpportunityAmountMustBeGreaterThanZero` | Opportunity amount $\le 0$ rejected |
| **06** | `Scenario06_OpportunityProbabilityRange` | Probability $> 100$ or $< 0$ rejected |
| **07** | `Scenario07_OpportunityExpectedCloseDateCannotBeInPast` | Past expected close date rejected for active deals |
| **08** | `Scenario08_FollowUpDateCannotBeEarlierThanToday` | Past date rejected for new/planned follow-up |
| **09** | `Scenario09_SalesExecutiveRoleScoping` | Sales rep cannot read or modify another rep's customers |
| **10** | `Scenario10_ManagerTeamAccess` | Manager can access subordinate records in their team |
| **11** | `Scenario11_AdminAccessToSecurityAndAudit` | Admin has access to security settings, roles, and audit trail |
| **12** | `Scenario12_AuditTrailGenerated` | Data modifications generate structured audit log entries |
| **13** | `Scenario13_RestApiCustomersAuthorizedResponse` | REST API `/api/customers` returns authorized JSON data |
| **14** | `Scenario14_DashboardKpisAndCharts` | Dashboard calculates KPIs and formats Chart.js datasets |
| **15** | `Scenario15_LockoutOnRepeatedFailedLogins` | Multiple failed logins trigger account lockout |

To execute the test suite:
```powershell
dotnet test
```

---

## 📄 License & Academic Integrity

This project is created for educational and academic project evaluation purposes. All architectural patterns, code, and documentation were written to provide a clean, maintainable reference implementation of an enterprise CRM system.
