# Raaya Git Deploy — Developer Workbench Core Design

## Goal
Turn the current Git Review foundation into a usable Windows developer workbench where a developer can inspect repository changes and commits, run repository-aware terminal commands, manage reusable commands, and then progress into deployment planning without leaving the application.

## Product shell
The main window becomes a persistent workspace shell with a left navigation rail and repository context header. Primary destinations are Changes, Commits, Terminal, Commands, Deploy Queue, Servers, and History. Repository path, branch, HEAD and refresh/open actions remain visible regardless of destination.

## Changes
Preserve the existing machine-readable working-tree and compare behavior, but present it as a usable review workspace: changed-file list, status, review state, deploy selection, and a readable diff inspector. Repository switching must clear stale state.

## Commits
Add a first-class recent commit list. Each item exposes short SHA, subject, author and date. Selecting a commit loads the files changed by that commit; selecting a file loads its commit diff. Initial delivery is a list/detail workflow, not a graphical commit graph.

## Terminal
Add an integrated repository-aware terminal using ConPTY on Windows. The initial shell is PowerShell. Opening/switching repositories updates the default working directory for new terminal sessions. The terminal must stream output, accept input, expose running/exited state, support cancellation/close, and avoid UI-thread blocking.

## Commands
Saved Commands are named command presets such as Build, Test, or project-specific scripts. A command has name, command text and optional working-directory override. Running a saved command executes it through the terminal/command execution boundary and exposes exit status and output. Persistence is local and must not store credentials.

## Deploy Queue
Build on the already independent deploy-selection state. Queue shows selected changed files and supports explicit Add File/Add Folder entries. It is a planning surface only until server mapping and transport are configured. Local-to-remote mapping must be previewable before any transfer.

## Servers
Introduce server profiles for SFTP/SSH. Non-secret metadata can be persisted locally; passwords/private-key secrets must never be committed to the repository. Connection and deployment implementation must keep transport concerns behind an interface.

## History
Deployment history records repository, branch, HEAD, timestamp, target profile and per-item result. Until real deployment is implemented the destination exists in navigation but must clearly communicate that no deployment history exists rather than fabricating entries.

## Architecture
Keep the existing Core / Infrastructure / Presentation / App separation. Domain contracts and records live in Core, git/terminal/persistence/transport implementations in Infrastructure, testable state and commands in Presentation, and WinUI controls/platform handles in App. UI code-behind should be limited to WinUI-specific integration such as window handles and terminal control wiring.

## Safety and correctness
Git arguments remain structured and user-controlled refs are resolved safely before diff operations. Terminal commands are explicitly user-triggered; no repository content is automatically executed. Deployment requires preview/dry-run before destructive or remote operations. Remote deletion requires explicit confirmation. Credentials never enter source control or normal structured logs.

## UX
Dark-first dense desktop UI. The application should feel like a developer tool rather than a settings form. Navigation remains visible; major workflows use master/detail layouts; empty states explain the next action. Long-running Git/terminal/network work must not freeze the UI.

## Verification
Development is TDD-first. Core, Infrastructure and Presentation behavior receive automated tests. Windows-specific ConPTY and WinUI integration also require explicit manual acceptance on a real Windows desktop. A test is only recorded as passing when it was actually executed successfully.

## Delivery order
1. Workspace navigation and repository header.
2. Commit history and commit diff workflow.
3. ConPTY terminal and terminal UI.
4. Saved Commands and command execution.
5. Deploy Queue planning.
6. Server profiles and SFTP/SSH transport boundary.
7. Dry-run/mapping and deployment execution safety.
8. Deployment History and final workflow polish.
