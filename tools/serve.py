"""本機開發伺服器：以專案根目錄為網站根，並停用快取（重新產生美術後直接重新整理即可）。

用法: python tools/serve.py [port]
然後開啟 http://localhost:8787/preview/
"""
import sys
import webbrowser
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


class NoCacheHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, *args):
        pass


if __name__ == "__main__":
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8787
    url = f"http://localhost:{port}/preview/"
    server = ThreadingHTTPServer(("127.0.0.1", port), partial(NoCacheHandler, directory=str(ROOT)))
    print(f"serving {ROOT} at {url}")
    if "--no-open" not in sys.argv:
        webbrowser.open(url)
    server.serve_forever()
