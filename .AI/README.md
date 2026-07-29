# AI developer guidance

Canonical, tool-agnostic instructions for coding agents working on this repository.

Tool-specific entry points only **point here** — do not duplicate rule bodies elsewhere.

| Entry point | Tool |
|-------------|------|
| [`AGENTS.md`](../AGENTS.md) | Cursor, Codex, and other AGENTS.md-aware tools |
| [`CLAUDE.md`](../CLAUDE.md) | Claude Code (`@AGENTS.md` import) |
| [`.cursor/rules/ai-guidance.mdc`](../.cursor/rules/ai-guidance.mdc) | Cursor project rules (always apply) |

## Active rules

| File | Topic |
|------|--------|
| [git-workflow.md](./git-workflow.md) | Protected `main`, feature branches, PRs only |
| [commit-and-pr-style.md](./commit-and-pr-style.md) | No trailers or tool branding in commits/PRs |

## Recommended next rules

Suggested additions (create when you want them enforced):

1. **`architecture.md`** — Library vs Cards vs Settings responsibilities; SQLite as source of truth for library/cards; Write SD Card owns drive selection and `Manager.Save`; avoid overloading `Manager.ItemList` across unrelated tabs without reloading.
2. **`avalonia-ui.md`** — After `AvaloniaXamlLoader.Load`, resolve named controls with `FindControl` (generated name fields stay null with this project's Load pattern); TabControl `SelectionChanged` must ignore bubbled ListBox/DataGrid selection; prefer dialogs for focused input (see `TextInputWindow`).
3. **`database.md`** — Schema lives in `Schema.sql` + versioned migrations in `AppDatabase`; repositories only; AppData paths via `AppStorage` (`app.db`, `settings.json`); never commit DB files or user settings.
4. **`build-and-ci.md`** — Target `net10.0` / Avalonia 12; verify with `dotnet build` / `dotnet test` on `src/GDMENUOrganizer.sln`; keep GitHub Actions on supported Node runtimes (e.g. `softprops/action-gh-release@v3`).
5. **`security-and-secrets.md`** — Do not commit secrets, tokens, or local AppData; treat SD/library paths as user machine-specific.
6. **`docs.md`** — Keep `README.md` aligned with real UX when behavior changes; screenshots under `docs/` may lag — call that out if still outdated.

When adding a rule: put the full markdown in `.AI/`, list it under **Active rules**, and leave tool entry points unchanged.

## Local setup

Point Git at the repo hooks (strips Cursor/Claude attribution trailers as a backstop):

```bash
git config core.hooksPath .githooks
```

Also turn off Cursor **Settings → Agent → Attribution** (Commit + PR) so the product does not inject `--trailer` / “Made with Cursor” in the first place.
