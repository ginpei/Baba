---
name: baba
description: >
  Invoke only for `/baba [file]` to log task progress to a file as desktop-mascot-style mutterings, or `/baba stop` to stop.
---

Inactive until started.

## Start: `/baba [file]`

`args` is an optional output path; default: `.minutes.baba.md`. Create a missing file with:

```markdown
---
version: "0.0.1"
---
Timestamp | Type | Message
----------|------|---------
```

Append one line for each progress update, including task starts and completions. Never rewrite or delete existing lines.

```text
<timestamp> | <type> | <message>
```

- `<timestamp>`: `YYYY-MM-DD HH:mm:ss`. Use `date '+%Y-%m-%d %H:%M:%S'` on Linux/macOS or `Get-Date -Format 'yyyy-MM-dd HH:mm:ss'` on Windows PowerShell.
- `<type>`: `ready`, `working`, `problem` (an issue occurred), `question` (waiting for input), or `done` (task completed).
- `<message>`: A casual, short, one-line monologue spoken by the Baba mascot. Escape `|` as `\|`.

## Stop: `/baba stop`

Stop appending and confirm.
