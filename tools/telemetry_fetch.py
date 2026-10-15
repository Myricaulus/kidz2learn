"""Fetches Kidz2Learn session telemetry from the receiver (tools/telemetry_receiver.py) and deletes
it there once it is safely stored locally.

    python3 tools/telemetry_fetch.py --init --url https://<receiver>:8766 --ca <ca.crt>
    python3 tools/telemetry_fetch.py              # fetch into inbox/telemetrie/, delete finished ones
    python3 tools/telemetry_fetch.py --keep       # fetch only

--init creates a random token and stores url, token and CA path in ~/.config/kidz2learn/
telemetrie-abruf.json (mode 600); the receiver URL and token never go into this public repo. It
prints the token's SHA-256, which is what the receiver gets (--read-token-sha256).

A session is deleted on the server only after the local copy was written and reads back byte for
byte, and only if the server's copy is older than --min-age minutes: the app re-sends the whole
session after every answer, so a younger file may still be growing (a later fetch picks it up again
and overwrites the local copy). Standard library only.
"""

import argparse
import hashlib
import json
import os
import secrets
import ssl
import sys
import urllib.error
import urllib.request
from pathlib import Path

CONFIG = Path.home() / ".config" / "kidz2learn" / "telemetrie-abruf.json"
DEFAULT_OUT = Path(__file__).resolve().parent.parent / "inbox" / "telemetrie"


def init(url: str, ca: Path) -> int:
    if CONFIG.exists():
        print(f"{CONFIG} exists already - delete it first to create a new token", file=sys.stderr)
        return 1
    token = secrets.token_urlsafe(32)
    CONFIG.parent.mkdir(parents=True, exist_ok=True)
    fd = os.open(CONFIG, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, "w") as f:
        json.dump({"url": url.rstrip("/"), "token": token, "ca": str(ca.resolve())}, f, indent=2)
    print(f"wrote {CONFIG}")
    print(hashlib.sha256(token.encode()).hexdigest())
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--init", action="store_true", help="create token + config, print its SHA-256")
    parser.add_argument("--url", help="receiver base URL (with --init)")
    parser.add_argument("--ca", type=Path, help="CA certificate of the receiver (with --init)")
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT)
    parser.add_argument("--keep", action="store_true", help="never delete on the server")
    parser.add_argument("--min-age", type=int, default=60, help="minutes untouched before deleting (default 60)")
    args = parser.parse_args()

    if args.init:
        if not args.url or not args.ca:
            parser.error("--init needs --url and --ca")
        return init(args.url, args.ca)

    config = json.loads(CONFIG.read_text())
    context = ssl.create_default_context(cafile=config["ca"])
    auth = {"Authorization": f"Bearer {config['token']}"}

    def call(method: str, path: str) -> bytes:
        request = urllib.request.Request(config["url"] + path, method=method, headers=auth)
        with urllib.request.urlopen(request, context=context, timeout=20) as response:
            return response.read()

    args.out.mkdir(parents=True, exist_ok=True)
    sessions = json.loads(call("GET", "/sessions"))
    fetched = deleted = 0
    for s in sessions:
        body = call("GET", f"/sessions/{s['id']}")
        json.loads(body)  # refuse to store (and later delete) anything that isn't valid JSON
        target = args.out / f"{s['id']}.json"
        tmp = target.with_suffix(".tmp")
        tmp.write_bytes(body)
        tmp.replace(target)
        fetched += 1
        if args.keep or s["ageSeconds"] < args.min_age * 60:
            print(f"{s['id']}: fetched, kept on server ({s['ageSeconds'] // 60} min old)")
            continue
        if target.read_bytes() != body:
            print(f"{s['id']}: local copy differs, not deleting", file=sys.stderr)
            continue
        call("DELETE", f"/sessions/{s['id']}")
        deleted += 1
        print(f"{s['id']}: fetched, deleted on server")
    print(f"{fetched} fetched, {deleted} deleted, saved in {args.out}")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except urllib.error.HTTPError as e:
        print(f"receiver: HTTP {e.code} {e.read().decode(errors='replace')}", file=sys.stderr)
        sys.exit(1)
    except (urllib.error.URLError, OSError) as e:
        print(f"error: {e}", file=sys.stderr)
        sys.exit(1)
