# QS3D AutoCAD release security

## Release invariants

A GitHub tag is not sufficient evidence to publish QS3D AutoCAD. The release workflow is intentionally fail-closed and requires all of the following:

1. The tagged commit is an ancestor of `origin/main`.
2. Repository variable `QS3D_NATIVE_ACCEPTED_SHA` equals the exact tagged commit SHA.
3. Repository secrets `QS3D_SIGNING_PFX_BASE64` and `QS3D_SIGNING_PFX_PASSWORD` contain a real Authenticode certificate and password.
4. The package is built from a clean tracked working tree.
5. The staged AutoCAD assemblies and Setup.exe are signed and verified with `signtool`.
6. `RELEASE-PROVENANCE.json` records the exact source SHA, version, host matrix, signing state, artifact sizes and SHA-256 hashes.
7. `scripts/verify-artifacts.ps1 -RequireSigned` validates provenance and `SHA256SUMS.txt` before GitHub Release creation.

`QS3D_TIMESTAMP_URL` is an optional repository variable for the RFC3161 timestamp endpoint. The workflow uses its documented default only when the variable is empty.

## Native acceptance handoff

Issue #4 owns native runtime qualification. After an exact candidate has passed AutoCAD 2025, 2026 and 2027 acceptance, set `QS3D_NATIVE_ACCEPTED_SHA` to that exact full 40-character commit SHA. Do not point the variable at a moving branch name.

If source changes after acceptance, the SHA changes and the release gate must fail until the new exact commit is accepted again.

## Signing secret handling

Store the PFX only as a GitHub Actions secret encoded as base64. Never commit the certificate private key, PFX bytes or password to the repository. The release workflow decodes the PFX into the runner temporary directory, uses it for signing, and deletes the temporary PFX in a `finally` block.

The manual `Package native AutoCAD release` workflow intentionally produces an unsigned engineering package unless explicit signing inputs are added in a controlled environment. An unsigned package must not be represented as a production-signed commercial release.

## Provenance files

Every package operation creates:

- `QS3D-AutoCAD-<version>.zip`
- `QS3D-AutoCAD-<version>-Setup.exe`
- `RELEASE-PROVENANCE.json`
- `SHA256SUMS.txt`

The provenance manifest contains hashes for the ZIP and Setup executable. `SHA256SUMS.txt` additionally hashes the provenance manifest itself, avoiding a self-referential hash inside the manifest.

## Installer safety

Setup refuses install, upgrade and uninstall while `acad.exe` is running. Upgrades are staged in the Autodesk `ApplicationPlugins` directory, validated for all required host payloads, and only then swapped into place. If the new candidate cannot replace the installed bundle, Setup attempts to restore the previous bundle from its backup instead of deleting the working install first.

## Licensing and update service boundary

This repository does not contain a production licensing backend, activation token issuer or update network service. Do not add an always-allow license implementation, embed production secrets, or treat artifact checksum verification as a complete secure updater.

Pure-Core client trust primitives are defined in `src/QS3D.Core/Commercial` and documented in `docs/COMMERCIAL-TRUST-BOUNDARY.md`:

- `LicensePolicy` deterministically evaluates an already-authenticated lease snapshot as active, bounded offline grace or denied. It does not authenticate/issue a lease and never mutates drawing data.
- `UpdateManifestVerifier` verifies an externally signed RSA-PSS/SHA-256 manifest with an externally configured public key, requires an HTTPS package URI, enforces update channel and AutoCAD-generation compatibility, and verifies the downloaded package SHA-256 before a future install step.

Production activation still requires a real authenticated backend/token contract. Production updating still requires a real manifest/download service, protected publisher private key, rollback execution and native acceptance for the target AutoCAD generation. The plugin must never contain the updater private signing key or convert missing/invalid service state into implicit authorization.
