"""Build the kit by real requests to the running local Blender MCP bridge."""
import json,sys
from pathlib import Path
from blender_mcp_client import call
NAMES=['tyre-barrier','loose-tyre','traffic-cone','chevron-sign','route-flag','finish-gantry','launch-tree','container','workshop-shed','service-canopy','fuel-tank','barrel','utility-crate','quarry-conveyor','silo','service-mast','cliff-1','cliff-2','cliff-3','dry-bush']
source=Path(__file__).with_name('blender_race_kit.py').resolve();base=f"from pathlib import Path\nexec(compile(Path({str(source)!r}).read_text(), {str(source)!r}, 'exec'),globals())\n"
for name in sys.argv[1:] or NAMES:
    r=call('execute_python',{'code':base+f'result=create({name!r})'})
    print(json.dumps(r.get('result',r)),flush=True)
print('Blender MCP asset batch complete',flush=True)
