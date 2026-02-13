#!/usr/bin/env python3
# Rust Mod Suite local API server
# by Shmatko
from __future__ import annotations

import argparse
import shutil
import json
import os
from http import HTTPStatus
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlparse


class ConfiguratorHandler(SimpleHTTPRequestHandler):
    root_dir: Path = Path(".")
    target_privilege_config_path: Path = Path("PrivilegeSystem.json")
    target_loot_config_path: Path = Path("ContainerLootManager.json")
    privilege_plugin_source_path: Path = Path("PrivilegeSystem.cs")
    loot_plugin_source_path: Path = Path("ContainerLootManager.cs")
    plugin_target_dir: Path = Path(".")

    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(self.root_dir), **kwargs)

    def do_GET(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path == "/health":
            self._send_json(
                HTTPStatus.OK,
                {
                    "ok": True,
                    "targets": {
                        "privilege_config": str(self.target_privilege_config_path),
                        "loot_config": str(self.target_loot_config_path),
                        "plugin_dir": str(self.plugin_target_dir),
                    },
                },
            )
            return
        super().do_GET()

    def do_POST(self) -> None:
        parsed = urlparse(self.path)
        if parsed.path == "/api/save-config":
            self._handle_save_config()
            return
        if parsed.path == "/api/deploy-plugin":
            self._handle_deploy_plugin()
            return
        if parsed.path == "/api/deploy-all":
            self._handle_deploy_all()
            return
        self._send_json(HTTPStatus.NOT_FOUND, {"ok": False, "error": "not found"})

    def _read_json_payload(self) -> dict | None:
        content_length = self.headers.get("Content-Length", "0")
        try:
            raw_len = int(content_length)
        except Exception:
            raw_len = 0

        if raw_len <= 0:
            self._send_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": "empty request body"})
            return None

        body = self.rfile.read(raw_len)
        try:
            payload = json.loads(body.decode("utf-8"))
        except Exception as exc:
            self._send_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": f"invalid json: {exc}"})
            return None
        if not isinstance(payload, dict):
            self._send_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": "json payload must be an object"})
            return None
        return payload

    def _resolve_target_config_path(self, payload: dict) -> tuple[Path | None, str | None]:
        target_override = payload.get("target_path")
        if target_override:
            target = Path(str(target_override))
            if not target.is_absolute():
                return None, "target path must be absolute"
            return target, None

        raw_config_type = payload.get("config_type", payload.get("profile", "privilege"))
        config_type = str(raw_config_type).strip().lower()
        if config_type in ("privilege", "privilegesystem", "priv"):
            return self.target_privilege_config_path, None
        if config_type in ("loot", "containerlootmanager", "container"):
            return self.target_loot_config_path, None
        return None, "unsupported config_type (expected: privilege or loot)"

    def _handle_save_config(self) -> None:
        payload = self._read_json_payload()
        if payload is None:
            return

        config = payload.get("config")
        if not isinstance(config, dict):
            self._send_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": "field 'config' must be an object"})
            return

        target, resolve_error = self._resolve_target_config_path(payload)
        if resolve_error:
            self._send_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": resolve_error})
            return
        assert target is not None

        try:
            target.parent.mkdir(parents=True, exist_ok=True)
            encoded = json.dumps(config, ensure_ascii=False, indent=2) + os.linesep
            target.write_text(encoded, encoding="utf-8")
        except Exception as exc:
            self._send_json(HTTPStatus.INTERNAL_SERVER_ERROR, {"ok": False, "error": f"write failed: {exc}"})
            return

        self._send_json(
            HTTPStatus.OK,
            {
                "ok": True,
                "path": str(target),
                "bytes": len(encoded.encode("utf-8")),
            },
        )

    def _resolve_plugin_source(self, plugin_name: str) -> tuple[Path | None, str | None, str | None]:
        plugin_key = plugin_name.strip().lower()
        if plugin_key in ("privilege", "privilegesystem", "priv"):
            return self.privilege_plugin_source_path, "PrivilegeSystem.cs", "privilege"
        if plugin_key in ("loot", "containerlootmanager", "container"):
            return self.loot_plugin_source_path, "ContainerLootManager.cs", "loot"
        return None, None, None

    def _deploy_plugin_file(self, plugin_name: str) -> tuple[bool, dict]:
        source, target_filename, normalized = self._resolve_plugin_source(plugin_name)
        if source is None or target_filename is None or normalized is None:
            return False, {"ok": False, "error": f"unsupported plugin '{plugin_name}'"}

        if not source.exists():
            return False, {"ok": False, "error": f"source not found: {source}"}

        try:
            self.plugin_target_dir.mkdir(parents=True, exist_ok=True)
            target_path = self.plugin_target_dir / target_filename
            shutil.copyfile(str(source), str(target_path))
        except Exception as exc:
            return False, {"ok": False, "error": f"deploy failed: {exc}"}

        return True, {
            "ok": True,
            "plugin": normalized,
            "source": str(source),
            "path": str(target_path),
        }

    def _handle_deploy_plugin(self) -> None:
        payload = self._read_json_payload()
        if payload is None:
            return

        plugin_raw = payload.get("plugin")
        if plugin_raw is None:
            self._send_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": "field 'plugin' is required"})
            return

        ok, result = self._deploy_plugin_file(str(plugin_raw))
        if not ok:
            self._send_json(HTTPStatus.BAD_REQUEST, result)
            return
        self._send_json(HTTPStatus.OK, result)

    def _handle_deploy_all(self) -> None:
        payload = self._read_json_payload()
        if payload is None:
            return

        plugins_raw = payload.get("plugins")
        if plugins_raw is None:
            plugins = ["privilege", "loot"]
        elif isinstance(plugins_raw, list):
            plugins = [str(item) for item in plugins_raw if item is not None]
        else:
            self._send_json(HTTPStatus.BAD_REQUEST, {"ok": False, "error": "field 'plugins' must be array"})
            return

        deployed = []
        errors = []
        for plugin in plugins:
            ok, result = self._deploy_plugin_file(plugin)
            if ok:
                deployed.append(result)
            else:
                errors.append(result.get("error", "unknown error"))

        status = HTTPStatus.OK if not errors else HTTPStatus.BAD_REQUEST
        self._send_json(
            status,
            {
                "ok": len(errors) == 0,
                "deployed": deployed,
                "errors": errors,
            },
        )

    def _send_json(self, status: HTTPStatus, payload: dict) -> None:
        raw = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(int(status))
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(raw)))
        self.end_headers()
        self.wfile.write(raw)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Rust mods configurator local server")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=18765)
    parser.add_argument("--root", default="")
    parser.add_argument(
        "--target-config",
        default="",
        help="legacy alias for privilege config target path",
    )
    parser.add_argument(
        "--target-privilege-config",
        default=r"C:\rust\server\oxide\config\PrivilegeSystem.json",
    )
    parser.add_argument(
        "--target-loot-config",
        default=r"C:\rust\server\oxide\config\ContainerLootManager.json",
    )
    parser.add_argument(
        "--plugin-target-dir",
        default=r"C:\rust\server\oxide\plugins",
    )
    parser.add_argument(
        "--privilege-plugin-source",
        default=r"C:\rust\mods\privilege-system\PrivilegeSystem.cs",
    )
    parser.add_argument(
        "--loot-plugin-source",
        default=r"C:\rust\mods\container-loot-manager\ContainerLootManager.cs",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()

    script_dir = Path(__file__).resolve().parent
    default_root = script_dir.parent
    root_dir = Path(args.root).resolve() if args.root else default_root
    if args.target_config:
        target_privilege_config = Path(args.target_config).resolve()
    else:
        target_privilege_config = Path(args.target_privilege_config).resolve()
    target_loot_config = Path(args.target_loot_config).resolve()
    plugin_target_dir = Path(args.plugin_target_dir).resolve()
    privilege_plugin_source = Path(args.privilege_plugin_source).resolve()
    loot_plugin_source = Path(args.loot_plugin_source).resolve()

    ConfiguratorHandler.root_dir = root_dir
    ConfiguratorHandler.target_privilege_config_path = target_privilege_config
    ConfiguratorHandler.target_loot_config_path = target_loot_config
    ConfiguratorHandler.plugin_target_dir = plugin_target_dir
    ConfiguratorHandler.privilege_plugin_source_path = privilege_plugin_source
    ConfiguratorHandler.loot_plugin_source_path = loot_plugin_source

    server = ThreadingHTTPServer((args.host, args.port), ConfiguratorHandler)
    print(f"Configurator server listening on http://{args.host}:{args.port}/")
    print(f"Serving root: {root_dir}")
    print(f"Privilege config target: {target_privilege_config}")
    print(f"Loot config target: {target_loot_config}")
    print(f"Plugin deploy dir: {plugin_target_dir}")
    server.serve_forever()


if __name__ == "__main__":
    main()
