# Git → Host Auto Deploy — Checkpoint

Updated: 2026-09-19
Branch: `feat/git-host-auto-deploy`

## Milestone 1

Target flow: Project → Repository/Branch → Host validation → Revision → Deployment Plan → Dry Run → safe observable transfer.

### Completed foundation

- `DeploymentProject` defines repository/branch, host profile, application/public roots, strategy and protected production paths.
- `DeploymentPlanner` enforces protected paths as `Skip` operations rather than uploads.
- SFTP remains the first transport behind the existing transport abstraction; cPanel without SSH must be implemented as another adapter, not a special case in the project model.
- Project definition has a presentation validation contract and reusable WinUI editor component.
- `HostProfileEditorViewModel` now creates validated SSH-key host profiles and exposes explicit Idle/Validating/Success/Error connection states.
- Host validation reuses `ServersViewModel` + `IRemoteTransport`; raw private key material is not accepted by this editor contract, only a credential/key reference.

### UI contract

The current desktop stack is WinUI 3 / Windows App SDK. There is no Google-official Material 3 WinUI control package in the project, so this workstream does not add a parallel UI framework merely for branding. Native accessible WinUI controls are reused while Material 3 principles are represented through centralized component geometry/tokens and component composition. A package should only be added after compatibility, maintenance and license review demonstrates a real benefit.

`DeploymentProjectEditor` provides Source, Target Mapping, Strategy and Protected Paths sections with validation/error state. The host-profile presentation contract is now ready for a reusable `HostProfileCard` / `ConnectionStatus` UI with real asynchronous loading/disabled/success/error states.

### Safety decisions

- Never deploy directly by blind `git pull` in `public_html`.
- `.env`, `storage`, `uploads` and project-specific protected paths remain excluded from deployment operations.
- Credentials are not part of `DeploymentProject`; host/Git secret storage remains a separate concern.
- Host profile stores only a key/credential reference; private key material must be resolved by the credential abstraction.
- Destructive operations and migrations require explicit policy/confirmation before implementation.

### Verification

- Host profile validation tests were added, but `dotnet test` / WinUI build are **UNTESTED** in this run because the GitHub connector does not provide an executable repository workspace/.NET runtime.
- UI visual/runtime QA: **UNTESTED** until built on Windows.

### UI/UX review

**MUST-FIX**
- Move temporary component spacing/shape resources into centralized application design tokens before HostProfileCard duplicates them.
- Replace free-text Host profile field with the reusable selector/card backed by `HostProfileEditorViewModel`.
- Bind connection states to visible Material-3-aligned progress/status treatment and disable duplicate connection attempts while validating.
- Do not expose raw key path/private-key contents in the normal profile form; select a credential reference through the secure credential layer when that UI exists.

**POLISH/LATER**
- Adaptive two-column layout for wide windows after the complete Milestone 1 form exists.
- RTL localization after functional labels/copy stabilize; component layout must remain direction-safe.

## Exact next step

Build `HostProfileCard` + `ConnectionStatus` on the new host editor contract, replace the Project editor free-text host id with a real profile selector, then add Repository/Branch selection backed by existing Git contracts. After that connect `DeploymentPlanner` to a Material-3-aligned `DryRunSummary` component.
