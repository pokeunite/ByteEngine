"""Summarize a Chrome trace, keeping frame processes and CPU profiles separate.

The symbol map MUST belong to the exact native WASM binary in the trace.
CPU samples are statistical samples, not per-method wall-clock totals.
"""
import argparse
import collections
import json
import pathlib
import re

parser = argparse.ArgumentParser()
parser.add_argument('trace')
parser.add_argument('--pid', type=int, required=True)
parser.add_argument('--symbols', required=True)
parser.add_argument('--output', required=True)
args = parser.parse_args()
events = json.loads(pathlib.Path(args.trace).read_text(encoding='utf-8-sig'))['traceEvents']
symbols = {}
for line in pathlib.Path(args.symbols).read_text(encoding='utf-8-sig').splitlines():
    index, separator, name = line.partition(':')
    if separator and index.isdigit():
        symbols[int(index)] = name

nodes = {}
samples = collections.Counter()
weights = collections.Counter()
for event in events:
    if event.get('pid') != args.pid or event.get('name') != 'ProfileChunk':
        continue
    data = event['args']['data']
    profile = data.get('cpuProfile', {})
    prefix = (event.get('pid'), event.get('tid'), event.get('id'))
    for node in profile.get('nodes', []):
        nodes[prefix + (node['id'],)] = node
    deltas = data.get('timeDeltas', [])
    for i, node in enumerate(profile.get('samples', [])):
        key = prefix + (node,)
        samples[key] += 1
        weights[key] += deltas[i] if i < len(deltas) else 0

totals = collections.Counter()
durations = collections.Counter()
for key, count in samples.items():
    frame = nodes.get(key, {}).get('callFrame', {})
    name = frame.get('functionName', '(unresolved)')
    match = re.fullmatch(r'wasm-function\[(\d+)\]', name)
    if match:
        name = symbols.get(int(match[1]), name)
    totals[name] += count
    durations[name] += weights[key] / 1000

main_threads = {event['tid'] for event in events if event.get('pid') == args.pid
                and event.get('name') == 'thread_name'
                and event.get('args', {}).get('name') == 'CrRendererMain'}
tasks = [event['dur'] / 1000 for event in events if event.get('pid') == args.pid
         and event.get('tid') in main_threads and event.get('name') == 'RunTask'
         and event.get('ph') == 'X' and event.get('dur', 0) >= 50000]
result = {
    'traceEvents': len(events), 'gamePid': args.pid,
    'longMainThreadTasks': len(tasks), 'longestMainThreadTaskMs': max(tasks, default=0),
    'symbolMap': str(pathlib.Path(args.symbols).resolve()),
    'sampleCount': sum(totals.values()),
    'hotspots': [{'name': name, 'samples': count, 'sampleWeightMs': durations[name]}
                 for name, count in totals.most_common(40)],
    'note': 'Exclusive sampled CPU leaves; nested durations must not be summed as independent work. Symbol-map matching must be verified separately.',
}
pathlib.Path(args.output).write_text(json.dumps(result, indent=2), encoding='utf-8')
print(json.dumps(result, indent=2))
