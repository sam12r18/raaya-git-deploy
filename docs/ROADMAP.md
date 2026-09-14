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

### Extension Candidates

- FTP
- FTPS
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
