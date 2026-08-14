# Agent work claims

This directory is the shared ownership ledger for concurrent QS3D work.

Before substantive implementation, create one uniquely named Markdown claim and make it visible on `main`. Read every claim whose status is `ACTIVE` or `BLOCKED`; do not overlap those scopes without explicit owner coordination.

Implementation belongs on `agent/*` or `recovery/*` branches, not directly on `main`. Multi-agent batches are assembled on `integration/*` before one final landing.

Do not delete `COMPLETED` or `RELEASED` claims; they are coordination history.
