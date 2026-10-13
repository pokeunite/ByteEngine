from pathlib import Path
from blender_mcp_client import call
source=Path(__file__).with_name('blender_race_kit.py').resolve()
print(call('execute_python',{'code':f"from pathlib import Path\nexec(compile(Path({str(source)!r}).read_text(),{str(source)!r},'exec'),globals())\nresult=stage_preview()\nbpy.ops.render.render(write_still=True)"}))
