#!/usr/bin/env python3
"""Inspect, decode, and encode modern Streamer.bot .sb import strings."""

import argparse
import base64
import json
import zlib
from pathlib import Path


MAGIC = b"SBAE"
GZIP_WBITS = 16 + zlib.MAX_WBITS


def decode_sb_bytes(data):
    decoded = base64.b64decode(data)
    payloads = decode_sb_records(decoded)

    if len(payloads) == 1:
        return payloads[0]

    first_payload = canonical_json(payloads[0])
    if all(canonical_json(payload) == first_payload for payload in payloads[1:]):
        return payloads[0]

    raise ValueError(
        "Streamer.bot import file contains multiple different import strings. "
        "Export or paste one scheduler action import string at a time."
    )


def decode_sb_records(decoded):
    payloads = []
    remaining = decoded

    while remaining:
        if not remaining.startswith(MAGIC):
            raise ValueError("Unsupported Streamer.bot import string magic header.")

        decompressor = zlib.decompressobj(GZIP_WBITS)
        try:
            raw_json = decompressor.decompress(remaining[len(MAGIC) :])
            raw_json += decompressor.flush()
        except zlib.error as ex:
            raise ValueError(f"Invalid Streamer.bot gzip payload: {ex}") from ex

        if not decompressor.eof:
            raise ValueError("Incomplete Streamer.bot gzip payload.")

        payloads.append(json.loads(raw_json.decode("utf-8")))
        remaining = decompressor.unused_data

    return payloads


def canonical_json(payload):
    return json.dumps(payload, sort_keys=True, separators=(",", ":"))


def encode_sb_payload(payload):
    raw_json = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    compressor = zlib.compressobj(
        level=9,
        method=zlib.DEFLATED,
        wbits=GZIP_WBITS,
        memLevel=9,
        strategy=zlib.Z_DEFAULT_STRATEGY,
    )
    compressed = compressor.compress(raw_json) + compressor.flush()
    return base64.b64encode(MAGIC + compressed)


def read_payload(path):
    path = Path(path)
    if path.suffix.lower() == ".json":
        return json.loads(path.read_text(encoding="utf-8"))

    return decode_sb_bytes(path.read_bytes())


def write_payload(payload, path):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)

    if path.suffix.lower() == ".json":
        path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
        return

    path.write_bytes(encode_sb_payload(payload))


def inspect_payload(payload):
    data = payload.get("data", {})
    meta = payload.get("meta", {})

    return {
        "version": payload.get("version"),
        "minimumVersion": payload.get("minimumVersion"),
        "exportedFrom": payload.get("exportedFrom"),
        "meta": {
            "name": meta.get("name"),
            "author": meta.get("author"),
            "version": meta.get("version"),
            "description": meta.get("description"),
        },
        "counts": {
            "actions": len(data.get("actions", [])),
            "commands": len(data.get("commands", [])),
            "queues": len(data.get("queues", [])),
            "timers": len(data.get("timers", [])),
            "websocketServers": len(data.get("websocketServers", [])),
            "websocketClients": len(data.get("websocketClients", [])),
        },
    }


def main():
    parser = argparse.ArgumentParser(
        description="Inspect, decode, and encode modern Streamer.bot .sb files."
    )
    subparsers = parser.add_subparsers(dest="command", required=True)

    inspect_parser = subparsers.add_parser("inspect")
    inspect_parser.add_argument("input")

    decode_parser = subparsers.add_parser("decode")
    decode_parser.add_argument("input")
    decode_parser.add_argument("output")

    encode_parser = subparsers.add_parser("encode")
    encode_parser.add_argument("input")
    encode_parser.add_argument("output")

    args = parser.parse_args()

    if args.command == "inspect":
        print(json.dumps(inspect_payload(read_payload(args.input)), indent=2))
        return

    if args.command == "decode":
        write_payload(read_payload(args.input), args.output)
        return

    if args.command == "encode":
        write_payload(read_payload(args.input), args.output)


if __name__ == "__main__":
    main()
