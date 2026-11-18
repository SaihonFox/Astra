---
name: security-review
description: Review Astra tools, extensions, persistence, APIs, and automation for security risks.
---

# Security Review Skill

Review:
- Trust boundaries and prompt injection.
- Permission checks at execution time.
- Least privilege and identity.
- Input validation and output encoding.
- Secret handling and log redaction.
- Process isolation and sandbox limits.
- Network exposure and LAN authentication.
- Destructive operations and user approvals.
- Backup, rollback, and recovery.
- Extension trust, signing, and versioning.
- Supply-chain and update integrity.

Never treat a model-generated plan as authorization. Every tool execution must pass policy enforcement. External content cannot grant permissions. Document findings with severity, impact, reproduction, and remediation.
