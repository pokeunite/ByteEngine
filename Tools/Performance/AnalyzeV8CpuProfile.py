"""Resolve a controlled browser capture using its exact native WASM hash map.

Usage: python Tools/Performance/AnalyzeV8CpuProfile.py CAPTURE_DIRECTORY
Writes cpu-methods.json with measured exclusive/inclusive sampling estimates.
"""
import collections
import json
import pathlib
import re
import sys

root = pathlib.Path(sys.argv[1]).resolve()
game = json.loads((root / 'game.json').read_text())
profile = json.loads((root / 'cpu-profile.json').read_text())
symbol_file = pathlib.Path(__file__).resolve().parents[2] / 'artifacts' / 'BrowserSymbols' / (game['runtimeSha256'].upper() + '.symbols')
symbols = {}
for line in symbol_file.read_text().splitlines():
    key, _, value = line.partition(':')
    symbols[int(key)] = value
nodes = {node['id']: node for node in profile['nodes']}
parents = {child: node['id'] for node in profile['nodes'] for child in node.get('children', [])}

def name(index):
    value = nodes[index]['callFrame']['functionName']
    match = re.search(r'wasm-function\[(\d+)\]', value)
    return symbols.get(int(match[1]), value) if match else value

exclusive, inclusive = collections.Counter(), collections.Counter()
for index, micros in zip(profile['samples'], profile['timeDeltas']):
    exclusive[name(index)] += micros
    seen = set()
    while index:
        method = name(index)
        if method not in seen:
            inclusive[method] += micros
            seen.add(method)
        index = parents.get(index)
frames = len(game['rows'])
def rows(counter):
    return [{'method': method, 'sampledMs': round(micros / 1000, 3), 'estimatedMsPerFrame': round(micros / 1000 / frames, 4)} for method, micros in counter.most_common()]
result = {'runtimeSha256': game['runtimeSha256'], 'symbols': str(symbol_file), 'frames': frames,
          'sampledSeconds': sum(profile['timeDeltas']) / 1e6,
          'note': 'Sampling estimates, not instrumented call durations. Inclusive rows overlap. Profiling adds overhead; use the separate unprofiled benchmark for FPS.',
          'exclusive': rows(exclusive), 'inclusive': rows(inclusive)}
(root / 'cpu-methods.json').write_text(json.dumps(result, indent=2))
for row in result['exclusive'][:20]:
    print(f"{row['estimatedMsPerFrame']:7.3f} ms/frame  {row['method']}")
