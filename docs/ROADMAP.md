# Raaya Git Deploy — Capability Roadmap

This roadmap is intentionally **not organized as V1 / V2 / V3**.

Raaya Git Deploy is a public project and may evolve through maintainer work, contributor pull requests, new use cases, and validated user needs.

Release numbers describe software builds. They do not permanently reserve capabilities for arbitrary product versions.

---

## Status Labels

### Core Baseline

Fundamental to the product and architecture.

### Active Development

Currently being implemented.

### Extension Candidate

Aligned with the product, but not necessarily scheduled.

### Experimental

Requires validation before becoming part of the stable product contract.

---

## Git & Repository

### Core Baseline

- Repository selection
- Branch and HEAD display
- Working Tree Changes
- Changes Since Commit/Branch
- Added / Modified / Deleted / Renamed / Untracked
- Machine-readable Git parsing
- File-level selection
- File-level review state

### Active Development

- IDE-style commit browser: repository/branch hierarchy, commit subject, author, author date/time, changed paths and commit diff in a dense master/detail workspace
- Commit metadata columns and filtering comparable to modern IDE Git-log workflows

### Extension Candidates

- Commit graph
- Patch/hunk selection
- Partial staging
- Git stash helpers
- Pull request comparison
- Remote tracking status
- GitHub/GitLab/Bitbucket integration

---

## Review Experience

### Core Baseline

- Diff viewer
- Approved / Excluded / Unreviewed states
- Independent deploy-selection state
- Clear changed-file summary
- Review warnings before deployment

### Extension Candidates

- AI-generated change summaries
- Risk flags
- Review notes/comments
- Session persistence
- Compare previous review sessions
- Large-change review workflows

---

## Terminal & Commands

### Core Baseline

- Integrated ConPTY terminal
- Repository-aware working directory
- PowerShell command execution
- Saved commands

### Active Development

- Replace the current output-plus-command-box surface with an IDE-grade interactive terminal experience
- Native prompt/input behavior, ANSI/VT rendering, selection/copy/paste, scrolling, keyboard shortcuts and resize propagation
- Keep repository-aware working directory and explicit process lifecycle

### Extension Candidates

- Multiple terminal tabs
- Command history UI
- Command groups
- Pre-deploy validation commands
- Conditional pipelines
- Post-deploy checks
- Per-repository command presets

---

## Deployment Planning

### Core Baseline

- Select changed files
- Add File
- Add Folder
- Deployment Queue
- Local → Remote mapping
- Multiple server profiles
- Dry Run

### Extension Candidates

- Include/exclude patterns
- Mapping templates
- Per-project deployment rules
- Generated build-output presets
- Remote existence comparison
- Content hash comparison

---

## Deployment Transports

### Core Baseline

- SFTP/SSH
- Password authentication
- SSH key authentication
- SSH host-key verification

### Active Development

- FTP transport for cPanel/shared-hosting deployment
- FTPS support where the hosting provider exposes TLS
- Server profile protocol selector so SFTP/SSH and FTP/FTPS share deployment planning/history contracts while transport-specific fields remain isolated
- Connection testing and actionable protocol-specific errors

### Extension Candidates

- SCP
- Hosting-panel APIs
- Custom transport integrations

New transports should implement the transport contract without leaking transport-specific behavior into the deployment domain.

---

## Deployment Safety

### Core Baseline

- Explicit remote-delete confirmation
- Upload-before-delete
- Block delete phase when required uploads fail
- Remote Root containment
- Structured logs
- Cancellation

### Extension Candidates

- Remote backup before overwrite
- Rollback strategy
- Deployment verification
- Atomic deployment strategies where server capabilities allow
- Automatic retry policies

---

## Deployment History

### Core Baseline

- Deployment result log
- Deployment Snapshot model
- Repository / branch / HEAD context
- Per-item result

### Extension Candidates

- Changes Since Last Deploy
- Compare deployments
- Restore previous deployment selection
- Server-specific deployment timelines
- Audit export

---

## UX / Desktop

### Core Baseline

- WinUI 3
- Dark-first developer-oriented UI
- Dense information layout
- Main repository workspace
- Changes / Queue / History / Servers / Commands navigation

### Active Development

Manual Windows acceptance on 2026-09-22 confirmed the application shell, repository context, commit diff surface and repository-aware terminal can render on a real Windows desktop. The following usability work is now part of the active workbench flow rather than deferred cosmetic polish:

- Fix clipped/truncated navigation labels and establish a resizable minimum-width navigation rail
- Improve visual hierarchy between repository header, navigation and active workspace
- Use resizable split panes for master/detail views instead of large fixed empty regions
- Add meaningful empty states for clean working trees, empty deployment queues and deployment history
- Prevent stale deployment failure/dry-run messages from appearing as the primary empty state
- Improve commit browser density and alignment using IDE-style metadata columns
- Make long diffs/logs readable with appropriate scrolling and monospace presentation
- Improve form density/alignment in Commands and Servers; avoid oversized unused list panes when empty
- Add disabled/busy/loading/error states to actions whose prerequisites are missing
- Preserve component-based views so these improvements do not turn the workspace into one monolithic XAML file
- Track RTL/Persian support without allowing it to block the main workbench flow

### Extension Candidates

- Theme customization
- Multiple repository workspaces
- Keyboard-first command palette
- Layout persistence
- Advanced accessibility options

---

## Contribution Policy for Roadmap Items

An Extension Candidate may move into Active Development when:

- a maintainer decides to implement it,
- a contributor proposes a sound design,
- a real user need validates the capability,
- it does not violate the core product direction.

Large changes should start with an issue/discussion and, when architectural, an ADR.

The roadmap is expected to evolve.
