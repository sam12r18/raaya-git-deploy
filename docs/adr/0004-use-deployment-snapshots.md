# ADR 0004: Use Deployment Snapshots Instead of Only Last Deployed Commit

**Status:** Accepted  
**Date:** 2026-09-14

## Context

A deployment may contain:

- only some files from a commit,
- uncommitted files,
- untracked files,
- manually added files,
- manually added folders.

Therefore, a single “last deployed commit” value cannot accurately describe remote state.

## Decision

Persist a Deployment Snapshot containing deployment context and per-item results.

The commit SHA remains valuable metadata, but it is not the complete deployment identity.

## Consequences

Future features such as “Changes Since Last Deploy” can be more accurate.

The persistence model is slightly richer but avoids a misleading deployment history model.
