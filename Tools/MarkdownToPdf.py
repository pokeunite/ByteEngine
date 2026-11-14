#!/usr/bin/env python3
"""Render a Markdown file to a styled, print-ready PDF.

Pipeline: Markdown -> styled HTML -> headless Chrome/Edge -> PDF.

Images may be referenced with relative paths (resolved against the Markdown
file's folder), absolute local paths (C:\\shots\\a.png or /home/me/a.png), or
file:// URIs. Missing images are reported and left in place.

Setup (once):
    python -m pip install markdown

Usage:
    python Tools/MarkdownToPdf.py Docs/MyDoc.md
    python Tools/MarkdownToPdf.py Docs/MyDoc.md -o "%USERPROFILE%\\Downloads\\MyDoc.pdf"
    python Tools/MarkdownToPdf.py Docs/MyDoc.md --title "Release notes" --no-cover
    python Tools/MarkdownToPdf.py Docs/MyDoc.md --css extra.css --page-size Letter
"""

from __future__ import annotations

import argparse
import datetime
import html
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import quote

try:
    import markdown
except ImportError:  # pragma: no cover - environment guard
    sys.exit("Missing dependency. Run: python -m pip install markdown")

BROWSER_CANDIDATES = (
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    "/usr/bin/google-chrome",
    "/usr/bin/chromium",
    "/usr/bin/chromium-browser",
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
)

# Markdown image syntax: ![alt](target "optional title")
IMAGE_PATTERN = re.compile(r"(!\[[^\]]*\]\()([^)\s]+)(.*?\))")
RAW_IMG_PATTERN = re.compile(r"(<img\b[^>]*?\bsrc=[\"'])([^\"']+)([\"'])", re.IGNORECASE)

BASE_CSS = """
@page {{ size: {page_size}; margin: 16mm 14mm 18mm 14mm; }}
* {{ box-sizing: border-box; }}
body {{
  font-family: "Segoe UI", "Helvetica Neue", Arial, sans-serif;
  font-size: 10.2pt; line-height: 1.45; color: #1b2127; margin: 0;
  -webkit-print-color-adjust: exact; print-color-adjust: exact;
}}
h1 {{ font-size: 22pt; line-height: 1.15; margin: 0 0 6pt; color: {heading}; }}
h2 {{
  font-size: 14.5pt; color: {heading}; margin: 20pt 0 7pt; padding-bottom: 3pt;
  border-bottom: 1.2px solid #d5dde4; break-after: avoid;
}}
h3 {{ font-size: 11.6pt; color: {accent}; margin: 13pt 0 4pt; break-after: avoid; }}
h4 {{ font-size: 10.6pt; margin: 10pt 0 3pt; break-after: avoid; }}
p, ul, ol {{ margin: 0 0 6pt; }}
li {{ margin: 1.5pt 0; }}
ul, ol {{ padding-left: 16pt; }}
strong {{ color: #102a3c; }}
a {{ color: {accent}; text-decoration: none; }}
hr {{ border: 0; border-top: 1px solid #e2e8ed; margin: 12pt 0; }}
blockquote {{
  margin: 8pt 0; padding: 5pt 9pt; border-left: 3px solid #9fc2d8;
  background: #f6f9fb; color: #33424f;
}}
code {{
  font-family: Consolas, "Cascadia Mono", monospace; font-size: 8.8pt;
  background: #f1f4f6; border: 1px solid #e3e8ec; border-radius: 3px; padding: 0 2.5px;
}}
pre {{
  background: #f7f9fa; border: 1px solid #e1e6ea; border-radius: 3px;
  padding: 7pt 8pt; margin: 6pt 0 8pt; overflow: hidden; break-inside: avoid;
}}
pre code {{ background: none; border: 0; padding: 0; font-size: 8.2pt; line-height: 1.35; }}
table {{ width: 100%; border-collapse: collapse; margin: 6pt 0 10pt; font-size: 8.9pt; }}
th, td {{ border: 1px solid #cdd6de; padding: 3.5pt 5pt; vertical-align: top; text-align: left; }}
th {{ background: #eef3f7; font-weight: 600; }}
tr {{ break-inside: avoid; }}
img {{
  max-width: 100%; max-height: {image_height}; height: auto;
  border: 1px solid #cbd4dc; border-radius: 3px;
  margin: 5pt 0 9pt; break-inside: avoid;
}}
.cover {{ height: 250mm; display: flex; flex-direction: column; justify-content: center; }}
.cover .kicker {{ font-size: 9.5pt; letter-spacing: 1.5px; text-transform: uppercase; color: #5b7c93; }}
.cover h1 {{ font-size: 27pt; margin: 8pt 0 10pt; }}
.cover .subtitle {{ font-size: 12.5pt; color: #33566e; }}
.cover .meta {{ margin-top: 26pt; font-size: 9.5pt; color: #5a6b78; }}
.cover .meta div {{ margin: 2pt 0; }}
.cover .rule {{ width: 70px; height: 3px; background: {accent}; margin: 14pt 0; }}
.page-break {{ page-break-after: always; }}
"""


