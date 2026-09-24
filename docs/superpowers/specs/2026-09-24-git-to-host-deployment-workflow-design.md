# Raaya Git Deploy — Git-to-Host Deployment Workflow Design

## Purpose

Raaya Git Deploy must remove the manual FileZilla-like workflow of pulling Git changes, locating every changed file by hand, and uploading those files one-by-one to hosting.

The primary product path becomes:

**Open Repository → Update/Pull → Detect Pending Deployment → Review → Auto Queue → Dry Run → Deploy → Record Successful HEAD → History**

This is the product north star for both Desktop and Mobile. Desktop performs local Git and transport operations. Mobile remains a secure companion that reviews and starts agent-controlled deployment without receiving raw host credentials.

## Success Criteria

A normal deployment should not require the user to manually search for files changed by a pull.

For a configured repository and server profile, the application must answer four questions automatically:

1. What commit is currently deployed to this server profile?
2. What commits and paths are pending between that deployed commit and the current local HEAD?
3. Exactly what remote operations will occur if deployment starts now?
4. Did deployment finish successfully enough to advance the deployed baseline?

Multiple Git pulls before deployment must accumulate into one pending deployment range. A failed or partial deployment must never advance the last-successful deployed HEAD.

## Current Architecture to Reuse

The existing architecture already provides useful boundaries:

- `IGitRepositoryService` is the Git read/source-of-truth boundary and already supports repository context, commit metadata, commit changes and comparisons.
- `IGitMutationService` owns explicit Git mutation operations.
- Deployment planning already has `DeploymentQueueItem`, `DeploymentPlanner`, `DeploymentPlan`, `DeploymentExecutor`, server profiles and `IRemoteTransport`.
- SFTP and FTP/FTPS implementations remain Infrastructure concerns.
- `IDeploymentHistoryStore` already persists deployment results, but its entry model is currently too small for repository/branch/from-SHA/to-SHA tracking.
- Android companion already uses repository/profile IDs and an agent API boundary rather than transport credentials.

The new workflow should compose these capabilities instead of creating a second deployment system.

## Core Design Decisions

### 1. Last Successful Deployment Is the Primary Baseline

The canonical pending deployment range is:

`last_successfully_deployed_head(repository, profile) .. current_local_head`

The most recent Pull is useful context, but it is not the deployment baseline.

This prevents lost changes when the user pulls more than once before deploying.

The deployed baseline is profile-specific because production, staging and another host may be at different commits.

### 2. Successful Deployment Metadata Extends History

Deployment history must record enough context to reconstruct the baseline:

- repository identity/path,
- branch,
- `FromHead`,
- `ToHead`,
- server profile ID/name,
- started/completed time,
- overall result,
- per-item result.

The last successful history entry for a repository/profile can then provide the deployed HEAD. A dedicated cached baseline store may be added later for performance, but history remains the auditable source of truth.

A partial, failed, cancelled or blocked deployment does not advance `ToHead` as the successful baseline.

### 3. Git Update Is Safe and Explicit

Desktop adds an explicit Update/Pull workflow.

Initial safety policy:

- require a valid configured upstream,
- refuse automatic Pull when the working tree contains uncommitted changes,
- use fast-forward-only Pull/update behavior,
- stop on divergence/conflict rather than creating an implicit merge,
- capture `OldHead` before update and `NewHead` after update,
- refresh repository context immediately after update.

This keeps the tool predictable and avoids surprising repository mutations.

`OldHead..NewHead` is shown as the latest update event, while deployment planning still uses `LastDeployedHead..CurrentHead`.

### 4. Pending Deployment Snapshot

Introduce a domain-level pending deployment snapshot that contains:

- repository identity/path,
- branch,
- deployed/base SHA,
- current/target SHA,
- commits in the range,
- Git changes in the range,
- queue candidates,
- warnings.

Git changes map into deployment intent as follows:

