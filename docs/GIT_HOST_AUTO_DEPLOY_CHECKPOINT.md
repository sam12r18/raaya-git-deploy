# Git → Host Auto Deploy — Checkpoint

Updated: 2026-09-20
Branch: `feat/git-host-auto-deploy`

## Milestone 1

Target flow: Project → Repository/Branch → Host validation → Revision → Deployment Plan → Dry Run → safe observable transfer.

### Completed foundation

- `DeploymentProject` defines repository/branch, host profile, application/public roots, strategy and protected production paths.
- `DeploymentPlanner` enforces protected paths as `Skip` operations rather than uploads.
- SFTP remains the first transport behind the existing transport abstraction; cPanel without SSH must be implemented as another adapter, not a special case in the project model.
- Project definition has a presentation validation contract and reusable WinUI editor component.
- `HostProfileEditorViewModel` creates validated SSH-key host profiles and exposes Idle/Validating/Success/Error connection states.
- Host validation reuses the existing remote transport abstraction; raw private key material is not accepted by the editor contract, only a credential/key reference.
- Material-3-aligned spacing, shape, content-width and surface-card resources are now centralized in `App.xaml`; `DeploymentProjectEditor` consumes these shared tokens rather than defining local duplicates.
- The application no longer forces `RequestedTheme="Dark"`, leaving native WinUI theme selection available for Light/Dark/system behavior while semantic ThemeResource brushes remain in use.

### UI contract

The current desktop stack is WinUI 3 / Windows App SDK. There is no Google-official Material 3 WinUI control package in the project, so this workstream does not add a parallel UI framework merely for branding. Native accessible WinUI controls are reused while Material 3 principles are represented through centralized tokens and component composition. A package should only be added after compatibility, maintenance and license review demonstrates a real benefit.

`DeploymentProjectEditor` provides Source, Target Mapping, Strategy and Protected Paths sections with validation/error state. Shared tokens now cover spacing, large/medium shapes, content width, container padding and surface-card composition. The host-profile presentation contract is ready for a reusable `HostProfileCard` / `ConnectionStatus` UI with real asynchronous loading/disabled/success/error states.

### Safety decisions

- Never deploy directly by blind `git pull` in `public_html`.
- `.env`, `storage`, `uploads` and project-specific protected paths remain excluded from deployment operations.
- Credentials are not part of `DeploymentProject`; host/Git secret storage remains a separate concern.
- Host profile stores only a key/credential reference; private key material must be resolved by the credential abstraction.
- Destructive operations and migrations require explicit policy/confirmation before implementation.

### Verification

- Source-level XAML/token refactor completed.
- `dotnet test` / WinUI build: **UNTESTED** in this run because the GitHub connector does not provide an executable repository workspace/.NET runtime.
- UI visual/runtime QA: **UNTESTED** until built on Windows.

### UI/UX review

**MUST-FIX**
- Replace free-text Host profile field with the reusable selector/card backed by `HostProfileEditorViewModel`.
- Bind connection states to visible progress/status treatment and disable duplicate connection attempts while validating.
- Do not expose raw key path/private-key contents in the normal profile form; select a credential reference through the secure credential layer when that UI exists.
- Validate the centralized resources with a real WinUI build before expanding their use to additional controls.

**POLISH/LATER**
- Add explicit semantic color/elevation/state-layer aliases only when a second component demonstrates the need; avoid speculative token growth.
- Adaptive two-column layout for wide windows after the complete Milestone 1 form exists.
- RTL localization after functional labels/copy stabilize; component layout must remain direction-safe.

## Exact next step

Build `HostProfileCard` + `ConnectionStatus` on the host editor contract and replace the Project editor free-text host id with a real profile selector. Then add Repository/Branch selection backed by existing Git contracts. After that connect `DeploymentPlanner` to a Material-3-aligned `DryRunSummary` component.