def find_browser() -> str:
    for candidate in BROWSER_CANDIDATES:
        if Path(candidate).exists():
            return candidate
    raise SystemExit(
        "No Chrome/Edge found. Install one of them, or pass --browser <path to chrome.exe>."
    )


def local_uri(path: Path) -> str:
    return "file:///" + quote(path.as_posix(), safe="/:")


def resolve_local_image(target: str, base_dir: Path) -> Path | None:
    """Return the local file an image reference points at, if it exists."""
    if target.startswith(("data:", "http://", "https://", "file:")):
        return None
    cleaned = target.replace("\\", "/").split("?", 1)[0]
    candidate = Path(cleaned)
    if not candidate.is_absolute():
        candidate = (base_dir / cleaned).resolve()
    return candidate if candidate.is_file() else None


def rewrite_images(text: str, base_dir: Path, missing: list[str]) -> str:
    """Turn every resolvable local image reference into an absolute file:// URI."""

    def fix(target: str) -> str | None:
        resolved = resolve_local_image(target, base_dir)
        if resolved is None:
            if not target.startswith(("data:", "http://", "https://", "file:")):
                missing.append(target)
            return None
        return local_uri(resolved)

    def markdown_replacer(match: re.Match[str]) -> str:
        uri = fix(match.group(2))
        return match.group(0) if uri is None else f"{match.group(1)}{uri}{match.group(3)}"

    def raw_replacer(match: re.Match[str]) -> str:
        uri = fix(match.group(2))
        return match.group(0) if uri is None else f"{match.group(1)}{uri}{match.group(3)}"

    text = IMAGE_PATTERN.sub(markdown_replacer, text)
    return RAW_IMG_PATTERN.sub(raw_replacer, text)


def split_title(text: str) -> tuple[str, str]:
    match = re.match(r"#\s+(.+?)\s*$", text, re.MULTILINE)
    if not match:
        return "Document", text
    return match.group(1).strip(), text[match.end():]


def build_html(*, title: str, subtitle: str, body_html: str, base_dir: Path, cover: bool, cover_meta: list[str], css: str) -> str:
    safe_title = html.escape(title)
    cover_html = ""
    if cover:
        meta_lines = "".join(f"<div>{html.escape(line)}</div>" for line in cover_meta if line)
        cover_html = f"""<section class="cover page-break">
  <div class="kicker">ByteEngine &middot; design review</div>
  <h1>{safe_title}</h1>
  <div class="rule"></div>
  <div class="subtitle">{html.escape(subtitle)}</div>
  <div class="meta">{meta_lines}</div>
</section>
"""
    return f"""<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>{safe_title}</title>
<base href="{local_uri(base_dir)}/">
<style>{css}</style>
</head>
<body>
{cover_html}{body_html}
</body>
</html>
"""


