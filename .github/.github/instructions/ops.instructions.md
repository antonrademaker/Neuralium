# Ops & Infrastructure Instructions

## Containers
- Podman-compatible
- Rootless by default
- Minimal base images

## systemd
- One-shot services
- Timers must be idempotent

## Scripts
- POSIX shell with powershell
- Safe defaults (`set -euo pipefail`)
