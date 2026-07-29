# Branch and merge workflow

`main` is protected. Do not commit to `main`, push directly to `main`, or merge locally into `main`.

## Required flow

1. Create a feature branch from up-to-date `main` (e.g. `feature/…`, `fix/…`, `docs/…`).
2. Commit all work on that branch.
3. Push the feature branch and open a pull request into `main`.
4. Merge only via the pull request (after review/CI as required by the repo).

## Agent behavior

- If asked to commit or push while on `main`, create or switch to a feature branch first (or ask which branch name to use).
- Never use `git push origin main` for feature work; push the feature branch with `-u` when needed.
- When asked to open a PR, use `gh pr create` targeting `main`.
- Do not amend, force-push, or rewrite history on `main`.
