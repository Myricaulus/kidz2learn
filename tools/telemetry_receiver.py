"""Receives Kidz2Learn session telemetry and saves one JSON file per session.

The app posts each session as JSON to <endpoint> on devices opted in with ?telemetrie=<endpoint>
(see Services/TelemetryService.cs). The app is served over https, so the receiver must be too:
pass --cert/--key, signed by a CA the browsers on those devices trust. Without them it serves plain
http, which is only useful for local testing.

    python3 tools/telemetry_receiver.py --out /srv/telemetrie --cert server.crt --key server.key \
        --allow 192.168.178.0/24 --allow fe80::/10

Standard library only; one file per session id, overwritten by every later send of that session.
"""

import argparse
import ipaddress
import json
import re
import socket
import ssl
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

MAX_BODY = 2 * 1024 * 1024
SESSION_ID = re.compile(r"^[0-9A-Za-z-]{1,64}$")


def make_handler(out_dir: Path, allowed: list):
    class Handler(BaseHTTPRequestHandler):
        def _allowed(self) -> bool:
            if not allowed:
                return True
            ip = ipaddress.ip_address(self.client_address[0])
            if isinstance(ip, ipaddress.IPv6Address) and ip.ipv4_mapped:
                ip = ip.ipv4_mapped
            return any(ip in net for net in allowed)

        def _reply(self, status: int, text: str = ""):
            body = text.encode()
            self.send_response(status)
            self.send_header("Access-Control-Allow-Origin", self.headers.get("Origin") or "*")
            self.send_header("Access-Control-Allow-Methods", "POST, OPTIONS")
            self.send_header("Access-Control-Allow-Headers", "Content-Type")
            self.send_header("Access-Control-Allow-Private-Network", "true")
            self.send_header("Content-Type", "text/plain; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_OPTIONS(self):
            if not self._allowed():
                return self._reply(403)
            self._reply(204)

        def do_GET(self):
            # Opening the endpoint in a browser is a quick way to check the certificate.
            if not self._allowed():
                return self._reply(403)
            self._reply(200, "kidz2learn telemetry receiver")

        def do_POST(self):
            if not self._allowed():
                return self._reply(403)
            if self.path != "/session":
                return self._reply(404, "not found")
            length = int(self.headers.get("Content-Length") or 0)
            if not 0 < length <= MAX_BODY:
                return self._reply(413, "bad size")
            try:
                session = json.loads(self.rfile.read(length))
                session_id = session["sessionId"]
            except (ValueError, KeyError, TypeError):
                return self._reply(400, "bad json")
            if not isinstance(session_id, str) or not SESSION_ID.match(session_id):
                return self._reply(400, "bad session id")

            session["receivedFrom"] = {"ip": self.client_address[0], "userAgent": self.headers.get("User-Agent")}
            target = out_dir / f"{session_id}.json"
            tmp = out_dir / f".{session_id}.tmp"
            tmp.write_text(json.dumps(session, ensure_ascii=False, indent=2), encoding="utf-8")
            tmp.replace(target)
            print(f"{session_id}: {len(session.get('entries', []))} entries from {self.client_address[0]}", flush=True)
            self._reply(200, "ok")

        def log_message(self, format, *args):
            pass

    return Handler


class DualStackServer(ThreadingHTTPServer):
    address_family = socket.AF_INET6
    daemon_threads = True
    tls: ssl.SSLContext | None = None

    def server_bind(self):
        self.socket.setsockopt(socket.IPPROTO_IPV6, socket.IPV6_V6ONLY, 0)
        super().server_bind()

    def finish_request(self, request, client_address):
        # Handshake per connection in its worker thread, so a stalled client can't block accept().
        request.settimeout(15)
        if self.tls:
            try:
                request = self.tls.wrap_socket(request, server_side=True)
            except (ssl.SSLError, OSError):
                return
        super().finish_request(request, client_address)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--port", type=int, default=8766)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--cert", type=Path)
    parser.add_argument("--key", type=Path)
    parser.add_argument("--allow", action="append", default=[], help="client network (CIDR), repeatable")
    args = parser.parse_args()

    allowed = [ipaddress.ip_network(a) for a in args.allow]
    args.out.mkdir(parents=True, exist_ok=True)
    server = DualStackServer(("::", args.port), make_handler(args.out, allowed))
    if args.cert:
        context = ssl.create_default_context(ssl.Purpose.CLIENT_AUTH)
        context.load_cert_chain(args.cert, args.key)
        server.tls = context
    scheme = "https" if args.cert else "http"
    print(f"{scheme} on port {args.port}, saving to {args.out}, allowed: {args.allow or 'all'}", flush=True)
    server.serve_forever()


if __name__ == "__main__":
    main()
