# OWASP ASVS 5.0 — Level 2 checklist

InvoiceFlow targets **ASVS 5.0 Level 2** as the verification standard (**NFR-SEC-14** in planning `NON-FUNCTIONAL-REQUIREMENTS.md`). This file tracks **applicable** requirements for the current architecture (Angular SPA, .NET API/worker, Keycloak OIDC, PostgreSQL per tenant, RabbitMQ, local k3d demo). It is a living review artefact, not an automated ASVS scanner.

**Status legend**

| Status | Meaning |
| --- | --- |
| **Verified** | Implemented and checked by automated test, CI, or a scripted runbook drill |
| **Implemented** | Present in code/deployment; evidence is manual review or documented procedure |
| **N/A** | Not applicable to version 1 (justification in *Notes*) |

Re-check OWASP document versions at the start of each stage that touches security (see planning `NON-FUNCTIONAL-REQUIREMENTS.md` §2.1).

**Last reviewed:** Stage 8 (credential rotation drill + checklist completion).

---

## Mapping to InvoiceFlow NFRs

Project-specific security acceptance lives in planning `NON-FUNCTIONAL-REQUIREMENTS.md` §2.2 (`NFR-SEC-01` … `NFR-SEC-22`). Each row below references those IDs where they subsume the ASVS control.

---

## V1 — Architecture, design and threat modelling

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 1.1.1 | Secure SDLC and security requirements documented | **Implemented** | PLAN + NFR doc (external repo); ADRs |
| 1.2.1 | Threat model maintained for trust boundaries | **Implemented** | STRIDE updated per stage in planning; boundaries: browser, ingress, Keycloak, tenant DBs, on-prem TLS, Ollama host |
| 1.4.1 | Access control enforced on trusted enforcement layer | **Verified** | API policies (`NFR-SEC-01`, `NFR-SEC-04`); IT |
| 1.5.1 | Input and output components defined | **Verified** | DTOs + FluentValidation (`NFR-SEC-17`); OpenAPI in CI |
| 1.14.1 | Configuration secrets not in source | **Verified** | gitleaks CI; `create-local-secrets.sh` (`NFR-SEC-06`) |

---

## V2 — Authentication

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 2.1.1 | Strong auth for application users | **Verified** | Keycloak OIDC Auth Code + PKCE; realm `invoiceflow` |
| 2.2.1 | Authenticator lifecycle | **Implemented** | Personas in Keycloak; brute force enabled in realm import (`NFR-SEC-16`) |
| 2.4.1 | Credential storage | **N/A** | No local password store; Keycloak holds credentials |
| 2.5.1 | Credential recovery | **N/A** | `resetPasswordAllowed: false` in demo realm |
| 2.7.1 | Session/token security | **Verified** | JWT validation: signature, iss, aud, lifetime (`NFR-SEC-02`); access token 300 s |
| 2.8.1 | Credential rotation | **Verified** | [`credential-rotation-drill.md`](../runbooks/credential-rotation-drill.md) (`NFR-SEC-19`) |

---

## V3 — Session management

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 3.1.1 | Session tokens not in URLs | **Verified** | Bearer tokens; PKCE |
| 3.2.1 | Logout / session expiry | **Implemented** | Keycloak SSO timeouts in realm JSON; OIDC logout from SPA |
| 3.3.1 | Session fixation | **N/A** | OIDC provider issues tokens; no server-side session cookie |

---

## V4 — Access control

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 4.1.1 | Enforce access control | **Verified** | Deny by default (`NFR-SEC-01`) |
| 4.1.2 | Attribute / tenant isolation | **Verified** | Tenant from token; DB per tenant (`NFR-SEC-03`) |
| 4.1.3 | Least privilege roles | **Verified** | Clerk / approver / finance; separation of duties (`NFR-SEC-04`) |
| 4.2.1 | IDOR prevention | **Verified** | Cross-tenant matrix IT (404) |

---

## V5 — Validation, sanitisation and encoding

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 5.1.1 | Input validation | **Verified** | Request models, unknown JSON rejected (`NFR-SEC-17`) |
| 5.2.1 | Sanitisation for output | **Verified** | Angular default sanitisation; CI ban on `bypassSecurityTrust*` (`NFR-SEC-21`) |
| 5.3.1 | Output encoding | **Verified** | JSON API; no HTML injection path for invoice fields |

---