- Added → Upload/Create
- Modified → Upload/Overwrite
- Deleted → Remote Delete candidate
- Renamed → Upload new path + delete old path, unless a transport-specific safe rename capability is explicitly introduced later

Delete/rename-derived remote deletes remain destructive and require Dry Run visibility and explicit confirmation.

### 5. Queue Has Three Explicit Sources

Deployment Queue merges items from three sources while preserving provenance:

1. **GitDetected** — paths derived from the pending Git range.
2. **GeneratedRule** — configured generated/build output paths such as `public/build/**`.
3. **Manual** — explicit Add File/Add Folder items.

The same normalized local path must not become duplicate upload operations simply because it is present in more than one source.

Generated rules do not automatically execute repository code. Build commands remain explicit user-triggered commands. Rules only describe which generated outputs should join the deployment after they exist locally.

### 6. Initial Baseline Requires Explicit User Choice

When a repository/profile has no successful deployment history, the application must not guess what is already deployed.

The user chooses one explicit onboarding action:

- **Mark current HEAD as existing deployed baseline** — for an already-synchronized server.
- **Choose a Git commit as baseline** — when the host corresponds to a known older commit.
- **Deploy from an explicitly selected base** — for a first managed deployment.

This action is auditable and profile-specific.

### 7. Dry Run Is the Deployment Contract

Deployment execution must operate from a reviewed plan/preview, not from an ad-hoc file list.

Dry Run displays:

- repository and branch,
- from/to SHA,
- target server profile and transport,
- remote root,
- upload/create/delete operations,
- source of each queue item,
- destructive-operation warnings,
- missing generated outputs or invalid mappings.

The execution request should reference the reviewed plan/preview identity where possible, so the plan cannot silently change between review and deployment.

### 8. Desktop UX Centers on Update & Deploy

The primary Desktop workspace should stop feeling like a generic FTP client.

The dominant view presents:

- repository + branch + current HEAD,
- selected server profile,
- last deployed HEAD,
- pending commit count,
- pending file count,
- `Update / Pull` action,
- pending commits/files master-detail review,
- `Prepare Deployment` / `Dry Run`,
- `Deploy` after successful preview.

Advanced Git review, Terminal, Commands, Servers and History remain available, but they support this workflow rather than competing with it.

The user should normally move through:

**Update → Review Pending → Dry Run → Deploy**

without opening a filesystem picker for every changed file.

### 9. Mobile Uses Agent-Issued Plans, Not Arbitrary Host Paths

Mobile mirrors deployment intent but does not become an FTP/SSH client.

Preferred Mobile path:

**Repository → Profile → Pending Range → Review → Dry Run → Confirm → Progress/Result → History**

The agent calculates Git ranges and queue plans. Mobile receives safe metadata:

- repository/profile IDs,
- base/target SHA,
- commit summaries,
- allowed pending items,
- preview/plan ID,
- deployment progress/result.

As the companion API evolves, arbitrary client-provided path lists should be replaced by an agent-issued pending plan ID plus optional selection of item IDs from that authorized plan. Mobile must not be able to invent arbitrary filesystem paths for deployment.

Raw SSH keys, SFTP/FTP/FTPS passwords, Git credentials and transport-specific connection secrets never cross the Mobile API boundary.

### 10. Android Platform Security

Portable `RaayaGitDeploy.Android.Core` remains platform-neutral.

When the Android application/platform host is created:

- session/API credentials are protected by Android Keystore,
- existing `IPlatformAccessTokenProtector` / protected token store boundaries are reused,
- no host deployment credential is stored on device,
- offline/rate-limited/recoverable states remain explicit to the UI.

## Suggested Core Contracts

Names may be refined during implementation, but responsibilities should stay separated.

### Git update boundary

A dedicated update service is preferable to overloading staging/commit mutation APIs:

- capture current context/HEAD,
- validate clean working tree and upstream,
- perform fast-forward-only update,
- return before/after HEAD and update result.

