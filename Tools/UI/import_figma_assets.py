"""Resolve the image slots from Figma's generated design context, preserving source assets."""
import json
import re
import subprocess
from concurrent.futures import ThreadPoolExecutor
from html.parser import HTMLParser
from pathlib import Path

SOURCE = Path('Temp/FigmaImport')
DEST = Path('Assets/Art/UI/Figma20261116')

class Parser(HTMLParser):
    def __init__(self, urls):
        super().__init__()
        self.stack, self.urls, self.mapping = [], urls, {}
    def handle_starttag(self, tag, attrs):
        a = dict(attrs)
        if tag == 'img':
            src = a.get('src', '').strip('{}')
            node = next((v for t, v in reversed(self.stack) if v), None)
            if node and src in self.urls:
                self.mapping[node] = self.urls[src]
        elif tag not in ['br', 'hr', 'input']:
            self.stack.append((tag, a.get('data-node-id')))
    def handle_endtag(self, tag):
        for i in range(len(self.stack) - 1, -1, -1):
            if self.stack[i][0] == tag:
                self.stack = self.stack[:i]
                break

def collect():
    refs = json.loads((SOURCE / 'contexts.json').read_text(encoding='utf-8'))
    maps = {}
    for node, code in refs.items():
        prefix = re.search(r'const assetPathPrefix = "([^"]+)";', code).group(1)
        urls = {m.group(1): prefix + '/' + m.group(2) for m in re.finditer(r'const (\w+) = `\$\{assetPathPrefix\}/([^`]+)`;', code)}
        parser = Parser(urls)
        parser.feed(code)
        maps[node] = parser.mapping
    (SOURCE / 'assetMaps.json').write_text(json.dumps(maps), encoding='utf-8')
    print('Image slots:', sum(map(len, maps.values())))
    return maps

def download(maps):
    DEST.mkdir(parents=True, exist_ok=True)
    # The same Figma asset hash is reused between screens; retain one exact source file.
    urls = {u.rsplit('/', 1)[-1]: u for m in maps.values() for u in m.values()}
    def fetch(item):
        name, url = item
        path = DEST / name
        if not path.exists() or not path.stat().st_size:
            subprocess.run(['curl.exe', '-f', '-L', '--retry', '2', '--silent', '--show-error', '-o', str(path), url], check=True)
        return name
    with ThreadPoolExecutor(max_workers=6) as executor:
        files = list(executor.map(fetch, urls.items()))
    print('Local source assets:', len(files))

if __name__ == '__main__':
    import sys
    maps = collect()
    if '--download' in sys.argv:
        download(maps)
