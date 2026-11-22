# Contributing

Veyra is GPL-3.0-or-later. By contributing you agree your work is licensed under the same terms.

## Ground rules

- A feature is complete only when it builds, has tests where testable, and has recorded evidence on
  real hardware. "It compiles" is not evidence.
- Never claim support for hardware, vendors or OS versions that were not tested. Add them to
  `COMPATIBILITY.md` with the evidence, or mark them unverified.
- No feature may leave a display or colour setting changed without a confirmed way back.
- Colour and display writes must restore exact originals, including after a crash.
- The overlay must never inject, hook or interact with anti-cheat systems.
- Do not present system-wide measurements as per-process values.

## Workflow

1. `dotnet build Veyra.slnx` and `dotnet test Veyra.slnx` must pass.
2. Keep platform code behind the existing interfaces (`IColourProvider`, `IDisplayService`).
3. Add tests for pure logic: ramp maths, profile resolution, parsing, snapshot checksums, storage.
4. Update `CHANGELOG.md` and, when behaviour or support changes, `COMPATIBILITY.md`.
5. Do not commit secrets, signing keys, user data or machine-specific paths.

## Code style

- Nullable enabled, file-scoped namespaces, `sealed` by default.
- Async methods end in `Async` and take a `CancellationToken` where work can be cancelled.
- Failures return `OperationResult` with a message a user can act on; exceptions are for bugs.
- XAML resources live in `Theme/`; pages do not hard-code colours.

## Reporting bugs

Include the Windows version, GPU and driver version, display configuration, what you expected, what
happened, and the relevant log from `%LOCALAPPDATA%\Veyra\logs`.
