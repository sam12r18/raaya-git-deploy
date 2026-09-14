# ADR 0003: Use a Transport Abstraction with SFTP/SSH as the First Implementation

**Status:** Accepted  
**Date:** 2026-09-14

## Context

The product may eventually need FTP, FTPS, APIs, or other remote transports.

Hard-coding SFTP into deployment planning would make later transport support expensive.

## Decision

Define a remote transport abstraction in the architecture.

Implement SFTP/SSH first.

Conceptually:

```text
IRemoteTransport
    └── SftpTransport
    └── FtpTransport
    └── FtpsTransport
    └── Future transports
```

## Consequences

Deployment planning remains transport-neutral.

New transports can be added without redesigning Git review or deployment queue logic.
