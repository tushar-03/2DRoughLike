#!/usr/bin/env python3
"""
Serves Builds/WebGL locally the way Vercel will: Brotli files get the right Content-Encoding and Content-Type headers.
Use it to try the web build before deploying:

    python tools/serve-web.py            # http://localhost:8080
    python tools/serve-web.py 9000 path  # another port / folder

To try it on a phone, open http://<your computer's LAN address>:8080 on the same Wi-Fi.
"""
import http.server
import os
import socketserver
import sys

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8080
ROOT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), "..", "Builds", "WebGL")

TYPES = {".wasm": "application/wasm", ".js": "application/javascript", ".data": "application/octet-stream"}


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def end_headers(self):
        path = self.path.split("?")[0]
        for ext in (".br", ".gz"):
            if path.endswith(ext):
                inner = os.path.splitext(path[: -len(ext)])[1]
                self.send_header("Content-Encoding", "br" if ext == ".br" else "gzip")
                if inner in TYPES:
                    self.send_header("Content-Type", TYPES[inner])
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()


if __name__ == "__main__":
    socketserver.TCPServer.allow_reuse_address = True
    with socketserver.ThreadingTCPServer(("", PORT), Handler) as server:
        print(f"Serving {os.path.abspath(ROOT)} at http://localhost:{PORT}  (Ctrl+C to stop)")
        server.serve_forever()
