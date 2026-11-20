# Security Architecture

## Threat model baseline

Astra can read user data, execute tools, access the network, install software, automate browsers, and manage projects. Treat the agent runtime as a high-value local application.

Threats include:
- Prompt injection from websites, documents, repositories, and tool output.
- Malicious or compromised extensions.
- Malicious packages and supply-chain compromise.
- Accidental destructive actions.
- Credential leakage.
- Privilege escalation.
- Sandbox escape.
- Resource exhaustion.
- LAN unauthorized access.
- Corrupted updates and backups.

## Mandatory controls

1. Least privilege; no administrator/root by default.
2. Typed tool schemas and strict input validation.
3. Policy check before every privileged action.
4. Explicit approval for critical actions and Production deployments.
5. External content is data, never authority.
6. Secret storage through OS facilities.
7. Redaction of secrets and sensitive data in logs.
8. Isolated extension execution.
9. OS-specific sandboxing; Docker alone is insufficient.
10. CPU, memory, disk, time, and network limits.
11. Audit trail for sensitive operations.
12. Backup and rollback for supported mutations.
13. Signed or otherwise verifiable update artifacts where feasible.
14. Secure LAN pairing and authenticated API access.

## Permission levels

- L0: read.
- L1: temporary permission.
- L2: project/file modifications.
- L3: system changes and software installation.
- L4: critical operations.

Each tool declares required permissions and risk metadata. Policy is evaluated at execution time, not only when a plan is created.

## Prompt injection

Untrusted content must be clearly separated from system policy and user intent. Tool outputs must not be allowed to grant permissions, change policies, or authorize new tools. High-risk actions require independent policy checks and, where specified, user approval.

## Self-modification

Skills and knowledge can be versioned and tested. Core, permission engine, security policy, and trust roots cannot be modified autonomously. Updates must support validation and rollback.

## Current state (2026-11-20)

Stages 0–3 are signed off. The Policy Engine, permission model and audit trail are **not
implemented yet**; they are designed in
[ADR 0013](adr/0013-policy-engine-and-audit.md) (Proposed) and scheduled for Stage 4. Until
they exist:

- no tool can execute, and `Astra.Host` exposes only `/health`;
- external content must not be treated as instructions anywhere in code;
- prompts, completions, file contents and secrets must not be logged (central log redaction is
  still pending).

## Security review

Every new tool, extension interface, network endpoint, persistence feature, and update mechanism requires a threat review and tests.
