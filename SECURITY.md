# Security

## Reporting a vulnerability

Found a security issue? Please open a private report through GitHub's Security
advisories ("Report a vulnerability" on the repo's Security tab) instead of a
public issue. Tell me what you found and how to reproduce it, and I'll get back
to you as soon as I can.

## What the plugin can access

- Reads and changes the game's background music, locally, through Dalamud.
- Reads your in-game volume settings so local tracks play at a matching level.
- Plays audio files you import from your own PC. By default it copies them into
  the plugin's config folder so they keep working if you move the originals.
- Can remove a file, but only when you ask it to. If you tick the optional
  "delete original after importing" box, the plugin deletes that source file once
  it has safely copied it into its own folder. Removing a song from the Local
  Library deletes the copy the plugin made for it. Both are opt-in and ask for
  confirmation first; the plugin never deletes anything on its own.

Nothing leaves your machine. There is no network access, no telemetry, and no
account or login of any kind.

## Secrets

No keys, tokens, or secrets are committed to this repository. The plugin holds no
credentials, because it never talks to anything outside the game.
