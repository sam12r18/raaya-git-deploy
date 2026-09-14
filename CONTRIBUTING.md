# Contributing to Raaya Git Deploy

Thank you for considering a contribution.

Raaya Git Deploy is intended to be a public, extensible developer tool. Contributions are welcome when they preserve the central product workflow:

```text
Review → Verify → Deploy
```

## Before Starting

For small fixes:

1. Check existing issues.
2. Keep the change focused.
3. Add or update tests where applicable.

For new capabilities or architectural changes:

1. Open an issue or discussion describing the problem.
2. Explain the proposed behavior.
3. Identify affected architecture boundaries.
4. Add an ADR when the change modifies a baseline architectural decision.
5. Avoid implementing a large architectural change before the direction is agreed.

## Architecture Rules

The current baseline includes:

- .NET 10
- WinUI 3 / Windows App SDK
- `git.exe` as Git source of truth
- Core/App/Infrastructure separation
- ConPTY for integrated terminal
- Transport abstraction
- SFTP/SSH as the first transport implementation
- DPAPI/current-user secret protection
- Explicit destructive-operation confirmation
- Deployment Snapshot history model

See:

- `docs/architecture/PRODUCT_ARCHITECTURE.md`
- `docs/adr/`

## Keep Core Independent

`RaayaGitDeploy.Core` should not directly depend on UI or infrastructure frameworks such as:

- WinUI
- SSH.NET
- SQLite
- WebView2

Infrastructure implementations should satisfy contracts defined at appropriate boundaries.

## Git Integration

Prefer machine-readable Git output.

Do not parse localized human-readable Git messages when a porcelain or structured format exists.

## Security

Never:

- commit credentials,
- log passwords,
- log private keys,
- bypass SSH host-key verification,
- silently execute destructive remote actions,
- allow remote path traversal outside configured Remote Root.

## Testing

Tests are especially important for:

- Git parsers,
- path mapping,
- path normalization,
- deployment planning,
- rename behavior,
- destructive-operation gates,
- deployment ordering,
- snapshots.

A UI confirmation dialog is not a substitute for domain-level safety validation.

## Pull Requests

A good pull request should explain:

- What problem is being solved?
- Why this approach?
- Which architecture areas are affected?
- What tests were added or run?
- What remains intentionally out of scope?

## Capability Roadmap

The project does not reserve features for rigid product buckets such as V1/V2/V3.

See `docs/ROADMAP.md` for capability status.

Release versioning is separate from roadmap capability classification.
