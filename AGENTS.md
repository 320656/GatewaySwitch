# Repository Instructions

- Whenever the user mentions code changes, edits, fixes, or implementation work, run `powershell -ExecutionPolicy Bypass -File .\scripts\local-build.ps1` after the changes to build the project.
- Treat `scripts\local-build.ps1` as the canonical local build command. It outputs timestamped Release builds under `bin\Release\GatewaySwitch-yyyy-MM-dd-HHmmss`.
- Report the generated build output directory and whether the build succeeded.
