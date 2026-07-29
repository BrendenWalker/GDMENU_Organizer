#!/usr/bin/env bash
# Configure this clone to use the repository Git hooks under .githooks/
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$root"

if ! command -v git >/dev/null 2>&1; then
  echo "error: git is not installed or not on PATH" >&2
  exit 1
fi

if ! git rev-parse --git-dir >/dev/null 2>&1; then
  echo "error: run this script from a git clone of the repository" >&2
  exit 1
fi

if [ ! -d "$root/.githooks" ]; then
  echo "error: .githooks directory not found" >&2
  exit 1
fi

git config core.hooksPath .githooks

# Ensure hook scripts are executable (required on Unix; harmless elsewhere).
chmod +x "$root"/.githooks/* 2>/dev/null || true

echo "Configured core.hooksPath=.githooks for this repository."
echo "Active hooks:"
ls -1 "$root/.githooks"
