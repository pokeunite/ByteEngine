# Markdown to PDF

`Tools/MarkdownToPdf.py` renders a Markdown document to a styled, print-ready PDF
(A4 by default) with a cover page, tables, code blocks and local images. It is
the tool used for `Docs/GDevelop-EventsEditor-Research-2026-11-14.md` and any
other long design document that needs to leave the repository.

## How it works

```text
Markdown  ->  styled HTML  ->  headless Chrome/Edge  ->  PDF
```

The script converts Markdown with PyMarkdown (`extra`, `sane_lists`, `toc`),
inlines a print stylesheet, rewrites every resolvable local image reference to an
absolute `file://` URI, then asks the installed Chrome or Edge to print the HTML
with `--headless=new --print-to-pdf`. No LaTeX, Pandoc or wkhtmltopdf needed.

## Requirements

- Python 3.9+ with PyMarkdown: `python -m pip install markdown`
- Chrome or Edge (auto-detected in the usual Program Files locations)
- Optional: `python -m pip install pypdf` so the script can report the page count

## Usage

```powershell
# next to the source: Docs/MyDoc.pdf
python Tools\MarkdownToPdf.py Docs\MyDoc.md

# explicit destination (creates the folder if needed)
python Tools\MarkdownToPdf.py Docs\MyDoc.md -o "$HOME\Downloads\MyDoc.pdf"

# custom cover, no cover, different page size, tighter images
python Tools\MarkdownToPdf.py Docs\MyDoc.md --title "Release notes" --subtitle "v0.9"
python Tools\MarkdownToPdf.py Docs\MyDoc.md --no-cover --page-size Letter
python Tools\MarkdownToPdf.py Docs\MyDoc.md --image-height 90mm

# extra styling appended to the built-in stylesheet
python Tools\MarkdownToPdf.py Docs\MyDoc.md --css brand.css --accent "#c8641e"

# keep the intermediate HTML for debugging, or point at a specific browser
python Tools\MarkdownToPdf.py Docs\MyDoc.md --keep-html
python Tools\MarkdownToPdf.py Docs\MyDoc.md --browser "C:\Program Files\Microsoft\Edge\Application\msedge.exe"
```

## Images

Images are referenced from Markdown as usual and may be relative or absolute:

```markdown
![Event sheet](Research/Shots/events.png)                  <!-- relative to the .md -->
![Panel](C:\Users\me\Pictures\panel.png)                    <!-- absolute Windows path -->
![Chart](/home/me/charts/fps.png)                           <!-- absolute POSIX path -->
<img src="C:\shots\raw.png" alt="raw HTML">                 <!-- raw HTML tags work too -->
```

Relative paths resolve against the Markdown file's own folder, so a document and
its `Research/` images can be moved together. `http(s)://` and `data:` sources are
left untouched. Every reference that cannot be found is listed in the console
warning block at the end, which makes missing screenshots obvious before the PDF
is sent for review.

## Output and behaviour

- Cover page (unless `--no-cover`) with title, subtitle, source file and date.
- A4 portrait, 16/14/18/14 mm margins, no browser header or footer.
- Tables, fenced code, block quotes and inline code are styled for print; images
  are capped at the text width and never split across pages.
- The intermediate HTML lives next to the PDF while the browser prints and is
  deleted afterwards unless `--keep-html` is passed.
- Exit code is non-zero when the Markdown, the browser or the PDF itself is
  missing, so it is safe to call from a build script.
