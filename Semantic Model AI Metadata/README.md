# Semantic Model AI Metadata

Scripts for managing semantic model AI instructions and AI schema stored in
Power BI / Analysis Services culture linguistic metadata.

These scripts are provided as-is. They edit culture linguistic metadata directly
and are not official supported Tabular Editor product features. Test them on
copies or non-production models before using them on important models.

The v1 VS Code extension exposes these as friendly files:

- `Copilot/Instructions/instructions.md`
- `Copilot/schema.json`

In the tabular model, those map to:

- `CustomInstructions` for AI instructions
- `Entities` for the AI schema

## Scripts

- `Manage Semantic Model AI Metadata.csx`: non-interactive script for reading,
  setting, listing, or deleting AI instructions and AI schema. Best used with
  `te script`.
- `Edit Semantic Model AI Instructions.csx`: TE3 Desktop GUI editor for AI
  instructions. It uses the connected model, defaults to `en-US`, and does not
  require a selected object. It uses a standard WinForms multiline text box.
- `Edit Semantic Model AI Schema.csx`: TE3 Desktop GUI editor for AI schema.
  It opens on an object tree similar to the perspective editor and includes a
  JSON tab for exact roundtrips. The JSON tab uses a standard WinForms
  multiline text box.

## Non-interactive usage

PowerShell:

```powershell
$env:TE_AI_ACTION = "get"
$env:TE_AI_TARGET = "both"
te script -s "workspace" -d "model" `
  -S "Semantic Model AI Metadata/Manage Semantic Model AI Metadata.csx" `
  --output-format json --non-interactive
```

```powershell
$env:TE_AI_ACTION = "set"
$env:TE_AI_TARGET = "instructions"
$env:TE_AI_INPUT_FILE = "instructions.md"
te script -s "workspace" -d "model" `
  -S "Semantic Model AI Metadata/Manage Semantic Model AI Metadata.csx" `
  --save --output-format json --non-interactive
```

```powershell
$env:TE_AI_ACTION = "set"
$env:TE_AI_TARGET = "schema"
$env:TE_AI_INPUT_FILE = "schema.json"
te script -s "workspace" -d "model" `
  -S "Semantic Model AI Metadata/Manage Semantic Model AI Metadata.csx" `
  --save --output-format json --non-interactive
```

In bash/zsh, set the same variables inline before `te script`, for example:

```bash
TE_AI_ACTION=get TE_AI_TARGET=both \
  te script -s "workspace" -d "model" \
  -S "Semantic Model AI Metadata/Manage Semantic Model AI Metadata.csx" \
  --output-format json --non-interactive
```

## Environment variables

- `TE_AI_ACTION`: `list`, `get`, `set`, or `delete`. Default: `get`.
- `TE_AI_TARGET`: `instructions`, `schema`, or `both`.
- `TE_AI_CULTURE`: optional culture name. Defaults to the best available
  culture and creates `en-US` on `set` when needed.
- `TE_AI_INPUT_FILE`: payload file for `set`.
- `TE_AI_INPUT`: inline payload for `set`.
- `TE_AI_OUTPUT_FILE`: optional output file.
- `TE_AI_ALLOW_OVER_LIMIT=true`: allow instructions over 10000 characters.