### Pending deployment service

A coordinator composes:

- Git repository service,
- deployment history,
- deployment rules,
- queue normalization,
- deployment planner.

It produces the pending snapshot without performing remote mutation.

### Deployment history evolution

`DeploymentHistoryEntry` expands to include repository and Git range metadata. Migration/reader compatibility should tolerate older history entries that lack new fields and present them as legacy entries rather than crashing.

## Failure and Recovery Rules

- Dirty working tree blocks automated Pull; it does not discard local work.
- Pull divergence/conflict stops update and leaves deployment baseline unchanged.
- Missing base commit/history produces an explicit baseline-required state.
- Missing generated output is a Dry Run warning/error according to rule configuration.
- Upload failure blocks dependent delete operations according to existing deployment safety rules.
- Cancellation does not advance deployed HEAD.
- Partial failure is recorded in History but does not become the successful baseline.
- Successful deployment records the exact `ToHead` that was reviewed in Dry Run.
- If local HEAD changes after Dry Run, deployment must require a refreshed preview or otherwise prove that the reviewed plan still matches the intended target SHA.

## Auto Deploy Workstream Compatibility

`feat/git-host-auto-deploy` remains independent.

This design intentionally creates reusable concepts for that workstream:

- pending deployment range,
- last successful deployed HEAD,
- deployment rules,
- immutable/reviewed deployment plan,
- transport-neutral execution and history.

No blind merge is implied. When the workstreams meet, shared contracts should be reconciled explicitly and the independent Auto Deploy policy/scheduling behavior should remain separate from the interactive Desktop/Mobile workflow.

## Delivery Order

1. Extend deployment history with repository/branch/from/to SHA and successful-baseline semantics.
2. Add safe Desktop Git Update/Pull boundary with clean-tree + fast-forward-only policy.
3. Build pending deployment range (`last deployed HEAD..current HEAD`) and commit/file summary.
4. Auto-populate Deployment Queue from Git changes.
5. Add initial baseline onboarding for repository/profile pairs.
6. Merge generated-rule and manual items with source provenance.
7. Make Dry Run target-SHA aware and invalidate stale previews when HEAD changes.
8. Rework Desktop workspace around Update → Pending → Dry Run → Deploy.
9. Expose pending-plan summaries through the companion agent API.
10. Evolve Mobile workflow from raw path requests to agent-issued plan/item IDs.
11. Create Android platform host and implement Keystore-backed session protection.
12. Integrate with the independent Auto Deploy workstream only through reviewed shared contracts.

## Acceptance Scenarios

### Desktop — multiple pulls before deployment

Given production was successfully deployed at commit `A`,
when the user pulls to `B` and later pulls again to `C` without deploying,
then Pending Deployment must show the full `A..C` commit/file range.

### Desktop — failed deployment

Given the pending range is `A..C`,
when deployment fails or is cancelled,
then production remains recorded at `A` and the pending range remains `A..C`.

### Desktop — successful deployment

Given Dry Run reviewed target `C`,
when all required deployment operations succeed,
then history records `A → C` and the next pending range is empty at HEAD `C`.

### Desktop — generated build output

Given Git changes require a production build and a configured rule includes `public/build/**`,
when the user explicitly runs the build and prepares deployment,
then existing generated files join the queue as `GeneratedRule` items without needing manual file-by-file selection.

### Mobile — least-privilege deployment

Given an authorized repository/profile has a pending agent-issued plan,
when Mobile reviews and confirms that plan,
then the agent performs deployment using server-side credentials and Mobile receives progress/result without receiving host secrets.

### Safety — stale preview

Given Dry Run reviewed target SHA `C`,
when local/agent HEAD changes to `D` before deployment,
then the previous preview cannot silently deploy as though it still represented the current pending state; a new preview is required or the plan must remain explicitly pinned to `C`.
