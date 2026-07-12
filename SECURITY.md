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

- Downloads the community song-title spreadsheet on load (six public CSV sheets
  from Google Docs) so track names stay current. If the download fails, the
  plugin falls back to the copy bundled with it.

That download is the plugin's only network activity, and the request carries
nothing about you. Nothing about your usage leaves your machine. There is no
telemetry and no account or login of any kind.

## Secrets

No keys, tokens, or secrets are committed to this repository. The plugin holds no
credentials; the spreadsheet it fetches is public and needs none.