## V6 — Stored cryptography

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 6.1.1 | Sensitive data classification | **Implemented** | NFR privacy §3; audit vs logs split |
| 6.2.1 | Algorithms and key lengths | **Implemented** | TLS 1.2+ via Traefik/mkcert; RS256 OIDC; Postgres SCRAM |
| 6.4.1 | Secrets management | **Verified** | K8s Secrets; k3s encryption at rest; rotation runbook |

---

## V7 — Error handling and logging

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 7.1.1 | No sensitive data in errors | **Verified** | ProblemDetails + traceId (`NFR-SEC-18`) |
| 7.2.1 | No sensitive data in logs | **Verified** | Canary log tests (`NFR-SEC-07`) |
| 7.3.1 | Security events logged | **Implemented** | Structured log events catalogue; Keycloak admin events (realm) |

---

## V8 — Data protection

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 8.1.1 | Sensitive data minimisation | **Implemented** | `NFR-PRI-01`; no persisted prompts |
| 8.2.1 | Client-side caching | **Implemented** | Download attachments; API cache headers on sensitive resources |
| 8.3.1 | Sensitive data in server storage | **Implemented** | Tenant DB isolation; SeaweedFS per-tenant prefix |

---

## V9 — Communication

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 9.1.1 | TLS for client connections | **Verified** | HTTPS browser hostnames (`NFR-SEC-20`); E2E |
| 9.2.1 | TLS to external systems | **Verified** | On-prem Postgres `verify-full` (`NFR-SEC-08`); stage 6 runbook |
| 9.3.1 | Server-side crypto | **N/A** | No custom crypto protocols |

---

## V10 — Malicious code

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 10.2.1 | No unauthorised functionality | **Implemented** | Code review; dependency policy ADR |
| 10.3.1 | Deployed artefacts integrity | **Implemented** | Pinned base images (Keycloak digest); CI build from repo |

---

## V11 — Business logic

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 11.1.1 | Business logic security | **Verified** | Invoice state machine UT; approval threshold IT |
| 11.1.2 | Anti-automation | **Implemented** | Rate limits (`NFR-SEC-10`) |

---

## V12 — Files and resources

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 12.1.1 | File upload controls | **Verified** | Size, magic bytes, page limit (`NFR-SEC-05`) |
| 12.4.1 | Download controls | **Verified** | Attachment disposition; tenant-scoped auth (`NFR-PRI-05`) |

---

## V13 — API and web service

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 13.1.1 | Generic API security | **Verified** | OWASP API Top 10 mapping in NFR §2.3 |
| 13.2.1 | REST / JSON security | **Verified** | OpenAPI diff CI; no mass assignment |
| 13.4.1 | SSRF prevention | **Verified** | Config-only outbound URLs (`NFR-SEC-22`) |

---

## V14 — Configuration

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 14.1.1 | Secure build and deploy | **Verified** | Non-root, read-only FS, PSA restricted (`NFR-SEC-11`) |
| 14.2.1 | Security headers | **Verified** | CSP, nosniff, etc. (`NFR-SEC-09`) |
| 14.5.1 | Admin interfaces protected | **Verified** | Keycloak `/admin` not on ingress (`NFR-SEC-12`); RabbitMQ admin separate |

---

## V15 — Data validation and injection

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 15.1.1 | Injection prevention | **Verified** | EF Core parameterised; no shell from input (`NFR-SEC-21`) |

---

## V16 — Components and dependencies

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 16.1.1 | Dependency hygiene | **Verified** | CI vulnerability gates (`NFR-SEC-13`) |
| 16.2.1 | Unmaintained components | **Implemented** | Central package management; Renovate Could (Stage 9) |

---

## V17 — Logging and monitoring

| ASVS | Requirement (L2 summary) | Status | Evidence / notes |
| --- | --- | --- | --- |
| 17.1.1 | Security logging | **Implemented** | OBS/AUD NFRs; Grafana Loki |
| 17.2.1 | Log protection | **Implemented** | Logs in cluster; no PII in log pipeline by policy |

---

## AI extraction (OWASP LLM Top 10)

Mapped in NFR §2.3 (LLM01–LLM10). Controls **`NFR-SEC-15`**, **`NFR-AI-03`**, **`NFR-AI-04`**: no tools, schema validation, human approval, local Ollama only.

---

## Completion criteria (Stage 8)

- [x] Every **applicable** Level 2 row above has a status and evidence pointer.
- [x] **N/A** rows include a short justification.
- [x] Credential rotation drill documented and scripted ([`credential-rotation-drill.md`](../runbooks/credential-rotation-drill.md)).
- [ ] Operator-run drill evidence (date, timings) kept outside this repo (e.g. portfolio notes).

When adding a trust boundary or feature, add or update rows here in the same pull request.
