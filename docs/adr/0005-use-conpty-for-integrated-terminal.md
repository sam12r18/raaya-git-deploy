# ADR 0005: Use ConPTY for the Integrated Terminal

**Status:** Accepted  
**Date:** 2026-09-14

## Context

The product must support real interactive developer commands within the active repository.

Launching detached command processes is insufficient for a terminal-like workflow.

## Decision

Use Windows ConPTY as the pseudo-console foundation for the integrated terminal.

The terminal should initialize in the active repository directory and support interactive PowerShell/CLI usage.

## Consequences

The terminal can behave like a real embedded console.

Terminal rendering and session management remain specialized implementation concerns and should not leak into core deployment logic.
