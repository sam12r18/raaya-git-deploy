# Raaya Git Deploy — Product Architecture

**Status:** Approved architecture baseline  
**Date:** 2026-09-14  
**Repository:** `sam12r18/raaya-git-deploy`

---

## 1. Product Definition

Raaya Git Deploy is a specialized Windows desktop application for reviewing Git changes, validating them, approving them, and deploying selected files to a remote server.

The primary product scenario is AI-assisted development:

```text
AI Agent / Codex / Developer
          ↓
   Repository Changes
          ↓
      Git Discovery
          ↓
       Review
          ↓
     Diff Inspection
          ↓
 Terminal / Test / Build
          ↓
      Human Approval
          ↓
   Deployment Planning
          ↓
    Mapping Preview
          ↓
       Dry Run
          ↓
      SFTP Deploy
          ↓
 Deployment Snapshot
```

The product is therefore a **Git Review & Deploy Workbench**, not a generic FTP/SFTP client.

---

## 2. Architecture Goals

The architecture must provide:

1. Fast inspection of repository changes.
2. Strong support for AI-generated changes.
3. Clear separation between review approval and deployment selection.
4. Predictable Git behavior based on `git.exe`.
5. Safe remote path planning and preview.
6. Explicit handling of destructive operations.
7. Repository-aware terminal access.
8. Secure local credential storage.
9. Transport extensibility.
10. Contributor-friendly boundaries suitable for a public GitHub repository.

The architecture must avoid premature complexity such as a full plugin runtime or CI/CD engine unless those needs become concrete.

---

## 3. Scope Model

Product architecture is **capability-oriented**, not version-oriented.

We do not define permanent boundaries such as:

```text
V1 = fixed feature list
V2 = fixed feature list
V3 = fixed feature list
```

Instead, each capability may have one of these lifecycle states:

### Core Baseline

Capabilities fundamental to the product identity and architecture.

### Active Development

Capabilities currently being implemented.

### Extension Candidate

Capabilities that fit the product direction and may be implemented by maintainers or contributors.

### Experimental

Ideas that require validation before becoming a stable product contract.

Release versions remain useful for packaging and distribution, but they describe shipped builds rather than architectural limits.

---

## 4. Technology Direction

### 4.1 Desktop Shell

- .NET 10
- WinUI 3
- Windows App SDK
- MVVM
- CommunityToolkit.Mvvm
- Microsoft.Extensions.DependencyInjection

The UI should be dark-first, dense, professional, and developer-oriented.

The visual interaction model should feel closer to modern development tools such as GitHub Desktop or VS Code than to a classic administrative desktop application.

### 4.2 Git Integration

Git operations use the installed `git.exe`.

`git.exe` is the source of truth for repository behavior.

Reasons:

- Matches the developer's actual Git environment.
- Avoids semantic divergence from normal CLI Git.
- Makes troubleshooting reproducible.
- Allows incremental support for advanced Git operations.
- Works naturally with repository-specific configuration.

Git output consumed by the application should use machine-readable formats whenever available.

Example:

```text
git status --porcelain=v2 -z
```

### 4.3 Diff Rendering

The application requires a capable code diff experience.

The diff layer may use WebView2 as an isolated rendering host for a specialized editor/diff component where this creates a materially better review experience.

The rest of the application should remain native.

### 4.4 Integrated Terminal

Terminal sessions use Windows ConPTY.

The terminal is:

- interactive,
- repository-aware,
- initialized with the active repository as working directory,
- suitable for PowerShell and CLI tools.

Typical commands:

```text
git status
git diff
git pull
npm run build
php artisan test
composer install
```

The terminal is a first-class capability because reviewing AI-generated code frequently requires tests, builds, diagnostics, or manual Git inspection before deployment.

### 4.5 Remote Transport

The first transport implementation is SFTP/SSH.

The deployment domain depends on a transport abstraction:

```text
IRemoteTransport
    └── SftpTransport
    └── FtpTransport          (extension candidate)
    └── FtpsTransport         (extension candidate)
    └── Other transports      (future proposals)
```

The core deployment planner must not contain SFTP-specific assumptions.

### 4.6 Persistence

SQLite stores non-secret local application data such as:

- repository registrations,
- server profile metadata,
- saved commands,
- deployment history,
- deployment snapshots,
- review/session metadata where useful.

Secrets must not be stored as plaintext application data.

---

## 5. Proposed Solution Boundaries

```text
RaayaGitDeploy.sln

src/
  RaayaGitDeploy.App/
      Views/
      ViewModels/
      Controls/
      Navigation/
      Diff/
      Terminal/

  RaayaGitDeploy.Core/
      Git/
      Review/
      Deployment/
      Mapping/
      Servers/
      Commands/
      History/
      SecurityContracts/

  RaayaGitDeploy.Infrastructure/
      GitCli/
      Sftp/
      Security/
      Persistence/
      Terminal/
      FileSystem/

tests/
  RaayaGitDeploy.Core.Tests/
  RaayaGitDeploy.Infrastructure.Tests/
```

