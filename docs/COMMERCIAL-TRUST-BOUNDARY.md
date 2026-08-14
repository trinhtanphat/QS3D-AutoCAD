# Commercial trust boundary

Issue #6 ultimately requires real production licensing, signing and update services. The repository now contains pure-Core trust primitives that define how those future services must be consumed without pretending that a backend or production credential already exists.

## License lease decision

`LicensePolicy` evaluates a `LicenseLeaseSnapshot` and returns one of three states:

- `Active`: the authenticated lease is inside its normal validity window.
- `OfflineGrace`: normal validity has expired, but the explicit bounded offline-grace window has not.
- `Denied`: the lease is missing, malformed, not yet valid, expired, or bound to another device.

The snapshot carries account, subscription, device and seat identities plus `IssuedAtUtc`, `ValidUntilUtc` and `OfflineGraceUntilUtc`.

`LicensePolicy` does **not** authenticate or issue a lease. A future production activation client must first validate the backend response/token signature and only then construct/pass the snapshot. Treating arbitrary local data as an authenticated lease would violate this boundary.

License decisions are pure data decisions. They do not open a DWG transaction, erase/modify entities, or implement an always-allow development bypass. Runtime enforcement remains separate until the production backend and product policy are approved.

## Signed updater manifest

`UpdateManifestVerifier` consumes a JSON envelope with two fields:

```json
{
  "payload": "<base64 encoded UTF-8 manifest payload>",
  "signature": "<base64 RSA-PSS/SHA-256 signature over the exact payload bytes>"
}
```

The verified payload contains:

- schema version;
- update channel;
- QS3D version;
- minimum/maximum supported AutoCAD generation;
- absolute HTTPS package URI;
- expected package SHA-256;
- publication timestamp.

The verifier requires an externally configured public key in PEM form, verifies RSA-PSS with SHA-256 before parsing/trusting the payload, validates channel and AutoCAD-generation compatibility, requires HTTPS, and validates the package SHA-256 format.

After a future downloader obtains the package bytes, `VerifyPackage` compares SHA-256 using a fixed-time comparison before any install/replace operation.

## Production-key rule

Production source contains verification only. The updater private signing key must live in the future release/update publishing system and must never be committed, embedded, or reconstructed inside the AutoCAD plugin.

Smoke tests generate an ephemeral RSA key only to prove the verifier accepts a valid signature and rejects tampering. Test keys are not release credentials.

## What remains intentionally absent

This source lane does not add:

- account login or activation network calls;
- a production license/token issuer;
- secret storage or embedded credentials;
- update manifest fetching/downloading;
- automatic installation/rollback execution;
- production updater signing keys;
- telemetry upload endpoints;
- a code path that calls a local placeholder and grants production access.

Those remain issue #6 work that requires real service endpoints, credential/key management and product policy. The current primitives are the fail-closed client-side contract those future pieces must satisfy.
