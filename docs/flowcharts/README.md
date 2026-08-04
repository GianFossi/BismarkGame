# Flowcharts

A hierarchical set of flowcharts documenting the game logic, from macro
architecture down to individual function decision trees. Source files
are [Mermaid](https://mermaid.js.org) (`.mmd`) — plain text, diffable,
and editable without special tools (the VS Code extension
`bierner.markdown-mermaid` is in `.vscode/extensions.json` for live
preview).

## Structure

```
00-overview/                          Two diagrams: the rules-level turn
                                       cycle, and the software-level
                                       Command dispatch architecture.
01-phases/                            One flowchart per Sequence of Play
                                       phase (rule 4.0), macro level —
                                       what each phase does and which
                                       Update.fs command(s) drive it.
02-detailed-functions/                Function-level decision trees for
                                       the five most rule-dense handlers:
                                       MoveShip+fuelCost, SearchZone,
                                       naval combat placement, naval fire
                                       resolution, and Mobilize.
```

Every macro-level flowchart in `01-phases/` that has a corresponding
detailed one in `02-detailed-functions/` says so in its own top comment
and in an in-diagram note — follow those cross-references to go from
"what happens in this phase" to "exactly how this one function decides."

## Rendering to print-ready A3 pages

These are checked in as source only (`docs/flowcharts/**/*.pdf|png|svg`
is gitignored) — render them locally:

```bash
# One-time setup:
npm install -g @mermaid-js/mermaid-cli
npx puppeteer browsers install chrome-headless-shell   # if mmdc can't find Chrome

# Render one file to an A3-sized PNG (2339x3307px ≈ 200dpi portrait A3):
mmdc -i 00-overview/00-turn-cycle.mmd -o 00-overview/00-turn-cycle.png -w 2339 -H 3307

# Render everything at once:
for f in $(find . -name '*.mmd'); do
  mmdc -i "$f" -o "${f%.mmd}.png" -w 2339 -H 3307
done
```

For the wider diagrams (naval combat, the detailed function flowcharts),
swap width/height for landscape A3 (3307x2339) — check each rendered
image and re-run with the other orientation if content is cramped.

If `mmdc` can't find a Chrome binary (common in sandboxed/CI
environments), point it at one explicitly:

```bash
echo '{"executablePath": "/path/to/chrome", "args": ["--no-sandbox"]}' > puppeteer-config.json
mmdc -i input.mmd -o output.png -w 2339 -H 3307 -p puppeteer-config.json
```

To print: open the PNG in any image viewer, print, and select "Fit to
page" with A3 selected as the paper size — the images are sized close
enough to A3's aspect ratio (297x420mm) that no cropping should occur.
