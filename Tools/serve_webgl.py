"""Local Unity WebGL preview with headers for precompressed Brotli/gzip builds."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit


class UnityHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        for suffix in (".br", ".gz"):
            if path.endswith(suffix):
                path = path[:-len(suffix)]
                break
        if path.endswith(".wasm"):
            return "application/wasm"
        if path.endswith(".js"):
            return "application/javascript"
        if path.endswith(".data"):
            return "application/octet-stream"
        return super().guess_type(path)

    def end_headers(self):
        path = urlsplit(self.path).path
        if Path(self.translate_path(path)).is_file():
            if path.endswith(".br"):
                self.send_header("Content-Encoding", "br")
            elif path.endswith(".gz"):
                self.send_header("Content-Encoding", "gzip")
        self.send_header("Cache-Control", "no-store")
        super().end_headers()


def main():
    default_build = Path(__file__).resolve().parents[1] / "Builds" / "FootballCoop"
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--directory", type=Path, default=default_build)
    parser.add_argument("--port", type=int, default=8080)
    parser.add_argument("--host", default="127.0.0.1", help="Use 0.0.0.0 for access from other devices.")
    args = parser.parse_args()
    directory = args.directory.resolve()
    if not (directory / "index.html").is_file():
        parser.error(f"No index.html in {directory}; choose the build folder with --directory.")
    server = ThreadingHTTPServer((args.host, args.port), partial(UnityHandler, directory=str(directory)))
    print(f"Unity WebGL preview: http://localhost:{args.port}/\nServing {directory}\nCtrl+C stops the server.", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
