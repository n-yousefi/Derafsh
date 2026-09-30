# Security Policy

Derafsh generates SQL for mapped relational graph operations, so correctness and parameter safety are treated as core requirements.

## Reporting a vulnerability

Please report suspected security issues through GitHub's private security-advisory feature for this repository rather than opening a public issue with exploit details.

Include a minimal reproduction, affected version, database/schema assumptions, and the expected versus actual behavior when possible.

## Scope

Derafsh parameterizes runtime values and quotes mapped SQL identifiers. Application-level authorization remains the responsibility of the calling application. A successful graph operation means the persistence mapping is valid; it does not imply that the current user is authorized to read or modify that graph.