### Dependency Rule

`Core` must not depend on:

- WinUI,
- SSH.NET,
- SQLite,
- WebView2,
- ConPTY implementation details.

The application and infrastructure layers consume core contracts.

---

## 6. Repository Model

The application operates on one active repository at a time.

For an active repository, the system resolves:

- repository root,
- current branch,
- current HEAD SHA,
- Git executable availability,
- working-tree state,
- selected comparison reference where applicable.

Previously registered repositories may be stored locally for fast reopening.

---

## 7. Git Change Discovery

### 7.1 Working Tree

The application identifies at least:

- Added
- Modified
- Deleted
- Renamed
- Untracked

Git parsing must not rely on localized human-readable output.

### 7.2 Changes Since Reference

Users can compare the repository against a:

- commit,
- branch,
- other supported Git reference.

Typical command direction:

```text
git diff --name-status -M <ref>...
```

The UI must clearly distinguish committed comparison results from working-tree changes.

### 7.3 Rename Semantics

A rename is a single logical review item.

For deployment planning it normally becomes:

```text
Upload new/path
Delete old/path
```

The delete remains destructive and requires explicit confirmation.

---

## 8. Review Model

Review state and deployment state are separate.

A change can be:

- `Unreviewed`
- `Approved`
- `Excluded`

Deployment selection is an independent boolean/queue decision.

Examples:

```text
Approved + Selected
→ reviewed and scheduled for deployment

Approved + Not Selected
→ reviewed but intentionally omitted from this deployment

Excluded
→ intentionally rejected

Unreviewed + Selected
→ requires a prominent warning or explicit confirmation
```

This separation is essential for reliable review of AI-generated changes.

---

## 9. Deployment Selection

Users can add deployment items from:

- Git change selection,
- manual Add File,
- manual Add Folder.

### Add Folder

Adding a folder means “deploy the folder contents according to the planned mapping”, not “select every Git change under the folder”.

This supports build output such as:

```text
public/build
dist
wwwroot
```

File-system traversal must avoid unsafe recursion through junctions, symbolic links, or reparse points unless explicitly supported.

---

## 10. Deployment Queue

Every deployment item has an explicit operation:

- Upload
- Delete

Each queue entry should contain enough information to preview execution:

- source,
- local path,
- repository-relative path where applicable,
- remote path,
- operation,
- file size where applicable,
- Git status where applicable,
- review state where applicable.

The queue must be inspectable before execution.

---

## 11. Path Mapping

Path mapping belongs to the deployment planner, not to SFTP implementation code.

Example:

```text
Repository:
D:\Projects\shop

Local:
D:\Projects\shop\public\build\assets\app.js

Repository Relative:
public/build/assets/app.js

Remote Root:
/public_html

Remote:
/public_html/public/build/assets/app.js
```

All remote paths must be normalized.

A computed path must never escape the configured Remote Root.

Inputs involving traversal such as:

```text
../../
```

must be rejected.

---

## 12. Server Profiles

A server profile stores non-secret connection configuration such as:

- Name
- Transport type
- Host
- Port
- Username
- Remote Root
- Authentication type
- Protected credential reference
- Accepted SSH host key fingerprint

Multiple server profiles are supported.

Typical profiles:

- production
- staging
- test server

---

## 13. Credentials and SSH Trust

Sensitive values such as:

- password,
- SSH private-key passphrase,
- future secret tokens,

must be protected in the current Windows user context using DPAPI or an equivalent Windows-native protection mechanism.

Secrets:

- must not be committed,
- must not be written into project config,
- must not appear in logs.

### SSH Host Key

The first connection should expose the server fingerprint for explicit trust.

Once trusted, the fingerprint is stored.

If it unexpectedly changes, connection is blocked until the user explicitly reviews and accepts the new fingerprint.

---

## 14. Dry Run

Dry Run performs validation without mutating remote state.

It may include:

- repository validation,
- queue validation,
- path mapping,
- SSH connection,
- authentication,
- host-key validation,
- remote-root inspection,
- remote path checks.

Dry Run must not:

- upload,
- overwrite,
- delete,
- rename remote content.

---

## 15. Deployment Execution Safety

Recommended operation ordering:

```text
Preflight
    ↓
Ensure required directories
    ↓
Uploads
    ↓
Verify upload results
    ↓
Explicitly approved deletes
    ↓
Persist deployment snapshot
```

If required uploads fail, pending destructive deletes must not continue automatically.

Remote deletion always requires explicit user approval.

---

## 16. Deployment Snapshot

