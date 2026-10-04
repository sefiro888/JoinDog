"""Serve this project's exported game without stale browser preview files."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import argparse


class PreviewHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-store, max-age=0")
        super().end_headers()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8766)
    args = parser.parse_args()
    exported_game = Path(__file__).resolve().parents[1] / "docs"
    if not (exported_game / "index.html").is_file():
        parser.error(f"The exported JoinDog game is missing: {exported_game}")
    handler = partial(PreviewHandler, directory=str(exported_game))
    print(f"JoinDog preview: http://127.0.0.1:{args.port}/\nProject: {exported_game}", flush=True)
    with ThreadingHTTPServer(("127.0.0.1", args.port), handler) as server:
        server.serve_forever()
