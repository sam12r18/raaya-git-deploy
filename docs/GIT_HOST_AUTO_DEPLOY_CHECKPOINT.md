# Git → Host Auto Deploy — Checkpoint

Updated: 2026-09-19
Branch: `feat/git-host-auto-deploy`

## Milestone 1

Target flow: Project → Repository/Branch → Host validation → Revision → Deployment Plan → Dry Run → safe observable transfer.

### Completed foundation

- `DeploymentProject` defines repository/branch, host profile, application/public roots, strategy and protected production paths.
- `DeploymentPlanner` enforces protected paths as `Skip` operations rather than uploads.
- SFTP remains the first transport behind the existing transport abstraction; cPanel without SSH must be implemented as another adapter, not a special case in the project model.
- Project definition now has a presentation validation contract and reusable WinUI editor component.

### UI contract

The current desktop stack is WinUI 3 / Windows App SDK. There is no Google-official Material 3 WinUI control package in the project, so this workstream does not add a parallel UI framework merely for branding. Native accessible WinUI controls are reused while Material 3 principles are represented through centralized component geometry/tokens and component composition. A package should only be added after compatibility, maintenance and license review demonstrates a real benefit.

`DeploymentProjectEditor` currently provides Source, Target Mapping, Strategy and Protected Paths sections with validation/error state. Next UI slices should extract shared Material-3-aligned tokens to application resources and add Repository/Branch selector, HostProfileCard/ConnectionStatus and DryRunSummary without duplicating existing controls.

### Safety decisions

- Never deploy directly by blind `git pull` in `public_html`.
- `.env`, `storage`, `uploads` and project-specific protected paths remain excluded from deployment operations.
- Credentials are not part of `DeploymentProject`; host/Git secret storage remains a separate concern.
- Destructive operations and migrations require explicit policy/confirmation before implementation.

### Verification

- Source changes were committed through GitHub in this automation environment.
- `dotnet test` / WinUI build: **UNTESTED** in this run because no executable repository workspace/.NET runtime is attached to the connector execution environment.
- UI visual/runtime QA: **UNTESTED** until built on Windows.

### UI/UX review

**MUST-FIX**
- Move temporary component spacing/shape resources into centralized application design tokens before a second component duplicates them.
- Replace free-text Host profile field with a real reusable selector once host-profile binding is wired.
- Add loading/disabled/success state when repository and host validation become asynchronous.

**POLISH/LATER**
- Adaptive two-column layout for wide windows after the complete Milestone 1 form exists.
- RTL localization after functional labels/copy stabilize; component layout must remain direction-safe.

## Exact next step

Wire the validated `DeploymentProject` into a project store/workspace, add Repository/Branch selection backed by existing Git contracts, then bind Host profile selection and connection validation. After that connect `DeploymentPlanner` to a Material-3-aligned `DryRunSummary` component.
