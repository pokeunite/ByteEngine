"""Exercise the actual error UI and JSON download without a game export."""
from pathlib import Path
import json, sys
sys.path.insert(0, str(Path('.artifacts/web-test-tools').resolve()))
from playwright.sync_api import sync_playwright

root = Path('Player/ByteEngine.Browser/wwwroot')
main = (root / 'main.js').read_text(encoding='utf-8')
handler = main[main.index('function fail(error)'):main.index("window.addEventListener('error'")]
with sync_playwright() as p:
    browser = p.chromium.launch(headless=True, channel='msedge')
    page = browser.new_page(viewport={'width': 960, 'height': 640}, accept_downloads=True)
    page.set_content((root / 'index.html').read_text(encoding='utf-8').replace('<script type="module" src="./main.js"></script>', ''))
    page.add_script_tag(content=(root / 'performance.js').read_text(encoding='utf-8').replace('export function', 'function'))
    page.add_script_tag(content="""
        const capture=createPerformanceCapture(),status=document.querySelector('#status'),progress=document.querySelector('#progress'),retry=document.querySelector('#retry');
        let stopped=false;const readManagedError=()=>"Browser frame failed during scene update in 'Quarry': System.ArgumentOutOfRangeException: test";
    """ + handler + ";fail(new Error('dotnet.js:3:6165'));fail(new Error('duplicate'));")
    assert page.get_by_role('button', name='Save error report').count() == 1
    assert page.get_by_role('button', name='Retry loading').is_visible()
    with page.expect_download() as download:
        page.get_by_role('button', name='Save error report').click()
    report = json.loads(Path(download.value.path()).read_text())
    assert 'System.ArgumentOutOfRangeException' in report['events'][-1]['message']
    assert report['recording'] is False
    page.screenshot(path='.artifacts/browser-crash-report-ui.png')
    browser.close()
print('PASS actual Chromium error UI, duplicate guard and downloaded managed exception report.')
