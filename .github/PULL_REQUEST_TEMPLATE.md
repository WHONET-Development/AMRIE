## Summary

<!-- Provide a concise summary of the changes introduced by this Pull Request. -->

Fixes #<!-- Issue number, if applicable -->

## Type of Change

- [ ] **Bug Fix** (non-breaking change which fixes an issue)
- [ ] **Clinical Resource / Rule Update** (updates to breakpoints, organisms, intrinsic rules, or expert rules)
- [ ] **New Feature / Enhancement** (non-breaking change which adds functionality)
- [ ] **Breaking Change** (fix or feature that would cause existing functionality or APIs to change)
- [ ] **Documentation / Licensing Update**

## Verification & Testing Checklist

- [ ] `AMRIE.Library` builds cleanly for both `net10.0` and `net48` targets (`dotnet build AMRIE.Library\AMRIE.Library.csproj`).
- [ ] All unit and matrix tests in `AMRIE.Library.Tests` pass (`dotnet test AMRIE.Library.Tests\AMRIE.Library.Tests.csproj`).
- [ ] Added or updated unit tests covering the modified behavior.
- [ ] Updated relevant documentation under [`docs/`](docs/index.md) and [`CHANGELOG.md`](CHANGELOG.md).
- [ ] If third-party package dependencies were added or updated, [`licenses/THIRD-PARTY-NOTICES.md`](licenses/THIRD-PARTY-NOTICES.md) and WiX installer manifests (`Package.wxs` / `Windows Installer.wxs`) have been updated accordingly.

## Legal & Data Privacy Attestation

- [ ] **No PHI/PII:** I confirm that this Pull Request contains **no** patient health information (PHI) or personally identifiable information (PII), and all test data is strictly synthetic or de-identified in accordance with HIPAA/GDPR.
- [ ] **License Compliance:** I agree that my contribution is licensed under Section 4 of the [MGB Open Access License 1.0](License.md).

