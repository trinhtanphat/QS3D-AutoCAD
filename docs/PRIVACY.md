# QS3D AutoCAD privacy posture

## Current behavior

The current QS3D AutoCAD plugin does not send telemetry, drawing geometry, project metadata, account identifiers or usage events to a QS3D server. There is no production login/licensing network call in the current implementation.

Local QS3D metadata is stored inside the DWG through QS3D XData and the Named Objects Dictionary so it travels with the drawing by design.

## Future telemetry requirements

Telemetry must remain disabled unless a user or organization explicitly opts in. Before any production telemetry is enabled, the implementation must define and document:

- an allowlist of event names and fields;
- the purpose for each collected field;
- retention and deletion periods;
- endpoints and processor/operator responsibility;
- organization-level controls where applicable;
- a visible way to disable telemetry again.

Raw DWG files, entity geometry, free-form project names, command-line text, user-entered notes and file paths must not be collected by default.

Crash/error reporting must minimize payloads and scrub local paths, document names and user-entered content before transmission.

## Licensing boundary

A future licensing service may necessarily process account, subscription, device or seat identifiers. Those fields must be separated from optional product analytics, minimized to what activation requires, and documented before the service is enabled.

No source-only placeholder or always-allow license implementation should be described as production licensing.