Tracking only a `Last Deployed Commit` is insufficient because deployments may include:

- partial commit contents,
- uncommitted files,
- untracked files,
- manually added files,
- manually added folders.

Therefore, the architecture uses **Deployment Snapshot** as the durable model.

A snapshot may contain:

- repository identity,
- branch,
- HEAD SHA,
- timestamp,
- server profile,
- deployment items,
- local paths,
- remote paths,
- operation,
- file size,
- hash where useful,
- Git state,
- execution result.

A future “Changes Since Last Deploy” feature should derive from snapshot-aware logic rather than assuming an entire commit was deployed.

---

## 17. Integrated Terminal

The terminal is part of the main development workflow.

Requirements:

- active repository as initial working directory,
- interactive shell,
- resize support,
- standard input/output,
- cancellation/termination controls,
- clear process/session lifecycle.

The terminal may later support multiple tabs or profiles.

AI-generated commands must never execute silently.

Potentially destructive commands should always remain visible and user-controlled.

---

## 18. Saved Commands

Users can define reusable commands.

Minimum model:

```text
Name
Command
Working Directory
```

Examples:

```text
PHP Tests
php artisan test

Frontend Build
npm run build

Composer Install
composer install
```

Saved Commands reduce repetitive typing while preserving human control.

Multi-step conditional pipelines are an extension candidate, not a requirement for the initial implementation baseline.

---

## 19. Logging

The product should provide structured logs for:

- Git command execution,
- deployment planning,
- transport connection,
- upload results,
- delete results,
- dry-run validation,
- deployment completion/failure.

Secrets must be redacted.

Logs should distinguish:

- informational,
- warning,
- error,
- destructive-action confirmation,
- user cancellation.

---

## 20. Error Handling

The UI should present errors in terms meaningful to the user.

Examples:

- Git executable unavailable.
- Invalid repository.
- Authentication failed.
- SSH host key changed.
- Remote Root unavailable.
- Local file disappeared after queue creation.
- Remote path escaped configured root.
- Upload interrupted.
- Delete blocked because prerequisite upload failed.

Errors must not be swallowed.

Where safe, retry can be supported, but retry must not duplicate destructive operations accidentally.

---

## 21. Extensibility

The architecture should support future contribution in areas such as:

### Remote Transports

- FTP
- FTPS
- SCP or other transports where justified

### Git Review

- richer commit comparison,
- patch staging,
- commit graph,
- pull request integration,
- AI-assisted review summaries.

### Deployment

- deployment templates,
- include/exclude rules,
- remote backups,
- rollback strategies,
- upload verification,
- parallel transfer.

### Commands

- command groups,
- pre-deploy checks,
- conditional pipelines,
- post-deploy validation.

### Integrations

- GitHub
- GitLab
- Bitbucket
- hosting panels or deployment APIs

These capabilities should be proposed through issues/discussions and ADRs when they change architectural contracts.

---

## 22. Explicit Non-Goals

Raaya Git Deploy should not automatically become:

- a full IDE,
- a replacement for Git itself,
- a full CI/CD platform,
- a general SSH administration suite,
- a server file manager with no Git context.

Features outside the central Review → Verify → Deploy workflow require a clear product justification.

---

## 23. Testing Strategy

Core logic should be independently testable.

Priority test areas:

- Git porcelain parser
- Git name-status parser
- path normalization
- remote-root escape prevention
- deployment planning
- rename conversion
- upload-before-delete ordering
- destructive operation gates
- deployment snapshot generation
- credential-storage contracts

Infrastructure tests should cover:

- git.exe integration
- SFTP behavior against a controlled test environment
- SQLite persistence
- terminal session lifecycle where feasible

No deployment operation should be considered safe solely because the UI behaves correctly.

---

## 24. Public Repository Architecture Policy

Major architectural changes should:

1. Explain the problem.
2. Identify the affected contracts.
3. Describe alternatives.
4. Record the decision in an ADR when appropriate.
5. Include or update tests.
6. Update documentation.

Contributors should be able to add capabilities without reverse-engineering hidden assumptions from UI code.

That requirement is one reason the project separates Core, App, and Infrastructure responsibilities.

---

## 25. Architecture Baseline

The following decisions are considered part of the current architecture baseline:

- Windows-native desktop application.
- WinUI 3 / Windows App SDK.
- .NET 10.
- `git.exe` as Git source of truth.
- Human-reviewed Git workflow.
- Separate review state and deploy selection.
- ConPTY-based integrated terminal.
- Transport abstraction.
- SFTP/SSH as first transport implementation.
- DPAPI/current-user credential protection.
- SSH host-key verification.
- Deployment Queue and mapping preview.
- Explicit remote-delete confirmation.
- Deployment Snapshots instead of commit-only deployment history.

Any proposal that changes one of these decisions should include an ADR.
