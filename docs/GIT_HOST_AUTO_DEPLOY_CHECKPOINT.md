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
- `HostProfileCard` now provides a reusable WinUI host editor/connection-validation surface with loading, disabled, success and error treatment. It emits only a validated `ServerProfile` to its consumer.
- Host validation reuses the existing remote transport abstraction; raw private key material is not accepted by the editor contract, only a credential/key reference.
- Material-3-aligned spacing, shape, content-width and surface-card resources are centralized in `App.xaml`; components consume these shared tokens rather than defining local duplicates.
- The application does not force a dark theme, leaving native WinUI Light/Dark/system behavior available while semantic ThemeResource brushes remain in use.

### UI contract

The desktop stack is WinUI 3 / Windows App SDK. No parallel UI framework is added merely for Material branding. Native accessible WinUI controls are reused while Material 3 principles are represented through centralized tokens, hierarchy and reusable component composition. A package should only be added after compatibility, maintenance and license review demonstrates a real benefit.

`DeploymentProjectEditor` provides Source, Target Mapping, Strategy and Protected Paths sections with validation/error state. `HostProfileCard` reuses the same surface/spacing tokens and exposes the real async connection states from `HostProfileEditorViewModel`. During validation, editable fields and the submit action are disabled and indeterminate progress plus an InfoBar communicate state.

### Safety decisions

- Never deploy directly by blind `git pull` in `public_html`.
- `.env`, `storage`, `uploads` and project-specific protected paths remain excluded from deployment operations.
- Credentials are not part of `DeploymentProject`; host/Git secret storage remains a separate concern.
- Host profile stores only a key/credential reference; private key material must be resolved by the credential abstraction.
- Destructive operations and migrations require explicit policy/confirmation before implementation.

### Verification

- Source-level HostProfileCard implementation completed.
- `dotnet test` / WinUI build: **UNTESTED** in this run because the GitHub connector does not provide an executable repository workspace/.NET runtime.
- UI visual/runtime QA: **UNTESTED** until built on Windows.

### UI/UX review

**MUST-FIX**
- Replace the Project editor free-text Host profile id with a real selector that consumes validated profiles and can launch/reuse `HostProfileCard`.
- Resolve `KeyReference` through the secure credential picker/store rather than expecting users to type implementation identifiers once the credential UI is available.
- Validate `HostProfileCard` focus order, NumberBox behavior, InfoBar announcements and narrow-width layout in a real WinUI build.
- Validate the centralized resources with a real WinUI build before expanding their use further.

**POLISH/LATER**
- Split `ConnectionStatus` into a smaller reusable visual only when a second flow needs the same status presentation; do not create speculative components.
- Adaptive two-column layout for wide windows after the complete Milestone 1 form exists.
- RTL localization after functional labels/copy stabilize; component layout must remain direction-safe.

## Exact next step

Wire validated host profiles into `DeploymentProjectEditor` through a real host selector instead of free-text id. Then add Repository/Branch selection backed by existing Git contracts. After that connect `DeploymentPlanner` to a Material-3-aligned `DryRunSummary` component.
