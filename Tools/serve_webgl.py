# Servidor local para testar builds WebGL comprimidas (Brotli .br / Gzip .gz) sem "decompression fallback".
# O Unity precisa que o servidor mande "Content-Encoding" nesses arquivos (o itch e os portais fazem isso).
# Uso:  python Tools/serve_webgl.py <pasta_da_build> <porta>
import http.server
import os
import sys

ROOT = os.path.abspath(sys.argv[1])
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 8126

TYPES = {".wasm": "application/wasm", ".js": "application/javascript", ".data": "application/octet-stream",
         ".json": "application/json", ".symbols.json": "application/json"}


class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def end_headers(self):
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def guess_type(self, path):
        base = path
        for ext in (".br", ".gz"):
            if base.endswith(ext):
                base = base[: -len(ext)]
        for ext, mime in TYPES.items():
            if base.endswith(ext):
                return mime
        return super().guess_type(path)

    def send_response(self, code, message=None):
        super().send_response(code, message)
        path = self.path.split("?")[0]
        if code == 200 and path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        elif code == 200 and path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")


http.server.ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