def write_pdf(browser: str, html_path: Path, pdf_path: Path) -> None:
    profile = Path.home() / ".cache" / "markdown-to-pdf-profile"
    profile.mkdir(parents=True, exist_ok=True)
    common_args = [
        "--disable-gpu",
        "--no-first-run",
        "--no-default-browser-check",
        "--disable-extensions",
        "--allow-file-access-from-files",
        "--no-pdf-header-footer",
        "--virtual-time-budget=8000",
        f"--user-data-dir={profile}",
        f"--print-to-pdf={pdf_path}",
        html_path.as_uri(),
    ]
    for headless in ("--headless=new", "--headless"):
        if pdf_path.exists():
            pdf_path.unlink()
        try:
            result = subprocess.run(
                [browser, headless, *common_args],
                capture_output=True,
                text=True,
                timeout=180,
            )
        except subprocess.TimeoutExpired:
            print(f"  {headless} attempt timed out")
            continue
        if pdf_path.exists() and pdf_path.stat().st_size > 1024:
            return
        lines = (result.stderr or result.stdout or "").strip().splitlines()
        detail = lines[-1] if lines else f"exit {result.returncode}"
        print(f"  {headless} attempt failed: {detail}")
    raise SystemExit("PDF was not produced by the browser.")


def page_count(pdf_path: Path) -> int | None:
    try:
        from pypdf import PdfReader  # optional, only for reporting
    except ImportError:
        return None
    return len(PdfReader(str(pdf_path)).pages)


def main() -> int:
    parser = argparse.ArgumentParser(description="Render Markdown to a styled PDF.")
    parser.add_argument("source", help="Markdown file to render")
    parser.add_argument("-o", "--output", help="PDF path (default: next to the source)")
    parser.add_argument("--title", help="Cover title (default: first H1)")
    parser.add_argument("--subtitle", default="", help="Cover subtitle")
    parser.add_argument("--no-cover", action="store_true", help="Skip the cover page")
    parser.add_argument("--page-size", default="A4", choices=["A4", "Letter", "Legal"])
    parser.add_argument("--image-height", default="none", help="CSS max-height for images, e.g. 90mm")
    parser.add_argument("--accent", default="#1f6fb2", help="Accent colour (CSS hex)")
    parser.add_argument("--heading", default="#102a3c", help="Heading colour (CSS hex)")
    parser.add_argument("--css", help="Extra CSS file appended to the built-in stylesheet")
    parser.add_argument("--browser", help="Explicit path to chrome.exe / msedge.exe")
    parser.add_argument("--keep-html", action="store_true", help="Keep the intermediate HTML")
    args = parser.parse_args()

    source = Path(args.source).expanduser().resolve()
    if not source.is_file():
        parser.error(f"Markdown file not found: {source}")

    output = Path(args.output).expanduser() if args.output else source.with_suffix(".pdf")
    output = output.resolve()
    output.parent.mkdir(parents=True, exist_ok=True)

    text = source.read_text(encoding="utf-8")
    doc_title, body = split_title(text)
    title = args.title or doc_title
    subtitle = args.subtitle or f"{title} — {source.name}"

    missing: list[str] = []
    body = rewrite_images(body, source.parent, missing)

    converter = markdown.Markdown(
        extensions=["extra", "sane_lists", "toc"],
        extension_configs={"toc": {"permalink": False}},
    )
    body_html = converter.convert(body)

    css = BASE_CSS.format(
        page_size=args.page_size,
        image_height=args.image_height,
        accent=args.accent,
        heading=args.heading,
    )
    if args.css:
        css += "\n" + Path(args.css).expanduser().resolve().read_text(encoding="utf-8")

    html_text = build_html(
        title=title,
        subtitle=subtitle,
        body_html=body_html,
        base_dir=source.parent,
        cover=not args.no_cover,
        cover_meta=[
            f"Source: {source.name}",
            f"Generated {datetime.date.today().isoformat()}",
        ],
        css=css,
    )

    html_path = output.with_suffix(".html")
    html_path.write_text(html_text, encoding="utf-8")

    browser = args.browser or find_browser()
    print(f"Markdown : {source}")
    print(f"Browser  : {browser}")
    write_pdf(browser, html_path, output)
    if not args.keep_html:
        html_path.unlink(missing_ok=True)

    size_kb = output.stat().st_size / 1024
    pages = page_count(output)
    if missing:
        print(f"Warning: {len(set(missing))} image reference(s) not found locally:")
        for item in sorted(set(missing)):
            print(f"  - {item}")
    page_text = f", {pages} pages" if pages else ""
    print(f"PDF      : {output} ({size_kb:,.0f} KB{page_text})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())



