# QS3D AutoCAD operational gates

This runbook turns the remaining native, governance and commercial release gates into explicit operator workflows. These commands **do not** manufacture a licensed AutoCAD PASS, GitHub administrator success, signing credentials or production services. They only prepare, verify and report evidence that actually exists.

## 1. Prepare one exact engineering candidate

Use the engineering prerelease generated from a successful exact-main CI run:

```powershell
./scripts/prepare-native-candidate.ps1 `
  -Tag test-v0.1.0-ci.<run-number>
```

The helper resolves the Git tag to its exact commit SHA, requires a GitHub prerelease, downloads `RELEASE-PROVENANCE.json` and `SHA256SUMS.txt` plus the exact ZIP/Setup assets, verifies provenance and hashes in a temporary directory, then transactionally places the four files under `artifacts/`.

It writes local `artifacts/NATIVE-CANDIDATE.json` metadata and refuses mixed/stale `QS3D-AutoCAD-*.zip` / `*.exe` files. If an expected SHA is already known, bind it explicitly:

```powershell
./scripts/prepare-native-candidate.ps1 `
  -Tag test-v0.1.0-ci.<run-number> `
  -ExpectedCommit <40-character-SHA>
```

The manual download procedure in `NATIVE-CANDIDATE-HANDOFF.md` remains a fallback, but the preparation helper is preferred because it checks tag -> exact commit -> provenance -> checksums as one fail-closed operation.

## 2. Inspect native acceptance progress

After preparing the candidate, inspect the three supported host generations:

```powershell
./scripts/show-native-acceptance-status.ps1
```

The report shows AutoCAD 2025/2026/2027 host detection, evidence-file state, candidate-SHA match, and pass/fail/blocked/pending counts against `native-acceptance/required-checks.json`.

For machine-readable output:

```powershell
./scripts/show-native-acceptance-status.ps1 -Json
```

A status report is not native acceptance. Real evidence must still be created and recorded with `new-native-acceptance.ps1`, `record-native-runtime.ps1` and `record-native-result.ps1`, then finalized by `validate-native-acceptance.ps1` exactly as documented in `NATIVE-ACCEPTANCE.md`.

## 3. Protect `main` with real GitHub settings

Repository Markdown rules cannot physically prevent an authenticated writer from bypassing the branch-first policy. Use the helper only with an authenticated `gh` session whose token has repository-administration permission.

Read back the current state without modifying it:

```powershell
./scripts/configure-main-protection.ps1 -Mode Verify
```

Apply the intended policy and immediately read it back:

```powershell
./scripts/configure-main-protection.ps1 `
  -Mode Apply `
  -ConfirmApply
```

The requested policy requires PR-based updates, strict `core-host-and-guards` status checking, administrator enforcement, resolved conversations, and forbids force-push/deletion. The script fails unless GitHub's read-back actually satisfies those conditions.

Issue #11 may close only after this remote read-back succeeds. Merely landing this helper is not proof that branch protection is enabled.

## 4. Check commercial release readiness

Run the preflight before attempting a production `v*` tag:

```powershell
./scripts/test-commercial-release-readiness.ps1
```

For automation that must fail when any prerequisite is missing:

```powershell
./scripts/test-commercial-release-readiness.ps1 -FailOnBlocked
```

The preflight checks the prepared exact candidate, exact `QS3D_NATIVE_ACCEPTED_SHA`, parseable Authenticode PFX/private key + password, an HTTPS licensing endpoint, an HTTPS update-manifest endpoint with a public verification key, and the explicit telemetry/privacy posture.

Supported environment/config inputs are:

- `QS3D_NATIVE_ACCEPTED_SHA`
- `QS3D_SIGNING_PFX_BASE64`
- `QS3D_SIGNING_PFX_PASSWORD`
- `QS3D_LICENSE_API_URL`
- `QS3D_UPDATE_MANIFEST_URL`
- `QS3D_UPDATE_PUBLIC_KEY_PEM`
- `QS3D_TELEMETRY_MODE` (`disabled` or `opt-in`)
- `QS3D_TELEMETRY_ENDPOINT` when telemetry is `opt-in`

The preflight does not call an always-allow license path, does not print secret values and does not publish a release. `.github/workflows/release.yml` remains the authoritative fail-closed production signing/native gate.

## 5. CI regression coverage

Hosted CI runs `scripts/test-ops-tooling.ps1`. The smoke test uses a deterministic local fixture to prove:

- artifact verification works against an explicit artifacts directory;
- exact candidate preparation validates provenance/checksums and rejects mixed/stale assets;
- empty native evidence never becomes PASS;
- commercial readiness remains blocked when real native/signing/service prerequisites are missing;
- branch-protection helper source retains apply confirmation and read-back invariants.

Hosted CI intentionally does **not** apply GitHub administration settings, contact production licensing/update services, create native PASS evidence, or require production secrets.

## Completion boundary

Repository-side operational tooling can be complete while external gates remain open. The product may be called native-qualified or commercially release-ready only after real licensed AutoCAD evidence, remote GitHub protection read-back, production service configuration and Authenticode release evidence satisfy their corresponding issues and release policy.