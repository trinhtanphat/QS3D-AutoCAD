# Native candidate artifact handoff

The normal `CI` workflow publishes one **engineering/native-acceptance candidate** from each successful push to `main`. This artifact is a handoff mechanism for licensed AutoCAD qualification. It is not a commercial release, does not imply native PASS, and may be unsigned.

## Artifact identity

The Actions artifact name is:

```text
QS3D-AutoCAD-native-candidate-<40-character-main-SHA>
```

The workflow uploads it only after `scripts/package.ps1 -Version 0.0.0-ci` and `scripts/verify-artifacts.ps1 -Version 0.0.0-ci` succeed on the exact checkout SHA.

The artifact contains exactly the verified handoff files:

```text
QS3D-AutoCAD-0.0.0-ci.zip
QS3D-AutoCAD-0.0.0-ci-Setup.exe
RELEASE-PROVENANCE.json
SHA256SUMS.txt
```

GitHub also records an immutable Actions-artifact id/digest. The QS3D provenance/checksum files remain authoritative for the candidate files themselves.

## GitHub Releases test handoff

After a successful push CI on `main`, `.github/workflows/engineering-release.yml` republishes the **same already-verified Actions artifact** to the repository Releases page as a GitHub prerelease. It does not rebuild, resign, or replace the candidate.

Engineering release tags use the explicit test-only form:

```text
test-v0.1.0-ci.<CI-run-number>
```

The tag is created at the exact CI-tested `main` SHA. The prerelease contains the same Setup.exe, bundle ZIP, `RELEASE-PROVENANCE.json`, and `SHA256SUMS.txt`. The workflow re-runs artifact verification and refuses a provenance/source-SHA mismatch before publication.

The `test-v...` prefix is intentionally outside the signed production workflow's `v*` tag namespace. A GitHub engineering prerelease remains unsigned and must never be represented as a signed commercial release or native AutoCAD PASS.

## Native test preparation

1. Choose a successful `main` CI run whose exact SHA is the candidate intended for qualification.
2. Download either the Actions artifact whose name ends with that exact 40-character SHA or the GitHub engineering prerelease whose tag resolves to that exact SHA. Do not use a PR artifact, another commit, or mixed assets from different runs.
3. Place the four extracted files under the repository/worktree `artifacts/` directory used by the native acceptance scripts.
4. Verify before installation:

```powershell
./scripts/verify-artifacts.ps1 `
  -Version 0.0.0-ci `
  -ExpectedCommit <exact-40-character-SHA>
```

5. Confirm `RELEASE-PROVENANCE.json` reports the same source SHA and that `SHA256SUMS.txt` matches the ZIP, Setup.exe and provenance file.
6. Use the generated `QS3D-AutoCAD-0.0.0-ci-Setup.exe` for the native installer/autoload tests. Do not substitute a locally rebuilt DLL or manual `NETLOAD` package.
7. Create AutoCAD 2025/2026/2027 evidence sessions with `-Version 0.0.0-ci` so the session creator binds them to the exact handoff provenance/artifact hashes.

## Evidence-chain rule

All three AutoCAD generations must test the same downloaded candidate. Rebuilding source, repackaging, editing the ZIP, replacing Setup.exe or mixing files from different workflow runs invalidates the evidence chain and requires new sessions.

The Actions artifact retention window is intentionally finite. A GitHub engineering prerelease may remain available longer, but its tag/provenance/checksums must still resolve to the exact qualified SHA. If a candidate is replaced by a newer intended qualification target, qualify the newer exact candidate instead of silently mixing versions.

## Security boundary

The engineering candidate and engineering GitHub prerelease are published without signing secrets/private keys. Production release signing remains fail-closed and separate. A successful artifact/release upload proves only that the hosted runner produced, verified, and handed off the exact package; licensed AutoCAD behavior must still be recorded through `docs/NATIVE-ACCEPTANCE.md` and the native evidence scripts.
