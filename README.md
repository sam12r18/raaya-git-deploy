# Raaya Git Deploy

**Raaya Git Deploy** is a Windows-native Git review and deployment workbench built for a modern AI-assisted development workflow.

Its core use case is simple:

> AI agents, Codex, ChatGPT, or developers make changes in a Git repository; a human reviews those changes, validates them, selects what should be deployed, previews the exact local-to-remote mapping, and deploys safely to a server.

Raaya Git Deploy is intentionally **not just another FTP client**. Its product model is:

```text
Git Changes
    ↓
Review
    ↓
Diff Inspection
    ↓
Terminal / Validation
    ↓
Approval
    ↓
Deployment Queue
    ↓
Mapping Preview
    ↓
Dry Run
    ↓
Deploy
    ↓
Deployment History
```

## Product Principles

- Human approval remains central, especially for AI-generated changes.
- Review state and deployment selection are separate concepts.
- Git is the source of truth for repository change discovery.
- Deployment must be explicit, previewable, and auditable.
- Destructive remote operations require explicit confirmation.
- Secrets never belong in the repository.
- The architecture should remain extensible for new transports and workflows.
- The project is public and should be contributor-friendly from the beginning.

## Technology Direction

- .NET 10
- WinUI 3 + Windows App SDK
- MVVM
- `git.exe` for Git integration
- ConPTY for the integrated terminal
- SFTP/SSH as the first transport implementation
- SSH.NET for SFTP/SSH
- Windows DPAPI/current-user protection for credentials
- SQLite for local non-secret state
- WebView2 only where it materially improves specialized experiences such as advanced diff rendering

## Core Capability Areas

### Git Review

- Select/open a repository
- Display current branch and HEAD
- Working Tree Changes
- Changes Since Commit/Branch
- Added / Modified / Deleted / Renamed / Untracked
- Per-file review state
- Per-file deployment selection
- Advanced diff viewer

### Deployment Planning

- Select individual changed files
- Add File manually
- Add Folder for complete-folder deployment such as `public/build`
- Deployment Queue
- Local → Remote mapping preview
- Multiple server profiles
- Dry Run
- Explicit destructive-operation confirmation

### Deployment Execution

- SFTP/SSH transport
- Password or SSH key authentication
- Progress
- Cancellation
- Structured logs
- Upload-before-delete safety policy
- Deployment history and snapshots

### Developer Workflow

- Integrated repository-aware terminal
- PowerShell / command execution
- Saved project commands
- Build/test/diagnostic workflows
- Fast verification of AI-generated changes before deployment

## Scope Model

This repository does **not** use rigid product buckets such as “V1 features” and “V2 features” as architectural boundaries.

Capabilities are instead classified as:

- **Core Baseline** — fundamental product behavior the architecture must support.
- **Active Development** — work currently being implemented.
- **Extension Candidate** — useful capability that can be proposed and developed without being tied to a predetermined product version.
- **Experimental** — ideas that require validation before becoming part of the stable product contract.

Software releases may still use Semantic Versioning or another release numbering strategy. Release numbers describe shipped software; they do not define permanent product boundaries.

## Documentation

- [`docs/architecture/PRODUCT_ARCHITECTURE.md`](docs/architecture/PRODUCT_ARCHITECTURE.md)
- [`docs/ROADMAP.md`](docs/ROADMAP.md)
- [`CONTRIBUTING.md`](CONTRIBUTING.md)
- [`docs/adr/`](docs/adr/) — Architecture Decision Records

## Current Status

**Architecture and specification stage.**

Implementation should follow the documented architecture and ADR process. Major changes should update the relevant documentation before or together with code.
