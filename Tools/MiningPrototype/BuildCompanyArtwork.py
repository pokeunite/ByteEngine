from pathlib import Path
import json,shutil,uuid
w=Path(__file__).resolve().parents[2];out=w/'artifacts/company-kit';out.mkdir(parents=True,exist_ok=True)
icons={
'delivery-truck':'<path fill="#f5f2e9" d="M1 4h13v11H1zm14 3h4l4 5v6h-2v-3h-6z"/><path fill="#202020" d="M16 8h2l3 4h-5z"/><circle cx="6" cy="18" r="4" fill="#f5f2e9"/><circle cx="18" cy="18" r="4" fill="#f5f2e9"/><circle cx="6" cy="18" r="1.7" fill="#202020"/><circle cx="18" cy="18" r="1.7" fill="#202020"/>',
'exchange-ingots':'<path fill="#f5f2e9" d="m9 2 7-1 4 6-2 4-9-1-3-3zM2 13l7-2 4 6-3 5-9-1zM15 13l6-1 3 6-3 4-8-1z"/><path fill="none" stroke="#202020" stroke-width="1" d="m9 3 2 5 7 1M3 14l4 3-1 4m11-8 1 5 4 1"/>',
'camp':'<path fill="#f5f2e9" d="M1 22 12 2l11 20h-8l-3-9-3 9z"/>',
'company-bank':'<path fill="#f5f2e9" d="m1 7 11-6 11 6v3H1zm1 12h20v4H2zM3 11h4v7H3zm7 0h4v7h-4zm7 0h4v7h-4z"/>',
'scatter':'<circle cx="12" cy="6" r="4.3" fill="#f5f2e9"/><circle cx="6" cy="17" r="4.3" fill="#f5f2e9"/><circle cx="18" cy="17" r="4.3" fill="#f5f2e9"/>',
'lock':'<path fill="none" stroke="#f5f2e9" stroke-width="2" d="M6 10V6l3-4h6l3 4v4"/><path fill="#f5f2e9" d="M3 10h18v13H3z"/><circle cx="12" cy="15" r="2" fill="#202020"/><path fill="#202020" d="M11 15h2v5h-2z"/>'}
for name,body in icons.items():(out/('company-'+name+'.svg')).write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="128" height="128" viewBox="0 0 24 24">{body}</svg>')
print('Created six editable Company Systems SVG pictograms; rasterize with RasterizeFieldHud.ps1 -Folder artifacts/company-kit.')
