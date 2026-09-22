# NexusOXP Phase Control

This file tracks the actual repo state and keeps the current unfinished phase locked before moving to later work.

## Current status

Current active phase: Phase 17
Status: Phase 0 through Phase 16 are substantially implemented. Phase 17 through Phase 20 are partially complete and should remain open.
Reason: the app now builds cleanly and includes the core project/team/task/comment/notification/activity/file/API flows from the plan, but the PDF plan still calls for a broader architecture upgrade, deeper security validation, real automated tests, and final responsive UI polish.

Release-ready state: not final. The project is functional, but portfolio-grade completion still requires the remaining open work below.

---

## Phase checklist

### Phase 1 - Project foundation
- [x] ASP.NET Core MVC app created
- [x] app config created
- [x] solution/project structure established

### Phase 2 - Data layer foundation
- [x] DbContext added
- [x] Identity model integration created
- [x] SQL Server dependency configured

### Phase 3 - User & authentication
- [x] Identity setup enabled
- [x] login/register flow implemented
- [x] authorization policies created
- [x] admin roles seeded

### Phase 4 - Core domain models
- [x] Project model defined
- [x] Task model defined
- [x] Team model defined
- [x] comments, notifications, activity log modeled

### Phase 5 - Project management
- [x] project CRUD implemented
- [x] project dashboard implemented
- [x] project filters and permissions implemented

### Phase 6 - Task management
- [x] task CRUD implemented
- [x] board and list views implemented
- [x] status transitions implemented

### Phase 7 - Team management
- [x] team creation and membership flow implemented
- [x] manager/member relations created

### Phase 8 - Activity and notifications
- [x] activity logging added
- [x] notifications system added
- [x] task/project lifecycle events hooked in

### Phase 9 - Search and dashboard
- [x] dashboard analytics implemented
- [x] search flow implemented
- [x] overview pages created

### Phase 10 - Account and profile
- [x] account settings implemented
- [x] profile update flow implemented
- [x] password changes implemented

### Phase 11 - UI and app flow
- [x] layout and navigation implemented
- [x] views created for CRUD pages
- [x] status messaging added

### Phase 12 - Core feature completion
- [x] main project features are present in the codebase
- [x] core flows appear implemented
- [ ] runtime behavior still needs verification

### Phase 13 - Startup, database, and runtime validation
- [x] dotnet restore runs successfully
- [x] dotnet build runs successfully
- [x] SQL Server instance is available
- [x] database exists / migrations applied
- [x] app starts on configured localhost port
- [x] login works
- [x] dashboard loads
- [x] project/task/team flows work
- [x] no startup/runtime errors remain

### Phase 14 - Deployment hardening
- [x] production-safe configuration review
- [x] secure env values check
- [x] deployment prep

### Phase 15 - Security and cleanup
- [x] permission review
- [x] role validation
- [x] hidden edge-case checks

### Phase 16 - QA and bugfix pass
- [x] end-to-end regression test pass
- [x] fix runtime issues
- [x] confirm business flow works

### Phase 17 - Architecture upgrade
- [x] shared services exist for activity logging, project access, file storage, and email
- [x] shared planning/date validation added
- [ ] move project CRUD business logic into ProjectService
- [ ] move task lifecycle/notification logic into TaskService
- [ ] move team membership logic into TeamService
- [ ] keep controllers thin and orchestration-focused

### Phase 18 - Validation and security
- [x] Identity password policy configured
- [x] authorization policies and role checks configured
- [x] anti-forgery protection used on MVC write actions
- [x] file extension and size validation implemented
- [x] API project date validation aligned with MVC validation
- [x] API task update validates assignees
- [ ] add a focused authorization audit for every edit/delete/download/API endpoint
- [ ] add production logging/error handling review

### Phase 19 - Testing
- [x] smoke test project exists
- [x] DTO/default workflow tests exist
- [x] file upload validation tests exist
- [x] planning/date validation tests exist
- [ ] convert smoke tests into proper unit/integration tests
- [ ] add ProjectServiceTests after service extraction
- [ ] add TaskServiceTests after service extraction
- [ ] add authentication and authorization tests

### Phase 20 - Professional UI and deployment
- [x] responsive Bootstrap layout exists
- [x] main dashboard/project/task/team/admin views exist
- [x] Azure and Cloudflare deployment notes exist
- [ ] complete responsive QA on desktop/tablet/mobile
- [ ] final visual polish pass across CRUD, board, profile, admin, and error pages
- [ ] final deployment dry run with production environment values

---

## Rules

1. Do not start a later phase while the current phase is not green.
2. If a phase fails validation, return to that phase and fix it before advancing.
3. Only move to the next phase after all checkboxes in the current phase are complete.

---

## Commands to run for Phase 13

From a real local PowerShell terminal:

```powershell
cd "C:\Users\Dorart\source\repos\NexusOXP\NexusOXP"

dotnet restore
dotnet build .\NexusOXP.sln
dotnet ef database update
dotnet run --project .\NexusOXP.csproj
```

Expected URLs from the launch profile:
- https://localhost:7178
- http://localhost:5281
