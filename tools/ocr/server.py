"""Private loopback ddddocr service. No school login, files or credentials."""

import argparse
import base64
import binascii
import json
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer


def create_handler(recognizer):
    lock = threading.Lock()

    class Handler(BaseHTTPRequestHandler):
        def respond(self, status, payload):
            body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
            self.send_response(status)
            self.send_header("Content-Type", "application/json; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            self.respond(200 if self.path == "/health" else 404,
                         {"status": "ready"} if self.path == "/health" else {"error": "not_found"})

        def do_POST(self):
            if self.path != "/recognize":
                self.respond(404, {"error": "not_found"})
                return
            try:
                length = int(self.headers.get("Content-Length", "0"))
                if length <= 0 or length > 2 * 1024 * 1024:
                    self.respond(400, {"error": "invalid_image_size"})
                    return
                payload = json.loads(self.rfile.read(length))
                image = base64.b64decode(payload["image"], validate=True)
                if not image:
                    raise ValueError("empty image")
            except (ValueError, TypeError, KeyError, binascii.Error):
                self.respond(400, {"error": "invalid_image"})
                return
            try:
                with lock:
                    code = recognizer.classification(image).strip()
                self.respond(200, {"code": code})
            except Exception:
                self.respond(422, {"error": "recognition_failed"})

        def log_message(self, format, *args):
            # HTTP request bodies and recognized codes stay in memory.
            pass

    return Handler


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8866)
    args = parser.parse_args()
    import ddddocr

    recognizer = ddddocr.DdddOcr(show_ad=False)
    server = ThreadingHTTPServer(("127.0.0.1", args.port), create_handler(recognizer))
    print(f"ddddocr ready: http://127.0.0.1:{args.port}/recognize", flush=True)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
