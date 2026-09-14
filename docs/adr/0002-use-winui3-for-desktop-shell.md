# ADR 0002: Use WinUI 3 for the Desktop Shell

**Status:** Accepted  
**Date:** 2026-09-14

## Context

The product requires a modern Windows-native developer-tool experience. A classic WPF visual style is not the desired primary direction.

## Decision

Use:

- .NET 10
- WinUI 3
- Windows App SDK

Use WebView2 only for specialized components where web rendering clearly improves the experience, such as an advanced diff viewer.

## Consequences

Positive:

- modern Windows UI stack,
- suitable for a polished native shell,
- supports integration with Windows-native APIs.

Trade-offs:

- some developer-tool widgets may require custom implementation,
- specialized components may still benefit from WebView2.
