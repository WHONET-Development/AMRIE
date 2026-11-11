# How to Publish AMRIE to winget

This guide explains the automated GitHub Actions workflow that submits AMRIE to the [Windows Package Manager Community Repository](https://github.com/microsoft/winget-pkgs), and how to run the submission manually if the automation fails.

## Package identity

| Field | Value |
|---|---|
| **Package ID** | `WHONET.AMRIE` |
| **Repository** | `microsoft/winget-pkgs` |
| **Submission tool** | [Komac](https://github.com/michidk/run-komac) via `michidk/run-komac@v2` |

---

## Automated workflow (`publish-to-winget.yml`)

### Trigger

The workflow fires when a **GitHub Release** is published (not drafted or pre-released):

```yaml
on:
  release:
    types: [released]
```

### What it does

1. **Checks out** the repository.
2. **Strips the `v` prefix** from the release tag (e.g., `v26.11.11` → `26.11.11`).
3. **Runs Komac** to create or update the `WHONET.AMRIE` manifest in `microsoft/winget-pkgs` and opens a Pull Request:

```
komac update WHONET.AMRIE \
  --version <clean_version> \
  --urls https://github.com/<owner>/<repo>/releases/download/<tag>/AMR_Interpretation_Engine_<tag>.msi \
  --submit \
  --token ${{ secrets.WINGET_TOKEN }}
```

> [!IMPORTANT]
> The workflow requires a repository secret named **`WINGET_TOKEN`** — a GitHub Personal Access Token with `public_repo` scope, authorized to submit PRs to `microsoft/winget-pkgs`. Set this in **Settings → Secrets and variables → Actions**.

### Expected release asset naming

The workflow constructs the MSI URL using the release tag directly. Your release **must** attach an MSI asset named:

```
AMR_Interpretation_Engine_<tag>.msi
```

For example, tag `v26.11.11` → asset `AMR_Interpretation_Engine_v26.11.11.msi`.

---

## Creating a release (normal flow)

1. Build and sign the MSI: see [Packaging: WiX MSI](./packaging-wix-msi.md).
2. In GitHub, navigate to **Releases → Draft a new release**.
3. Set the tag to `v<yy.M.d>` (e.g., `v26.11.11`), targeting `main`.
4. Upload the signed MSI as a release asset.
5. Click **Publish release** — the workflow triggers automatically.
6. Monitor the Actions tab; on success, a PR is opened in `microsoft/winget-pkgs`.

---

## Manual submission (if automation fails)

Install Komac locally:

```powershell
winget install Komac
```

Then run:

```powershell
komac update WHONET.AMRIE `
    --version 26.11.11 `
    --urls "https://github.com/WHONET-Development/AMRIE/releases/download/v26.11.11/AMR_Interpretation_Engine_v26.11.11.msi" `
    --submit `
    --token <your_github_pat>
```

> [!NOTE]
> Komac validates the manifest schema and the installer hash automatically. Ensure the MSI URL is publicly accessible before submitting.

---

## Checking submission status

After submission, monitor the PR in `microsoft/winget-pkgs`. The Microsoft bot (`wingetbot`) runs automated validation; if it passes, a maintainer merges the PR, and the package becomes available via:

```powershell
winget upgrade WHONET.AMRIE
```

---

## See Also

- [Packaging: WiX MSI](./packaging-wix-msi.md)
- [Building and Testing](./building-and-testing.md)
