# Security Policy

## Supported Versions

The WHONET development team actively maintains and provides security updates for the latest release of **AMRIE** targeting **.NET 10 LTS** and **.NET Framework 4.8**.

| Version | Target Frameworks | Supported |
| :--- | :--- | :--- |
| **26.x (Current)** | `.NET 10.0 LTS`, `.NET Framework 4.8` | ✅ Yes |
| **< 26.0 (Legacy)** | `.NET 8.0`, `.NET Framework 4.8` | ❌ No (Please upgrade to 26.x) |

---

## Reporting a Vulnerability

We take the security and data integrity of clinical laboratory and public health software seriously.

> [!IMPORTANT]
> **Please do NOT report security vulnerabilities through public GitHub issues, discussions, or pull requests.**

### How to Report
If you discover a potential security vulnerability in `AMRIE.Library`, `AMRIE.CLI`, `AMRIE.WinUI`, `AMRIE.WinForms`, or our installer packages:

1. **GitHub Private Vulnerability Reporting:** Use the [GitHub Security Advisories](https://github.com/WHONET-Development/AMRIE/security/advisories/new) feature on this repository to submit a private report directly to the maintainers.
2. **Direct Contact:** Alternatively, reach out to the WHONET development team via the official contact methods listed at **[https://whonet.org](https://whonet.org)**.

### What to Include in Your Report
To help us triage and resolve the issue quickly, please include:
- The affected component(s) (`AMRIE.Library`, `AMRIE.CLI`, `AMRIE.WinUI`, `AMRIE.WinForms`, or WiX MSI Installer).
- The version or commit SHA tested.
- A clear description of the vulnerability and its potential impact (e.g., malformed file parsing, path traversal, arbitrary code execution, denial of service / memory exhaustion).
- Step-by-step instructions or a minimal proof-of-concept (using strictly synthetic, non-patient data) to reproduce the issue.

### Response Timeline & Disclosure Policy
- **Acknowledgment:** We aim to acknowledge receipt of vulnerability reports within **5 business days**.
- **Assessment & Remediation:** We will investigate the report, keep you informed of our progress, and prepare a fix and regression test.
- **Coordinated Disclosure:** We ask that you give us a reasonable window (typically up to 90 days) to release a patched version before making any public disclosure. Credit will be given in the release notes and security advisory unless you prefer to remain anonymous.

---

## Security Design Practices in AMRIE

- **Bounded Memory File Parsing:** `IO_Library.InterpretDataFileBounded` enforces strict per-batch row limits (`2,048` rows) and character budgets (`4,194,304` UTF-16 characters), preventing memory exhaustion (DoS) when parsing large or malformed delimited files.
- **Bounded Cache Eviction:** In-memory caches (`BoundedCache`) enforce hard entry and character caps with deterministic FIFO eviction.
- **Atomic File Publication:** Batch outputs are staged to temporary files in the destination directory and published via atomic rename only after successful completion.
- **Code Signing:** Official Release binaries and MSI installers are Authenticode-signed using SHA-256 digests and RFC 3161 timestamping.

