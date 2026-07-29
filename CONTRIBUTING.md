# Contributing

Thanks for contributing to GDMENU Organizer.

## Branch workflow

`main` is protected. All work must land through a pull request:

1. Create a feature branch from up-to-date `main` (for example `feature/…`, `fix/…`, `docs/…`).
2. Commit on that branch.
3. Push the branch and open a PR targeting `main`.
4. Merge only via the pull request after review/CI.

Do not commit directly to `main`, push to `main`, or merge locally into `main`.

Full agent-facing detail: [`.AI/git-workflow.md`](.AI/git-workflow.md).

## AI developer guidance

Canonical instructions for humans and coding agents live under [`.AI/`](.AI/README.md). Tool-specific files only **point** there — do not duplicate rule bodies.

| Entry point | Used by |
|-------------|---------|
| [`.AI/`](.AI/README.md) | Source of truth |
| [`AGENTS.md`](AGENTS.md) | Cursor, Codex, and other AGENTS.md-aware tools |
| [`CLAUDE.md`](CLAUDE.md) | Claude Code (`@AGENTS.md` import) |
| [`.cursor/rules/ai-guidance.mdc`](.cursor/rules/ai-guidance.mdc) | Cursor always-on project rule |

### Active rules

| File | Topic |
|------|--------|
| [`.AI/git-workflow.md`](.AI/git-workflow.md) | Protected `main`, feature branches, PRs only |
| [`.AI/commit-and-pr-style.md`](.AI/commit-and-pr-style.md) | No trailers or tool branding in commits/PRs |

When adding guidance, put the full markdown in `.AI/`, list it in [`.AI/README.md`](.AI/README.md), and leave the tool entry points unchanged.

### Commit and PR style

Do not add git trailers (`--trailer`, `Co-authored-by:`, `Made-with:`, and similar) or AI/product branding to commit messages or pull request text unless a maintainer explicitly asks for a specific trailer. See [`.AI/commit-and-pr-style.md`](.AI/commit-and-pr-style.md).

If you use Cursor, also turn off **Settings → Agent → Attribution** (Commit Attribution and PR Attribution) so the product does not inject trailers automatically.

## Install Git hooks

This repo uses `core.hooksPath` pointed at [`.githooks/`](.githooks/). The `commit-msg` hook strips common Cursor/Claude attribution trailers as a backstop.

Run **one** of these from the repository root after cloning (or after resetting Git config):

**Linux / macOS / Git Bash**

```bash
./setup-hooks.sh
```

**Windows (cmd / PowerShell)**

```bat
setup-hooks.bat
```

Both scripts run `git config core.hooksPath .githooks` for this repository only (not a global setting). The shell script also marks hooks as executable.

Verify:

```bash
git config --get core.hooksPath
# expect: .githooks
```

## Building

See [README.md](README.md) for .NET 10 / Avalonia build and publish instructions.
