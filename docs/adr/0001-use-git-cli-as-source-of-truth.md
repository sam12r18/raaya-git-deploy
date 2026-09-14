# ADR 0001: Use git.exe as the Git Source of Truth

**Status:** Accepted  
**Date:** 2026-09-14

## Context

Raaya Git Deploy must inspect real local repositories and present results consistent with the developer's normal Git environment.

Using a separate Git implementation could introduce behavior differences or require duplicating Git semantics.

## Decision

Use installed `git.exe` as the source of truth for Git operations.

Prefer porcelain or machine-readable output.

## Consequences

Positive:

- consistent with normal developer Git behavior,
- easier troubleshooting,
- lower semantic divergence,
- flexible future Git support.

Trade-offs:

- Git must be installed,
- process execution and parsing must be robust,
- version compatibility must be handled.
