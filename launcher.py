# Welcome to main version of Orange Launcher! 
# Here is the code for the launcher, written in Python 3.14 pi version in jokes. Rewrtitten to PySide6.
# Recently edited by Adasjusk, 2026-06-05 (v note: what?)

# imports
import os
import sys
import atexit
import base64
import builtins
import copy
import glob
import hashlib
import importlib
import importlib.util
import io
import json
import math
import platform
import queue
import random
import re
import shutil
import socket
import stat
import struct
import subprocess
import tarfile
import tempfile
import threading
import time
import traceback
import urllib.parse
import uuid as uuid_module
import weakref
import webbrowser
import zipfile
from collections import deque
from datetime import datetime, timedelta
from pathlib import Path
from typing import List, Dict, Any, Optional, Union, Callable, Tuple
os.environ["PYGAME_HIDE_SUPPORT_PROMPT"] = "hide"




class _LazyModule:
    def __init__(self, name):
        object.__setattr__(self, "_lazy_name", name)
        object.__setattr__(self, "_lazy_mod", None)
    def _lazy_load(self):
        mod = object.__getattribute__(self, "_lazy_mod")
        if mod is None:
            mod = importlib.import_module(object.__getattribute__(self, "_lazy_name"))
            object.__setattr__(self, "_lazy_mod", mod)
        return mod
    def __getattr__(self, name):
        return getattr(self._lazy_load(), name)
    def __setattr__(self, name, value):
        setattr(self._lazy_load(), name, value)
    def __bool__(self):
        return True


minecraft_launcher_lib = _LazyModule("minecraft_launcher_lib")
requests = _LazyModule("requests")
_NEOFORGE_COMPAT = [None]


def _NeoforgeCompat():
    if _NEOFORGE_COMPAT[0] is None:
        from minecraft_launcher_lib.mod_loader import Neoforge
        class _NeoforgeCompatImpl(Neoforge):
            _LEGACY_MC_VERSION = "1.20.1"
            _LEGACY_API_URL = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge"
            _API_URL = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge"

            @staticmethod
            def _version_prefix(minecraft_version):
                if not minecraft_version:
                    return None
                parts = minecraft_version.split(".")
                legacy_scheme = parts[0] == "1"
                comps = parts[1:] if legacy_scheme else parts[:]
                target = 2 if legacy_scheme else 3
                if not comps or len(comps) > target:
                    return None
                comps += ["0"] * (target - len(comps))
                if not all(c.isdigit() for c in comps):
                    return None
                return ".".join(comps) + "."

            def get_loader_versions(self, minecraft_version, stable_only):
                if minecraft_version == self._LEGACY_MC_VERSION:
                    url, prefix = self._LEGACY_API_URL, f"{self._LEGACY_MC_VERSION}-"
                else:
                    url, prefix = self._API_URL, self._version_prefix(minecraft_version)
                    if prefix is None:
                        return []
                try:
                    resp = requests.get(url, timeout=15)
                    resp.raise_for_status()
                    versions = [v for v in resp.json().get("versions", []) if v.startswith(prefix)]
                except Exception as e:
                    print(f"[DEBUG] Failed to fetch NeoForge versions: {e}")
                    return []
                if stable_only:
                    stable = [v for v in versions if "beta" not in v and "alpha" not in v]
                    if stable:
                        versions = stable
                versions.reverse()
                return versions

            def get_installer_url(self, minecraft_version, loader_version):
                if loader_version.startswith(f"{self._LEGACY_MC_VERSION}-"):
                    return (f"https://maven.neoforged.net/releases/net/neoforged/forge/"
                            f"{loader_version}/forge-{loader_version}-installer.jar")
                return super().get_installer_url(minecraft_version, loader_version)

            def get_installed_version(self, minecraft_version, loader_version):
                if loader_version.startswith(f"{self._LEGACY_MC_VERSION}-"):
                    suffix = loader_version[len(self._LEGACY_MC_VERSION) + 1:]
                    return f"{self._LEGACY_MC_VERSION}-forge-{suffix}"
                return super().get_installed_version(minecraft_version, loader_version)

        _NEOFORGE_COMPAT[0] = _NeoforgeCompatImpl
    return _NEOFORGE_COMPAT[0]()

def _qimage_rgba(source):
    img = QtGui.QImage()
    if isinstance(source, (bytes, bytearray)):
        img.loadFromData(bytes(source))
    else:
        img.load(str(source))
    if img.isNull():
        raise ValueError("could not decode image")
    return img.convertToFormat(QtGui.QImage.Format_RGBA8888)


def _qimage_thumbnail(source, max_size):
    img = _qimage_rgba(source)
    if img.width() > max_size or img.height() > max_size:
        img = img.scaled(max_size, max_size, Qt.KeepAspectRatio, Qt.SmoothTransformation)
    return img

class _LazySession:
    def __init__(self):
        object.__setattr__(self, "_session", None)
    def _get(self):
        session = object.__getattribute__(self, "_session")
        if session is None:
            session = requests.Session()
            session.headers.update({"User-Agent": "Orang-Studio/OrangLaunch/7.0 (github.com/Orang-Studio/OrangLaunch)"})
            object.__setattr__(self, "_session", session)
        return session
    def __getattr__(self, name):
        return getattr(self._get(), name)
    def __setattr__(self, name, value):
        setattr(self._get(), name, value)

_http_session = _LazySession()
_image_cache: Dict[str, bytes] = {}
_image_cache_lock = threading.Lock()

def _cached_image_get(url: str, timeout: int = 8) -> bytes:
    with _image_cache_lock:
        if url in _image_cache:
            return _image_cache[url]
    try:
        r = _http_session.get(url, timeout=timeout)
        r.raise_for_status()
    except Exception:
        time.sleep(0.5)
        r = _http_session.get(url, timeout=timeout)
        r.raise_for_status()
    data = r.content
    with _image_cache_lock:
        if len(_image_cache) >= 500:
            keys = list(_image_cache.keys())[:100]
            for k in keys:
                del _image_cache[k]
        _image_cache[url] = data
    return data

def get_resource_path():
    if getattr(sys, 'frozen', False) and hasattr(sys, '_MEIPASS'):
        return Path(sys._MEIPASS)  # type: ignore
    else:
        return Path(__file__).parent
def find_resource(relative_path):
    base_path = get_resource_path()
    resource_path = base_path / relative_path
    if resource_path.exists():
        return resource_path
    alt_path = Path(relative_path)
    if alt_path.exists():
        return alt_path
    return None
class MinecraftInstance:
    def __init__(self, name: str, version: str, mod_loader: str = "vanilla",
                 instance_id: Optional[str] = None, java_args: Optional[str] = None, ram: str = "4G",
                 installed_version_id: Optional[str] = None, loader_version: Optional[str] = None):
        self.name = name
        self.version = version
        self.mod_loader = mod_loader.lower()
        self.loader_version = loader_version or ""
        self.instance_id = instance_id or str(uuid_module.uuid4())
        self.java_args = java_args or f"-Xmx{ram}"
        self.ram = ram
        self.java_path = ""
        self.created_date = datetime.now().isoformat()
        self.last_played = None
        self.play_time = 0
        self.installed_version_id = installed_version_id
        self.env_vars = ""
        self.opts = {}
        self.base_path = InstanceManager.get_instances_dir() / self.instance_id
        self.minecraft_dir = self.base_path / ".minecraft"
        self.mods_dir = self.minecraft_dir / "mods"
        self.saves_dir = self.minecraft_dir / "saves"
        self.resourcepacks_dir = self.minecraft_dir / "resourcepacks"
        self.shaderpacks_dir = self.minecraft_dir / "shaderpacks"

    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "version": self.version,
            "mod_loader": self.mod_loader,
            "loader_version": self.loader_version,
            "instance_id": self.instance_id,
            "java_args": self.java_args,
            "ram": self.ram,
            "java_path": self.java_path,
            "created_date": self.created_date,
            "last_played": self.last_played,
            "play_time": self.play_time,
            "installed_version_id": self.installed_version_id,
            "env_vars": self.env_vars,
            "opts": dict(self.opts),
            "base_path": str(self.base_path),
            "minecraft_dir": str(self.minecraft_dir)
        }
    def opt(self, key, default=None):
        value = self.opts.get(key)
        return default if value in (None, "") else value
    def set_opt(self, key, value):
        if value is None or value == "" or value is False or value == 0:
            self.opts.pop(key, None)
        else:
            self.opts[key] = value
    @classmethod
    def from_dict(cls, data: dict) -> 'MinecraftInstance':
        instance = cls(
            name=data["name"],
            version=data["version"],
            mod_loader=data.get("mod_loader", "vanilla"),
            instance_id=data["instance_id"],
            java_args=data.get("java_args"),
            ram=data.get("ram", "4G"), # I WILL FUCKING EAT YOUR RAM, IM HUNGRY FOR IT
            installed_version_id=data.get("installed_version_id"),
            loader_version=data.get("loader_version")
        )
        instance.created_date = data.get("created_date", instance.created_date)
        instance.last_played = data.get("last_played")
        instance.play_time = data.get("play_time", 0)
        instance.installed_version_id = data.get("installed_version_id")
        instance.env_vars = data.get("env_vars", "")
        instance.java_path = data.get("java_path", "")
        opts = data.get("opts")
        instance.opts = dict(opts) if isinstance(opts, dict) else {}
        return instance
    def create_directories(self):
        directories = [
            self.base_path,
            self.minecraft_dir,
            self.mods_dir,
            self.saves_dir,
            self.resourcepacks_dir,
            self.shaderpacks_dir
        ]
        for directory in directories:
            directory.mkdir(parents=True, exist_ok=True)
        instance_file = self.base_path / "instance.json"
        with open(instance_file, 'w', encoding='utf-8') as f:
            json.dump(self.to_dict(), f, indent=2, ensure_ascii=False)
    def get_mod_count(self) -> int:
        if not self.mods_dir.exists():
            return 0
        return len([f for f in self.mods_dir.iterdir() if f.suffix.lower() == '.jar'])
    def get_saves_count(self) -> int:
        if not self.saves_dir.exists():
            return 0
        return len([d for d in self.saves_dir.iterdir() if d.is_dir()])
    
# Change this to the current version
CURRENT_VERSION = "8.0.1"
# If you fork, at least leave some credit in the code, thanks.
# I'm not getting paid for this. Please don't sell this or use it for profit. 
# I worked hard on this and I want to keep it free for everyone.
REPO_OWNER = "Orang-Studio"
REPO_NAME = "OrangLaunch"
# Remember that changes to this will break the updater.
GITHUB_API_URL = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases/latest"
def check_for_updates():
    def _parse_version(v):
        clean = v.split("-")[0].lstrip("v")
        try:
            return [int(x) for x in clean.split(".")]
        except ValueError:
            return []

    if shutil.which("yay") and (Path("/etc/arch-release").exists() or Path("/etc/manjaro-release").exists()):
        try:
            # the aur api for the oranglauncher-bin package not the source package, as the source package is not maintained that well.
            response = _http_session.get("https://aur.archlinux.org/rpc/?v=5&type=info&arg[]=oranglauncher-bin", timeout=5)
            if response.status_code == 200:
                data = response.json()
                if data.get("results"):
                    aur_version = data["results"][0].get("Version", "").split('-')[0]
                    if _parse_version(aur_version) > _parse_version(CURRENT_VERSION):
                        return True, aur_version, "AUR", f"Update available via AUR: {aur_version}"
        except Exception as e:
            print(f"AUR check failed: {e}")

    try:
        response = _http_session.get(GITHUB_API_URL, timeout=5)
        if response.status_code == 200:
            data = response.json()
            tag_name = data.get("tag_name", "")
            latest_version = tag_name.lstrip("v")
            compare_version = latest_version.replace("-Linux", "")
            current_compare = CURRENT_VERSION.replace("-Linux", "") 

            if _parse_version(compare_version) > _parse_version(current_compare):
                download_url = None
                assets = data.get("assets", [])
                
                for asset in assets:
                    if asset["name"] == "launcher_x64_linux.tar.gz":
                        download_url = asset["browser_download_url"]
                        break
                
                if not download_url:
                     for asset in assets:
                         if "linux" in asset["name"].lower() and asset["name"].endswith(".tar.gz"):
                             download_url = asset["browser_download_url"]
                             break

                if not download_url:
                    for asset in assets:
                        if asset["name"].endswith(".zip") and "source" not in asset["name"]:
                            download_url = asset["browser_download_url"]
                            break
                
                if not download_url:
                    download_url = data.get("zipball_url")
                
                if download_url:
                    return True, latest_version, download_url, data.get("body", "")
        return False, CURRENT_VERSION, None, None
    except Exception as e:
        print(f"Update check failed: {e}")
        return False, CURRENT_VERSION, None, None

    # the logic for the update
def perform_update(download_url, launcher_root):
    if download_url == "AUR":
        terminals = ["gnome-terminal", "konsole", "xfce4-terminal", "mate-terminal", "xterm", "kitty", "alacritty"]
        cmd = ["yay", "-S", "oranglauncher-bin"]
        opened = False
        for t in terminals:
            if shutil.which(t):
                try:
                    if t in ["gnome-terminal", "mate-terminal", "xfce4-terminal"]:
                         subprocess.Popen([t, "--", "yay", "-S", "oranglauncher-bin"])
                    elif t == "konsole":
                         subprocess.Popen([t, "-e", "yay -S oranglauncher-bin"])
                    elif t == "xterm":
                         subprocess.Popen([t, "-e", "yay -S oranglauncher-bin"])
                    else:
                         subprocess.Popen([t, "-e", "yay -S oranglauncher-bin"])
                    opened = True
                    break
                except Exception as e:
                    print(f"Failed to launch terminal {t}: {e}")
        
        if not opened:
             messagebox.showinfo("Update", "Please run 'yay -S oranglauncher-bin' in your terminal to update.")
        return

    try:
        print(f"Downloading update from {download_url}...")
        is_tar_gz = download_url.endswith(".tar.gz")
        filename = "update.tar.gz" if is_tar_gz else "update.zip"
        update_file = Path.home() / ".cache" / "oranglauncher" / filename
        update_file.parent.mkdir(parents=True, exist_ok=True)
        with _http_session.get(download_url, stream=True) as response:
            response.raise_for_status()
            with open(update_file, "wb") as f:
                for chunk in response.iter_content(chunk_size=8192):
                    f.write(chunk)
                # temp directory for extraction
        extract_path = Path.home() / ".cache" / "oranglauncher" / "update_temp"
        if extract_path.exists():
            shutil.rmtree(extract_path)
        extract_path.mkdir(parents=True, exist_ok=True)
        
        if is_tar_gz:
            with tarfile.open(update_file, "r:gz") as tar:
                tar.extractall(path=extract_path)
        else:
            with zipfile.ZipFile(update_file, 'r') as zip_ref:
                zip_ref.extractall(extract_path)
                
        items = list(extract_path.iterdir())
        if len(items) == 1 and items[0].is_dir():
            source_dir = items[0]
            update_source = source_dir
        else:
            update_source = extract_path
            
        update_script = Path.home() / ".cache" / "oranglauncher" / "apply_update.sh"
        python_exe = sys.executable
        main_script = Path(launcher_root) / "launcher.py"
        # this is brittle
        script_content = f"""#!/bin/bash
        sleep 2
        echo "Updating OrangLauncher..."
        cp -r "{update_source}"/* "{launcher_root}/"
        rm -rf "{extract_path}"
        rm -f "{update_file}"
        rm -f "$0"
        cd "{launcher_root}"
        "{python_exe}" "{main_script}" &
        """
        with open(update_script, "w") as f:
            f.write(script_content)
        os.chmod(update_script, 0o755)
        subprocess.Popen(["/bin/bash", str(update_script)])
        sys.exit(0)
    except Exception as e:
        messagebox.showerror("Update Failed", f"Failed to perform update:\\n{e}")
        try:
            if 'update_file' in locals() and update_file.exists():
                update_file.unlink()
            if extract_path.exists():
                shutil.rmtree(extract_path)
        except:
            pass

# ui components


def _install_java_pm_or_download(major: int, status_fn, done_fn):
    def work():
        # check pacman
        if shutil.which("pacman"):
            pkg = f"jre{major}-openjdk" if major >= 11 else f"jre8-openjdk"
            status_fn(f"Trying pacman -S {pkg}…")
            r = subprocess.run(["pkexec", "pacman", "-S", "--noconfirm", "--needed", pkg],
                               capture_output=True, text=True)
            if r.returncode == 0:
                done_fn(True, f"Installed via pacman ({pkg})")
                return
        # check apt, ubuntu ui is not good but i may release the linux version for ubuntu.
        if shutil.which("apt-get"):
            pkg = "default-jre" if major == 8 else f"openjdk-{major}-jre"
            status_fn(f"Trying apt-get install {pkg}…")
            r = subprocess.run(["pkexec", "apt-get", "install", "-y", pkg],
                               capture_output=True, text=True)
            if r.returncode == 0:
                done_fn(True, f"Installed via apt ({pkg})")
                return
        # Adoptium download, i didn't find better provider
        status_fn(f"Downloading Java {major} from Adoptium…")
        path = download_java_runtime(major, progress_callback=lambda p, m: status_fn(m))
        if path:
            done_fn(True, f"Downloaded to {path}")
        else:
            done_fn(False, f"Failed - check your internet connection")
    threading.Thread(target=work, daemon=True).start()


mc_profile_url = "https://api.minecraftservices.com/minecraft/profile"


def _mc_profile(acc):
    if acc.get("type") != "microsoft":
        return None
    r = _mc_request("GET", mc_profile_url, acc)
    return r.json() if r is not None and r.ok else None


def _mc_request(method, url, acc, **kw):
    for attempt in (0, 1):
        token = acc.get("minecraft_token")
        if token and token != "0":
            r = _http_session.request(method, url, headers={"Authorization": f"Bearer {token}"}, timeout=15, **kw)
            if r.status_code != 401:
                return r
        if attempt or not acc.get("microsoft_refresh_token"):
            return None
        acc.update(refresh_mc_token(acc["microsoft_refresh_token"], acc.get("ms_client_id")))
        _store_profile(acc)


def _set_active_cape(acc, cape_id):
    url = mc_profile_url + "/capes/active"
    r = _mc_request("PUT", url, acc, json={"capeId": cape_id}) if cape_id else _mc_request("DELETE", url, acc)
    if r is None:
        raise Exception("Not signed in")
    if not r.ok:
        raise Exception(f"HTTP {r.status_code}: {r.text[:200]}")
    return r.json()


def _fetch_skin_texture(acc):
    try:
        data = _mc_profile(acc)
        if data:
            skins = data.get("skins", [])
            active = next((s for s in skins if s.get("state") == "ACTIVE"), skins[0] if skins else None)
            capes = data.get("capes", [])
            active_cape = next((c for c in capes if c.get("state") == "ACTIVE"), None)
            if active and active.get("url"):
                slim = (active.get("variant", "").upper() == "SLIM")
                skin_img = _qimage_rgba(_cached_image_get(active["url"]))
                cape_img = None
                if active_cape and active_cape.get("url"):
                    cape_img = _qimage_rgba(_cached_image_get(active_cape["url"]))
                return skin_img, slim, cape_img, capes
    except Exception as e:
        print(f"[skin] profile fetch failed: {e}")
    try:
        uuid = (acc.get('uuid') or "").replace("-", "")
        # offline accounts store an all-zero placeholder uuid; resolve by name instead
        if not uuid or uuid.strip("0") == "":
            name = acc.get('username')
            if not name:
                return None, False, None, []
            r = _http_session.get(f"https://api.mojang.com/users/profiles/minecraft/{name}", timeout=10)
            if not r.ok:
                return None, False, None, []
            uuid = r.json().get("id", "")
            if not uuid:
                return None, False, None, []
        r = _http_session.get(f"https://sessionserver.mojang.com/session/minecraft/profile/{uuid}", timeout=10)
        if not r.ok:
            return None, False, None, []
        props = r.json().get("properties", [])
        textures_b64 = next((p.get("value") for p in props if p.get("name") == "textures"), None)
        if not textures_b64:
            return None, False, None, []
        textures = json.loads(base64.b64decode(textures_b64)).get("textures", {})
        skin_info = textures.get("SKIN", {})
        skin_url = skin_info.get("url")
        if not skin_url:
            return None, False, None, []
        slim = skin_info.get("metadata", {}).get("model") == "slim"
        skin_img = _qimage_rgba(_cached_image_get(skin_url))
        cape_img = None
        cape_url = textures.get("CAPE", {}).get("url")
        if cape_url:
            try:
                cape_img = _qimage_rgba(_cached_image_get(cape_url))
            except Exception:
                cape_img = None
        return skin_img, slim, cape_img, []
    except Exception:
        return None, False, None, []

    # this was a pain in the ass
def _skin_add_box(faces, center, w, h, d, texU, texV, boxW, boxH, boxD, mirror, inflate):
    hw = w / 2 + inflate; hh = h / 2 + inflate; hd = d / 2 + inflate
    cx, cy, cz = center
    p000 = (cx - hw, cy - hh, cz - hd); p001 = (cx - hw, cy - hh, cz + hd)
    p010 = (cx - hw, cy + hh, cz - hd); p011 = (cx - hw, cy + hh, cz + hd)
    p100 = (cx + hw, cy - hh, cz - hd); p101 = (cx + hw, cy - hh, cz + hd)
    p110 = (cx + hw, cy + hh, cz - hd); p111 = (cx + hw, cy + hh, cz + hd)
    u, v, bw, bh, bd = texU, texV, boxW, boxH, boxD
    faces.append((p011, p111, p101, p001, u + bd, v + bd, bw, bh, mirror, (0, 0, 1)))
    faces.append((p110, p010, p000, p100, u + bd + bw + bd, v + bd, bw, bh, mirror, (0, 0, -1)))
    faces.append((p111, p110, p100, p101, u + bd + bw, v + bd, bd, bh, mirror, (1, 0, 0)))
    faces.append((p010, p011, p001, p000, u, v + bd, bd, bh, mirror, (-1, 0, 0)))
    faces.append((p010, p110, p111, p011, u + bd, v, bw, bd, mirror, (0, 1, 0)))
    faces.append((p001, p101, p100, p000, u + bd + bw, v, bw, bd, mirror, (0, -1, 0)))


    # 6 hours later.
def _skin_build_model(slim, overlay, legacy):
    f = []
    arm_w = 3 if slim else 4
    arm_x = 5.5 if slim else 6.0
    _skin_add_box(f, (0, 10, 0), 8, 8, 8, 0, 0, 8, 8, 8, False, 0)           # head
    _skin_add_box(f, (0, 0, 0), 8, 12, 4, 16, 16, 8, 12, 4, False, 0)        # body
    _skin_add_box(f, (-arm_x, 0, 0), arm_w, 12, 4, 40, 16, arm_w, 12, 4, False, 0)  # right arm
    # legacy for the notch based format
    if legacy:
        _skin_add_box(f, (arm_x, 0, 0), arm_w, 12, 4, 40, 16, arm_w, 12, 4, True, 0)
    else:
        _skin_add_box(f, (arm_x, 0, 0), arm_w, 12, 4, 32, 48, arm_w, 12, 4, False, 0)
    _skin_add_box(f, (-2, -12, 0), 4, 12, 4, 0, 16, 4, 12, 4, False, 0)      # right leg
    if legacy:
        _skin_add_box(f, (2, -12, 0), 4, 12, 4, 0, 16, 4, 12, 4, True, 0)
    else:
        _skin_add_box(f, (2, -12, 0), 4, 12, 4, 16, 48, 4, 12, 4, False, 0)
    if overlay:
        _skin_add_box(f, (0, 10, 0), 8, 8, 8, 32, 0, 8, 8, 8, False, 0.5)    # hat
        if not legacy:
            _skin_add_box(f, (0, 0, 0), 8, 12, 4, 16, 32, 8, 12, 4, False, 0.25)          # jacket
            _skin_add_box(f, (-arm_x, 0, 0), arm_w, 12, 4, 40, 32, arm_w, 12, 4, False, 0.25)  # right sleeve
            _skin_add_box(f, (arm_x, 0, 0), arm_w, 12, 4, 48, 48, arm_w, 12, 4, False, 0.25)   # left sleeve
            _skin_add_box(f, (-2, -12, 0), 4, 12, 4, 0, 32, 4, 12, 4, False, 0.25)         # right leg overlay
            _skin_add_box(f, (2, -12, 0), 4, 12, 4, 0, 48, 4, 12, 4, False, 0.25)          # left leg overlay
    return f

    # capes nowdays are dumb. The minecon were best ones.
def _skin_build_cape():
    raw = []
    _skin_add_box(raw, (0, 0, 0), 10, 16, 1, 0, 0, 10, 16, 1, False, 0)
    # a worn cape shows its outer texture backwards
    ry = math.pi
    rx = math.radians(10.0)
    cy_, sy_ = math.cos(ry), math.sin(ry)
    cx_, sx_ = math.cos(rx), math.sin(rx)
    box_top = (0, 8, 0)
    anchor = (0, 6, -3.0)

    def tilt(p):
        x, y, z = p[0] - box_top[0], p[1] - box_top[1], p[2] - box_top[2]
        x1 = x * cy_ + z * sy_; z1 = -x * sy_ + z * cy_
        y2 = y * cx_ - z1 * sx_; z2 = y * sx_ + z1 * cx_
        return (x1 + anchor[0], y2 + anchor[1], z2 + anchor[2])

    faces = []
    for A, B, C, D, U, V, Uw, Vh, mirror, n in raw:
        nx, ny, nz = n
        n1x = nx * cy_ + nz * sy_; n1z = -nx * sy_ + nz * cy_
        n2y = ny * cx_ - n1z * sx_; n2z = ny * sx_ + n1z * cx_
        faces.append((tilt(A), tilt(B), tilt(C), tilt(D), U, V, Uw, Vh, mirror, (n1x, n2y, n2z)))
    return faces


def _render_skin_3d(skin, width, height, yaw_deg=25.0, pitch_deg=10.0, slim=False, overlay=True, cape=None):
    # painters algo on qpainter
    legacy = skin.height() == 32
    yaw = math.radians(yaw_deg); pitch = math.radians(pitch_deg)
    cyr, syr = math.cos(yaw), math.sin(yaw)
    cxr, sxr = math.cos(pitch), math.sin(pitch)

    def rot(p):
        x, y, z = p
        x1 = x * cyr + z * syr
        z1 = -x * syr + z * cyr
        return (x1, y * cxr - z1 * sxr, y * sxr + z1 * cxr)

    scale = height / 40.0
    cx = width / 2.0; cy = height / 2.0
    lx, ly, lz = -0.3, 0.9, 0.6
    ll = math.sqrt(lx * lx + ly * ly + lz * lz); lx /= ll; ly /= ll; lz /= ll

    draw = []  # depth, texture, tile rect, tile corners matching quad corners, projected quad, shade
    def collect(face_list, texture):
        for A, B, C, D, fU, fV, fUw, fVh, mirror, normal in face_list:
            nx, ny, nz = rot(normal)
            if nz <= 0.02:
                continue
            shade = 0.62 + 0.38 * max(0.0, nx * lx + ny * ly + nz * lz)
            quad = QtGui.QPolygonF(); depth = 0.0
            for corner in (A, B, C, D):
                px_, py_, pz_ = rot((corner[0], corner[1] - 2, corner[2]))
                persp = 140.0 / (140.0 - pz_)
                quad.append(QPointF(cx + px_ * scale * persp, cy - py_ * scale * persp))
                depth += pz_
            tl, tr, br, bl = QPointF(0, 0), QPointF(fUw, 0), QPointF(fUw, fVh), QPointF(0, fVh)
            src = QtGui.QPolygonF([tr, tl, bl, br] if mirror else [tl, tr, br, bl])
            draw.append((depth, texture, QRect(fU, fV, fUw, fVh), src, quad, shade))

    collect(_skin_build_model(slim, overlay, legacy), skin)
    if cape is not None:
        collect(_skin_build_cape(), cape)
    draw.sort(key=lambda d: d[0])

    out = QtGui.QImage(width, height, QtGui.QImage.Format_ARGB32_Premultiplied)
    out.fill(0)
    painter = QtGui.QPainter(out)
    painter.setRenderHint(QtGui.QPainter.SmoothPixmapTransform, False)
    for _, texture, rect, src, quad, shade in draw:
        xf = QtGui.QTransform.quadToQuad(src, quad)
        if xf is None:
            continue
        # shade
        tile = texture.copy(rect)
        if shade < 0.999:
            tp = QtGui.QPainter(tile)
            tp.setCompositionMode(QtGui.QPainter.CompositionMode_SourceAtop)
            tp.fillRect(tile.rect(), QtGui.QColor(0, 0, 0, int(255 * (1.0 - shade))))
            tp.end()
        # quad clipping
        path = QtGui.QPainterPath(); path.addPolygon(quad); path.closeSubpath()
        painter.save()
        painter.setClipPath(path)
        painter.setTransform(xf)
        painter.drawImage(QPointF(0, 0), tile)
        painter.restore()
    painter.end()
    return out


_SKIN_PREVIEW_W = 180
_SKIN_PREVIEW_H = 280

# the pre logic was bad.
def _add_plugin_file(launcher):
    plugin_dir = Path.home() / ".local" / "share" / "oranglauncher" / "plugins"
    info_text = (
        "Native Plugin Installation:\n\n"
        "1. Create the plugins directory (if it doesn't exist):\n"
        f"   {plugin_dir}\n\n"
        "2. Place your .py plugin files in that directory\n\n"
        "3. Restart the launcher to load new plugins\n\n"
        "Plugins are automatically discovered and loaded on startup."
    )
    messagebox.showinfo("Add Plugin", info_text)


# the toggle
def _toggle_discord_rpc(launcher):
    if launcher.discord_rpc_enabled.get():
        launcher._start_discord_rpc()
    else:
        launcher._stop_discord_rpc()
        # Vakarux, I removed the section and made it as easteregg
def _get_settings_path():
    config_dir = Path.home() / ".config" / "oranglauncher"
    config_dir.mkdir(parents=True, exist_ok=True)
    return config_dir / "launcher_config.json"
_ADV_CACHE = {}
_ADV_LOADED = [False]
def _adv_all():
    if not _ADV_LOADED[0]:
        _ADV_LOADED[0] = True
        try:
            config_path = _get_settings_path()
            if config_path.exists():
                with open(config_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                adv = data.get('advanced')
                if isinstance(adv, dict):
                    _ADV_CACHE.update(adv)
        except Exception as e:
            print(f"Error loading advanced settings: {e}")
    return _ADV_CACHE
def _adv_get(key, default=None):
    value = _adv_all().get(key)
    return default if value is None else value
def _adv_set(key, value):
    _adv_all()[key] = value
    try:
        config_path = _get_settings_path()
        data = {}
        if config_path.exists():
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
            except Exception:
                data = {}
        data['advanced'] = dict(_ADV_CACHE)
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"Error saving advanced settings: {e}")
    # loader settings
def _load_settings(launcher):
    try:
        apply_launcher_proxy()
    except Exception as e:
        print(f"[Proxy] apply failed: {e}")
    try:
        config_path = _get_settings_path()
        if config_path.exists():
            with open(config_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            launcher.show_status_bar.set(data.get('show_status_bar', False))
            launcher.discord_rpc_enabled.set(data.get('discord_rpc_enabled', True))
            if not hasattr(launcher, 'delete_telemetry_on_startup'):
                launcher.delete_telemetry_on_startup = _Var(False)
            launcher.delete_telemetry_on_startup.set(data.get('delete_telemetry_on_startup', False))
            if not hasattr(launcher, 'custom_layout_enabled'):
                launcher.custom_layout_enabled = _Var(False)
            launcher.custom_layout_enabled.set(data.get('custom_layout_enabled', False))
            if not hasattr(launcher, 'debug_mode_enabled'):
                launcher.debug_mode_enabled = _Var(False)
            launcher.debug_mode_enabled.set(data.get('debug_mode_enabled', False))
            if not hasattr(launcher, 'show_progress_bar'):
                launcher.show_progress_bar = _Var(False)
            launcher.show_progress_bar.set(data.get('show_progress_bar', False))
            if not hasattr(launcher, 'use_dri_prime'):
                launcher.use_dri_prime = _Var(False)
            launcher.use_dri_prime.set(data.get('use_dri_prime', False))
            for attr, key in [('share_options', 'share_options'),
                               ('share_resourcepacks', 'share_resourcepacks'),
                               ('share_shaderpacks', 'share_shaderpacks'),
                               ('share_servers', 'share_servers'),
                               ('share_screenshots', 'share_screenshots')]:
                if not hasattr(launcher, attr):
                    setattr(launcher, attr, _Var(False))
                getattr(launcher, attr).set(data.get(key, False))
    except Exception as e:
        print(f"Error loading settings: {e}")
def _on_share_toggle(launcher):
    # persist the new state
    _save_settings(launcher)
    if hasattr(launcher, '_apply_sharing_all'):
        threading.Thread(target=launcher._apply_sharing_all, daemon=True).start()

def _save_settings(launcher):
    try:
        config_path = _get_settings_path()
        data = {}
        if os.path.exists(config_path):
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
            except Exception:
                data = {}
        data['advanced'] = dict(_adv_all())
        # update the settings with current values from launcher
        data.update({
            'show_status_bar': launcher.show_status_bar.get(),
            'discord_rpc_enabled': launcher.discord_rpc_enabled.get(),
            'delete_telemetry_on_startup': launcher.delete_telemetry_on_startup.get() if hasattr(launcher, 'delete_telemetry_on_startup') else False,
            'custom_layout_enabled': launcher.custom_layout_enabled.get() if hasattr(launcher, 'custom_layout_enabled') else False,
            'debug_mode_enabled': launcher.debug_mode_enabled.get() if hasattr(launcher, 'debug_mode_enabled') else False,
            'show_progress_bar': launcher.show_progress_bar.get() if hasattr(launcher, 'show_progress_bar') else False,
            'use_dri_prime': launcher.use_dri_prime.get() if hasattr(launcher, 'use_dri_prime') else False,
            'share_options': launcher.share_options.get() if hasattr(launcher, 'share_options') else False,
            'share_resourcepacks': launcher.share_resourcepacks.get() if hasattr(launcher, 'share_resourcepacks') else False,
            'share_shaderpacks': launcher.share_shaderpacks.get() if hasattr(launcher, 'share_shaderpacks') else False,
            'share_servers': launcher.share_servers.get() if hasattr(launcher, 'share_servers') else False,
            'share_screenshots': launcher.share_screenshots.get() if hasattr(launcher, 'share_screenshots') else False,
            'language': launcher.current_locale
        })
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"Error saving settings: {e}")
        # language preference is saved separately
def _save_language_preference(language_code):
    try:
        config_path = os.path.expanduser("~/.minecraft_launcher_config.json")
        data = {}
        if os.path.exists(config_path):
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
            except Exception:
                data = {}
        data['language'] = language_code
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"Error saving language preference: {e}")
def _save_and_apply(launcher, apply_func):
    _save_settings(launcher)
    if apply_func:
        apply_func()
def load_saved_language():
    try:
        config_path = os.path.expanduser("~/.minecraft_launcher_config.json")
        if os.path.exists(config_path):
            with open(config_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            language = data.get('language', 'en-US')
            return language
        else:
            print("[DEBUG] No config file found, using default language")
    except Exception as e:
        print(f"[DEBUG] Error loading language: {e}")
    return 'en-US'

class StreamCapture:
    def __init__(self, log_file_path, original_stream):
        self.log_file_path = log_file_path
        self.original_stream = original_stream
        self.buffer = deque(maxlen=500)  # Keep last 500 lines
        self.log_file = None
        self.try_open_log_file()
    
    def try_open_log_file(self):
        try:
            Path(self.log_file_path).parent.mkdir(parents=True, exist_ok=True)
            self.log_file = open(self.log_file_path, 'a', encoding='utf-8')
        except Exception as e:
            self.original_stream.write(f"[DEBUG] Failed to open log file: {e}\n")
    
    def write(self, message):
        if not message:
            return
        self.buffer.append(message)
        self.original_stream.write(message)
        if self.log_file:
            try:
                self.log_file.write(message)
                self.log_file.flush()
            except Exception:
                pass
    
    def flush(self):
        self.original_stream.flush()
        if self.log_file:
            self.log_file.flush()
    
    def close(self):
        if self.log_file:
            self.log_file.close()
    
    def get_buffer_content(self) -> str:
        return ''.join(self.buffer)
        


_DEBUG_CAPTURE = {"active": False, "log_file": None, "stdout": None, "stderr": None}


# logging directory for debug logs
def _launcher_log_dir():
    log_dir = Path.home() / ".local" / "share" / "oranglauncher" / "logs"
    try:
        log_dir.mkdir(parents=True, exist_ok=True)
    except Exception:
        pass
    return log_dir

 # capture stdout and stderr to a log file when debug mode enabled
def _start_debug_capture():
    if _DEBUG_CAPTURE["active"]:
        return _DEBUG_CAPTURE["log_file"]
    log_file = _launcher_log_dir() / f"launcher_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
    if _DEBUG_CAPTURE["stdout"] is None:
        _DEBUG_CAPTURE["stdout"] = sys.stdout
        _DEBUG_CAPTURE["stderr"] = sys.stderr
    sys.stdout = StreamCapture(str(log_file), _DEBUG_CAPTURE["stdout"] or sys.__stdout__)
    sys.stderr = StreamCapture(str(log_file), _DEBUG_CAPTURE["stderr"] or sys.__stderr__)
    _DEBUG_CAPTURE["active"] = True
    _DEBUG_CAPTURE["log_file"] = str(log_file)
    print(f"[DEBUG] Debug mode enabled, logging to: {log_file}")
    print(f"[DEBUG] OrangLauncher {CURRENT_VERSION} on {platform.platform()} / Python {sys.version.split()[0]}")
    return str(log_file)


def _stop_debug_capture():
    if not _DEBUG_CAPTURE["active"]:
        return
    print("[DEBUG] Debug mode disabled")
    for name in ("stdout", "stderr"):
        stream = getattr(sys, name)
        if isinstance(stream, StreamCapture):
            stream.close()
    sys.stdout = _DEBUG_CAPTURE["stdout"] or sys.__stdout__
    sys.stderr = _DEBUG_CAPTURE["stderr"] or sys.__stderr__
    _DEBUG_CAPTURE["active"] = False


def _debug_mode_saved():
    try:
        config_path = Path.home() / ".config" / "oranglauncher" / "launcher_config.json"
        if config_path.exists():
            return bool(json.loads(config_path.read_text(encoding="utf-8")).get("debug_mode_enabled", False))
    except Exception:
        pass
    return False

 # crash report writing, submit to the  if you can
def _write_crash_report(kind, text):
    try:
        log_dir = _launcher_log_dir()
        path = log_dir / f"crash_{datetime.now().strftime('%Y%m%d_%H%M%S_%f')[:-3]}.log"
        lines = [f"=== OrangLauncher {CURRENT_VERSION} crash report ({kind}) ===",
                 f"Time: {datetime.now().isoformat()}",
                 f"Platform: {platform.platform()}",
                 f"Python: {sys.version}",
                 f"Executable: {sys.executable}",
                 f"Arguments: {' '.join(sys.argv)}",
                 f"Desktop: {os.environ.get('XDG_CURRENT_DESKTOP', '?')} / {os.environ.get('XDG_SESSION_TYPE', '?')}",
                 f"Debug log: {_DEBUG_CAPTURE.get('log_file') or 'off'}", "", text.rstrip(), ""]
        stream = sys.stdout if isinstance(sys.stdout, StreamCapture) else None
        if stream is not None:
            lines.append("=== Last console output ===")
            lines.append(stream.get_buffer_content().rstrip())
            lines.append("")
        launcher = _QT_APP_REF[0] if _QT_APP_REF else None
        buffer = getattr(launcher, "_log_buffer", None)
        if buffer:
            lines.append("=== Launcher log (last 300 lines) ===")
            for entry in list(buffer)[-300:]:
                lines.append(entry[0] if isinstance(entry, tuple) else str(entry))
        path.write_text("\n".join(lines), encoding="utf-8")
        old = sorted(log_dir.glob("crash_*.log"), key=lambda q: q.stat().st_mtime)
        for stale in old[:-25]:
            try:
                stale.unlink()
            except Exception:
                pass
        return path
    except Exception as e:
        try:
            sys.__stderr__.write(f"[crash] could not write crash report: {e}\n")
        except Exception:
            pass
    return None


def install_crash_handlers():
    if getattr(install_crash_handlers, "_done", False):
        return
    install_crash_handlers._done = True
    log_dir = _launcher_log_dir()
    try:
        import faulthandler
        fh = open(log_dir / "faulthandler.log", "a", encoding="utf-8")
        fh.write(f"\n=== OrangLauncher {CURRENT_VERSION} started {datetime.now().isoformat()} (pid {os.getpid()}) ===\n")
        fh.flush()
        faulthandler.enable(file=fh, all_threads=True)
        install_crash_handlers._faulthandler_file = fh
    except Exception as e:
        print(f"[crash] faulthandler unavailable: {e}")
    previous_hook = sys.excepthook

    def excepthook(exc_type, exc, exc_tb):
        text = "".join(traceback.format_exception(exc_type, exc, exc_tb))
        path = _write_crash_report("unhandled exception", text)
        try:
            sys.__stderr__.write(text)
            if path:
                sys.__stderr__.write(f"[crash] report written to {path}\n")
        except Exception:
            pass
        if isinstance(sys.stderr, StreamCapture):
            try:
                sys.stderr.write(text)
            except Exception:
                pass
        if previous_hook not in (None, sys.__excepthook__):
            try:
                previous_hook(exc_type, exc, exc_tb)
            except Exception:
                pass
    sys.excepthook = excepthook

    def thread_hook(args):
        if args.exc_type is SystemExit:
            return
        text = f"Thread: {getattr(args.thread, 'name', '?')}\n" + "".join(traceback.format_exception(args.exc_type, args.exc_value, args.exc_traceback))
        _write_crash_report("thread exception", text)
        try:
            sys.stderr.write(text)
        except Exception:
            pass
    try:
        threading.excepthook = thread_hook
    except Exception:
        pass

 # toggle debug mode and start/stop capturing stdout/stderr to a log file
def _toggle_debug_mode(launcher):
    _save_settings(launcher)
    if launcher.debug_mode_enabled.get():
        launcher._current_log_file = _start_debug_capture()
    else:
        _stop_debug_capture()


# modpack things 
class ModrinthPackImporter:
    def __init__(self, launcher=None):
        self.launcher = launcher
        self.instance_mgr = get_instance_manager()
    def import_mrpack(self, mrpack_path):
        try:
            print(f"[MRPACK] Starting import of {mrpack_path}")
            mrpack_file = Path(mrpack_path)
            if not mrpack_file.exists():
                return False, f"File not found: {mrpack_path}", None
            if not mrpack_file.is_file():
                return False, f"Path is not a file: {mrpack_path}", None
            file_size = mrpack_file.stat().st_size
            print(f"[MRPACK] File size: {file_size} bytes")
            if file_size == 0:
                return False, "The mrpack file is empty (0 bytes)", None
            if not zipfile.is_zipfile(mrpack_path):
                return False, f"The selected file is not a valid .mrpack (zip) file.\\n\\nFile: {mrpack_file.name}\\nSize: {file_size} bytes\\n\\nMake sure the file downloaded completely.", None
            print(f"[MRPACK] Verified as valid zip file")
            temp_dir = Path(tempfile.mkdtemp())
            print(f"[MRPACK] Created temp directory: {temp_dir}")
            with zipfile.ZipFile(mrpack_path, 'r') as zip_ref:
                zip_ref.extractall(temp_dir)
            print(f"[MRPACK] Extracted mrpack file")
            index_path = temp_dir / ORANGPACK_INDEX
            if not index_path.exists():
                index_path = temp_dir / "modrinth.index.json"
            if not index_path.exists():
                return False, "Invalid modpack: modrinth.index.json not found", None
            with open(index_path, 'r', encoding='utf-8') as f:
                pack_data = json.load(f)
            game_version = pack_data.get("dependencies", {}).get("minecraft", "")
            if not game_version:
                gv = pack_data.get("game_versions", [])
                game_version = gv[0] if gv else ""
            if not game_version:
                return False, "Invalid modpack: no Minecraft version found in modrinth.index.json", None
            pack_name = pack_data.get("name", "Unknown Pack")
            pack_version = pack_data.get("versionId") or pack_data.get("version_id") or "1.0"
            mod_loader, loader_version = self._detect_mod_loader(pack_data)
            print(f"[MRPACK] Pack info: {pack_name} v{pack_version}")
            print(f"[MRPACK] Minecraft version: {game_version}")
            print(f"[MRPACK] Mod loader: {mod_loader} {loader_version}")
            try:
                print(f"[MRPACK] Creating instance...")
                instance = self.instance_mgr.create_instance(
                    name=pack_name,
                    version=game_version,
                    mod_loader=mod_loader,
                    ram="4G",
                    loader_version=loader_version
                )
                if not instance:
                    return False, "Failed to create instance", None
                print(f"[MRPACK] Instance created: {instance.instance_id}")
                self.instance_mgr.set_selected_instance(instance.instance_id)
                print(f"[MRPACK] Instance selected")
                self._download_mods_to_instance(pack_data, instance)
                self._download_dependencies(pack_data, instance)
                self._import_overrides_to_instance(temp_dir, instance)
                self._save_pack_metadata(temp_dir, pack_data, instance, mrpack_file)
                self._apply_orangpack_settings(temp_dir, pack_data, instance)
            except ValueError as e:
                print(f"[MRPACK] Instance name exists, trying with version suffix...")
                try:
                    instance_name = f"{pack_name} ({pack_version})"
                    instance = self.instance_mgr.create_instance(
                        name=instance_name,
                        version=game_version,
                        mod_loader=mod_loader,
                        ram="4G",
                        loader_version=loader_version
                    )
                    if not instance:
                        return False, "Failed to create instance", None
                    print(f"[MRPACK] Instance created with suffix: {instance.instance_id}")
                    self.instance_mgr.set_selected_instance(instance.instance_id)
                    self._download_mods_to_instance(pack_data, instance)
                    self._download_dependencies(pack_data, instance)
                    self._import_overrides_to_instance(temp_dir, instance)
                    self._save_pack_metadata(temp_dir, pack_data, instance, mrpack_file)
                    self._apply_orangpack_settings(temp_dir, pack_data, instance)
                except Exception as inner_e:
                    return False, f"Failed to create instance: {str(inner_e)}", None
            shutil.rmtree(temp_dir)
            if self.launcher is not None and hasattr(self.launcher, '_apply_sharing_for_instance'):
                try:
                    self.launcher._apply_sharing_for_instance(instance)
                except Exception as share_e:
                    print(f"[MRPACK] sharing apply failed: {share_e}")
            self.instance_mgr._notify_callbacks()
            failed = getattr(self, '_failed_files', []) or []
            note = ""
            if failed:
                note = f"\n\n{len(failed)} file(s) could not be downloaded:\n" + "\n".join(failed[:8])
                if len(failed) > 8:
                    note += f"\n... and {len(failed) - 8} more"
            return True, f"Successfully imported {pack_name} (Minecraft {game_version}, {mod_loader}){note}", instance.name
        except Exception as e:
            traceback.print_exc()
            return False, f"Error importing modpack: {str(e)}", None
    def _apply_orangpack_settings(self, temp_dir, pack_data, instance):
        extra = pack_data.get("orangpack") if isinstance(pack_data, dict) else None
        if not isinstance(extra, dict):
            return
        inst_data = extra.get("instance") or {}
        try:
            if inst_data.get("ram"):
                instance.ram = inst_data["ram"]
                instance.java_args = f"-Xmx{instance.ram}"
            if inst_data.get("env_vars"):
                instance.env_vars = inst_data["env_vars"]
            opts = inst_data.get("opts")
            if isinstance(opts, dict):
                for k, v in opts.items():
                    if k in ("custom_lwjgl_dir", "glfw_path", "openal_path", "pre_launch_cmd", "wrapper_cmd", "post_exit_cmd"):
                        continue
                    instance.opts[k] = v
            inst_dir = temp_dir / "instance"
            if inst_dir.exists():
                for src_path in inst_dir.glob("**/*"):
                    if src_path.is_file() and src_path.name != "instance.json":
                        rel = src_path.relative_to(inst_dir)
                        dst = instance.base_path / rel
                        dst.parent.mkdir(parents=True, exist_ok=True)
                        shutil.copy2(src_path, dst)
                icon_txt = instance.base_path / "icon.txt"
                icon_png = instance.base_path / "icon.png"
                if icon_png.exists():
                    icon_txt.write_text(str(icon_png), encoding="utf-8")
            self.instance_mgr.save_instances()
        except Exception as e:
            print(f"[ORANGPACK] settings apply failed: {e}")
    def apply_pack_to_instance(self, pack_path, instance):
        pack_path = Path(pack_path)
        if not pack_path.exists() or not zipfile.is_zipfile(pack_path):
            return False, "Pack file is missing or not a zip."
        temp_dir = Path(tempfile.mkdtemp())
        try:
            with zipfile.ZipFile(pack_path, 'r') as zip_ref:
                zip_ref.extractall(temp_dir)
            index_path = temp_dir / ORANGPACK_INDEX
            if not index_path.exists():
                index_path = temp_dir / "modrinth.index.json"
            if not index_path.exists():
                return False, "Invalid modpack: modrinth.index.json not found"
            with open(index_path, 'r', encoding='utf-8') as f:
                pack_data = json.load(f)
            game_version = pack_data.get("dependencies", {}).get("minecraft", "") or instance.version
            mod_loader, loader_version = self._detect_mod_loader(pack_data)
            changed = (instance.version != game_version) or ((instance.mod_loader or "").lower() != mod_loader) or ((instance.loader_version or "") != (loader_version or ""))
            instance.version = game_version
            instance.mod_loader = mod_loader
            instance.loader_version = loader_version or ""
            if changed:
                instance.installed_version_id = None
            self._download_mods_to_instance(pack_data, instance)
            self._download_dependencies(pack_data, instance)
            self._import_overrides_to_instance(temp_dir, instance)
            self._save_pack_metadata(temp_dir, pack_data, instance, pack_path)
            self._apply_orangpack_settings(temp_dir, pack_data, instance)
            self.instance_mgr.save_instances()
            self.instance_mgr._notify_callbacks()
            failed = getattr(self, '_failed_files', []) or []
            note = f"\n\n{len(failed)} file(s) could not be downloaded." if failed else ""
            return True, f"Applied {pack_data.get('name', pack_path.name)} {pack_data.get('version_id', '')} to '{instance.name}'.{note}"
        except Exception as e:
            traceback.print_exc()
            return False, f"Error applying modpack: {e}"
        finally:
            shutil.rmtree(temp_dir, ignore_errors=True)

            # detect mod loader from pack data
    def _detect_mod_loader(self, pack_data):
        dependencies = pack_data.get("dependencies", {})
        if "fabric-loader" in dependencies:
            return "fabric", dependencies.get("fabric-loader", "")
        if "quilt-loader" in dependencies:
            return "quilt", dependencies.get("quilt-loader", "")
        if "neoforge" in dependencies:
            return "neoforge", dependencies.get("neoforge", "")
        if "forge" in dependencies:
            return "forge", dependencies.get("forge", "")
        files = pack_data.get("files", [])
        for file in files:
            file_path = file.get("path", "").lower()
            if "fabric" in file_path:
                return "fabric", ""
            if "quilt" in file_path:
                return "quilt", ""
            if "neoforge" in file_path:
                return "neoforge", ""
            if "forge" in file_path:
                return "forge", ""
        return "vanilla", ""

        # downloader
    def _download_mods_to_instance(self, pack_data, instance):
        files = pack_data.get("files", [])
        game_dir = instance.minecraft_dir
        game_dir.mkdir(parents=True, exist_ok=True)
        wanted = []
        for file_info in files:
            file_path = (file_info.get("path", "") or "").replace("\\", "/").lstrip("/")
            if not file_path or ".." in file_path.split("/"):
                continue
            env = file_info.get("env") or {}
            if env.get("client") == "unsupported":
                continue
            urls = [u for u in (file_info.get("downloads") or []) if u]
            if not urls:
                print(f"[MRPACK] Skipping {file_path} - no download URL")
                continue
            wanted.append((file_path, urls, file_info.get("hashes") or {}, file_info.get("fileSize")))
        total = len(wanted)
        self._failed_files = []
        done_count = [0]
        lock = threading.Lock()
        def _status(n):
            if self.launcher and hasattr(self.launcher, 'status_label'):
                try:
                    self.launcher.after(0, lambda n=n: self.launcher.status_label.config(text=f"Importing modpack: {n}/{total} files"))  # type: ignore
                except Exception:
                    pass
        _status(0)
        def _fetch(item):
            file_path, urls, hashes, size = item
            dest = game_dir / file_path
            dest.parent.mkdir(parents=True, exist_ok=True)
            want_sha1 = hashes.get("sha1")
            if dest.exists():
                try:
                    if not want_sha1 or _sha1_file(dest) == want_sha1:
                        return True
                except Exception:
                    pass
            last_err = None
            for attempt in range(3):
                for url in urls:
                    try:
                        response = _http_session.get(url, stream=True, timeout=60)
                        response.raise_for_status()
                        tmp = dest.with_name(dest.name + ".part")
                        with open(tmp, 'wb') as f:
                            for chunk in response.iter_content(chunk_size=65536):
                                if chunk:
                                    f.write(chunk)
                        if want_sha1 and _sha1_file(tmp) != want_sha1:
                            raise IOError("sha1 mismatch")
                        tmp.replace(dest)
                        return True
                    except Exception as e:
                        last_err = e
                time.sleep(0.5 * (attempt + 1))
            print(f"[MRPACK] Error downloading {file_path}: {last_err}")
            with lock:
                self._failed_files.append(file_path)
            return False
        from concurrent.futures import ThreadPoolExecutor
        with ThreadPoolExecutor(max_workers=6) as pool:
            for ok in pool.map(_fetch, wanted):
                with lock:
                    done_count[0] += 1
                    n = done_count[0]
                if n % 5 == 0 or n == total:
                    _status(n)
        print(f"[MRPACK] Downloaded {total - len(self._failed_files)}/{total} files")
        if self._failed_files:
            print(f"[MRPACK] Failed files: {self._failed_files}")
            # im not sponsoring modrinth or these, but this is a good way to get the dependencies from modrinth
    def _download_dependencies(self, pack_data, instance):
        dependencies = pack_data.get("dependencies", {})
        mods_dir = instance.mods_dir
        mod_dependencies = {
            "fabric-api": {"project_id": "P7dR8mSH", "name": "Fabric API"},
            "quilted-fabric-api": {"project_id": "qvIfYCYJ", "name": "Quilted Fabric API"},
            "quilt-standard-libraries": {"project_id": "qvIfYCYJ", "name": "Quilt Standard Libraries"},
            "cloth-config": {"project_id": "9s6osm5g", "name": "Cloth Config API"},
            "architectury-api": {"project_id": "lhGA9TYQ", "name": "Architectury API"},
            "modmenu": {"project_id": "mOgUt4GM", "name": "Mod Menu"},
            "fabric-language-kotlin": {"project_id": "Ha28R6CL", "name": "Fabric Language Kotlin"}
        }
        minecraft_version = dependencies.get("minecraft", "")
        if not minecraft_version:
            minecraft_version = pack_data.get("game_versions", [""])[0]
        detected = self._detect_mod_loader(pack_data)
        mod_loader = (detected[0] if isinstance(detected, (tuple, list)) else detected or "").lower()
        if mod_loader == "none" or mod_loader == "vanilla" or not mod_loader:
            mod_loader = "fabric"
        print(f"[MRPACK] Checking dependencies for {mod_loader} {minecraft_version}...")
        downloaded_deps = 0
        for dep_key, dep_info in mod_dependencies.items():
            dep_version = dependencies.get(dep_key)
            if dep_version or dep_key in dependencies:
                print(f"[MRPACK] Found dependency: {dep_info['name']}")
                try:
                    success = self._download_from_modrinth(
                        project_id=dep_info['project_id'],
                        minecraft_version=minecraft_version,
                        mod_loader=mod_loader,
                        mods_dir=mods_dir,
                        mod_name=dep_info['name']
                    )
                    if success:
                        downloaded_deps += 1
                        print(f"[MRPACK] Downloaded {dep_info['name']}")
                    else:
                        print(f"[MRPACK] Failed to download {dep_info['name']}")
                except Exception as e:
                    print(f"[MRPACK] Error downloading {dep_info['name']}: {e}")
        if downloaded_deps > 0:
            print(f"[MRPACK] Successfully downloaded {downloaded_deps} dependencies")
        else:
            print(f"[MRPACK] No additional dependencies needed")
    def _download_from_modrinth(self, project_id, minecraft_version, mod_loader, mods_dir, mod_name):
        try:
            api_url = f"https://api.modrinth.com/v2/project/{project_id}/version"
            headers = {"User-Agent": "OrangeLauncher/1.0"}
            print(f"[MRPACK] Querying Modrinth API for {mod_name} (MC {minecraft_version}, {mod_loader})...")
            response = _http_session.get(api_url, headers=headers, timeout=30)
            response.raise_for_status()
            all_versions = response.json()
            if not all_versions or len(all_versions) == 0:
                print(f"[MRPACK] No versions found for {mod_name}")
                return False
            compatible_versions = []
            for version in all_versions:
                game_versions = version.get("game_versions", [])
                loaders = version.get("loaders", [])
                mc_compatible = minecraft_version in game_versions
                loader_compatible = mod_loader in [l.lower() for l in loaders]
                if mc_compatible and loader_compatible:
                    compatible_versions.append(version)
            
            if not compatible_versions:
                print(f"[MRPACK] No exact match for {mod_name}, trying to find compatible version with any recent version for {mod_loader}...")
                for version in all_versions:
                    loaders = version.get("loaders", [])
                    loader_compatible = mod_loader in [l.lower() for l in loaders]
                    if loader_compatible:
                        compatible_versions.append(version)
            
            if not compatible_versions:
                print(f"[MRPACK] No compatible version found for {mod_name}")
                return False
            latest_version = pick_modrinth_version(compatible_versions)
            version_number = latest_version.get("version_number", "unknown")
            print(f"[MRPACK] Found compatible version: {version_number} "
                  f"({latest_version.get('version_type', 'release')})")
            files = latest_version.get("files", [])
            if not files:
                print(f"[MRPACK] No files found for {mod_name}")
                return False
            primary_file = None
            for file in files:
                if file.get("primary", False):
                    primary_file = file
                    break
            if not primary_file:
                primary_file = files[0]
            download_url = primary_file.get("url")
            filename = primary_file.get("filename")
            if not download_url or not filename:
                print(f"[MRPACK] Invalid file data for {mod_name}")
                return False
            mod_path = mods_dir / filename
            if mod_path.exists():
                print(f"[MRPACK] {filename} already exists, skipping")
                return True
            print(f"[MRPACK] Downloading {filename}...")
            file_response = _http_session.get(download_url, stream=True, timeout=60)
            file_response.raise_for_status()
            with open(mod_path, 'wb') as f:
                for chunk in file_response.iter_content(chunk_size=8192):
                    if chunk:
                        f.write(chunk)
            print(f"[MRPACK] Successfully downloaded {filename}")
            return True
        except Exception as e:
            print(f"[MRPACK] Error in _download_from_modrinth: {e}")
            traceback.print_exc()
            return False
            # copy overrides to instance
    def _import_overrides_to_instance(self, temp_dir, instance):
        game_dir = instance.minecraft_dir
        game_dir.mkdir(parents=True, exist_ok=True)
        copied_files = 0
        for folder in ("overrides", "client-overrides"):
            overrides_dir = temp_dir / folder
            if not overrides_dir.exists():
                continue
            for src_path in overrides_dir.glob("**/*"):
                if src_path.is_file():
                    rel_path = src_path.relative_to(overrides_dir)
                    dst_path = game_dir / rel_path
                    dst_path.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(src_path, dst_path)
                    copied_files += 1
        print(f"Copied {copied_files} override files")
    def _save_pack_metadata(self, temp_dir, pack_data, instance, source_path):
        meta = {
            "name": pack_data.get("name"),
            "version_id": pack_data.get("version_id"),
            "summary": pack_data.get("summary"),
            "dependencies": pack_data.get("dependencies", {}),
            "imported": datetime.now().isoformat(),
            "source_file": str(source_path),
            "file_count": len(pack_data.get("files", [])),
            "managed_files": [f.get("path") for f in pack_data.get("files", []) if f.get("path")],
        }
        try:
            sha1 = _sha1_file(source_path)
            found = _modrinth_lookup_hashes([sha1])
            version = found.get(sha1)
            if version:
                meta["modrinth_project_id"] = version.get("project_id")
                meta["modrinth_version_id"] = version.get("id")
                meta["modrinth_version_number"] = version.get("version_number")
                try:
                    r = _http_session.get(f"{MODRINTH_API_URL}/project/{version.get('project_id')}", timeout=15)
                    if r.ok:
                        proj = r.json()
                        meta["modrinth_slug"] = proj.get("slug")
                        meta["modrinth_title"] = proj.get("title")
                        icon_url = proj.get("icon_url")
                        if icon_url:
                            self._save_icon_from_url(icon_url, instance)
                except Exception as e:
                    print(f"[MRPACK] project lookup failed: {e}")
        except Exception as e:
            print(f"[MRPACK] hash lookup failed: {e}")
        if not (instance.base_path / "icon.txt").exists():
            for candidate in ("icon.png", "icon.jpg", "icon.jpeg", "icon.webp", "pack.png"):
                src = temp_dir / candidate
                if src.exists():
                    self._save_icon_file(src, instance)
                    break
        try:
            with open(instance.base_path / "modpack.json", "w", encoding="utf-8") as f:
                json.dump(meta, f, indent=2, ensure_ascii=False)
        except Exception as e:
            print(f"[MRPACK] metadata save failed: {e}")
    def _save_icon_file(self, src, instance):
        try:
            img = _qimage_thumbnail(src, 64)
            dest = instance.base_path / "icon.png"
            img.save(str(dest), "PNG")
            (instance.base_path / "icon.txt").write_text(str(dest), encoding="utf-8")
        except Exception as e:
            print(f"[MRPACK] icon save failed: {e}")
    def _save_icon_from_url(self, url, instance):
        try:
            data = _cached_image_get(url, timeout=15)
            img = _qimage_thumbnail(data, 64)
            dest = instance.base_path / "icon.png"
            img.save(str(dest), "PNG")
            (instance.base_path / "icon.txt").write_text(str(dest), encoding="utf-8")
        except Exception as e:
            print(f"[MRPACK] icon download failed: {e}")
def import_modpack(mrpack_path, launcher=None):
    importer = ModrinthPackImporter(launcher)
    return importer.import_mrpack(mrpack_path)


class CurseForgePackImporter:

    CURSEFORGE_API = "https://api.curseforge.com/v1"

    def __init__(self, launcher=None):
        self.launcher = launcher
        self.instance_mgr = get_instance_manager()

    def _log(self, msg):
        print(msg)
        if self.launcher and hasattr(self.launcher, '_safe_append_log'):
            self.launcher._safe_append_log(msg)

    def import_zip(self, zip_path):
        zip_path = Path(zip_path)
        if not zip_path.exists():
            return False, f"File not found: {zip_path}", None
        if not zipfile.is_zipfile(zip_path):
            return False, "Not a valid zip file.", None

        temp_dir = Path(tempfile.mkdtemp())
        try:
            with zipfile.ZipFile(zip_path, 'r') as zf:
                zf.extractall(temp_dir)

            manifest_path = temp_dir / "manifest.json"
            if not manifest_path.exists():
                return False, "manifest.json not found - this does not appear to be a CurseForge modpack.", None

            with open(manifest_path, 'r', encoding='utf-8') as f:
                manifest = json.load(f)

            if manifest.get("manifestType") != "minecraftModpack":
                return False, f"Unknown manifest type: {manifest.get('manifestType')}", None

            mc_info = manifest.get("minecraft", {})
            mc_version = mc_info.get("version", "")
            loader_id = ""
            loader_name = "vanilla"
            loader_version = None
            for ml in mc_info.get("modLoaders", []):
                if ml.get("primary"):
                    loader_id = ml.get("id", "") 
                    break
            if loader_id.startswith("forge-"):
                loader_name = "forge"
                loader_version = loader_id[len("forge-"):]
            elif loader_id.startswith("fabric-"):
                loader_name = "fabric"
                loader_version = loader_id[len("fabric-"):]
            elif loader_id.startswith("quilt-"):
                loader_name = "quilt"
                loader_version = loader_id[len("quilt-"):]

            pack_name = manifest.get("name", zip_path.stem)
            files = manifest.get("files", [])
            overrides_dir_name = manifest.get("overrides", "overrides")
            overrides_dir = temp_dir / overrides_dir_name

            instance = MinecraftInstance(
                name=pack_name,
                version=mc_version,
                mod_loader=loader_name,
                ram="4G",
            )
            instance.create_directories()
            self.instance_mgr.add_instance(instance)

            copied = 0
            if overrides_dir.exists():
                for src in overrides_dir.rglob("*"):
                    if src.is_file():
                        rel = src.relative_to(overrides_dir)
                        dst = instance.minecraft_dir / rel
                        dst.parent.mkdir(parents=True, exist_ok=True)
                        shutil.copy2(src, dst)
                        copied += 1

            required_mods = [f for f in files if f.get("required", True)]
            optional_mods = [f for f in files if not f.get("required", True)]
            # Curse is a shit company and they don't want people to download mods without an API key, so we have to check for that
            api_key = os.environ.get("CURSEFORGE_API_KEY", "")
            downloaded = 0
            failed_ids = []
            if api_key and required_mods:
                self._log(f"[CurseForge] API key found, attempting to download {len(required_mods)} mods...")
                for mod_file in required_mods:
                    project_id = mod_file.get("projectID")
                    file_id = mod_file.get("fileID")
                    try:
                        url = f"{self.CURSEFORGE_API}/mods/{project_id}/files/{file_id}/download-url"
                        resp = _http_session.get(url, headers={"x-api-key": api_key}, timeout=10)
                        if resp.status_code == 200:
                            dl_url = resp.json().get("data")
                            if dl_url:
                                r = _http_session.get(dl_url, timeout=60)
                                fname = dl_url.split("/")[-1].split("?")[0] or f"{project_id}-{file_id}.jar"
                                (instance.mods_dir / fname).write_bytes(r.content)
                                downloaded += 1
                                continue
                    except Exception as e:
                        self._log(f"[CurseForge] Failed to download {project_id}/{file_id}: {e}")
                    failed_ids.append((project_id, file_id))
            else:
                failed_ids = [(f["projectID"], f["fileID"]) for f in required_mods]

            self.instance_mgr.save_instances()

            summary_parts = [
                f"Created instance: {pack_name}",
                f"Minecraft {mc_version} + {loader_name}" + (f" {loader_version}" if loader_version else ""),
                f"Override files copied: {copied}",
                f"Mods downloaded: {downloaded}/{len(required_mods)}",
            ]
            if failed_ids:
                summary_parts.append(
                    f"\n{len(failed_ids)} mod(s) need manual download (no API key or download failed).\n"
                    f"Set CURSEFORGE_API_KEY env var, or download mods manually from CurseForge\n"
                    f"and place them in: {instance.mods_dir}"
                )
            if optional_mods:
                summary_parts.append(f"Optional mods not included: {len(optional_mods)}")

            return True, "\n".join(summary_parts), pack_name

        except Exception as e:
            self._log(f"[CurseForge] Import error: {e}\n{traceback.format_exc()}")
            return False, f"Import failed: {e}", None
        finally:
            try:
                shutil.rmtree(temp_dir, ignore_errors=True)
            except Exception:
                pass


def import_curseforge_pack(zip_path, launcher=None):
    importer = CurseForgePackImporter(launcher)
    return importer.import_zip(zip_path)

MODRINTH_API_BASE = "https://api.modrinth.com/v2"
_MODRINTH_CHANNEL_RANK = {"release": 0, "beta": 1, "alpha": 2}


def order_modrinth_versions(versions):
  #  order Modrinth versions by preference: stable releases first, then beta,
  #  then alpha, and newest first within each channel.
    ordered = sorted(versions or [], key=lambda v: v.get("date_published") or "", reverse=True)
    ordered.sort(key=lambda v: _MODRINTH_CHANNEL_RANK.get(
        (v.get("version_type") or "release").lower(), 3))
    return ordered


def pick_modrinth_version(versions):
    # best version to install from an already game-version/loader-filtered list.
    ordered = order_modrinth_versions(versions)
    return ordered[0] if ordered else None


def _normalize_query_from_filename(filename: str) -> str:
    name = filename.lower()
    if name.endswith('.jar'):
        name = name[:-4]
    name = name.replace('_', '-').replace('.', '-')
    parts = name.split('-')
    while parts and re.match(r'^[0-9]+([.-][0-9a-z]+)*$', parts[-1]):
        parts.pop()
    if not parts:
        return name
    return '-'.join(parts)


class ModrinthUpdater:
    def __init__(self, logger: Optional[Callable[[str], None]] = None):
        self.logger = logger or (lambda m: None)

    def _log(self, msg: str):
        try:
            self.logger(msg)
        except Exception:
            pass

    def search_projects(self, query: str, limit: int = 5) -> List[Dict]:
        url = f"{MODRINTH_API_BASE}/search"
        params = {"query": query, "limit": limit}
        try:
            r = _http_session.get(url, params=params, timeout=10)
            r.raise_for_status()
            data = r.json()
            return data.get('hits', [])
        except Exception as e:
            self._log(f"[Modrinth] Search failed for '{query}': {e}")
            return []

    def get_project_versions(self, slug: str) -> List[Dict]:
        url = f"{MODRINTH_API_BASE}/project/{slug}/version"
        try:
            r = _http_session.get(url, timeout=10)
            r.raise_for_status()
            return r.json()
        except Exception as e:
            self._log(f"[Modrinth] Get versions failed for '{slug}': {e}")
            return []

    def get_project_info(self, slug: str) -> Dict:
        url = f"{MODRINTH_API_BASE}/project/{slug}"
        try:
            r = _http_session.get(url, timeout=10)
            r.raise_for_status()
            return r.json()
        except Exception as e:
            self._log(f"[Modrinth] Get project info failed for '{slug}': {e}")
            return {}

    def _parse_numeric_ver(self, s: str) -> Optional[Tuple[int, int, int]]:
        try:
            m = re.search(r"(\d+)\.(\d+)(?:\.(\d+))?", str(s))
            if not m:
                return None
            a = int(m.group(1))
            b = int(m.group(2))
            c = int(m.group(3)) if m.group(3) else 0
            return (a, b, c)
        except Exception:
            return None

    def _extract_info_from_jar(self, jar_path: Path) -> Dict[str, Optional[str]]:
        info = {'id': None, 'name': None, 'version': None}
        try:
            if not jar_path.exists():
                return info
            with zipfile.ZipFile(jar_path, 'r') as z:
                namelist = z.namelist()
                for candidate in ('fabric.mod.json', 'quilt.mod.json'):
                    for n in namelist:
                        if n.endswith(candidate):
                            try:
                                raw = z.read(n).decode('utf-8')
                                data = json.loads(raw)
                                if isinstance(data, dict):
                                    if 'id' in data and not info['id']:
                                        info['id'] = data.get('id')
                                    if 'name' in data and not info['name']:
                                        nm = data.get('name')
                                        if isinstance(nm, str):
                                            info['name'] = nm
                                        elif isinstance(nm, dict):
                                            info['name'] = next(iter(nm.values()), None)
                                    if 'version' in data and not info.get('version'):
                                        try:
                                            info['version'] = str(data.get('version'))
                                        except Exception:
                                            pass
                            except Exception:
                                pass
                for n in namelist:
                    if n.endswith('mcmod.info'):
                        try:
                            raw = z.read(n).decode('utf-8')
                            data = json.loads(raw)
                            if isinstance(data, list) and data:
                                entry = data[0]
                                if not info['id'] and 'modid' in entry:
                                    info['id'] = entry.get('modid')
                                if not info['name'] and 'name' in entry:
                                    info['name'] = entry.get('name')
                                if not info.get('version') and 'version' in entry:
                                    try:
                                        info['version'] = str(entry.get('version'))
                                    except Exception:
                                        pass
                        except Exception:
                            pass
                    if n.endswith('mods.toml'):
                        try:
                            raw = z.read(n).decode('utf-8')
                            for line in raw.splitlines():
                                line = line.strip()
                                if line.startswith('modId') and '=' in line and not info['id']:
                                    val = line.split('=', 1)[1].strip().strip('"')
                                    info['id'] = val
                                if line.startswith('displayName') and '=' in line and not info['name']:
                                    val = line.split('=', 1)[1].strip().strip('"')
                                    info['name'] = val
                                if 'version' in line and '=' in line and not info.get('version'):
                                    parts = line.split('=', 1)
                                    key = parts[0].strip()
                                    if key.startswith('version') or key.endswith('version') or 'version' in key:
                                        val = parts[1].strip().strip('"')
                                        if len(val) < 64:
                                            info['version'] = val
                        except Exception:
                            pass
        except Exception:
            pass
        return info

    def _version_is_compatible(self, v: Dict, requested_game_version: Optional[str]) -> bool:
        if not requested_game_version:
            return True
        gversions = v.get('game_versions') or []
        gversions_l = [str(g).lower() for g in gversions]
        if str(requested_game_version).lower() in gversions_l:
            return True
        rq_nums = self._parse_numeric_ver(requested_game_version)
        if rq_nums:
            for gv in gversions:
                parsed = self._parse_numeric_ver(str(gv))
                if parsed and parsed == rq_nums:
                    return True
        return False

    def choose_best_version(self, versions: List[Dict], loader: str, game_version: str, aggressive: bool = False) -> Optional[Dict]:
        loader_l = loader.lower() if loader else ''

        rq_nums = self._parse_numeric_ver(game_version) if game_version else None
        rq_major_minor = (rq_nums[0], rq_nums[1]) if rq_nums else None

        exact_matches = []
        newer_or_equal_patch = []
        older_patch = []
        fallback_matches = []

        for v in versions:
            loaders = [l.lower() for l in v.get('loaders', [])]
            if loader_l and loader_l not in loaders:
                continue
            gversions = [str(g) for g in v.get('game_versions', [])]
            gversions_l = [gv.lower() for gv in gversions]

            version_number_text = str(v.get('version_number', '')).lower()
            if game_version and (str(game_version).lower() in gversions_l or str(game_version).lower() in version_number_text):
                exact_matches.append(v)
                continue

            numeric_versions = [self._parse_numeric_ver(gv) for gv in gversions]
            vn_parsed = self._parse_numeric_ver(version_number_text)
            if vn_parsed is not None:
                numeric_versions.append(vn_parsed)
            numeric_versions = [nv for nv in numeric_versions if nv is not None]

            if rq_nums and numeric_versions:
                max_nv = max(numeric_versions)
                if rq_major_minor and (max_nv[0], max_nv[1]) == rq_major_minor:
                    if max_nv >= rq_nums:
                        newer_or_equal_patch.append(v)
                    else:
                        older_patch.append(v)
                    continue

            if rq_major_minor and any(str(gv).startswith(f"{rq_major_minor[0]}.{rq_major_minor[1]}") for gv in gversions):
                older_patch.append(v)
                continue

            fallback_matches.append(v)

        def _is_version_compatible(v: Dict, requested_game_version: Optional[str]) -> bool:
            if not requested_game_version:
                return True
            gversions = v.get('game_versions') or []
            gversions_l = [str(g).lower() for g in gversions]
            if str(requested_game_version).lower() in gversions_l:
                return True
            rq_nums = self._parse_numeric_ver(requested_game_version)
            if rq_nums:
                for gv in gversions:
                    parsed = self._parse_numeric_ver(str(gv))
                    if parsed and parsed == rq_nums:
                        return True
            return False

        if aggressive:
            loader_matches = [v for v in versions if loader_l in [l.lower() for l in v.get('loaders', [])] and _is_version_compatible(v, game_version)]
            if loader_matches:
                # Stable channel first, newest within it - see pick_modrinth_version.
                best = pick_modrinth_version(loader_matches)
                self._log(f"[Modrinth] Aggressive mode: selected {best.get('version_number')}")
                return best

        for bucket in (exact_matches, newer_or_equal_patch, older_patch, fallback_matches):
            compat = [v for v in bucket if _is_version_compatible(v, game_version)]
            if compat:
                return pick_modrinth_version(compat)
        return None

    def _download_url_to_path(self, url: str, dest_path: Path) -> bool:
        try:
            with _http_session.get(url, stream=True, timeout=30) as r:
                r.raise_for_status()
                with open(dest_path, 'wb') as f:
                    shutil.copyfileobj(r.raw, f)
            return True
        except Exception as e:
            self._log(f"[Modrinth] Download failed from {url}: {e}")
            return False

    def file_hash_sha1(self, path: Path) -> str:
        h = hashlib.sha1()
        with open(path, 'rb') as f:
            while True:
                chunk = f.read(8192)
                if not chunk:
                    break
                h.update(chunk)
        return h.hexdigest()

    def update_mod(self, local_jar: Path, loader: str, game_version: str, aggressive: bool = False, force: bool = False) -> Tuple[bool, str]:
        name = local_jar.name
        query = _normalize_query_from_filename(name)
        self._log(f"[Modrinth] Searching for '{query}' (from {name})")
        hits = self.search_projects(query)
        if not hits:
            info = self._extract_info_from_jar(local_jar)
            tried = []
            if info.get('id'):
                tried.append(info.get('id'))
            if info.get('name'):
                tried.append(info.get('name'))
            for q in tried:
                self._log(f"[Modrinth] No search hits for normalized filename, trying jar metadata query: {q}")
                hits = self.search_projects(q)
                if hits:
                    break
            if not hits:
                return False, "No project found"

        slug = None
        versions = None
        best = None
        for h in hits:
            try:
                hslug = h.get('slug') or h.get('project_id')
                if not hslug:
                    continue
                self._log(f"[Modrinth] Examining project hit: slug={hslug}, title={h.get('title')}")
                v = self.get_project_versions(hslug)
                if not v:
                    continue
                b = self.choose_best_version(v, loader, game_version, aggressive=aggressive)
                if b:
                    slug = hslug
                    versions = v
                    best = b
                    break
            except Exception as e:
                self._log(f"[Modrinth] Error examining hit {h}: {e}")

        if not best:
            info = self._extract_info_from_jar(local_jar)
            for q in (info.get('id'), info.get('name')):
                if not q:
                    continue
                if q.lower() in (h.get('slug', '').lower() for h in hits if h.get('slug')):
                    continue
                self._log(f"[Modrinth] Fallback: trying jar metadata query: {q}")
                hits2 = self.search_projects(q)
                for h in hits2:
                    try:
                        hslug = h.get('slug') or h.get('project_id')
                        if not hslug:
                            continue
                        v = self.get_project_versions(hslug)
                        b = self.choose_best_version(v, loader, game_version, aggressive=aggressive)
                        if b:
                            slug = hslug
                            versions = v
                            best = b
                            break
                    except Exception as e:
                        self._log(f"[Modrinth] Error in fallback examining hit {h}: {e}")
                if best:
                    break
        if not best:
            return False, "No compatible version found"
        try:
            if not self._version_is_compatible(best, game_version):
                self._log(f"[Modrinth] Selected version {best.get('version_number')} is not compatible with requested game version {game_version}; skipping")
                return False, "No compatible version found"
        except Exception:
            pass
        if not best:
            self._log(f"[Modrinth] No compatible version found for {name} (requested {game_version})")
            return False, "No compatible version found"
        files = best.get('files', [])
        if not files:
            return False, "No downloadable files"
        chosen = None
        for f in files:
            fname = f.get('filename', '').lower()
            if fname.endswith('.jar'):
                chosen = f
                break
        if not chosen:
            chosen = files[0]
        try:
            self._log(f"[Modrinth] Chosen version: {best.get('version_number')} | game_versions={best.get('game_versions')} | loaders={best.get('loaders')}")
            self._log(f"[Modrinth] Chosen file: filename={chosen.get('filename')} | url={chosen.get('url')}")
        except Exception:
            pass
        download_url = chosen.get('url')
        if not download_url:
            return False, "No file url"
        rq_nums = self._parse_numeric_ver(game_version) if game_version else None
        chosen_version_text = str(best.get('version_number') or chosen.get('filename') or '')
        chosen_nv = self._parse_numeric_ver(chosen_version_text)
        if rq_nums and chosen_nv and chosen_nv < rq_nums:
            self._log(f"[Modrinth] Warning: chosen version {chosen.get('filename') or best.get('version_number')} appears older than requested {game_version}")

        try:
            local_info = self._extract_info_from_jar(local_jar)
            local_ver_text = local_info.get('version')
            local_nv = self._parse_numeric_ver(local_ver_text) if local_ver_text else None
            if local_nv is None:
                local_nv = self._parse_numeric_ver(local_jar.name)
            self._log(f"[Modrinth] Local jar: {local_jar.name} mod_version={local_ver_text} parsed_nv={local_nv}; Chosen parsed_nv={chosen_nv}")
            if local_nv and chosen_nv and chosen_nv < local_nv:
                if not force:
                    self._log(f"[Modrinth] Remote version {chosen.get('filename') or best.get('version_number')} appears older than local {local_jar.name}; skipping to avoid downgrade")
                    return False, "Remote version older than local (skipped)"
                else:
                    self._log(f"[Modrinth] Force update requested; overriding downgrade protection for {local_jar.name}")
        except Exception:
            self._log(f"[Modrinth] Could not determine local numeric version for {local_jar.name}")
        tmpdir = Path(tempfile.mkdtemp(prefix='mr_updater_'))
        tmpfile = tmpdir / chosen.get('filename', 'download.jar')
        ok = self._download_url_to_path(download_url, tmpfile)
        if not ok or not tmpfile.exists():
            shutil.rmtree(tmpdir, ignore_errors=True)
            return False, "Download failed"
        try:
            local_hash = self.file_hash_sha1(local_jar)
            new_hash = self.file_hash_sha1(tmpfile)
            self._log(f"[Modrinth] local_hash={local_hash} new_hash={new_hash}")
            if local_hash == new_hash:
                shutil.rmtree(tmpdir, ignore_errors=True)
                self._log(f"[Modrinth] Skipping update for {local_jar.name}: already identical to remote file")
                return False, "Already up to date"
            bak = local_jar.with_suffix(local_jar.suffix + '.bak')
            try:
                shutil.move(str(local_jar), str(bak))
            except Exception:
                pass
            try:
                shutil.move(str(tmpfile), str(local_jar))
            except Exception as e:
                try:
                    if bak.exists():
                        shutil.move(str(bak), str(local_jar))
                except Exception:
                    pass
                return False, f"Failed to replace file: {e}"
            try:
                if bak.exists():
                    bak.unlink()
            except Exception:
                pass
            return True, f"Updated to {best.get('version_number', '')}"
        finally:
            shutil.rmtree(tmpdir, ignore_errors=True)


        return False, 'update aborted'

    def check_mod(self, local_jar: Path, loader: str, game_version: str, aggressive: bool = False) -> Optional[Dict]:
        name = local_jar.name
        query = _normalize_query_from_filename(name)
        self._log(f"[Modrinth] Searching for '{query}' (from {name}) [check]")
        hits = self.search_projects(query)
        if not hits:
            info = self._extract_info_from_jar(local_jar)
            tried = []
            if info.get('id'):
                tried.append(info.get('id'))
            if info.get('name'):
                tried.append(info.get('name'))
            for q in tried:
                self._log(f"[Modrinth] No search hits for normalized filename, trying jar metadata query: {q} [check]")
                hits = self.search_projects(q)
                if hits:
                    break
            if not hits:
                return None

        for h in hits:
            try:
                hslug = h.get('slug') or h.get('project_id')
                if not hslug:
                    continue
                self._log(f"[Modrinth] Examining project hit: slug={hslug}, title={h.get('title')} [check]")
                versions = self.get_project_versions(hslug)
                if not versions:
                    continue
                best = self.choose_best_version(versions, loader, game_version, aggressive=aggressive)
                if not best:
                    continue
                files = best.get('files', [])
                if not files:
                    continue
                chosen = None
                for f in files:
                    if f.get('filename', '').lower().endswith('.jar'):
                        chosen = f
                        break
                if not chosen:
                    chosen = files[0]
                proj = self.get_project_info(hslug)
                return {
                    'slug': hslug,
                    'project_title': proj.get('title') or h.get('title') or hslug,
                    'project_icon_url': proj.get('icon_url') or proj.get('icon') or proj.get('icon_url'),
                    'version_number': best.get('version_number'),
                    'chosen_filename': chosen.get('filename'),
                    'chosen_url': chosen.get('url'),
                    'game_versions': best.get('game_versions', []),
                    'loaders': best.get('loaders', []),
                }
            except Exception as e:
                self._log(f"[Modrinth] Error checking hit {h}: {e}")
                continue
        return None

    def scan_dir(self, directory: Path, loader: str, game_version: str, exts=('.jar',),
                 progress: Optional[Callable[[int, int, str], None]] = None, stop_event: Optional[object] = None) -> Dict:
        files = [p for p in Path(directory).iterdir() if p.is_file() and p.suffix.lower() in exts]
        files.sort(key=lambda p: p.name.lower())
        total = len(files)
        by_hash = {}
        results = {}
        for idx, f in enumerate(files, 1):
            if stop_event is not None and getattr(stop_event, 'is_set', lambda: False)():
                return {'candidates': {}, 'downgrades': {}, 'results': {'__aborted__': 'Aborted'}}
            try:
                by_hash[_sha1_file(f)] = f
            except Exception as e:
                results[f.name] = f"Error: {e}"
            if progress and (idx % 10 == 0 or idx == total):
                progress(idx, total, f"Hashing {idx}/{total}")
        hashes = list(by_hash.keys())
        self._log(f"[Modrinth] Looking up {len(hashes)} files by hash")
        known = _modrinth_lookup_hashes(hashes)
        loaders = [loader.lower()] if loader and loader.lower() not in ('vanilla', 'none') else []
        if progress:
            progress(total, total, "Checking for updates on Modrinth...")
        updates = _modrinth_update_hashes(list(known.keys()), loaders, [game_version]) if known else {}
        project_ids = sorted({v.get('project_id') for v in updates.values() if v.get('project_id')})
        projects = {}
        for i in range(0, len(project_ids), 100):
            chunk = project_ids[i:i + 100]
            try:
                r = _http_session.get(f"{MODRINTH_API_BASE}/projects", params={"ids": json.dumps(chunk)}, timeout=20)
                r.raise_for_status()
                for pr in r.json():
                    projects[pr.get('id')] = pr
            except Exception as e:
                self._log(f"[Modrinth] project batch lookup failed: {e}")
        candidates, downgrades = {}, {}
        for sha1, f in by_hash.items():
            current = known.get(sha1)
            if not current:
                results[f.name] = 'No project found'
                continue
            latest = updates.get(sha1)
            if not latest:
                results[f.name] = 'No compatible version found'
                continue
            if latest.get('id') == current.get('id') or (latest.get('version_number') and latest.get('version_number') == current.get('version_number')):
                results[f.name] = 'Already up to date'
                continue
            chosen = None
            for vf in latest.get('files', []):
                if vf.get('primary'):
                    chosen = vf
                    break
            if not chosen and latest.get('files'):
                chosen = latest['files'][0]
            if not chosen:
                results[f.name] = 'No downloadable files'
                continue
            proj = projects.get(latest.get('project_id'), {})
            info = {
                'jar': f,
                'slug': proj.get('slug') or latest.get('project_id'),
                'project_title': proj.get('title') or current.get('name') or f.name,
                'project_icon_url': proj.get('icon_url'),
                'version_number': latest.get('version_number'),
                'current_version_number': current.get('version_number'),
                'chosen_filename': chosen.get('filename'),
                'chosen_url': chosen.get('url'),
                'chosen_sha1': (chosen.get('hashes') or {}).get('sha1'),
                'game_versions': latest.get('game_versions', []),
                'loaders': latest.get('loaders', []),
            }
            try:
                cur_date = current.get('date_published') or ''
                new_date = latest.get('date_published') or ''
                if cur_date and new_date and new_date < cur_date:
                    downgrades[f.name] = info
                    results[f.name] = 'Remote version older than local (skipped)'
                    continue
            except Exception:
                pass
            candidates[f.name] = info
        return {'candidates': candidates, 'downgrades': downgrades, 'results': results}

    def apply_candidate(self, info: Optional[Dict], force: bool = False) -> Tuple[bool, str]:
        if not info:
            return False, "No candidate"
        local = Path(info['jar'])
        url = info.get('chosen_url')
        if not url:
            return False, "No file url"
        filename = info.get('chosen_filename') or local.name
        target = local.parent / filename
        tmpdir = Path(tempfile.mkdtemp(prefix='mr_updater_'))
        tmpfile = tmpdir / filename
        try:
            if not self._download_url_to_path(url, tmpfile) or not tmpfile.exists():
                return False, "Download failed"
            want = info.get('chosen_sha1')
            if want and self.file_hash_sha1(tmpfile) != want:
                return False, "Downloaded file hash mismatch"
            if local.exists() and self.file_hash_sha1(local) == self.file_hash_sha1(tmpfile):
                return False, "Already up to date"
            disabled = local.suffix.lower() == '.disabled'
            if disabled:
                target = target.with_name(target.name + '.disabled')
            bak = local.with_suffix(local.suffix + '.bak')
            try:
                shutil.move(str(local), str(bak))
            except Exception:
                pass
            try:
                shutil.move(str(tmpfile), str(target))
            except Exception as e:
                if bak.exists():
                    shutil.move(str(bak), str(local))
                return False, f"Failed to replace file: {e}"
            try:
                if bak.exists():
                    bak.unlink()
            except Exception:
                pass
            return True, f"Updated to {info.get('version_number', '')}"
        finally:
            shutil.rmtree(tmpdir, ignore_errors=True)

    def update_mods_in_dir(self, mods_dir: Path, loader: str, game_version: str,
                           progress: Optional[Callable[[int, int, str], None]] = None,
                           stop_event: Optional[object] = None,
                           aggressive: bool = False,
                           force: bool = False) -> Dict[str, str]:
        results: Dict[str, str] = {}
        jars = [p for p in mods_dir.iterdir() if p.suffix.lower() == '.jar' and p.is_file()]
        total = len(jars)
        for idx, jar in enumerate(sorted(jars), start=1):
            if stop_event is not None:
                try:
                    if getattr(stop_event, 'is_set', lambda: False)():
                        results['__aborted__'] = 'Aborted'
                        break
                except Exception:
                    pass
            try:
                if progress:
                    progress(idx - 1, total, f"Checking {jar.name}...")
                updated, msg = self.update_mod(jar, loader, game_version, aggressive=aggressive, force=force)
                results[jar.name] = msg if not updated else f"Updated: {msg}"
            except Exception as e:
                results[jar.name] = f"Error: {e}"
            if progress:
                progress(idx, total, f"Processed {idx}/{total}")
        return results

class InstanceManager:
    def __init__(self):
        self.instances: Dict[str, MinecraftInstance] = {}
        self.selected_instance_id: Optional[str] = None
        self.callbacks = []
        self.load_instances()
    @staticmethod
    def get_instances_dir() -> Path:
        instances_dir = Path.home() / ".config" / "oranglauncher" / "instances"
        instances_dir.mkdir(parents=True, exist_ok=True)
        return instances_dir
    @staticmethod
    def get_config_file() -> Path:
        config_dir = Path.home() / ".config" / "oranglauncher"
        config_dir.mkdir(parents=True, exist_ok=True)
        return config_dir / "instances.json"
    def load_instances(self):
        config_file = self.get_config_file()
        if not config_file.exists():
            return
        try:
            with open(config_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
            if isinstance(data, list):
                instances_list = data
            else:
                instances_list = data.get("instances", [])
            self.instances = {}
            for instance_data in instances_list:
                instance = MinecraftInstance.from_dict(instance_data)
                self.instances[instance.instance_id] = instance
            self.selected_instance_id = data.get("selected_instance_id") if isinstance(data, dict) else None
        except Exception as e:
            print(f"Error loading instances: {e}")
    def save_instances(self):
        config_file = self.get_config_file()
        data = {
            "selected_instance_id": self.selected_instance_id,
            "instances": [instance.to_dict() for instance in self.instances.values()]
        }
        try:
            with open(config_file, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2, ensure_ascii=False)
        except Exception as e:
            print(f"Error saving instances: {e}")
    def create_default_instance(self):
        try:
            versions_data = minecraft_launcher_lib.utils.get_version_list()
            latest_release = None
            if isinstance(versions_data, dict) and "latest" in versions_data:
                latest_release = versions_data["latest"]["release"]
            elif isinstance(versions_data, list):
                for version in versions_data:
                    if version.get("type") == "release":
                        latest_release = version.get("id")
                        break
            if not latest_release:
                try:
                    resp = _http_session.get("https://launchermeta.mojang.com/mc/game/version_manifest.json", timeout=5)
                    if resp.status_code == 200:
                        latest_release = resp.json().get("latest", {}).get("release")
                except Exception:
                    pass
            if not latest_release:
                latest_release = "26.1.2"
            default_instance = MinecraftInstance(
                name="Latest Release",
                version=latest_release,
                mod_loader="vanilla",
                ram="4G"
            )
            self.add_instance(default_instance)
            self.selected_instance_id = default_instance.instance_id
        except Exception as e:
            print(f"Error creating default instance: {e}")
            default_instance = MinecraftInstance(
                name="Latest Release",
                version="26.1.2",
                mod_loader="vanilla",
                ram="4G"
            )
            self.add_instance(default_instance)
            self.selected_instance_id = default_instance.instance_id
    def add_instance(self, instance: MinecraftInstance) -> bool:
        try:
            instance.create_directories()
            self.instances[instance.instance_id] = instance
            self.save_instances()
            return True
        except Exception as e:
            print(f"Error adding instance: {e}")
            return False
    def remove_instance(self, instance_id: str) -> bool:
        if instance_id not in self.instances:
            return False
        try:
            instance = self.instances[instance_id]
            if instance.base_path.exists():
                shutil.rmtree(instance.base_path)
            del self.instances[instance_id]
            if self.selected_instance_id == instance_id:
                if self.instances:
                    self.selected_instance_id = next(iter(self.instances.keys()))
                else:
                    self.selected_instance_id = None
            self.save_instances()
            return True
        except Exception as e:
            print(f"Error removing instance: {e}")
            return False
    def get_instance(self, instance_id: str) -> Optional[MinecraftInstance]:
        return self.instances.get(instance_id)
    def get_selected_instance(self) -> Optional[MinecraftInstance]:
        if self.selected_instance_id:
            return self.instances.get(self.selected_instance_id)
        return None
    def set_selected_instance(self, instance_id: str) -> bool:
        if instance_id in self.instances:
            self.selected_instance_id = instance_id
            self.save_instances()
            self._notify_callbacks()
            return True
        return False
    def get_instance_names(self) -> List[str]:
        return [instance.name for instance in self.instances.values()]
    def get_instance_by_name(self, name: str) -> Optional[MinecraftInstance]:
        for instance in self.instances.values():
            if instance.name == name:
                return instance
        return None
    def create_instance(self, name: str, version: str, mod_loader: str = "vanilla",
                       ram: str = "4G", java_args: str = None, loader_version: str = None) -> Optional[MinecraftInstance]:
        if self.get_instance_by_name(name):
            raise ValueError(f"Instance with name '{name}' already exists")
        instance = MinecraftInstance(
            name=name,
            version=version,
            mod_loader=mod_loader,
            ram=ram,
            java_args=java_args,
            loader_version=loader_version
        )
        if self.add_instance(instance):
            return instance
        return None
    def register_callback(self, callback):
        if callback not in self.callbacks:
            self.callbacks.append(callback)
    def unregister_callback(self, callback):
        if callback in self.callbacks:
            self.callbacks.remove(callback)
    def _notify_callbacks(self):
        for callback in self.callbacks:
            try:
                callback()
            except Exception as e:
                print(f"Error in instance callback: {e}")
_instance_manager = None
def get_instance_manager() -> InstanceManager:
    global _instance_manager
    if _instance_manager is None:
        _instance_manager = InstanceManager()
    return _instance_manager

class MinecraftVersion:
    def __init__(self, version_id: str, version_type: str, release_time: str, url: Optional[str] = None):
        self.id = version_id
        self.type = version_type
        self.release_time = datetime.fromisoformat(release_time.replace('Z', '+00:00'))
        self.url = url
    def __str__(self):
        return f"{self.id} ({self.type})"
    def __repr__(self):
        return f"MinecraftVersion(id='{self.id}', type='{self.type}')"
_MODDED_VERSION_MARKERS = ("forge", "fabric", "quilt", "neo", "optifine", "loader")
def _is_modded_version_id(version_id: str) -> bool:
    vid = (version_id or "").lower()
    return any(marker in vid for marker in _MODDED_VERSION_MARKERS)

class MojangVersionManager:
    def __init__(self):
        self.cache_path = Path.home() / ".minecraft_versions_cache.json"
        self.versions = []
        self.last_updated = None
        self.cache_duration = timedelta(hours=6)
        self.is_fetching = False
        self.fetch_callbacks = []
    def load_cache(self):
        try:
            if self.cache_path.exists():
                with open(self.cache_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                self.last_updated = datetime.fromisoformat(data.get('last_updated', '2000-01-01T00:00:00'))
                versions_data = data.get('versions', [])
                self.versions = []
                for v_data in versions_data:
                    if isinstance(v_data, dict):
                        if _is_modded_version_id(v_data.get('id', '')):
                            continue
                        self.versions.append(MinecraftVersion(
                            version_id=v_data['id'],
                            version_type=v_data['type'],
                            release_time=v_data['release_time'],
                            url=v_data.get('url')
                        ))
                return True
        except Exception as e:
            print(f"Failed to load version cache: {e}")
        return False
    def save_cache(self):
        try:
            data = {
                'last_updated': self.last_updated.isoformat() if self.last_updated else datetime.now().isoformat(),
                'versions': [
                    {
                        'id': v.id,
                        'type': v.type,
                        'release_time': v.release_time.isoformat(),
                        'url': v.url
                    }
                    for v in self.versions
                ]
            }
            with open(self.cache_path, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
        except Exception as e:
            print(f"Failed to save version cache: {e}")
    def is_cache_valid(self) -> bool:
        if not self.last_updated:
            return False
        return datetime.now() - self.last_updated < self.cache_duration
    def fetch_versions_async(self, callback=None):
        if self.is_fetching:
            if callback:
                self.fetch_callbacks.append(callback)
            return
        if callback:
            self.fetch_callbacks.append(callback)
        def fetch_worker():
            success, error = self._fetch_versions_sync()
            for cb in self.fetch_callbacks:
                try:
                    cb(success, error)
                except Exception as e:
                    print(f"Error in fetch callback: {e}")
            self.fetch_callbacks.clear()
            self.is_fetching = False
        thread = threading.Thread(target=fetch_worker, daemon=True)
        thread.start()
    def _fetch_versions_sync(self):
        try:
            # versions
            response = _http_session.get('https://launchermeta.mojang.com/mc/game/version_manifest.json', timeout=10)
            response.raise_for_status()
            data = response.json()
            versions_data = data.get('versions', [])
            self.versions = []
            for v_data in versions_data:
                if _is_modded_version_id(v_data.get('id', '')):
                    continue
                self.versions.append(MinecraftVersion(
                    version_id=v_data['id'],
                    version_type=v_data['type'],
                    release_time=v_data['releaseTime'],
                    url=v_data.get('url')
                ))
            self.last_updated = datetime.now()
            self.save_cache()
            return True, None
        except requests.RequestException as e:
            print(f"Network error fetching versions: {e}")
            return False, str(e)
        except Exception as e:
            print(f"Error fetching versions: {e}")
            return False, str(e)
    def get_versions(self, force_refresh=False) -> List[MinecraftVersion]:
        if not self.versions:
            self.load_cache()
        if force_refresh or not self.is_cache_valid():
            if not self.is_fetching:
                self.fetch_versions_async()
        return self.versions
    def filter_versions(self, 
                       version_types: Optional[List[str]] = None,
                       search_query: Optional[str] = None,
                       limit: Optional[int] = None,
                       after_date: Optional[datetime] = None,
                       before_date: Optional[datetime] = None) -> List[MinecraftVersion]:
        versions = self.get_versions()
        filtered = versions
        if version_types:
            filtered = [v for v in filtered if v.type in version_types]
        if search_query:
            search_lower = search_query.lower()
            filtered = [v for v in filtered if search_lower in v.id.lower()]
        if after_date:
            filtered = [v for v in filtered if v.release_time >= after_date]
        if before_date:
            filtered = [v for v in filtered if v.release_time <= before_date]
        if limit:
            filtered = filtered[:limit]
        return filtered
    def get_latest_release(self) -> Optional[MinecraftVersion]:
        releases = self.filter_versions(version_types=['release'], limit=1)
        return releases[0] if releases else None
    def get_latest_snapshot(self) -> Optional[MinecraftVersion]:
        snapshots = self.filter_versions(version_types=['snapshot'], limit=1)
        return snapshots[0] if snapshots else None

class GameProfile:
    def __init__(self, profile_id=None, name="New Profile", version="26.1.2", 
                 mod_loader="None", game_dir=None, java_args=None, 
                 resolution_width=None, resolution_height=None, 
                 ram="2G", icon="default", created=None, last_used=None,
                 mods_list=None):
        self.id = profile_id or str(uuid_module.uuid4())
        self.name = name
        self.version = version
        self.mod_loader = mod_loader
        self.game_dir = game_dir
        self.java_args = java_args or []
        self.resolution_width = resolution_width
        self.resolution_height = resolution_height
        self.ram = ram
        self.icon = icon
        self.created = created or datetime.now().isoformat()
        self.last_used = last_used
        self.mods_list = mods_list or []
    def to_dict(self):
        return {
            "id": self.id,
            "name": self.name,
            "version": self.version,
            "mod_loader": self.mod_loader,
            "game_dir": self.game_dir,
            "java_args": self.java_args,
            "resolution_width": self.resolution_width,
            "resolution_height": self.resolution_height,
            "ram": self.ram,
            "icon": self.icon,
            "created": self.created,
            "last_used": self.last_used,
            "mods_list": self.mods_list
        }
    @classmethod
    def from_dict(cls, data):
        return cls(
            profile_id=data.get('id'),
            name=data.get('name', 'New Profile'),
            version=data.get('version', '26.1'),
            mod_loader=data.get('mod_loader', 'None'),
            game_dir=data.get('game_dir'),
            java_args=data.get('java_args', []),
            resolution_width=data.get('resolution_width'),
            resolution_height=data.get('resolution_height'),
            ram=data.get('ram', '2G'),
            icon=data.get('icon', 'default'),
            created=data.get('created'),
            last_used=data.get('last_used'),
            mods_list=data.get('mods_list', [])
        )
    def mark_used(self):
        self.last_used = datetime.now().isoformat()
    def get_mods_directory(self):
        base_dir = Path(self.game_dir) if self.game_dir else Path.home() / ".minecraft"
        return base_dir / "profiles" / self.id / "mods"
    def ensure_mods_directory(self):
        mods_dir = self.get_mods_directory()
        mods_dir.mkdir(parents=True, exist_ok=True)
        return mods_dir
class GameProfileManager:
    def __init__(self):
        self.config_path = Path.home() / ".minecraft_game_profiles.json"
        self.profiles = {}
        self.selected_profile_id = None
        self.version_manager = MojangVersionManager()
        self.mod_change_callbacks = []
        self.load_profiles()
    def load_profiles(self):
        try:
            if self.config_path.exists():
                with open(self.config_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                for profile_data in data.get('profiles', []):
                    if isinstance(profile_data, dict):
                        profile = GameProfile.from_dict(profile_data)
                        self.profiles[profile.id] = profile
                self.selected_profile_id = data.get('selected_profile_id')
        except Exception as e:
            print(f"Error loading game profiles: {e}")
            self.create_default_profile()
    def save_profiles(self):
        try:
            data = {
                'selected_profile_id': self.selected_profile_id,
                'profiles': [profile.to_dict() for profile in self.profiles.values()]
            }
            with open(self.config_path, 'w', encoding='utf-8') as f:
                json.dump(data, f, indent=2)
        except Exception as e:
            print(f"Error saving game profiles: {e}")
    def create_default_profile(self):
        latest_version = "26.1.2"
        try:
            latest = self.version_manager.get_latest_release()
            if latest:
                latest_version = latest.id
        except Exception as e:
            print(f"Could not get latest version: {e}")
        default_profile = GameProfile(
            name="Default",
            version=latest_version,
            mod_loader="None",
            ram="4G"
        )
        self.profiles[default_profile.id] = default_profile
        self.selected_profile_id = default_profile.id
        self.save_profiles()
    def create_profile(self, name=None, version="26.1.2", mod_loader="None"):
        if name is None:
            counter = 1
            while f"Profile {counter}" in [p.name for p in self.profiles.values()]:
                counter += 1
            name = f"Profile {counter}"
        for existing in self.profiles.values():
            if existing.name == name:
                raise ValueError(f"Profile with name '{name}' already exists")
        profile = GameProfile(
            name=name,
            version=version,
            mod_loader=mod_loader,
            ram="4G"
        )
        self.profiles[profile.id] = profile
        self.save_profiles()
        return profile
    def duplicate_profile(self, profile_id):
        if profile_id not in self.profiles:
            return None
        original = self.profiles[profile_id]
        base_name = original.name
        counter = 1
        while f"{base_name} (Copy {counter})" in [p.name for p in self.profiles.values()]:
            counter += 1
        new_name = f"{base_name} (Copy {counter})"
        new_profile = GameProfile(
            name=new_name,
            version=original.version,
            mod_loader=original.mod_loader,
            game_dir=original.game_dir,
            java_args=original.java_args.copy(),
            resolution_width=original.resolution_width,
            resolution_height=original.resolution_height,
            ram=original.ram,
            icon=original.icon,
            mods_list=original.mods_list.copy()
        )
        if original.mods_list:
            try:
                original_mods_dir = original.get_mods_directory()
                new_mods_dir = new_profile.ensure_mods_directory()
                if original_mods_dir.exists():
                    for mod_file in original_mods_dir.glob('*.jar'):
                        shutil.copy2(mod_file, new_mods_dir / mod_file.name)
            except Exception as e:
                print(f"Error copying mods during duplication: {e}")
        self.profiles[new_profile.id] = new_profile
        self.save_profiles()
        return new_profile
    def delete_profile(self, profile_id):
        if profile_id not in self.profiles:
            return False
        if len(self.profiles) == 1:
            raise ValueError("Cannot delete the last profile")
        profile = self.profiles[profile_id]
        try:
            mods_dir = profile.get_mods_directory()
            if mods_dir.exists():
                shutil.rmtree(mods_dir.parent)
        except Exception as e:
            print(f"Error deleting profile directory: {e}")
        del self.profiles[profile_id]
        if self.selected_profile_id == profile_id:
            self.selected_profile_id = next(iter(self.profiles.keys()))
        self.save_profiles()
        return True
    def get_profile(self, profile_id):
        return self.profiles.get(profile_id)
    def get_selected_profile(self):
        if self.selected_profile_id:
            return self.profiles.get(self.selected_profile_id)
        return None
    def set_selected_profile(self, profile_id):
        if profile_id in self.profiles:
            self.selected_profile_id = profile_id
            self.save_profiles()
            return True
        return False
    def get_profile_list(self):
        return sorted(
            self.profiles.values(),
            key=lambda p: (
                p.last_used is None,
                datetime.fromisoformat(p.last_used) if p.last_used else datetime.min
            ),
            reverse=True
        )
    def get_profile_names(self):
        return [p.name for p in self.get_profile_list()]
    def get_profile_by_name(self, name):
        for profile in self.profiles.values():
            if profile.name == name:
                return profile
        return None
    def update_profile(self, profile_id, **kwargs):
        if profile_id not in self.profiles:
            return False
        profile = self.profiles[profile_id]
        for key, value in kwargs.items():
            if hasattr(profile, key):
                setattr(profile, key, value)
        self.save_profiles()
        return True
    def add_mod_to_profile(self, profile_id, mod_file_path):
        if profile_id not in self.profiles:
            return False
        profile = self.profiles[profile_id]
        mods_dir = profile.ensure_mods_directory()
        try:
            mod_filename = Path(mod_file_path).name
            dest_path = mods_dir / mod_filename
            shutil.copy2(mod_file_path, dest_path)
            if mod_filename not in profile.mods_list:
                profile.mods_list.append(mod_filename)
                self.save_profiles()
            self._notify_mod_change()
            return True
        except Exception as e:
            print(f"Error adding mod: {e}")
            return False
    def remove_mod_from_profile(self, profile_id, mod_filename):
        if profile_id not in self.profiles:
            return False
        profile = self.profiles[profile_id]
        mods_dir = profile.get_mods_directory()
        try:
            mod_path = mods_dir / mod_filename
            if mod_path.exists():
                mod_path.unlink()
            if mod_filename in profile.mods_list:
                profile.mods_list.remove(mod_filename)
                self.save_profiles()
            self._notify_mod_change()
            return True
        except Exception as e:
            print(f"Error removing mod: {e}")
            return False
    def get_profile_mods(self, profile_id):
        if profile_id not in self.profiles:
            return []
        profile = self.profiles[profile_id]
        mods_dir = profile.get_mods_directory()
        if not mods_dir.exists():
            return []
        actual_mods = []
        for mod_file in mods_dir.glob('*.jar'):
            actual_mods.append(mod_file.name)
        profile.mods_list = actual_mods
        self.save_profiles()
        return actual_mods
    def prepare_mods_for_launch(self, profile_id):
        if profile_id not in self.profiles:
            return True
        profile = self.profiles[profile_id]
        mods_dir = profile.get_mods_directory()
        if not mods_dir.exists():
            return True
        minecraft_mods_dir = Path.home() / ".minecraft" / "mods"
        minecraft_mods_dir.mkdir(parents=True, exist_ok=True)
        try:
            for mod_file in minecraft_mods_dir.glob('*.jar'):
                mod_file.unlink()
        except Exception as e:
            print(f"Error clearing Minecraft mods directory: {e}")
        try:
            for mod_file in mods_dir.glob('*.jar'):
                dest = minecraft_mods_dir / mod_file.name
                shutil.copy2(mod_file, dest)
        except Exception as e:
            print(f"Error copying mods: {e}")
            return False
        return True
    def register_mod_change_callback(self, callback):
        if callback not in self.mod_change_callbacks:
            self.mod_change_callbacks.append(callback)
    def unregister_mod_change_callback(self, callback):
        if callback in self.mod_change_callbacks:
            self.mod_change_callbacks.remove(callback)
    def _notify_mod_change(self):
        for callback in self.mod_change_callbacks:
            try:
                callback()
            except Exception as e:
                print(f"Error in mod change callback: {e}")
    def refresh_versions(self, callback=None):
        self.version_manager.fetch_versions_async(callback)
    def get_versions(self, force_refresh=False):
        return self.version_manager.get_versions(force_refresh)
    def filter_versions(self, **kwargs):
        return self.version_manager.filter_versions(**kwargs)
    def get_version_types(self):
        versions = self.get_versions()
        return list(set(v.type for v in versions))
    def is_version_valid(self, version_id):
        versions = self.get_versions()
        return any(v.id == version_id for v in versions)
_game_profile_manager = None
def get_game_profile_manager():
    global _game_profile_manager
    if _game_profile_manager is None:
        _game_profile_manager = GameProfileManager()
    return _game_profile_manager
def get_profiles():
    return get_game_profile_manager().get_profile_list()
def get_game_profile_names():
    return get_game_profile_manager().get_profile_names()
def get_selected_profile():
    return get_game_profile_manager().get_selected_profile()
def set_selected_profile(profile_name):
    manager = get_game_profile_manager()
    profile = manager.get_profile_by_name(profile_name)
    if profile:
        return manager.set_selected_profile(profile.id)
    return False
def create_profile(name=None, version="26.1.2", mod_loader="None"):
    return get_game_profile_manager().create_profile(name, version, mod_loader)
def delete_profile_by_name(name):
    manager = get_game_profile_manager()
    profile = manager.get_profile_by_name(name)
    if profile:
        return manager.delete_profile(profile.id)
    return False
def duplicate_profile_by_name(name):
    manager = get_game_profile_manager()
    profile = manager.get_profile_by_name(name)
    if profile:
        return manager.duplicate_profile(profile.id)
    return None
def mark_profile_used(profile_name):
    manager = get_game_profile_manager()
    profile = manager.get_profile_by_name(profile_name)
    if profile:
        profile.mark_used()
        manager.save_profiles()
def add_mod_to_current_profile(mod_file_path):
    manager = get_game_profile_manager()
    profile = manager.get_selected_profile()
    if profile:
        return manager.add_mod_to_profile(profile.id, mod_file_path)
    return False
def remove_mod_from_current_profile(mod_filename):
    manager = get_game_profile_manager()
    profile = manager.get_selected_profile()
    if profile:
        return manager.remove_mod_from_profile(profile.id, mod_filename)
    return False
def get_current_profile_mods():
    manager = get_game_profile_manager()
    profile = manager.get_selected_profile()
    if profile:
        return manager.get_profile_mods(profile.id)
    return []
def prepare_mods_for_launch():
    manager = get_game_profile_manager()
    profile = manager.get_selected_profile()
    if profile:
        return manager.prepare_mods_for_launch(profile.id)
    return True
def register_mod_change_callback(callback):
    get_game_profile_manager().register_mod_change_callback(callback)
def unregister_mod_change_callback(callback):
    get_game_profile_manager().unregister_mod_change_callback(callback)
JAVA_RUNTIMES_DIR = Path.home() / ".config" / "oranglauncher" / "java_runtimes"
def _mc_version_tuple(version_str: str):
    try:
        clean = version_str.strip().lstrip("v").split("-")[0]
        parts = clean.split(".")
        return tuple(int(p) for p in parts[:3])
    except Exception:
        return (0,)

def get_required_java_version(mc_version: str) -> int:
    v = _mc_version_tuple(mc_version)
    if v >= (26, 0):
        return 25
    if v >= (1, 20, 5):
        return 21
    if v >= (1, 17, 0):
        return 17
    return 8

def find_java_executable(java_major: int) -> Optional[str]:
    candidates = [
        f"/usr/lib/jvm/java-{java_major}-openjdk/bin/java",
        f"/usr/lib/jvm/java-{java_major}-openjdk-amd64/bin/java",
        f"/usr/lib/jvm/java-{java_major}-openjdk-arm64/bin/java",
        f"/usr/lib/jvm/temurin-{java_major}/bin/java",
        f"/usr/lib/jvm/java-{java_major}/bin/java",
        f"/usr/local/lib/jvm/java-{java_major}/bin/java",
        str(JAVA_RUNTIMES_DIR / f"java-{java_major}" / "bin" / "java"),
    ]
    candidates += [
        f"/Library/Java/JavaVirtualMachines/temurin-{java_major}.jdk/Contents/Home/bin/java",
        f"/Library/Java/JavaVirtualMachines/jdk-{java_major}.jdk/Contents/Home/bin/java",
    ]
    for p in candidates:
        if os.path.isfile(p) and os.access(p, os.X_OK):
            return p
    java_home = os.environ.get("JAVA_HOME")
    if java_home:
        java_bin = os.path.join(java_home, "bin", "java")
        if os.path.isfile(java_bin) and os.access(java_bin, os.X_OK):
            try:
                result = subprocess.run([java_bin, "-version"], capture_output=True, text=True, timeout=5)
                out = result.stderr or result.stdout
                m = re.search(r'version "(\d+)', out)
                if m:
                    detected = int(m.group(1))
                    if detected == 1:
                        m2 = re.search(r'version "1\.(\d+)', out)
                        detected = int(m2.group(1)) if m2 else 8
                    if detected == java_major:
                        return java_bin
            except Exception:
                pass
    return None

def download_java_runtime(java_major: int, progress_callback=None) -> Optional[str]:
    
    os_name = {"linux": "linux", "darwin": "mac", "win32": "windows"}.get(sys.platform, "linux")
    machine = platform.machine().lower()
    arch = "aarch64" if machine in ("aarch64", "arm64") else "x64"
    api_url = (
        f"https://api.adoptium.net/v3/assets/latest/{java_major}/hotspot"
        f"?os={os_name}&architecture={arch}&image_type=jre"
    )
    try:
        if progress_callback:
            progress_callback(0, f"Fetching Java {java_major} download info...")
        resp = _http_session.get(api_url, timeout=15)
        resp.raise_for_status()
        releases = resp.json()
        if not releases:
            return None
        binary = releases[0].get("binary", {})
        pkg = binary.get("package", {})
        download_url = pkg.get("link")
        filename = pkg.get("name", f"java-{java_major}.tar.gz")
        if not download_url:
            return None
        JAVA_RUNTIMES_DIR.mkdir(parents=True, exist_ok=True)
        dest_archive = JAVA_RUNTIMES_DIR / filename
        if progress_callback:
            progress_callback(5, f"Downloading Java {java_major} JRE...")
        with _http_session.get(download_url, stream=True, timeout=120) as r:
            r.raise_for_status()
            total = int(r.headers.get("content-length", 0))
            downloaded = 0
            with open(dest_archive, "wb") as f:
                for chunk in r.iter_content(chunk_size=65536):
                    if chunk:
                        f.write(chunk)
                        downloaded += len(chunk)
                        if total and progress_callback:
                            pct = 5 + int((downloaded / total) * 80)
                            progress_callback(pct, f"Downloading Java {java_major}... {pct}%")
        if progress_callback:
            progress_callback(85, f"Extracting Java {java_major}...")
        extract_dir = JAVA_RUNTIMES_DIR / f"java-{java_major}_extract"
        if extract_dir.exists():
            shutil.rmtree(extract_dir)
        extract_dir.mkdir(parents=True, exist_ok=True)
        if filename.endswith(".tar.gz") or filename.endswith(".tgz"):
            with tarfile.open(dest_archive, "r:gz") as tar:
                tar.extractall(extract_dir)
        elif filename.endswith(".zip"):
            with zipfile.ZipFile(dest_archive) as zf:
                zf.extractall(extract_dir)
        extracted_dirs = [d for d in extract_dir.iterdir() if d.is_dir()]
        if not extracted_dirs:
            return None
        jre_root = extracted_dirs[0]
        final_dir = JAVA_RUNTIMES_DIR / f"java-{java_major}"
        if final_dir.exists():
            shutil.rmtree(final_dir)
        shutil.move(str(jre_root), str(final_dir))
        shutil.rmtree(extract_dir, ignore_errors=True)
        dest_archive.unlink(missing_ok=True)
        java_bin = final_dir / "bin" / "java"
        if java_bin.exists():
            java_bin.chmod(java_bin.stat().st_mode | 0o111)
            if progress_callback:
                progress_callback(100, f"Java {java_major} installed.")
            return str(java_bin)
    except Exception as e:
        print(f"[Java] Failed to download Java {java_major}: {e}")
    return None

def resolve_java_for_instance(instance, mc_version: str, log_fn=None) -> str:
    if instance and getattr(instance, 'java_path', ''):
        p = instance.java_path.strip()
        if p and p != "Auto":
            if os.path.isdir(p):
                candidate = os.path.join(p, "bin", "java")
                if os.path.isfile(candidate) and os.access(candidate, os.X_OK):
                    return candidate
            elif os.path.isfile(p) and os.access(p, os.X_OK):
                return p
    required = get_required_java_version(mc_version)
    found = find_java_executable(required)
    if found:
        if log_fn:
            log_fn(f"[Java] Using Java {required} at: {found}")
        return found
    system_java = shutil.which("java") or "java"
    try:
        result = subprocess.run([system_java, "-version"], capture_output=True, text=True, timeout=5)
        out = result.stderr or result.stdout
        m = re.search(r'version "(\d+)', out)
        if m:
            detected = int(m.group(1))
            if detected == 1:
                m2 = re.search(r'version "1\.(\d+)', out)
                detected = int(m2.group(1)) if m2 else 8
            if required > 8 and detected >= required:
                if log_fn:
                    log_fn(f"[Java] System Java {detected} satisfies requirement >= {required}")
                return system_java
    except Exception:
        pass
    if log_fn:
        log_fn(f"[Java] Java {required} not found, downloading from Adoptium...")
    downloaded = download_java_runtime(required, progress_callback=log_fn and (lambda p, m: log_fn(m)))
    if downloaded:
        return downloaded
    if log_fn:
        log_fn(f"[Java] Download failed, falling back to system java")
    return system_java

# OptiFine (standalone) support
OPTIFINE_SITE = "https://optifine.net"
_OPTIFINE_UA = {"User-Agent": "Mozilla/5.0"}


def get_optifine_files_for(mc_version):
    # Returns OptiFine jar filenames available for mc_version
    if not mc_version:
        return []
    try:
        r = _http_session.get(f"{OPTIFINE_SITE}/downloads", timeout=20, headers=_OPTIFINE_UA)
        r.raise_for_status()
        pat = r'adloadx\?f=((?:preview_)?OptiFine_' + re.escape(mc_version) + r'_[^"&]+\.jar)'
        seen, out = set(), []
        for f in re.findall(pat, r.text):
            if f not in seen:
                seen.add(f)
                out.append(f)
        return out
    except Exception as e:
        print(f"[OptiFine] version list failed: {e}")
        return []


def download_optifine(filename, dest_path, log_fn=None):
    # Resolves the per-session download token and saves the OptiFine jar
    r = _http_session.get(f"{OPTIFINE_SITE}/adloadx?f={filename}", timeout=20, headers=_OPTIFINE_UA)
    r.raise_for_status()
    m = re.search(r'downloadx\?f=[^"&]+&(?:amp;)?x=([0-9a-f]+)', r.text)
    if not m:
        raise RuntimeError("could not resolve OptiFine download token")
    url = f"{OPTIFINE_SITE}/downloadx?f={filename}&x={m.group(1)}"
    with _http_session.get(url, stream=True, timeout=120, headers=_OPTIFINE_UA) as dl:
        dl.raise_for_status()
        with open(dest_path, "wb") as out:
            for chunk in dl.iter_content(chunk_size=1024 * 128):
                if chunk:
                    out.write(chunk)
    if log_fn:
        log_fn(f"[OptiFine] Downloaded {filename}")


def optifine_version_id(filename):
    base = filename[:-4] if filename.lower().endswith(".jar") else filename
    if base.startswith("preview_"):
        base = base[len("preview_"):]
    rest = base.split("_", 1)[1] if "_" in base else base   # drop leading 'OptiFine'
    mc, _, edition = rest.partition("_")
    return f"{mc}-OptiFine_{edition}"


def install_optifine(mc_version, minecraft_directory, java_exe, filename=None,
                     log_fn=None, progress_fn=None):
    log = log_fn or (lambda *_: None)
    if not filename:
        files = get_optifine_files_for(mc_version)
        if not files:
            raise RuntimeError(f"no OptiFine build found for Minecraft {mc_version}")
        filename = files[0]
    log(f"[OptiFine] Selected {filename}")
    if progress_fn:
        progress_fn(0, f"Installing Minecraft {mc_version}...")
    minecraft_launcher_lib.install.install_minecraft_version(mc_version, minecraft_directory)
    if progress_fn:
        progress_fn(50, "Downloading OptiFine...")
    tmp_jar = Path(tempfile.gettempdir()) / filename
    download_optifine(filename, str(tmp_jar), log_fn=log)
    base_home = str(Path(minecraft_directory).parent)
    lp = Path(minecraft_directory) / "launcher_profiles.json"
    if not lp.exists():
        lp.parent.mkdir(parents=True, exist_ok=True)
        lp.write_text(json.dumps({"profiles": {}, "settings": {}, "version": 3}), encoding="utf-8")
    if progress_fn:
        progress_fn(80, "Installing OptiFine...")
    log(f"[OptiFine] Running installer (home={base_home})")
    proc = subprocess.run(
        [java_exe, f"-Duser.home={base_home}", "-cp", str(tmp_jar), "optifine.Installer"],
        capture_output=True, text=True, timeout=240)
    if (proc.stdout or "").strip():
        log(f"[OptiFine] {proc.stdout.strip()[:400]}")
    if proc.returncode != 0:
        raise RuntimeError(f"OptiFine installer failed: {(proc.stderr or proc.stdout or '').strip()[:300]}")
    try:
        tmp_jar.unlink()
    except Exception:
        pass
    version_id = optifine_version_id(filename)
    vjson = Path(minecraft_directory) / "versions" / version_id / f"{version_id}.json"
    if not vjson.exists():
        raise RuntimeError(f"OptiFine version {version_id} was not created")
    if progress_fn:
        progress_fn(100, "OptiFine installed")
    log(f"[OptiFine] Installed {version_id}")
    return version_id


def get_available_versions(force_refresh=False):
    manager = get_game_profile_manager()
    versions = manager.get_versions(force_refresh)
    return [v.id for v in versions]
def get_available_versions_detailed(force_refresh=False):
    manager = get_game_profile_manager()
    return manager.get_versions(force_refresh)
def filter_versions(version_types=None, search_query=None, limit=None):
    manager = get_game_profile_manager()
    return manager.filter_versions(
        version_types=version_types,
        search_query=search_query,
        limit=limit
    )
def get_version_types():
    return get_game_profile_manager().get_version_types()
def refresh_versions(callback=None):
    get_game_profile_manager().refresh_versions(callback)
def get_latest_release():
    manager = get_game_profile_manager()
    latest = manager.version_manager.get_latest_release()
    return latest.id if latest else None
def get_latest_snapshot():
    manager = get_game_profile_manager()
    latest = manager.version_manager.get_latest_snapshot()
    return latest.id if latest else None
def is_version_valid(version_id):
    return get_game_profile_manager().is_version_valid(version_id)
def get_available_mod_loaders():
    return ["None", "Forge", "Fabric", "Quilt"]
def get_ram_options():
    return ["1G", "2G", "3G", "4G", "6G", "8G", "12G", "16G", "24G", "32G", "64G", "128G", "256G", "512G", "1T"]

# I WANT TO ROB your ram
def _get_system_ram_mb() -> int:
    try:
        with open('/proc/meminfo') as f:
            for line in f:
                if line.startswith('MemTotal:'):
                    return int(line.split()[1]) // 1024
    except Exception:
        pass
    return 8192

# prism I love you, but modrinth is a shit in 2026 on linux.
LWJGL_META_INDEX_URL = "https://meta.prismlauncher.org/v1/org.lwjgl3/index.json"
LWJGL_META_VERSION_URL = "https://meta.prismlauncher.org/v1/org.lwjgl3/{ver}.json"
LWJGL_FALLBACK_VERSIONS = ["3.4.3", "3.4.2", "3.4.1", "3.3.6", "3.3.3", "3.3.2", "3.3.1", "3.2.2", "3.2.1", "3.1.6", "3.1.2"]
LWJGL_MODE_STOCK = "stock"
LWJGL_MODE_AUTO = "auto"
LWJGL_MIN_GOOD = (3, 3, 3)


def _lwjgl_os_name():
    system = platform.system()
    if system == "Windows":
        return "windows"
    if system == "Darwin":
        return "osx"
    return "linux"

# I would love to have riscv processor
def _lwjgl_arch_suffix():
    machine = platform.machine().lower()
    if machine in ("aarch64", "arm64"):
        return "arm64"
    if machine.startswith("arm"):
        return "arm32"
    if machine in ("riscv64",):
        return "riscv64"
    if machine in ("i386", "i686", "x86"):
        return "x86"
    return ""


def _version_tuple(text):
    nums = re.findall(r"\d+", str(text or ""))
    return tuple(int(n) for n in nums[:3]) if nums else ()


class LwjglManager:
    def __init__(self):
        self.store_dir = Path.home() / ".config" / "oranglauncher" / "lwjgl"
        self.store_dir.mkdir(parents=True, exist_ok=True)
        self._index_cache = None
        self._meta_cache = {}
        self._lock = threading.Lock()

    def list_versions(self, force=False):
        with self._lock:
            if self._index_cache and not force:
                return list(self._index_cache)
        index_file = self.store_dir / "index.json"
        versions = []
        try:
            r = _http_session.get(LWJGL_META_INDEX_URL, timeout=10)
            r.raise_for_status()
            data = r.json()
            versions = [v.get("version") for v in data.get("versions", []) if v.get("version")]
            index_file.write_text(json.dumps(data), encoding="utf-8")
        except Exception as e:
            print(f"[LWJGL] index fetch failed: {e}")
            try:
                data = json.loads(index_file.read_text(encoding="utf-8"))
                versions = [v.get("version") for v in data.get("versions", []) if v.get("version")]
            except Exception:
                versions = []
        if not versions:
            versions = list(LWJGL_FALLBACK_VERSIONS)
        versions = sorted(set(versions), key=_version_tuple, reverse=True)
        with self._lock:
            self._index_cache = versions
        return list(versions)

    def get_meta(self, version):
        with self._lock:
            if version in self._meta_cache:
                return self._meta_cache[version]
        meta_file = self.store_dir / f"{version}.json"
        meta = None
        if meta_file.exists():
            try:
                meta = json.loads(meta_file.read_text(encoding="utf-8"))
            except Exception:
                meta = None
        if meta is None:
            r = _http_session.get(LWJGL_META_VERSION_URL.format(ver=version), timeout=15)
            r.raise_for_status()
            meta = r.json()
            meta_file.write_text(json.dumps(meta), encoding="utf-8")
        with self._lock:
            self._meta_cache[version] = meta
        return meta

    @staticmethod
    def _rules_allow(rules):
        if not rules:
            return True
        os_name = _lwjgl_os_name()
        arch = _lwjgl_arch_suffix()
        allowed = False
        for rule in rules:
            os_spec = rule.get("os") or {}
            name = os_spec.get("name")
            if name is None or name == os_name or (arch and name == f"{os_name}-{arch}"):
                allowed = rule.get("action") == "allow"
        return allowed

    def applicable_libraries(self, meta):
        os_name = _lwjgl_os_name()
        arch = _lwjgl_arch_suffix()
        libs = []
        for lib in meta.get("libraries", []):
            if not self._rules_allow(lib.get("rules")):
                continue
            name = lib.get("name", "")
            artifact = (lib.get("downloads") or {}).get("artifact")
            classifiers = (lib.get("downloads") or {}).get("classifiers") or {}
            natives = lib.get("natives") or {}
            if artifact and artifact.get("url"):
                is_native = "natives-" in name
                if is_native:
                    tail = name.split(":")[1]
                    marker = tail.split("natives-", 1)[1]
                    wanted = os_name if not arch else f"{os_name}-{arch}"
                    if os_name == "osx" and arch == "arm64":
                        wanted = "macos-arm64"
                    elif os_name == "osx":
                        wanted = "macos"
                    if marker != wanted:
                        continue
                libs.append((name, artifact["url"], artifact.get("sha1"), is_native))
            if natives and classifiers:
                key = natives.get(os_name)
                if key and key in classifiers:
                    cls = classifiers[key]
                    if cls.get("url"):
                        libs.append((name + ":" + key, cls["url"], cls.get("sha1"), True))
        return libs

    @staticmethod
    def _sha1(path):
        h = hashlib.sha1()
        with open(path, "rb") as f:
            for chunk in iter(lambda: f.read(1 << 16), b""):
                h.update(chunk)
        return h.hexdigest()

    def ensure_version(self, version, log_fn=None, progress_fn=None):
        log = log_fn or (lambda m: None)
        meta = self.get_meta(version)
        libs = self.applicable_libraries(meta)
        dest_dir = self.store_dir / version
        dest_dir.mkdir(parents=True, exist_ok=True)
        jars, natives_jars = [], []
        total = len(libs)
        for idx, (name, url, sha1, is_native) in enumerate(libs, 1):
            filename = url.rsplit("/", 1)[-1]
            target = dest_dir / filename
            ok = target.exists() and (not sha1 or self._sha1(target) == sha1)
            if not ok:
                log(f"[LWJGL] Downloading {filename}")
                for attempt in range(3):
                    try:
                        r = _http_session.get(url, timeout=60, stream=True)
                        r.raise_for_status()
                        tmp = target.with_suffix(target.suffix + ".part")
                        with open(tmp, "wb") as f:
                            for chunk in r.iter_content(1 << 16):
                                if chunk:
                                    f.write(chunk)
                        if sha1 and self._sha1(tmp) != sha1:
                            raise IOError("sha1 mismatch")
                        tmp.replace(target)
                        ok = True
                        break
                    except Exception as e:
                        log(f"[LWJGL] retry {attempt + 1} for {filename}: {e}")
                if not ok:
                    raise RuntimeError(f"Could not download LWJGL library {filename}")
            if progress_fn:
                try:
                    progress_fn(idx, total, filename)
                except Exception:
                    pass
            (natives_jars if is_native else jars).append(str(target))
        return {"version": version, "jars": jars, "natives_jars": natives_jars}

    @staticmethod
    def extract_natives(natives_jars, dest_dir):
        dest = Path(dest_dir)
        dest.mkdir(parents=True, exist_ok=True)
        wanted = (".so", ".dll", ".dylib", ".jnilib")
        for jar in natives_jars:
            try:
                with zipfile.ZipFile(jar) as z:
                    for member in z.namelist():
                        if member.endswith("/") or member.startswith("META-INF/"):
                            continue
                        if not member.lower().endswith(wanted):
                            continue
                        out = dest / os.path.basename(member)
                        if out.exists() and out.stat().st_size == z.getinfo(member).file_size:
                            continue
                        with z.open(member) as src, open(out, "wb") as dst:
                            shutil.copyfileobj(src, dst)
            except Exception as e:
                print(f"[LWJGL] natives extract failed for {jar}: {e}")
        return str(dest)

    @staticmethod
    def stock_version(minecraft_directory, version_id):
        seen = set()
        current = version_id
        while current and current not in seen:
            seen.add(current)
            json_path = Path(minecraft_directory) / "versions" / current / f"{current}.json"
            if not json_path.exists():
                break
            try:
                data = json.loads(json_path.read_text(encoding="utf-8"))
            except Exception:
                break
            for lib in data.get("libraries", []):
                name = lib.get("name", "")
                if name.startswith("org.lwjgl:lwjgl:"):
                    return name.split(":")[2], 3
                if name.startswith("org.lwjgl.lwjgl:lwjgl:"):
                    return name.split(":")[2], 2
            current = data.get("inheritsFrom")
        return None, 0

    def choose_auto(self, stock_version, major):
        if major != 3 or not stock_version:
            return None
        if _lwjgl_os_name() == "windows":
            return None
        if _version_tuple(stock_version) >= LWJGL_MIN_GOOD:
            return None
        candidates = [v for v in self.list_versions() if _version_tuple(v)[:2] == (3, 3)]
        return candidates[0] if candidates else None

    @staticmethod
    def rewrite_command(command, jars, natives_dir):
        out = []
        for arg in command:
            if arg.startswith("-Djava.library.path="):
                arg = "-Djava.library.path=" + natives_dir
            elif arg.startswith("-Dorg.lwjgl.system.SharedLibraryExtractPath="):
                arg = "-Dorg.lwjgl.system.SharedLibraryExtractPath=" + natives_dir
            out.append(arg)
        try:
            cp_index = out.index("-cp")
        except ValueError:
            cp_index = -1
        if cp_index >= 0 and cp_index + 1 < len(out):
            entries = out[cp_index + 1].split(os.pathsep)
            entries = [e for e in entries if e and "/org/lwjgl/" not in e.replace("\\", "/")]
            out[cp_index + 1] = os.pathsep.join(entries + list(jars))
        out.insert(1, "-Dorg.lwjgl.librarypath=" + natives_dir)
        return out


_lwjgl_manager = None


def get_lwjgl_manager():
    global _lwjgl_manager
    if _lwjgl_manager is None:
        _lwjgl_manager = LwjglManager()
    return _lwjgl_manager


def _find_shared_library(names):
    system = platform.system()
    candidates = []
    # it works on linux but idk if it works on macos
    if system == "Linux":
        dirs = ["/usr/lib", "/usr/lib64", "/usr/lib/x86_64-linux-gnu", "/usr/lib/aarch64-linux-gnu",
                "/usr/local/lib", "/usr/lib/i386-linux-gnu", "/lib", "/lib64", "/run/current-system/sw/lib"]
        for d in dirs:
            for n in names:
                candidates.append(os.path.join(d, n))
        try:
            out = subprocess.run(["/sbin/ldconfig", "-p"], capture_output=True, text=True, timeout=5).stdout
            for line in out.splitlines():
                for n in names:
                    if n in line and "=>" in line:
                        candidates.append(line.split("=>", 1)[1].strip())
        except Exception:
            pass
    elif system == "Darwin":
        for d in ["/opt/homebrew/lib", "/usr/local/lib", "/opt/local/lib"]:
            for n in names:
                candidates.append(os.path.join(d, n))
    for c in candidates:
        if c and os.path.isfile(c):
            return c
    return ""


def detect_system_glfw():
    if platform.system() == "Linux":
        return _find_shared_library(["libglfw.so.3", "libglfw.so"])
    # tf is darwin doing with their library names
    if platform.system() == "Darwin":
        return _find_shared_library(["libglfw.3.dylib", "libglfw.dylib"])
    return ""


def detect_system_openal():
    if platform.system() == "Linux":
        return _find_shared_library(["libopenal.so.1", "libopenal.so"])
    if platform.system() == "Darwin":
        return _find_shared_library(["libopenal.1.dylib", "libopenal.dylib"])
    return ""


def _instance_or_global(instance, key, default=None):
    if instance is not None:
        value = instance.opt(key)
        if value not in (None, "", "inherit"):
            return value
    return _adv_get(key, default)


def resolve_lwjgl_override(instance, minecraft_directory, version_id, log_fn=None, progress_fn=None):
    log = log_fn or (lambda m: None)
    mgr = get_lwjgl_manager()
    custom_dir = instance.opt("custom_lwjgl_dir") if instance is not None else None
    if custom_dir and Path(custom_dir).is_dir():
        jars = sorted(str(p) for p in Path(custom_dir).glob("*.jar"))
        natives_jars = [j for j in jars if "natives" in os.path.basename(j)]
        natives_dir = Path(minecraft_directory) / "versions" / version_id / "natives-custom"
        LwjglManager.extract_natives(natives_jars, natives_dir)
        for lib in Path(custom_dir).iterdir():
            if lib.suffix.lower() in (".so", ".dll", ".dylib", ".jnilib"):
                try:
                    shutil.copy2(lib, natives_dir / lib.name)
                except Exception:
                    pass
        log(f"[LWJGL] Using custom LWJGL folder: {custom_dir}")
        return {"jars": jars, "natives_dir": str(natives_dir), "version": "custom"}
    mode = _instance_or_global(instance, "lwjgl_mode", LWJGL_MODE_AUTO)
    if mode == LWJGL_MODE_STOCK:
        return None
    stock, major = mgr.stock_version(minecraft_directory, version_id)
    if mode == LWJGL_MODE_AUTO:
        target = mgr.choose_auto(stock, major)
        if not target:
            return None
    else:
        target = mode
        if major == 2:
            log("[LWJGL] Stock LWJGL 2 version detected, override not applied")
            return None
    if stock == target:
        return None
    log(f"[LWJGL] Overriding LWJGL {stock or '?'} -> {target}")
    info = mgr.ensure_version(target, log_fn=log, progress_fn=progress_fn)
    natives_dir = Path(minecraft_directory) / "versions" / version_id / f"natives-lwjgl-{target}"
    LwjglManager.extract_natives(info["natives_jars"], natives_dir)
    return {"jars": info["jars"] + info["natives_jars"], "natives_dir": str(natives_dir), "version": target}


def native_library_jvm_args(instance):
    args = []
    if _instance_or_global(instance, "use_system_glfw", False):
        path = _instance_or_global(instance, "glfw_path", "") or detect_system_glfw()
        if path and os.path.isfile(path):
            args.append("-Dorg.lwjgl.glfw.libname=" + os.path.abspath(path))
    if _instance_or_global(instance, "use_system_openal", False):
        path = _instance_or_global(instance, "openal_path", "") or detect_system_openal()
        if path and os.path.isfile(path):
            args.append("-Dorg.lwjgl.openal.libname=" + os.path.abspath(path))
    return args


def _detect_cursor_env():
    theme, size = "", ""
    try:
        kcm = Path.home() / ".config" / "kcminputrc"
        if kcm.exists():
            section = ""
            for line in kcm.read_text(encoding="utf-8", errors="ignore").splitlines():
                line = line.strip()
                if line.startswith("[") and line.endswith("]"):
                    section = line[1:-1]
                elif section == "Mouse" and "=" in line:
                    k, _, v = line.partition("=")
                    if k.strip() == "cursorTheme":
                        theme = v.strip()
                    elif k.strip() == "cursorSize":
                        size = v.strip()
    except Exception:
        pass
    if not theme and shutil.which("gsettings"):
        try:
            out = subprocess.run(["gsettings", "get", "org.gnome.desktop.interface", "cursor-theme"],
                                 capture_output=True, text=True, timeout=3).stdout.strip().strip("'")
            if out:
                theme = out
            out = subprocess.run(["gsettings", "get", "org.gnome.desktop.interface", "cursor-size"],
                                 capture_output=True, text=True, timeout=3).stdout.strip()
            if out.isdigit():
                size = out
        except Exception:
            pass
    if not theme:
        try:
            idx = Path.home() / ".icons" / "default" / "index.theme"
            if not idx.exists():
                idx = Path.home() / ".local" / "share" / "icons" / "default" / "index.theme"
            if idx.exists():
                for line in idx.read_text(encoding="utf-8", errors="ignore").splitlines():
                    if line.strip().startswith("Inherits="):
                        theme = line.split("=", 1)[1].strip()
                        break
        except Exception:
            pass
    return theme, size

# cmon use wayland already, it's 2026/27 not 2000
def _is_wayland_session():
    return bool(os.environ.get("WAYLAND_DISPLAY")) or os.environ.get("XDG_SESSION_TYPE", "").lower() == "wayland"

# hell for nvidia
def _nvidia_present():
    return os.path.exists("/proc/driver/nvidia/version")


def build_launch_env(instance, launcher=None):
    # :D
    env = os.environ.copy()
    if env.get("ORANG_FC_PRELOAD") == "1":
        env.pop("ORANG_FC_PRELOAD", None)
        parts = [p for p in env.get("LD_PRELOAD", "").split(":") if p and "libfontconfig" not in p]
        if parts:
            env["LD_PRELOAD"] = ":".join(parts)
        else:
            env.pop("LD_PRELOAD", None)
    for key in ("GDK_BACKEND", "PYGAME_HIDE_SUPPORT_PROMPT", "ORANG_FC_PRELOAD"):
        env.pop(key, None)
    if env.get("LD_LIBRARY_PATH_ORIG") is not None:
        env["LD_LIBRARY_PATH"] = env.pop("LD_LIBRARY_PATH_ORIG")
    elif getattr(sys, "frozen", False) or "__compiled__" in globals():
        env.pop("LD_LIBRARY_PATH", None)
    for key in ("HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy"):
        if env.get("ORANG_PROXY_SET") == "1":
            env.pop(key, None)
    env.pop("ORANG_PROXY_SET", None)
    if platform.system() == "Linux":
        backend = _instance_or_global(instance, "display_backend", "x11")
        if backend == "x11" and _is_wayland_session():
            env["XDG_SESSION_TYPE"] = "x11"
            env["SDL_VIDEO_DRIVER"] = "x11"
            env["SDL_VIDEODRIVER"] = "x11"
        elif backend == "wayland":
            env.pop("SDL_VIDEODRIVER", None)
            env.pop("SDL_VIDEO_DRIVER", None)
        theme, size = _detect_cursor_env()
        if theme and not env.get("XCURSOR_THEME"):
            env["XCURSOR_THEME"] = theme
        if size and not env.get("XCURSOR_SIZE"):
            env["XCURSOR_SIZE"] = size
        if _instance_or_global(instance, "use_zink", False):
            env["MESA_LOADER_DRIVER_OVERRIDE"] = "zink"
            env["__GLX_VENDOR_LIBRARY_NAME"] = "mesa"
        use_prime = _instance_or_global(instance, "use_prime", False)
        if not use_prime and launcher is not None and getattr(launcher, "use_dri_prime", None):
            try:
                use_prime = bool(launcher.use_dri_prime.get())
            except Exception:
                use_prime = False
        if use_prime:
            env["DRI_PRIME"] = "1"
            if _nvidia_present():
                env["__NV_PRIME_RENDER_OFFLOAD"] = "1"
                env["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia"
                env["__VK_LAYER_NV_optimus"] = "NVIDIA_only"
    if instance is not None and getattr(instance, "env_vars", None):
        for line in instance.env_vars.splitlines():
            line = line.strip()
            if "=" in line and not line.startswith("#"):
                k, _, v = line.partition("=")
                env[k.strip()] = v.strip()
    return env


def instance_command_vars(instance, java_exe="", version=""):
    if instance is None:
        return {}
    return {
        "INST_NAME": instance.name,
        "INST_ID": instance.instance_id,
        "INST_DIR": str(instance.base_path),
        "INST_MC_DIR": str(instance.minecraft_dir),
        "INST_JAVA": java_exe or "",
        "INST_MC_VER": version or instance.version,
    }


def run_hook_command(command_text, cwd, env, log_fn=None, wait=True):
    if not command_text or not command_text.strip():
        return 0
    log = log_fn or (lambda m: None)
    try:
        proc = subprocess.Popen(command_text, shell=True, cwd=cwd, env=env,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        if not wait:
            return 0
        for line in proc.stdout:
            log(f"[Hook] {line.rstrip()}")
        proc.wait()
        return proc.returncode
    except Exception as e:
        log(f"[Hook] failed: {e}")
        return -1


def apply_wrapper_command(command, wrapper_text):
    if not wrapper_text or not wrapper_text.strip():
        return command
    try:
        import shlex
        return shlex.split(wrapper_text) + list(command)
    except Exception:
        return command


def memory_jvm_args(instance, ram):
    args = [f"-Xmx{ram}"]
    min_mb = instance.opt("min_ram_mb") if instance is not None else None
    if min_mb:
        args.append(f"-Xms{int(min_mb)}M")
    else:
        args.append(f"-Xms{ram}")
    permgen = instance.opt("permgen_mb") if instance is not None else None
    if permgen:
        args.append(f"-XX:MetaspaceSize={int(permgen)}M")
    return args


def extra_jvm_args(instance):
    if instance is None:
        return []
    text = instance.opt("jvm_args", "") or ""
    try:
        import shlex
        return [a for a in shlex.split(text) if a]
    except Exception:
        return text.split()


def _options_txt_set(path, updates):
    lines = []
    if path.exists():
        try:
            lines = path.read_text(encoding="utf-8", errors="ignore").splitlines()
        except Exception:
            lines = []
    seen = set()
    out = []
    for line in lines:
        key = line.split(":", 1)[0] if ":" in line else None
        if key in updates:
            out.append(f"{key}:{updates[key]}")
            seen.add(key)
        else:
            out.append(line)
    for key, value in updates.items():
        if key not in seen:
            out.append(f"{key}:{value}")
    try:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text("\n".join(out) + "\n", encoding="utf-8")
    except Exception as e:
        print(f"[options.txt] write failed: {e}")


def _options_txt_get(path, key, default=None):
    if not path.exists():
        return default
    try:
        for line in path.read_text(encoding="utf-8", errors="ignore").splitlines():
            if line.startswith(key + ":"):
                return line.split(":", 1)[1]
    except Exception:
        pass
    return default

# sodium pls
def apply_video_options(instance):
    if instance is None:
        return
    options = instance.minecraft_dir / "options.txt"
    if options.is_symlink():
        options = options.resolve()
    updates = {}
    if instance.opt("fullscreen", False):
        updates["fullscreen"] = "true"
        instance.set_opt("fullscreen_managed", True)
    elif instance.opt("fullscreen_managed", False):
        updates["fullscreen"] = "false"
        instance.set_opt("fullscreen_managed", False)
    width = instance.opt("res_width")
    height = instance.opt("res_height")
    if width and height:
        updates["overrideWidth"] = str(int(width))
        updates["overrideHeight"] = str(int(height))
        instance.set_opt("res_managed", True)
    elif instance.opt("res_managed", False):
        updates["overrideWidth"] = "0"
        updates["overrideHeight"] = "0"
        instance.set_opt("res_managed", False)
    if updates and (options.exists() or instance.opt("fullscreen", False) or (width and height)):
        _options_txt_set(options, updates)

# it may work
def legacy_jvm_args(instance, mc_version, java_major):
    args = []
    if instance is None or not instance.opt("legacy_fixes", False):
        return args
    ver = _version_tuple(mc_version)
    if ver and ver < (1, 13):
        args.append("-Djava.util.Arrays.useLegacyMergeSort=true")
        args.append("-Dfml.ignoreInvalidMinecraftCertificates=true")
        args.append("-Dfml.ignorePatchDiscrepancies=true")
    if platform.system() == "Linux" and ver and ver < (1, 13) and not shutil.which("xrandr"):
        args.append("-DLWJGL_DISABLE_XRANDR=true")
    if platform.system() == "Darwin" and ver and ver < (1, 13):
        args.append("-XstartOnFirstThread")
    return args


_BLOCKLIST_SERVICE = "META-INF/services/com.mojang.blocklist.BlockListSupplier"
_BLOCKLIST_JAR_CACHE = {}


def _jar_provides_blocklist(path):
    try:
        st = os.stat(path)
    except OSError:
        return False
    key = (path, st.st_size, int(st.st_mtime))
    cached = _BLOCKLIST_JAR_CACHE.get(key)
    if cached is not None:
        return cached
    result = False
    try:
        with zipfile.ZipFile(path) as zf:
            result = _BLOCKLIST_SERVICE in zf.namelist()
    except Exception:
        result = False
    _BLOCKLIST_JAR_CACHE[key] = result
    return result


def strip_server_blocklist(command, instance, mc_version, log_fn=None):
    if instance is None or not bool(instance.opt("anti_ban", True)):
        return command
    removed = []
    out = []
    for arg in command:
        if "patchy" not in arg or os.pathsep not in arg and not arg.lower().endswith(".jar"):
            out.append(arg)
            continue
        prefix = ""
        body = arg
        if arg.startswith("-") and "=" in arg:
            prefix, body = arg.split("=", 1)
            prefix += "="
        entries = body.split(os.pathsep)
        kept = []
        for entry in entries:
            name = os.path.basename(entry).lower()
            if name.startswith("patchy-") and name.endswith(".jar") and _jar_provides_blocklist(entry):
                removed.append(entry)
                continue
            kept.append(entry)
        out.append(prefix + os.pathsep.join(kept))
    if removed and log_fn:
        log_fn(f"[ServerBlocklist] Removed Mojang's blocked-server list provider from the classpath ({os.path.basename(removed[0])})")
    elif log_fn and _version_tuple(mc_version) and _version_tuple(mc_version) >= (1, 16, 4):
        log_fn("[ServerBlocklist] No blocklist provider found in the classpath, nothing to bypass")
    return out


def find_installed_loader_version(minecraft_directory, mod_loader, mc_version, loader_version=""):
    versions_dir = Path(minecraft_directory) / "versions"
    if not versions_dir.exists():
        return None
    loader = (mod_loader or "").lower()
    lv = loader_version or ""
    candidates = []
    for d in versions_dir.iterdir():
        if not d.is_dir() or not (d / f"{d.name}.json").exists():
            continue
        name = d.name
        low = name.lower()
        if loader == "fabric" and low.startswith("fabric-loader-") and low.endswith(f"-{mc_version}".lower()):
            if not lv or lv in name:
                candidates.append(name)
        elif loader == "quilt" and low.startswith("quilt-loader-") and low.endswith(f"-{mc_version}".lower()):
            if not lv or lv in name:
                candidates.append(name)
        elif loader == "forge" and (low.startswith(f"{mc_version}-forge".lower()) or low.startswith(f"forge-{mc_version}".lower())):
            if not lv or lv in name:
                candidates.append(name)
        elif loader == "neoforge" and (low.startswith("neoforge-") or low.startswith(f"{mc_version}-forge-")):
            if not lv or lv in name:
                candidates.append(name)
        elif loader == "optifine" and low.startswith(f"{mc_version}-optifine".lower()):
            if not lv or lv in name:
                candidates.append(name)
    if not candidates:
        return None
    candidates.sort(key=lambda n: (versions_dir / n).stat().st_mtime, reverse=True)
    return candidates[0]


_PROXY_ENV_KEYS = ("HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy")


def _normalize_proxy_url(proxy):
    proxy = (proxy or "").strip()
    if not proxy:
        return ""
    if "://" not in proxy:
        proxy = "http://" + proxy
    return proxy


def _proxy_scheme_supported(proxy):
    scheme = urllib.parse.urlsplit(proxy).scheme.lower()
    if scheme.startswith("socks"):
        try:
            return importlib.util.find_spec("socks") is not None, scheme
        except Exception:
            return False, scheme
    return scheme in ("http", "https"), scheme


def apply_launcher_proxy():
    proxy = _normalize_proxy_url(_adv_get("proxy_url", ""))
    if proxy:
        supported, scheme = _proxy_scheme_supported(proxy)
        if not supported:
            _http_session.proxies.clear()
            for k in _PROXY_ENV_KEYS:
                if os.environ.get("ORANG_PROXY_SET") == "1":
                    os.environ.pop(k, None)
            os.environ.pop("ORANG_PROXY_SET", None)
            if scheme.startswith("socks"):
                return False, _qt_t("QT_PROXY_NEEDS_PYSOCKS", "SOCKS proxies need the PySocks package (pip install PySocks). Proxy not applied.")
            return False, _qt_t("QT_PROXY_BAD_SCHEME", "Unsupported proxy type '{scheme}'. Use http://, https://, socks5:// or socks5h://.").format(scheme=scheme or "?")
        _http_session.proxies.update({"http": proxy, "https": proxy})
        _http_session.trust_env = True
        for k in _PROXY_ENV_KEYS:
            os.environ[k] = proxy
        os.environ["ORANG_PROXY_SET"] = "1"
        return True, _qt_t("QT_PROXY_ACTIVE", "Proxy active: {proxy}").format(proxy=proxy)
    _http_session.proxies.clear()
    if os.environ.get("ORANG_PROXY_SET") == "1":
        for k in _PROXY_ENV_KEYS:
            os.environ.pop(k, None)
        os.environ.pop("ORANG_PROXY_SET", None)
    return True, _qt_t("QT_PROXY_NONE", "No proxy configured.")


def test_launcher_proxy(proxy):
    proxy = _normalize_proxy_url(proxy)
    if not proxy:
        return False, _qt_t("QT_PROXY_EMPTY", "Enter a proxy address first.")
    supported, scheme = _proxy_scheme_supported(proxy)
    if not supported:
        if scheme.startswith("socks"):
            return False, _qt_t("QT_PROXY_NEEDS_PYSOCKS", "SOCKS proxies need the PySocks package (pip install PySocks). Proxy not applied.")
        return False, _qt_t("QT_PROXY_BAD_SCHEME", "Unsupported proxy type '{scheme}'. Use http://, https://, socks5:// or socks5h://.").format(scheme=scheme or "?")
    started = time.time()
    try:
        r = requests.get("https://api.modrinth.com/v2/", proxies={"http": proxy, "https": proxy}, timeout=15,
                         headers={"User-Agent": "Orang-Studio/OrangLaunch (proxy test)"})
        r.raise_for_status()
    except Exception as e:
        return False, _qt_t("QT_PROXY_TEST_FAIL", "Proxy test failed: {error}").format(error=e)
    return True, _qt_t("QT_PROXY_TEST_OK", "Proxy works ({ms} ms to reach Modrinth).").format(ms=int((time.time() - started) * 1000))


def open_with_browser(url):
    custom = (_adv_get("custom_browser", "") or "").strip()
    if custom:
        try:
            import shlex
            subprocess.Popen(shlex.split(custom) + [url])
            return True
        except Exception as e:
            print(f"[Browser] custom browser failed: {e}")
    try:
        return webbrowser.open(url)
    except Exception:
        return False


def open_with_editor(path):
    custom = (_adv_get("custom_editor", "") or "").strip()
    if custom:
        try:
            import shlex
            subprocess.Popen(shlex.split(custom) + [str(path)])
            return True
        except Exception as e:
            print(f"[Editor] custom editor failed: {e}")
    return open_path_native(path)


def open_path_native(path):
    try:
        if platform.system() == "Windows":
            os.startfile(str(path))
        elif platform.system() == "Darwin":
            subprocess.Popen(["open", str(path)])
        else:
            subprocess.Popen(["xdg-open", str(path)])
        return True
    except Exception as e:
        print(f"[Open] failed for {path}: {e}")
        return False


def _native_picker_tool():
    if platform.system() != "Linux":
        return None
    if not _adv_get("native_file_picker", True):
        return None
    desktop = (os.environ.get("XDG_CURRENT_DESKTOP", "") + " " + os.environ.get("DESKTOP_SESSION", "")).lower()
    if "kde" in desktop or "plasma" in desktop:
        if shutil.which("kdialog"):
            return "kdialog"
    if shutil.which("zenity"):
        return "zenity"
    if shutil.which("kdialog"):
        return "kdialog"
    if shutil.which("yad"):
        return "yad"
    return None


def _filetypes_to_kdialog(filetypes):
    parts = []
    for label, pattern in (filetypes or []):
        pats = " ".join(pattern.split())
        parts.append(f"{label} ({pats})")
    return ";;".join(parts)


def _filetypes_to_zenity(filetypes):
    args = []
    for label, pattern in (filetypes or []):
        pats = " ".join(pattern.split())
        args.append(f"--file-filter={label} | {pats}")
    return args


def _run_picker(cmd):
    try:
        env = dict(os.environ)
        env.pop("GDK_BACKEND", None)
        if QT_ACTIVE[0] and _qt_is_main_thread():
            box = {}
            done = threading.Event()

            def run():
                try:
                    box["result"] = subprocess.run(cmd, capture_output=True, text=True, timeout=600, env=env)
                except Exception as e:
                    box["error"] = e
                finally:
                    done.set()
            threading.Thread(target=run, daemon=True).start()
            app = QtWidgets.QApplication.instance()
            while not done.wait(0.02):
                app.processEvents(QtCore.QEventLoop.AllEvents, 50)
            if "error" in box:
                raise box["error"]
            result = box["result"]
        else:
            result = subprocess.run(cmd, capture_output=True, text=True, timeout=600, env=env)
        if result.returncode != 0:
            return ""
        return result.stdout.strip()
    except Exception as e:
        print(f"[Picker] failed: {e}")
        return None


def _pick_open_file(**kwargs):
    tool = _native_picker_tool()
    title = kwargs.get("title") or "Open"
    initialdir = kwargs.get("initialdir") or str(Path.home())
    filetypes = kwargs.get("filetypes")
    if tool == "kdialog":
        out = _run_picker(["kdialog", "--title", title, "--getopenfilename", initialdir, _filetypes_to_kdialog(filetypes)])
        if out is not None:
            return out
    elif tool in ("zenity", "yad"):
        cmd = [tool, "--file-selection", f"--title={title}", f"--filename={initialdir}/"] + _filetypes_to_zenity(filetypes)
        out = _run_picker(cmd)
        if out is not None:
            return out
    return filedialog.askopenfilename(**kwargs)


def _pick_open_files(**kwargs):
    tool = _native_picker_tool()
    title = kwargs.get("title") or "Open"
    initialdir = kwargs.get("initialdir") or str(Path.home())
    filetypes = kwargs.get("filetypes")
    if tool == "kdialog":
        out = _run_picker(["kdialog", "--title", title, "--multiple", "--separate-output", "--getopenfilename", initialdir, _filetypes_to_kdialog(filetypes)])
        if out is not None:
            return tuple(l for l in out.splitlines() if l.strip())
    elif tool in ("zenity", "yad"):
        cmd = [tool, "--file-selection", "--multiple", "--separator=\n", f"--title={title}", f"--filename={initialdir}/"] + _filetypes_to_zenity(filetypes)
        out = _run_picker(cmd)
        if out is not None:
            return tuple(l for l in out.splitlines() if l.strip())
    return filedialog.askopenfilenames(**kwargs)


def _pick_save_file(**kwargs):
    tool = _native_picker_tool()
    title = kwargs.get("title") or "Save"
    initialdir = kwargs.get("initialdir") or str(Path.home())
    initialfile = kwargs.get("initialfile") or ""
    filetypes = kwargs.get("filetypes")
    ext = kwargs.get("defaultextension") or ""
    result = None
    if tool == "kdialog":
        out = _run_picker(["kdialog", "--title", title, "--getsavefilename", os.path.join(initialdir, initialfile), _filetypes_to_kdialog(filetypes)])
        if out is not None:
            result = out
    elif tool in ("zenity", "yad"):
        cmd = [tool, "--file-selection", "--save", "--confirm-overwrite", f"--title={title}", f"--filename={os.path.join(initialdir, initialfile)}"] + _filetypes_to_zenity(filetypes)
        out = _run_picker(cmd)
        if out is not None:
            result = out
    if result is None:
        return filedialog.asksaveasfilename(**kwargs)
    if result and ext and not os.path.splitext(result)[1]:
        result += ext
    return result


def _pick_directory(**kwargs):
    tool = _native_picker_tool()
    title = kwargs.get("title") or "Select folder"
    initialdir = kwargs.get("initialdir") or str(Path.home())
    if tool == "kdialog":
        out = _run_picker(["kdialog", "--title", title, "--getexistingdirectory", initialdir])
        if out is not None:
            return out
    elif tool in ("zenity", "yad"):
        out = _run_picker([tool, "--file-selection", "--directory", f"--title={title}", f"--filename={initialdir}/"])
        if out is not None:
            return out
    return filedialog.askdirectory(**kwargs)


def _levelname_from_level_dat(level_dat):
    try:
        import gzip
        raw = gzip.open(level_dat, "rb").read()
        marker = b"\x08\x00\x09LevelName"
        idx = raw.find(marker)
        if idx < 0:
            return None
        pos = idx + len(marker)
        length = struct.unpack(">H", raw[pos:pos + 2])[0]
        return raw[pos + 2:pos + 2 + length].decode("utf-8", errors="replace")
    except Exception:
        return None


def _set_levelname_in_level_dat(level_dat, new_name):
    try:
        import gzip
        raw = gzip.open(level_dat, "rb").read()
        marker = b"\x08\x00\x09LevelName"
        idx = raw.find(marker)
        if idx < 0:
            return False
        pos = idx + len(marker)
        length = struct.unpack(">H", raw[pos:pos + 2])[0]
        encoded = new_name.encode("utf-8")
        patched = raw[:pos] + struct.pack(">H", len(encoded)) + encoded + raw[pos + 2 + length:]
        backup = Path(str(level_dat) + "_old")
        try:
            shutil.copy2(level_dat, backup)
        except Exception:
            pass
        with gzip.open(level_dat, "wb") as f:
            f.write(patched)
        return True
    except Exception as e:
        print(f"[World] rename failed: {e}")
        return False


def list_instance_worlds(instance):
    worlds = []
    saves = instance.saves_dir
    if saves.is_symlink():
        saves = saves.resolve()
    if not saves.exists():
        return worlds
    for d in sorted(saves.iterdir(), key=lambda p: p.stat().st_mtime, reverse=True):
        if not d.is_dir():
            continue
        level = d / "level.dat"
        if not level.exists():
            continue
        size = 0
        try:
            for f in d.rglob("*"):
                if f.is_file():
                    size += f.stat().st_size
        except Exception:
            pass
        worlds.append({
            "folder": d.name,
            "path": d,
            "name": _levelname_from_level_dat(level) or d.name,
            "size": size,
            "modified": datetime.fromtimestamp(d.stat().st_mtime),
            "icon": d / "icon.png" if (d / "icon.png").exists() else None,
        })
    return worlds


ORANGPACK_EXT = ".orangpack"
ORANGPACK_INDEX = "orangpack.json"
MODRINTH_DEFAULT_LOADERS = {"fabric": "fabric-loader", "quilt": "quilt-loader", "forge": "forge", "neoforge": "neoforge"}


def _modrinth_lookup_hashes(hashes, algorithm="sha1"):
    if not hashes:
        return {}
    result = {}
    for i in range(0, len(hashes), 400):
        chunk = hashes[i:i + 400]
        try:
            r = _http_session.post(f"{MODRINTH_API_URL}/version_files", json={"hashes": chunk, "algorithm": algorithm}, timeout=30)
            r.raise_for_status()
            result.update(r.json() or {})
        except Exception as e:
            print(f"[Modrinth] hash lookup failed: {e}")
    return result


def _modrinth_update_hashes(hashes, loaders, game_versions, algorithm="sha1"):
    if not hashes:
        return {}
    result = {}
    for i in range(0, len(hashes), 400):
        chunk = hashes[i:i + 400]
        try:
            r = _http_session.post(f"{MODRINTH_API_URL}/version_files/update", json={
                "hashes": chunk, "algorithm": algorithm,
                "loaders": loaders, "game_versions": game_versions}, timeout=40)
            r.raise_for_status()
            result.update(r.json() or {})
        except Exception as e:
            print(f"[Modrinth] update lookup failed: {e}")
    return result


def _sha1_file(path):
    h = hashlib.sha1()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def _sha512_file(path):
    h = hashlib.sha512()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def _collect_export_tree(root):
    entries = []
    root = Path(root)
    skip_names = {"instance.json", ".orangpack_tmp"}
    for p in sorted(root.rglob("*")):
        if p.is_symlink():
            continue
        rel = p.relative_to(root)
        if rel.parts and rel.parts[0] in skip_names:
            continue
        if any(part.startswith("natives") for part in rel.parts) and "versions" in rel.parts:
            continue
        entries.append((str(rel).replace("\\", "/"), p))
    return entries


DEFAULT_EXPORT_SKIP_DIRS = {".minecraft/versions", ".minecraft/libraries", ".minecraft/assets", ".minecraft/logs",
                            ".minecraft/crash-reports", ".minecraft/runtime", ".minecraft/saves", ".minecraft/screenshots",
                            ".minecraft/.fabric", ".minecraft/.mixin.out", ".minecraft/.cache", ".minecraft/usercache.json",
                            ".minecraft/usernamecache.json", ".minecraft/servers.dat_old", ".minecraft/options.txt"}


def export_instance_pack(instance, out_path, fmt, selected_relpaths, log_fn=None, progress_fn=None):
    log = log_fn or (lambda m: None)
    out_path = Path(out_path)
    base = instance.base_path
    mc_root = ".minecraft/"
    files = []
    for rel in selected_relpaths:
        p = base / rel
        if p.is_file() and not p.is_symlink():
            files.append((rel, p))
    if fmt == "zip":
        with zipfile.ZipFile(out_path, "w", zipfile.ZIP_DEFLATED) as zf:
            zf.writestr("instance.json", json.dumps(instance.to_dict(), indent=2, ensure_ascii=False))
            for i, (rel, p) in enumerate(files, 1):
                zf.write(p, rel)
                if progress_fn:
                    progress_fn(i, len(files), rel)
        return str(out_path)
    hash_candidates = [(rel, p) for rel, p in files if rel.startswith(mc_root) and p.suffix.lower() in (".jar", ".zip") and
                       any(rel[len(mc_root):].startswith(d + "/") for d in ("mods", "resourcepacks", "shaderpacks", "datapacks"))]
    sha1_map = {}
    for rel, p in hash_candidates:
        try:
            sha1_map[_sha1_file(p)] = (rel, p)
        except Exception:
            pass
    log(f"[Export] Looking up {len(sha1_map)} files on Modrinth")
    found = _modrinth_lookup_hashes(list(sha1_map.keys()))
    remote_files = []
    remote_rels = set()
    for sha1, version in found.items():
        rel, p = sha1_map[sha1]
        vf = None
        for f in version.get("files", []):
            if (f.get("hashes") or {}).get("sha1") == sha1:
                vf = f
                break
        if not vf:
            continue
        remote_rels.add(rel)
        remote_files.append({
            "path": rel[len(mc_root):],
            "hashes": {"sha1": sha1, "sha512": (vf.get("hashes") or {}).get("sha512") or _sha512_file(p)},
            "env": {"client": "required", "server": "required" if rel[len(mc_root):].startswith("mods/") else "unsupported"},
            "downloads": [vf.get("url")],
            "fileSize": p.stat().st_size,
        })
    deps = {"minecraft": instance.version}
    loader_key = MODRINTH_DEFAULT_LOADERS.get((instance.mod_loader or "").lower())
    if loader_key and instance.loader_version:
        deps[loader_key] = instance.loader_version
    elif loader_key and instance.installed_version_id:
        m = re.search(r"(\d+\.\d+\.\d+(?:[-.][\w.]+)?)", instance.installed_version_id.replace(instance.version, ""))
        if m:
            deps[loader_key] = m.group(1)
    index = {
        "formatVersion": 1,
        "game": "minecraft",
        "versionId": "1.0.0",
        "name": instance.name,
        "summary": f"Exported from OrangLauncher {CURRENT_VERSION}",
        "files": remote_files,
        "dependencies": deps,
    }
    with zipfile.ZipFile(out_path, "w", zipfile.ZIP_DEFLATED) as zf:
        if fmt == "orangpack":
            index["orangpack"] = {
                "launcher": CURRENT_VERSION,
                "instance": {k: v for k, v in instance.to_dict().items() if k in ("name", "version", "mod_loader", "loader_version", "ram", "java_args", "env_vars", "opts")},
            }
            zf.writestr(ORANGPACK_INDEX, json.dumps(index, indent=2, ensure_ascii=False))
        zf.writestr("modrinth.index.json", json.dumps(index, indent=2, ensure_ascii=False))
        icon_file = instance.base_path / "icon.txt"
        if icon_file.exists():
            try:
                icon_path = Path(icon_file.read_text(encoding="utf-8").strip())
                if icon_path.exists():
                    zf.write(icon_path, "icon" + icon_path.suffix.lower())
            except Exception:
                pass
        total = len(files)
        for i, (rel, p) in enumerate(files, 1):
            if rel in remote_rels:
                continue
            if rel.startswith(mc_root):
                zf.write(p, "overrides/" + rel[len(mc_root):])
            elif fmt == "orangpack":
                zf.write(p, "instance/" + rel)
            if progress_fn:
                progress_fn(i, total, rel)
    log(f"[Export] {len(remote_files)} files referenced remotely, rest packed as overrides")
    return str(out_path)


# mmh all the COLORS of the themes!!
class ThemeManager:
    def __init__(self):
        self.themes = {}
        self.current_theme = None
        self.theme_data = None
        self._load_themes()
    def _load_themes(self):
        themes_dir = find_resource("oranglauncher/themes")
        if not themes_dir or not os.path.exists(themes_dir):
            return
        for filename in os.listdir(themes_dir):
            if filename.endswith('.json'):
                theme_path = os.path.join(themes_dir, filename)
                try:
                    with open(theme_path, 'r', encoding='utf-8') as f:
                        theme_data = json.load(f)
                    theme_name = theme_data.get('name', filename.replace('.json', ''))
                    self.themes[theme_name] = theme_data
                except Exception as e:
                    print(f"[ThemeManager] Error loading theme {filename}: {e}")
    def get_available_themes(self):
        return list(self.themes.keys())
    def load_theme(self, theme_name):
        if theme_name in self.themes:
            self.current_theme = theme_name
            self.theme_data = self.themes[theme_name]
            return True
        else:
            print(f"[ThemeManager] Theme not found: {theme_name}")
            if 'Arc' in self.themes:
                self.current_theme = 'Arc'
                self.theme_data = self.themes['Arc']
                return True
            return False
    def get_color(self, color_key):
        if self.theme_data:
            return self.theme_data.get('colors', {}).get(color_key, '#000000')
        return '#000000'
    def get_font(self, font_key='primary'):
        if self.theme_data:
            return self.theme_data.get('fonts', {}).get(font_key, 'Segoe UI')
        return 'Segoe UI'

_theme_manager = None
def get_theme_manager():
    global _theme_manager
    if _theme_manager is None:
        _theme_manager = ThemeManager()
    return _theme_manager
def load_saved_theme():
    try:
        config_path = Path.home() / ".config" / "oranglauncher" / "launcher_config.json"
        if config_path.exists():
            with open(config_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            theme_name = data.get('theme', 'Arc')
            return theme_name
    except Exception as e:
        print(f"[ThemeManager] Error loading saved theme: {e}")
    return 'Arc'
def save_theme_preference(theme_name):
    try:
        config_dir = Path.home() / ".config" / "oranglauncher"
        config_dir.mkdir(parents=True, exist_ok=True)
        config_path = config_dir / "launcher_config.json"
        data = {}
        if config_path.exists():
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
            except Exception:
                data = {}
        data['theme'] = theme_name
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
        return True
    except Exception as e:
        print(f"[ThemeManager] Error saving theme: {e}")
        return False

# plugin system (i hate it kinda with hooking - adasjusk)
# nah it's good

# resource packs and shaders
class ResourceShaderManager:
    def __init__(self, instance_manager=None):
        self.profile_manager = get_game_profile_manager()
        self.instance_manager = instance_manager
        self.current_profile = None
        self.current_instance = None
        self.change_callbacks = []
        register_mod_change_callback(self.on_profile_changed)
        if self.instance_manager:
            self.instance_manager.register_callback(self.on_instance_changed)
    def on_profile_changed(self):
        self._notify_change()
    def on_instance_changed(self):
        self._notify_change()
    def register_change_callback(self, callback):
        if callback not in self.change_callbacks:
            self.change_callbacks.append(callback)
    def unregister_change_callback(self, callback):
        if callback in self.change_callbacks:
            self.change_callbacks.remove(callback)
    def _notify_change(self):
        for callback in self.change_callbacks:
            try:
                callback()
            except Exception:
                break
    def update_context(self):
        if self.instance_manager:
            self.current_instance = self.instance_manager.get_selected_instance()
            if self.current_instance:
                self.current_profile = None
                return
        self.current_profile = self.profile_manager.get_selected_profile()
        self.current_instance = None
    def get_resourcepacks_directory(self):
        self.update_context()
        if self.current_instance:
            return self.current_instance.resourcepacks_dir
        elif self.current_profile:
            base_dir = Path(self.current_profile.game_dir) if self.current_profile.game_dir else Path.home() / ".minecraft"
            return base_dir / "profiles" / self.current_profile.id / "resourcepacks"
        return None
    def get_shaderpacks_directory(self):
        self.update_context()
        if self.current_instance:
            return self.current_instance.shaderpacks_dir
        elif self.current_profile:
            base_dir = Path(self.current_profile.game_dir) if self.current_profile.game_dir else Path.home() / ".minecraft"
            return base_dir / "profiles" / self.current_profile.id / "shaderpacks"
        return None
    def ensure_resourcepacks_directory(self):
        rp_dir = self.get_resourcepacks_directory()
        if rp_dir:
            rp_dir.mkdir(parents=True, exist_ok=True)
            return rp_dir
        return None
    def ensure_shaderpacks_directory(self):
        sp_dir = self.get_shaderpacks_directory()
        if sp_dir:
            sp_dir.mkdir(parents=True, exist_ok=True)
            return sp_dir
        return None
    def get_resourcepacks(self):
        rp_dir = self.get_resourcepacks_directory()
        if not rp_dir or not rp_dir.exists():
            return []
        packs = []
        for item in rp_dir.iterdir():
            if item.is_file() and item.suffix.lower() == '.zip':
                packs.append(item.name)
            elif item.is_dir() and not item.name.startswith('.'):
                packs.append(item.name)
        return sorted(packs)
    def get_shaderpacks(self):
        sp_dir = self.get_shaderpacks_directory()
        if not sp_dir or not sp_dir.exists():
            return []
        packs = []
        for item in sp_dir.iterdir():
            if item.is_file() and item.suffix.lower() == '.zip':
                packs.append(item.name)
            elif item.is_dir() and not item.name.startswith('.'):
                packs.append(item.name)
        return sorted(packs)
    def add_resourcepacks(self, file_paths):
        rp_dir = self.ensure_resourcepacks_directory()
        if not rp_dir:
            return 0, len(file_paths)
        added_count = 0
        failed_count = 0
        for file_path in file_paths:
            try:
                source_path = Path(file_path)
                if source_path.is_file():
                    dest_path = rp_dir / source_path.name
                    shutil.copy2(source_path, dest_path)
                    added_count += 1
                elif source_path.is_dir():
                    dest_path = rp_dir / source_path.name
                    if dest_path.exists():
                        shutil.rmtree(dest_path)
                    shutil.copytree(source_path, dest_path)
                    added_count += 1
                else:
                    failed_count += 1
            except Exception as e:
                print("Error adding resource pack")
                failed_count += 1
        if added_count > 0:
            self._notify_change()
        return added_count, failed_count
    def add_shaderpacks(self, file_paths):
        sp_dir = self.ensure_shaderpacks_directory()
        if not sp_dir:
            return 0, len(file_paths)
        added_count = 0
        failed_count = 0
        for file_path in file_paths:
            try:
                source_path = Path(file_path)
                if source_path.is_file():
                    dest_path = sp_dir / source_path.name
                    shutil.copy2(source_path, dest_path)
                    added_count += 1
                elif source_path.is_dir():
                    dest_path = sp_dir / source_path.name
                    if dest_path.exists():
                        shutil.rmtree(dest_path)
                    shutil.copytree(source_path, dest_path)
                    added_count += 1
                else:
                    failed_count += 1
            except Exception as e:
                print(f"Error adding shader pack {file_path}: {e}")
                failed_count += 1
        if added_count > 0:
            self._notify_change()
        return added_count, failed_count
    def remove_resourcepacks(self, pack_names):
        rp_dir = self.get_resourcepacks_directory()
        if not rp_dir or not rp_dir.exists():
            return 0
        removed_count = 0
        for pack_name in pack_names:
            try:
                pack_path = rp_dir / pack_name
                if pack_path.exists():
                    if pack_path.is_file():
                        pack_path.unlink()
                    else:
                        shutil.rmtree(pack_path)
                    removed_count += 1
            except Exception as e:
                print(f"Error removing resource pack {pack_name}: {e}")
        if removed_count > 0:
            self._notify_change()
        return removed_count
    def remove_shaderpacks(self, pack_names):
        sp_dir = self.get_shaderpacks_directory()
        if not sp_dir or not sp_dir.exists():
            return 0
        removed_count = 0
        for pack_name in pack_names:
            try:
                pack_path = sp_dir / pack_name
                if pack_path.exists():
                    if pack_path.is_file():
                        pack_path.unlink()
                    else:
                        shutil.rmtree(pack_path)
                    removed_count += 1
            except Exception as e:
                print(f"Error removing shader pack {pack_name}: {e}")
        if removed_count > 0:
            self._notify_change()
        return removed_count
    def open_resourcepacks_folder(self):
        rp_dir = self.ensure_resourcepacks_directory()
        if not rp_dir:
            return False
        try:
            subprocess.run(["xdg-open", str(rp_dir)])
            return True
        except Exception as e:
            print(f"Error opening resourcepacks folder: {e}")
            return False
    def open_shaderpacks_folder(self):
        sp_dir = self.ensure_shaderpacks_directory()
        if not sp_dir:
            return False
        try:
            subprocess.run(["xdg-open", str(sp_dir)])
            return True
        except Exception as e:
            print(f"Error opening shaderpacks folder: {e}")
            return False
    def get_pack_info(self, pack_name, pack_type='resource'):
        if pack_type == 'resource':
            pack_dir = self.get_resourcepacks_directory()
        else:
            pack_dir = self.get_shaderpacks_directory()
        if not pack_dir or not pack_dir.exists():
            return None
        pack_path = pack_dir / pack_name
        if not pack_path.exists():
            return None
        try:
            if pack_path.is_file():
                size_mb = pack_path.stat().st_size / (1024 * 1024)
                return {
                    'name': pack_name,
                    'type': 'file',
                    'size_mb': size_mb
                }
            else:
                total_size = sum(f.stat().st_size for f in pack_path.rglob('*') if f.is_file())
                size_mb = total_size / (1024 * 1024)
                return {
                    'name': pack_name,
                    'type': 'directory',
                    'size_mb': size_mb
                }
        except Exception as e:
            print(f"Error getting pack info: {e}")
            return None
    def get_current_context_name(self):
        self.update_context()
        if self.current_instance:
            return self.current_instance.name
        elif self.current_profile:
            return self.current_profile.name
        return "None"
_resource_shader_manager = None
def get_resource_shader_manager(instance_manager=None):
    global _resource_shader_manager
    if _resource_shader_manager is None:
        _resource_shader_manager = ResourceShaderManager(instance_manager)
    return _resource_shader_manager


MODRINTH_API_URL = "https://api.modrinth.com/v2"


# minecraft server list ping thingy
def _write_varint(n: int) -> bytes:
    out = bytearray()
    while True:
        byte = n & 0x7F
        n >>= 7
        if n:
            byte |= 0x80
        out.append(byte)
        if not n:
            break
    return bytes(out)


def _read_varint(sock) -> int:
    result = 0
    shift = 0
    while True:
        b = sock.recv(1)
        if not b:
            raise ConnectionError("Socket closed while reading varint")
        byte = b[0]
        result |= (byte & 0x7F) << shift
        if not (byte & 0x80):
            break
        shift += 7
        if shift >= 35:
            raise ValueError("Varint too large")
    return result


def _slp_ping(host: str, port: int = 25565, timeout: int = 3):
    

    def read_varint_from_file(f):
        result = 0
        shift = 0
        while True:
            b = f.read(1)
            if not b:
                raise ConnectionError("Socket closed while reading varint")
            byte = b[0]
            result |= (byte & 0x7F) << shift
            if not (byte & 0x80):
                break
            shift += 7
            if shift >= 35:
                raise ValueError("Varint too large")
        return result

    try:
        t0 = time.monotonic()
        sock = socket.create_connection((host, port), timeout=timeout)
        sock.settimeout(timeout)
        f = sock.makefile('rb')

        host_bytes = host.encode('utf-8')
        handshake_data = (
            b'\x00'                          # packet id = 0
            + _write_varint(0x2F)            # protocol version 47, any works for status
            + _write_varint(len(host_bytes)) # string length prefix
            + host_bytes                     # server address
            + struct.pack('>H', port)        # port (big-endian unsigned short)
            + b'\x01'                        # next state: 1 = status
        )
        sock.sendall(_write_varint(len(handshake_data)) + handshake_data)

        sock.sendall(b'\x01\x00')
        read_varint_from_file(f)          # packet length ignored
        read_varint_from_file(f)          # packet id is 0x00
        str_len = read_varint_from_file(f)

        data = f.read(str_len)
        if len(data) < str_len:
            raise ConnectionError("Truncated response")

        latency = int((time.monotonic() - t0) * 1000)
        sock.close()
        info = json.loads(data.decode('utf-8'))
        players = info.get('players', {})
        description = info.get('description', '')
        if isinstance(description, dict):
            description = description.get('text', '')
        return {
            'latency': latency,
            'online': players.get('online', 0),
            'max': players.get('max', 0),
            'motd': str(description),
            'favicon': info.get('favicon', ''),
        }
    except Exception:
        return None


def _strip_mc_formatting(text: str) -> str:
    
    return re.sub(r'§[0-9a-fk-or]', '', text, flags=re.IGNORECASE)

class ServersNBT:
    TAG_END = 0
    TAG_BYTE = 1
    TAG_SHORT = 2
    TAG_INT = 3
    TAG_LONG = 4
    TAG_FLOAT = 5
    TAG_DOUBLE = 6
    TAG_BYTE_ARRAY = 7
    TAG_STRING = 8
    TAG_LIST = 9
    TAG_COMPOUND = 10
    TAG_INT_ARRAY = 11
    TAG_LONG_ARRAY = 12
    @staticmethod
    def read_servers_dat(path: Path) -> list:
        if not path.exists():
            return []
        try:
            with open(path, 'rb') as f:
                data = f.read()
            if not data:
                return []
            return ServersNBT._parse_nbt(data)
        except Exception as e:
            print(f"[ServersNBT] Error reading servers.dat: {e}")
            return []

    @staticmethod
    def _parse_nbt(data: bytes) -> list:
        
        pos = [0]

        def read_byte():
            val = data[pos[0]]
            pos[0] += 1
            return val

        def read_short():
            val = struct.unpack('>h', data[pos[0]:pos[0]+2])[0]
            pos[0] += 2
            return val

        def read_ushort():
            val = struct.unpack('>H', data[pos[0]:pos[0]+2])[0]
            pos[0] += 2
            return val

        def read_int():
            val = struct.unpack('>i', data[pos[0]:pos[0]+4])[0]
            pos[0] += 4
            return val

        def read_string():
            length = read_ushort()
            s = data[pos[0]:pos[0]+length].decode('utf-8', errors='replace')
            pos[0] += length
            return s

        def read_tag(tag_type):
            if tag_type == ServersNBT.TAG_BYTE:
                return read_byte()
            elif tag_type == ServersNBT.TAG_SHORT:
                return read_short()
            elif tag_type == ServersNBT.TAG_INT:
                return read_int()
            elif tag_type == ServersNBT.TAG_STRING:
                return read_string()
            elif tag_type == ServersNBT.TAG_LIST:
                list_type = read_byte()
                length = read_int()
                return [read_tag(list_type) for _ in range(length)]
            elif tag_type == ServersNBT.TAG_COMPOUND:
                result = {}
                while True:
                    t = read_byte()
                    if t == ServersNBT.TAG_END:
                        break
                    name = read_string()
                    result[name] = read_tag(t)
                return result
            else:
                return None

        tag_type = read_byte()
        if tag_type != ServersNBT.TAG_COMPOUND:
            return []
        read_string()
        root = read_tag(ServersNBT.TAG_COMPOUND)
        return root.get('servers', [])

    @staticmethod
    def write_servers_dat(path: Path, servers: list):
        out = bytearray()

        def write_byte(val):
            out.append(val & 0xFF)
        def write_short(val):
            out.extend(struct.pack('>h', val))
        def write_ushort(val):
            out.extend(struct.pack('>H', val))
        def write_int(val):
            out.extend(struct.pack('>i', val))

        def write_string(s):
            encoded = s.encode('utf-8')
            write_ushort(len(encoded))
            out.extend(encoded)

        def write_tag(tag_type, value):
            if tag_type == ServersNBT.TAG_BYTE:
                write_byte(value)
            elif tag_type == ServersNBT.TAG_STRING:
                write_string(value)
            elif tag_type == ServersNBT.TAG_COMPOUND:
                for k, v in value.items():
                    if isinstance(v, int) and not isinstance(v, bool):
                        write_byte(ServersNBT.TAG_BYTE if -128 <= v <= 127 else ServersNBT.TAG_INT)
                        write_string(k)
                        if -128 <= v <= 127:
                            write_byte(v)
                        else:
                            write_int(v)
                    elif isinstance(v, str):
                        write_byte(ServersNBT.TAG_STRING)
                        write_string(k)
                        write_string(v)
                    elif isinstance(v, bool):
                        write_byte(ServersNBT.TAG_BYTE)
                        write_string(k)
                        write_byte(1 if v else 0)
                write_byte(ServersNBT.TAG_END)
            elif tag_type == ServersNBT.TAG_LIST:
                write_byte(ServersNBT.TAG_COMPOUND)
                write_int(len(value))
                for item in value:
                    write_tag(ServersNBT.TAG_COMPOUND, item)

        write_byte(ServersNBT.TAG_COMPOUND)
        write_string('')
        write_byte(ServersNBT.TAG_LIST)
        write_string('servers')
        write_tag(ServersNBT.TAG_LIST, servers)
        write_byte(ServersNBT.TAG_END)
        path.parent.mkdir(parents=True, exist_ok=True)
        with open(path, 'wb') as f:
            f.write(out)


# discord presence (raw IPC, no pypresence)
class DiscordRPCManager:
    OP_HANDSHAKE = 0
    OP_FRAME = 1
    OP_CLOSE = 2

    def __init__(self, app_id: str, on_connected=None):
        self.app_id = app_id
        self.on_connected = on_connected
        self.thread = None
        self.sock = None
        self.lock = threading.Lock()
        self.stop_event = threading.Event()
        self.connected = False

    @staticmethod
    def _ipc_paths():
        if platform.system() == "Windows":
            return [f"\\\\?\\pipe\\discord-ipc-{i}" for i in range(10)]
        bases = []
        for var in ("XDG_RUNTIME_DIR", "TMPDIR", "TMP", "TEMP"):
            val = os.environ.get(var)
            if val:
                bases.append(val)
        bases.append("/tmp")
        paths = []
        for base in bases:
            for sub in ("", "app/com.discordapp.Discord", "snap.discord", "snap.discord-canary"):
                d = os.path.join(base, sub) if sub else base
                for i in range(10):
                    paths.append(os.path.join(d, f"discord-ipc-{i}"))
        return paths

    def _connect(self):
        for path in self._ipc_paths():
            try:
                if platform.system() == "Windows":
                    sock = open(path, "r+b", buffering=0)
                else:
                    if not os.path.exists(path):
                        continue
                    sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
                    sock.settimeout(5)
                    sock.connect(path)
                return sock
            except Exception:
                continue
        return None

    def _send_raw(self, data):
        if hasattr(self.sock, "sendall"):
            self.sock.sendall(data)
        else:
            self.sock.write(data)

    def _recv_raw(self, n):
        buf = b""
        while len(buf) < n:
            chunk = self.sock.recv(n - len(buf)) if hasattr(self.sock, "recv") else self.sock.read(n - len(buf))
            if not chunk:
                raise ConnectionError("Discord IPC closed")
            buf += chunk
        return buf

    def _send(self, op, payload):
        data = json.dumps(payload).encode("utf-8")
        with self.lock:
            self._send_raw(struct.pack("<II", op, len(data)) + data)

    def _recv(self):
        op, length = struct.unpack("<II", self._recv_raw(8))
        body = self._recv_raw(length) if length else b""
        try:
            return op, json.loads(body.decode("utf-8") or "{}")
        except Exception:
            return op, {}

    def start(self):
        if self.thread and self.thread.is_alive():
            return
        self.stop_event.clear()

        def _thread_main():
            self.sock = self._connect()
            if self.sock is None:
                print("[Discord] no IPC socket found, Rich Presence disabled")
                return
            try:
                self._send(self.OP_HANDSHAKE, {"v": 1, "client_id": self.app_id})
                op, reply = self._recv()
                if op == self.OP_CLOSE or (reply.get("evt") == "ERROR"):
                    raise ConnectionError(reply.get("data", {}).get("message", "handshake rejected"))
                self.connected = True
                if self.on_connected:
                    try:
                        self.on_connected()
                    except Exception:
                        pass
                if hasattr(self.sock, "settimeout"):
                    self.sock.settimeout(0.5)
                while not self.stop_event.is_set():
                    try:
                        op, _ = self._recv()
                        if op == self.OP_CLOSE:
                            break
                    except (socket.timeout, TimeoutError):
                        continue
                    except Exception:
                        break
            except Exception as e:
                print(f"[Discord] RPC connection failed: {e}")
            finally:
                self.connected = False
                try:
                    self.sock.close()
                except Exception:
                    pass
                self.sock = None
        self.thread = threading.Thread(target=_thread_main, name="DiscordRPC", daemon=True)
        self.thread.start()

    def _set_activity(self, activity):
        if not self.connected or self.sock is None:
            return
        payload = {"cmd": "SET_ACTIVITY", "args": {"pid": os.getpid(), "activity": activity}, "nonce": str(uuid_module.uuid4())}
        try:
            self._send(self.OP_FRAME, payload)
        except Exception:
            pass

    def update(self, state=None, details=None, start=None, end=None, large_image=None, large_text=None,
               small_image=None, small_text=None, buttons=None, **_):
        activity = {}
        if state:
            activity["state"] = state
        if details:
            activity["details"] = details
        if start or end:
            activity["timestamps"] = {k: int(v) for k, v in (("start", start), ("end", end)) if v}
        assets = {k: v for k, v in (("large_image", large_image), ("large_text", large_text),
                                    ("small_image", small_image), ("small_text", small_text)) if v}
        if assets:
            activity["assets"] = assets
        if buttons:
            activity["buttons"] = buttons
        self._set_activity(activity)

    def clear(self):
        self._set_activity(None)

    def stop(self):
        try:
            self.clear()
            self.stop_event.set()
            if self.thread:
                self.thread.join(timeout=3)
        except Exception:
            pass
_APP_REF = None
def _atexit_cleanup():
    try:
        if _APP_REF is not None:
            app = _APP_REF()
            if app:
                app._stop_discord_rpc()
    except Exception:
        pass
atexit.register(_atexit_cleanup)
try:
    ENCRYPTION_AVAILABLE = importlib.util.find_spec("cryptography") is not None
except Exception:
    ENCRYPTION_AVAILABLE = False
PROFILE_FILENAME = "profiles.json"
CLIENT_ID = "00000000402B5328"
DEVICE_CLIENT_ID = "00000000441cc96b"
REDIRECT_URI = "https://login.live.com/oauth20_desktop.srf"
SCOPE = "service::user.auth.xboxlive.com::MBI_SSL"
_TOKEN_ENCRYPTION_VERSION = "v2"
def _get_machine_key():
    try:
        machine_id = Path('/etc/machine-id').read_text().strip()
    except:
        try:
            machine_id = platform.node() + os.getlogin()
        except:
            machine_id = str(Path.home())
    salt = f"oranglauncher-{_TOKEN_ENCRYPTION_VERSION}-{platform.system()}-{machine_id}".encode()

    if not ENCRYPTION_AVAILABLE:
        return None
    from cryptography.hazmat.primitives import hashes
    from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
    kdf = PBKDF2HMAC(
        algorithm=hashes.SHA256(),
        length=32,
        salt=salt,
        iterations=150000,
    )
    return base64.urlsafe_b64encode(kdf.derive(machine_id.encode()))

def _get_cipher():
    if not ENCRYPTION_AVAILABLE:
        return None
    key = _get_machine_key()
    if key:
        from cryptography.fernet import Fernet
        return Fernet(key)
    return None
def encrypt_token(token):
    if not token or not ENCRYPTION_AVAILABLE:
        return token
    try:
        cipher = _get_cipher()
        if cipher:
            encrypted = cipher.encrypt(token.encode())
            versioned = f"{_TOKEN_ENCRYPTION_VERSION}:{base64.urlsafe_b64encode(encrypted).decode()}"
            return versioned
        return token
    except Exception as e:
        print(f"Encryption error: {e}")
        return token
def decrypt_token(encrypted_token):
    if not encrypted_token or not ENCRYPTION_AVAILABLE:
        return encrypted_token
    try:
        cipher = _get_cipher()
        if cipher:
            token_data = encrypted_token
            if encrypted_token.startswith(f"{_TOKEN_ENCRYPTION_VERSION}:"):
                token_data = encrypted_token[len(_TOKEN_ENCRYPTION_VERSION) + 1:]
            
            encrypted_bytes = base64.urlsafe_b64decode(token_data.encode())
            decrypted = cipher.decrypt(encrypted_bytes)
            return decrypted.decode()
        return encrypted_token
    except Exception:
        return encrypted_token
_SENSITIVE_FIELDS = [
    'microsoft_token', 'access_token', 'refresh_token',
    'minecraft_token', 'microsoft_refresh_token',
    'password', 'secret', 'key', 'auth_token',
    'client_secret', 'bearer_token', 'oauth_token'
]
class SecureProfileWrapper:
    def __init__(self, profile_data):
        self._data = profile_data
    def __getitem__(self, key):
        if key in _SENSITIVE_FIELDS:
            return '[PROTECTED]'
        return self._data.get(key)
    def get(self, key, default=None):
        if key in _SENSITIVE_FIELDS:
            return '[PROTECTED]'
        return self._data.get(key, default)
    def __contains__(self, key):
        return key in self._data and key not in _SENSITIVE_FIELDS
    def keys(self):
        return [k for k in self._data.keys() if k not in _SENSITIVE_FIELDS]
    def items(self):
        return [(k, v) for k, v in self._data.items() if k not in _SENSITIVE_FIELDS]
    def __repr__(self):
        safe_data = {k: v for k, v in self._data.items() if k not in _SENSITIVE_FIELDS}
        return f"SecureProfile({safe_data})"
def get_data_dir():
    return Path.home() / ".minecraft"
def profile_path():
    return get_data_dir() / PROFILE_FILENAME
def load_profiles():
    try:
        path = profile_path()
        if not path.exists():
            return []
        with open(path, "r") as f:
            profiles_data = json.load(f)
        for profile in profiles_data:
            for field in _SENSITIVE_FIELDS:
                if field in profile and profile[field]:
                    profile[field] = decrypt_token(profile[field])
        return profiles_data
    except Exception as e:
        print(f"Error loading profiles: {e}")
        return []
def load_profiles_safe():
    profiles_data = load_profiles()
    return [SecureProfileWrapper(p) for p in profiles_data]
def _set_secure_file_permissions(file_path):
    try:
        
        os.chmod(file_path, stat.S_IRUSR | stat.S_IWUSR)
    except Exception as e:
        print(f"Warning: Could not set secure file permissions: {e}")

def save_profiles(profiles_data):
    profiles_to_save = copy.deepcopy(profiles_data)
    for profile in profiles_to_save:
        for field in _SENSITIVE_FIELDS:
            if field in profile and profile[field]:
                profile[field] = encrypt_token(profile[field])
    p = profile_path()
    p.parent.mkdir(parents=True, exist_ok=True)
    with open(p, "w") as f:
        json.dump(profiles_to_save, f, indent=2)
    _set_secure_file_permissions(p)
def ms_token_flow_interactive():
    if QT_ACTIVE[0]:
        return _ms_token_flow_qt()
    return _ms_token_flow_browser()


def _extract_code_from_redirect(text):
    if not text:
        return None
    text = text.strip()
    idx = text.lower().find('code=')
    if idx >= 0:
        code = text[idx + 5:]
        amp = code.find('&')
        if amp >= 0:
            code = code[:amp]
        return urllib.parse.unquote(code) or None
    if '://' in text or ' ' in text:
        return None
    return text or None


def _ms_token_flow_browser():
    oauth_url = (
        "https://login.live.com/oauth20_authorize.srf"
        f"?client_id={CLIENT_ID}"
        f"&redirect_uri={REDIRECT_URI}"
        "&response_type=code"
        f"&scope={SCOPE}"
    )
    try:
        open_with_browser(oauth_url)
    except Exception as e:
        raise Exception(f"Could not open browser for login: {e}")
    prompt = (
        "The embedded browser is unavailable, so your web browser was opened instead.\n"
        "1. Sign in with your Microsoft account in the browser.\n"
        "2. When you land on a blank page, copy its full address IMMEDIATELY -\n"
        "    the code is removed from the address a moment after the page loads.\n"
        "3. Paste that address (or just the code=... value) below."
    )
    pasted = _qt_askstring("Sign in with your browser", prompt)
    if not pasted:
        raise Exception("Login cancelled or failed")
    auth_code = _extract_code_from_redirect(pasted)
    if not auth_code:
        raise Exception("Could not extract the authorization code from the pasted text")
    return _complete_oauth_flow(auth_code)


def _complete_oauth_flow(auth_code):
    r = requests.post("https://login.live.com/oauth20_token.srf", data={
        "client_id": CLIENT_ID,
        "scope": SCOPE,
        "code": auth_code,
        "redirect_uri": REDIRECT_URI,
        "grant_type": "authorization_code"
    }, timeout=30)
    if not r.ok:
        raise Exception("Failed to get Microsoft token: " + r.text)
    return _ms_complete_with_tokens(r.json())
def refresh_mc_token(microsoft_refresh_token, client_id=None):
    r = requests.post("https://login.live.com/oauth20_token.srf", data={
        "scope": SCOPE,
        "client_id": client_id or CLIENT_ID,
        "grant_type": "refresh_token",
        "refresh_token": microsoft_refresh_token
    }, timeout=30)
    if not r.ok:
        raise Exception("Failed to refresh Microsoft token: " + r.text)
    tokens = r.json()
    tokens.setdefault("refresh_token", microsoft_refresh_token)
    result = _ms_complete_with_tokens(tokens)
    result["ms_client_id"] = client_id or CLIENT_ID
    return result
def check_mc_token(minecraft_token):
    r = _http_session.get("https://api.minecraftservices.com/minecraft/profile", headers={
        "Authorization": f"Bearer {minecraft_token}"
    })
    return r.ok
def _store_profile(profile):
    profiles_data = load_profiles()
    for idx, p in enumerate(profiles_data):
        if p.get("uuid") == profile.get("uuid") or (p.get("type") == "microsoft" and p.get("username") == profile.get("username")):
            profiles_data[idx] = profile
            break
    save_profiles(profiles_data)
def ensure_mc_profile_valid(profile):
    if profile.get("type") != "microsoft":
        return profile
    token = profile.get("minecraft_token")
    refresh_token = profile.get("microsoft_refresh_token")
    if token and check_mc_token(token):
        return profile
    try:
        newdata = refresh_mc_token(refresh_token, profile.get("ms_client_id"))
        profile.update(newdata)
        _store_profile(profile)
        return profile
    except Exception as e:
        print(f"Refresh failed: {e}. Need full login.")
        try:
            newdata = ms_token_flow_interactive()
            profile.update(newdata)
            _store_profile(profile)
            return profile
        except Exception as ee:
            raise Exception("Could not refresh or re-authenticate: " + str(ee))


def _get_setup_mark_path():
    config_dir = Path.home() / ".config" / "oranglauncher"
    config_dir.mkdir(parents=True, exist_ok=True)
    return config_dir / "setup.mark"

def is_setup_done():
    path = _get_setup_mark_path()
    try:
        if not path.exists():
            path.write_text("setup_done=false\n", encoding="utf-8")
            return False
        for line in path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line.startswith("setup_done="):
                return line.split("=", 1)[1].strip().lower() == "true"
        return False
    except Exception as e:
        print(f"[setup] failed to read setup.mark: {e}")
        return False

def mark_setup_done(done=True):
    try:
        _get_setup_mark_path().write_text(
            f"setup_done={'true' if done else 'false'}\n", encoding="utf-8")
    except Exception as e:
        print(f"[setup] failed to write setup.mark: {e}")

def _wizard_detect_javas():
    found = []
    for major in (25, 21, 17, 11, 8):
        p = find_java_executable(major)
        if p:
            found.append((major, p))
    return found

def _wizard_loader_versions(loader, mc_version):
    loader = (loader or "").lower()
    if loader in ("", "vanilla") or not mc_version:
        return []
    try:
        if loader == "forge" and hasattr(minecraft_launcher_lib, "forge"):
            fv = minecraft_launcher_lib.forge.list_forge_versions()
            return [v for v in reversed(fv) if v.startswith(f"{mc_version}-")]
        if loader == "neoforge":
            nf = _NeoforgeCompat()
            return nf.get_loader_versions(mc_version, True) or nf.get_loader_versions(mc_version, False)
        if loader == "fabric" and hasattr(minecraft_launcher_lib, "fabric"):
            return [v["version"] for v in minecraft_launcher_lib.fabric.get_all_loader_versions()]
        if loader == "quilt" and hasattr(minecraft_launcher_lib, "quilt"):
            return [v["version"] for v in minecraft_launcher_lib.quilt.get_all_loader_versions()]
    except Exception as e:
        print(f"[setup] loader versions fetch failed: {e}")
    return []


# main app

def terminal_launch_game(instance, profile, ram="4G"):
    try:
        version = instance.version
        mod_loader = instance.mod_loader
        username = profile.get("username", "Steve")
        uuid = profile.get("uuid", str(uuid_module.uuid4()))
        access_token = profile.get("minecraft_token", "0")
        minecraft_directory = str(instance.minecraft_dir)
        Path(minecraft_directory).mkdir(parents=True, exist_ok=True)
        print(f"\n[Launcher] Launching Minecraft {version} ({mod_loader}) as {username}...")
        java_exe = resolve_java_for_instance(instance, version, log_fn=print)
        print(f"[Java] Using: {java_exe}")
        if not ram.endswith('G') and not ram.endswith('M'):
            ram = f"{ram}G"
        jvm_args = memory_jvm_args(instance, ram) + extra_jvm_args(instance) + native_library_jvm_args(instance)
        options = {
            'username': username,
            'uuid': uuid,
            'token': access_token,
            'executablePath': java_exe,
            'jvmArguments': jvm_args,
            'launcherName': 'OrangLauncher',
            'launcherVersion': CURRENT_VERSION
        }
        if instance.opt("demo", False):
            options['demo'] = True
        print(f"[Launcher] Preparing {version}...")

        # Handle mod loaders
        if mod_loader and mod_loader.lower() != "vanilla" and mod_loader.lower() != "none":
            if instance.installed_version_id and instance.installed_version_id not in ['Latest', 'N/A', '']:
                local_versions_dir = Path(minecraft_directory) / "versions" / instance.installed_version_id
                version_exists = local_versions_dir.exists() and (local_versions_dir / f"{instance.installed_version_id}.json").exists()
                if version_exists:
                    version = instance.installed_version_id
                    print(f"[Launcher] Using installed version: {version}")
        print(f"[Launcher] Installing Minecraft {version}...")
        minecraft_launcher_lib.install.install_minecraft_version(version, minecraft_directory)
        print(f"[Launcher] Starting Minecraft...")
        try:
            apply_video_options(instance)
        except Exception:
            pass
        command = minecraft_launcher_lib.command.get_minecraft_command(version, minecraft_directory, options)
        command = strip_server_blocklist(command, instance, instance.version, log_fn=print)
        command = [arg for arg in command if arg != "--sun-misc-unsafe-memory-access=allow"]
        try:
            lwjgl_override = resolve_lwjgl_override(instance, minecraft_directory, version, log_fn=print)
            if lwjgl_override:
                command = LwjglManager.rewrite_command(command, lwjgl_override["jars"], lwjgl_override["natives_dir"])
        except Exception as e:
            print(f"[LWJGL] override failed, using stock libraries: {e}")
        launch_env = build_launch_env(instance)
        command = apply_wrapper_command(command, instance.opt("wrapper_cmd"))
        _launch_start = time.time()
        mc_process = subprocess.Popen(
            command,
            cwd=minecraft_directory,
            env=launch_env
        )
        
        print(f"\n[Launcher] Game is running. Press Ctrl+C to detach...")
        try:
            exit_code = mc_process.wait()
        except KeyboardInterrupt:
            print("\n[Launcher] Detaching from game process...")
            exit_code = 0
        elapsed = int(time.time() - _launch_start)
        if elapsed > 5:
            instance.play_time = (instance.play_time or 0) + elapsed
            instance.last_played = datetime.now().isoformat()
        print(f"[Launcher] Minecraft exited with code {exit_code}")
        return True
    except Exception as e:
        print(f"[ERROR] Launch failed: {e}")
        traceback.print_exc()
        return False
def terminal_main():
    try:
        print("\n" + "="*50)
        print("OrangLauncher - Joke Mode, Less ram :>")
        print("="*50 + "\n")
        instance_manager = get_instance_manager()
        if not instance_manager.instances:
            print("[ERROR] No instances found!")
            return
        instances_list = sorted(instance_manager.instances.values(), key=lambda inst: inst.name.lower())
        print("Available Instances:")
        for i, inst in enumerate(instances_list, 1):
            print(f"  {i}. {inst.name} (MC {inst.version}, {inst.mod_loader})")
        while True:
            try:
                choice = input(f"\nSelect instance (1-{len(instances_list)}): ").strip()
                idx = int(choice) - 1
                if 0 <= idx < len(instances_list):
                    selected_instance = instances_list[idx]
                    break
                else:
                    print(f"Invalid choice. Please select 1-{len(instances_list)}")
            except ValueError:
                print(f"Invalid input. Please enter a number 1-{len(instances_list)}")
        profiles = load_profiles()
        if not profiles:
            print("\n[ERROR] No game profiles found yk! Please add a profile in GUI mode first you linuxer. Sorry no actual creation of profiles yet.")
            return
        print(f"\nAvailable Profiles for your idk what:")
        for i, profile in enumerate(profiles, 1):
            profile_type = profile.get("type", "unknown")
            username = profile.get("username", "Unknown")
            if profile_type == "offline":
                print(f"  {i}. {username} (Offline)")
            elif profile_type == "microsoft":
                print(f"  {i}. {username} (Microsoft)")
            else:
                print(f"  {i}. {username}")
        while True:
            try:
                choice = input(f"\nSelect profile (1-{len(profiles)}): ").strip()
                idx = int(choice) - 1
                if 0 <= idx < len(profiles):
                    selected_profile = profiles[idx]
                    break
                else:
                    print(f"Invalid choice. Please select 1-{len(profiles)}")
            except ValueError:
                print(f"Invalid input. Please enter a number 1-{len(profiles)}")
        # Get RAM for me I want 64gb plz, I only have 16 GB now :>
        # I have now 32 again, yayyy and yay works halfway..
        ram_input = input("\nEnter RAM amount (default: 4G): ").strip()
        ram = ram_input if ram_input else "4G"
        print(f"\nLaunching {selected_instance.name} as {selected_profile.get('username')}...")
        terminal_launch_game(selected_instance, selected_profile, ram)
    except KeyboardInterrupt:
        print("\n[Launcher] Cancelled by user")
        sys.exit(0)
    except Exception as e:
        print(f"[ERROR] Terminal mode error: {e}")
        traceback.print_exc()
        sys.exit(1)

def _system_desktop_file():
    for base in ("/usr/share/applications", "/usr/local/share/applications", "/var/lib/flatpak/exports/share/applications"):
        candidate = Path(base) / "oranglauncher.desktop"
        if candidate.exists():
            return candidate
    return None


def _launcher_exec_command():
    compiled = getattr(sys, "frozen", False) or "__compiled__" in globals()
    wrapper = shutil.which("oranglauncher")
    if compiled:
        exe = Path(sys.executable).resolve()
        if wrapper and Path(wrapper).resolve() != exe:
            return f'"{wrapper}" %f'
        return f'"{exe}" %f'
    return f'"{sys.executable}" "{Path(__file__).resolve()}" %f'


def _register_mrpack_association():
    try:
        home = Path.home()
        apps_dir = home / ".local/share/applications"
        apps_dir.mkdir(parents=True, exist_ok=True)
        desktop_file = apps_dir / "oranglauncher.desktop"
        system_desktop = _system_desktop_file()
        if system_desktop is not None:
            if desktop_file.exists():
                try:
                    desktop_file.unlink()
                    subprocess.run(["update-desktop-database", str(apps_dir)], check=False, capture_output=True)
                except Exception:
                    pass
        else:
            icon_path = find_resource("oranglauncher/images/orange.png")
            desktop = (
                "[Desktop Entry]\n"
                "Type=Application\n"
                "Name=OrangLauncher\n"
                "Comment=Modular Minecraft launcher\n"
                f"Exec={_launcher_exec_command()}\n"
                f"Icon={icon_path or 'oranglauncher'}\n"
                "Terminal=false\n"
                "Categories=Game;\n"
                "StartupWMClass=oranglauncher\n"
                "MimeType=application/x-modrinth-modpack+zip;application/x-orangpack;\n"
            )
            if not desktop_file.exists() or desktop_file.read_text() != desktop:
                desktop_file.write_text(desktop)
        mime_dir = home / ".local/share/mime/packages"
        mime_dir.mkdir(parents=True, exist_ok=True)
        mime_xml = (
            '<?xml version="1.0" encoding="UTF-8"?>\n'
            '<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">\n'
            '  <mime-type type="application/x-modrinth-modpack+zip">\n'
            '    <comment>Modrinth Modpack</comment>\n'
            '    <glob pattern="*.mrpack"/>\n'
            '  </mime-type>\n'
            '</mime-info>\n'
        )
        mime_file = mime_dir / "oranglauncher-mrpack.xml"
        if not mime_file.exists() or mime_file.read_text() != mime_xml:
            mime_file.write_text(mime_xml)
            subprocess.run(["update-mime-database", str(home / ".local/share/mime")],
                           check=False, capture_output=True)
            subprocess.run(["update-desktop-database", str(apps_dir)],
                           check=False, capture_output=True)
        subprocess.run(["xdg-mime", "default", "oranglauncher.desktop",
                        "application/x-modrinth-modpack+zip"],
                       check=False, capture_output=True)
        orang_xml = (
            '<?xml version="1.0" encoding="UTF-8"?>\n'
            '<mime-info xmlns="http://www.freedesktop.org/standards/shared-mime-info">\n'
            '  <mime-type type="application/x-orangpack">\n'
            '    <comment>OrangLauncher Modpack</comment>\n'
            '    <glob pattern="*.orangpack"/>\n'
            '  </mime-type>\n'
            '</mime-info>\n'
        )
        orang_file = mime_dir / "oranglauncher-orangpack.xml"
        if not orang_file.exists() or orang_file.read_text() != orang_xml:
            orang_file.write_text(orang_xml)
            subprocess.run(["update-mime-database", str(home / ".local/share/mime")],
                           check=False, capture_output=True)
            subprocess.run(["update-desktop-database", str(apps_dir)],
                           check=False, capture_output=True)
        subprocess.run(["xdg-mime", "default", "oranglauncher.desktop", "application/x-orangpack"],
                       check=False, capture_output=True)
    except Exception as e:
        print(f"[setup] mrpack association failed: {e}")

try:
    from PySide6 import QtCore, QtGui, QtWidgets
    from PySide6.QtCore import Qt, Signal, QObject, QTimer, QSize, QPointF, QRect
    QT_AVAILABLE = True
except Exception:
    QT_AVAILABLE = False
    class _QtPlaceholder:
        def __init__(self, *a, **k):
            pass
    class _QtNamespace:
        def __getattr__(self, name):
            return _QtPlaceholder
    QtCore = QtGui = QtWidgets = _QtNamespace()
    Qt = _QtNamespace()
    QObject = _QtPlaceholder
    QTimer = _QtPlaceholder
    QSize = _QtPlaceholder
    def Signal(*a, **k):
        return None
QT_ACTIVE = [False]
_QT_MAIN_THREAD = [None]
_QT_APP_REF = [None]


def _qt_is_main_thread():
    return threading.get_ident() == _QT_MAIN_THREAD[0]


def _trim_memory():
    try:
        import gc
        gc.collect()
        if platform.system() == "Linux":
            import ctypes
            ctypes.CDLL("libc.so.6").malloc_trim(0)
    except Exception:
        pass


class _QtInvoker(QObject):
    call = Signal(object)

    def __init__(self):
        super().__init__()
        self.call.connect(self._run, Qt.QueuedConnection)

    def _run(self, fn):
        try:
            fn()
        except Exception as e:
            print(f"[Qt] deferred call failed: {e}")
            traceback.print_exc()
_QT_INVOKER = [None]

def _qt_later(fn, ms=0):
    if not QT_ACTIVE[0]:
        return
    if _qt_is_main_thread():
        if ms <= 0:
            QTimer.singleShot(0, fn)
        else:
            QTimer.singleShot(int(ms), fn)
        return
    inv = _QT_INVOKER[0]
    if inv is None:
        return
    if ms <= 0:
        inv.call.emit(fn)
    else:
        inv.call.emit(lambda: QTimer.singleShot(int(ms), fn))


def _qt_blocking(fn):
    if _qt_is_main_thread():
        return fn()
    box = {}
    done = threading.Event()

    def run():
        try:
            box["value"] = fn()
        except Exception as e:
            box["error"] = e
        finally:
            done.set()
    _qt_later(run)
    done.wait()
    if "error" in box:
        raise box["error"]
    return box.get("value")


class _Var:
    def __init__(self, value=None):
        self._value = value
        self._traces = []

    def get(self):
        return self._value

    def set(self, value):
        self._value = value
        for cb in list(self._traces):
            try:
                cb()
            except Exception as e:
                print(f"[Var] trace failed: {e}")

    def trace_add(self, mode, callback):
        self._traces.append(lambda: callback(None, None, mode))
        return callback
    def trace_remove(self, mode, name):
        pass


class _LabelAdapter:
    def __init__(self, label):
        self.label = label
        self.master = label

    def config(self, **kw):
        if "text" in kw:
            text = str(kw["text"])
            _qt_later(lambda: self.label.setText(text))

    configure = config

    def cget(self, key):
        if key == "text":
            return self.label.text()
        return None
    def winfo_exists(self):
        return True


class _ProgressAdapter:
    def __init__(self, bar):
        self.bar = bar

    def config(self, **kw):
        mode = kw.get("mode")
        if mode == "indeterminate":
            _qt_later(lambda: self.bar.setRange(0, 0))
        elif mode == "determinate":
            _qt_later(lambda: (self.bar.setRange(0, 100), self.bar.setValue(0)))

    configure = config

    def start(self, *a):
        _qt_later(lambda: self.bar.setRange(0, 0))

    def stop(self):
        _qt_later(lambda: (self.bar.setRange(0, 100), self.bar.setValue(0)))


class _QtMessageBox:
    def _parent(self, kw):
        p = kw.get("parent")
        if isinstance(p, QtWidgets.QWidget):
            return p
        return QtWidgets.QApplication.activeWindow()

    def _show(self, icon, title, message, **kw):
        def run():
            box = QtWidgets.QMessageBox(self._parent(kw))
            box.setIcon(icon)
            box.setWindowTitle(str(title))
            box.setText(str(message))
            box.setStandardButtons(QtWidgets.QMessageBox.Ok)
            box.exec()
            return "ok"
        return _qt_blocking(run)

    def showinfo(self, title="", message="", **kw):
        return self._show(QtWidgets.QMessageBox.Information, title, message, **kw)

    def showwarning(self, title="", message="", **kw):
        return self._show(QtWidgets.QMessageBox.Warning, title, message, **kw)

    def showerror(self, title="", message="", **kw):
        return self._show(QtWidgets.QMessageBox.Critical, title, message, **kw)

    def _ask(self, title, message, buttons, default, mapping, **kw):
        def run():
            box = QtWidgets.QMessageBox(self._parent(kw))
            box.setIcon(QtWidgets.QMessageBox.Question)
            box.setWindowTitle(str(title))
            box.setText(str(message))
            box.setStandardButtons(buttons)
            box.setDefaultButton(default)
            return mapping.get(box.exec())
        return _qt_blocking(run)

    def askyesno(self, title="", message="", **kw):
        return self._ask(title, message, QtWidgets.QMessageBox.Yes | QtWidgets.QMessageBox.No, QtWidgets.QMessageBox.Yes,
                         {QtWidgets.QMessageBox.Yes: True, QtWidgets.QMessageBox.No: False}, **kw)

    def askquestion(self, title="", message="", **kw):
        return "yes" if self.askyesno(title, message, **kw) else "no"

    def askokcancel(self, title="", message="", **kw):
        return self._ask(title, message, QtWidgets.QMessageBox.Ok | QtWidgets.QMessageBox.Cancel, QtWidgets.QMessageBox.Ok,
                         {QtWidgets.QMessageBox.Ok: True, QtWidgets.QMessageBox.Cancel: False}, **kw)

    def askretrycancel(self, title="", message="", **kw):
        return self._ask(title, message, QtWidgets.QMessageBox.Retry | QtWidgets.QMessageBox.Cancel, QtWidgets.QMessageBox.Retry,
                         {QtWidgets.QMessageBox.Retry: True, QtWidgets.QMessageBox.Cancel: False}, **kw)

    def askyesnocancel(self, title="", message="", **kw):
        return self._ask(title, message, QtWidgets.QMessageBox.Yes | QtWidgets.QMessageBox.No | QtWidgets.QMessageBox.Cancel,
                         QtWidgets.QMessageBox.Yes,
                         {QtWidgets.QMessageBox.Yes: True, QtWidgets.QMessageBox.No: False, QtWidgets.QMessageBox.Cancel: None}, **kw)


def _qt_filter_string(filetypes):
    parts = []
    for item in filetypes or []:
        try:
            label, patterns = item[0], item[1]
        except Exception:
            continue
        if isinstance(patterns, (list, tuple)):
            patterns = " ".join(patterns)
        parts.append(f"{label} ({patterns})")
    return ";;".join(parts) if parts else "All files (*)"


class _QtFileDialog:
    def _parent(self):
        return QtWidgets.QApplication.activeWindow()

    def _start_dir(self, kw):
        d = kw.get("initialdir") or str(Path.home())
        f = kw.get("initialfile")
        return os.path.join(d, f) if f else d

    def askopenfilename(self, **kw):
        def run():
            path, _ = QtWidgets.QFileDialog.getOpenFileName(self._parent(), kw.get("title") or "Open", self._start_dir(kw), _qt_filter_string(kw.get("filetypes")))
            return path or ""
        return _qt_blocking(run)

    def askopenfilenames(self, **kw):
        def run():
            paths, _ = QtWidgets.QFileDialog.getOpenFileNames(self._parent(), kw.get("title") or "Open", self._start_dir(kw), _qt_filter_string(kw.get("filetypes")))
            return tuple(paths or ())
        return _qt_blocking(run)

    def asksaveasfilename(self, **kw):
        def run():
            path, _ = QtWidgets.QFileDialog.getSaveFileName(self._parent(), kw.get("title") or "Save", self._start_dir(kw), _qt_filter_string(kw.get("filetypes")))
            if path and kw.get("defaultextension") and not os.path.splitext(path)[1]:
                path += kw["defaultextension"]
            return path or ""
        return _qt_blocking(run)

    def askdirectory(self, **kw):
        def run():
            return QtWidgets.QFileDialog.getExistingDirectory(self._parent(), kw.get("title") or "Select folder", kw.get("initialdir") or str(Path.home())) or ""
        return _qt_blocking(run)

messagebox = _QtMessageBox()
filedialog = _QtFileDialog()

def _qt_askstring(title, prompt, parent=None, launcher=None, initialvalue=None, **kw):
    def run():
        p = parent if isinstance(parent, QtWidgets.QWidget) else QtWidgets.QApplication.activeWindow()
        dlg = QtWidgets.QInputDialog(p)
        dlg.setWindowTitle(str(title))
        dlg.setLabelText(str(prompt))
        dlg.setInputMode(QtWidgets.QInputDialog.TextInput)
        if initialvalue:
            dlg.setTextValue(str(initialvalue))
        dlg.resize(480, dlg.sizeHint().height())
        if dlg.exec() == QtWidgets.QDialog.Accepted:
            return dlg.textValue()
        return None
    return _qt_blocking(run)

# remove whitespace from mojang logo, i gave this to slopilot
def _mojang_logo_pixmap(width=240):
    path = find_resource("oranglauncher/images/mojang.png")
    if not path:
        return None
    try:
        img = _qimage_rgba(path)
        w, h = img.width(), img.height()
        buf = bytes(img.constBits())
        has_alpha = any(buf[i] < 255 for i in range(3, len(buf), 4))
        x0, y0, x1, y1 = w, h, -1, -1
        for y in range(h):
            row = y * w * 4
            for x in range(w):
                i = row + x * 4
                if has_alpha:
                    keep = buf[i + 3] > 0
                else:
                    keep = (buf[i] * 299 + buf[i + 1] * 587 + buf[i + 2] * 114) // 1000 < 245
                if keep:
                    if x < x0: x0 = x
                    if x > x1: x1 = x
                    if y < y0: y0 = y
                    if y > y1: y1 = y
        if x1 >= x0 and y1 >= y0:
            pad = int((x1 + 1 - x0) * 0.05)
            img = img.copy(max(0, x0 - pad), max(0, y0 - pad), min(w, x1 + 1 + pad) - max(0, x0 - pad), min(h, y1 + 1 + pad) - max(0, y0 - pad))
        img = img.scaledToWidth(width, Qt.SmoothTransformation).convertToFormat(QtGui.QImage.Format_RGBA8888)
        launcher = _QT_APP_REF[0]
        if launcher is not None and launcher.theme.c("is_dark", True):
            fg = QtGui.QColor(launcher.theme.c("fg_primary", "#ffffff"))
            fr, fg_, fb = fg.red(), fg.green(), fg.blue()
            pix = bytearray(bytes(img.constBits()))
            for i in range(0, len(pix), 4):
                r, g, b, a = pix[i], pix[i + 1], pix[i + 2], pix[i + 3]
                if a and max(r, g, b) < 110 and abs(r - g) < 40 and abs(g - b) < 40:
                    pix[i], pix[i + 1], pix[i + 2] = fr, fg_, fb
            img = QtGui.QImage(bytes(pix), img.width(), img.height(), img.width() * 4, QtGui.QImage.Format_RGBA8888).copy()
        return QtGui.QPixmap.fromImage(img)
    except Exception as e:
        print(f"[Accounts] mojang logo failed: {e}")
        return None


def _qt_ask_profile_type(parent=None):
    def run():
        p = parent if isinstance(parent, QtWidgets.QWidget) else QtWidgets.QApplication.activeWindow()
        dlg = QtWidgets.QDialog(p)
        dlg.setWindowTitle(_qt_t("QT_ADD_ACCOUNT", "Add account"))
        lay = QtWidgets.QVBoxLayout(dlg)
        lay.setSpacing(12)
        logo = _mojang_logo_pixmap(240)
        if logo is not None:
            frame = QtWidgets.QLabel()
            frame.setPixmap(logo)
            frame.setAlignment(Qt.AlignCenter)
            frame.setContentsMargins(0, 8, 0, 8)
            lay.addWidget(frame, 0, Qt.AlignHCenter)
        lay.addWidget(_qt_label(_qt_t("QT_ADD_ACCOUNT_QUESTION", "Which kind of account do you want to add?"), "h3"))
        lay.addWidget(_qt_label(_qt_t("QT_ADD_ACCOUNT_MS_DESC", "Microsoft: sign in with the account that owns Minecraft."), "muted", wrap=True))
        lay.addWidget(_qt_label(_qt_t("QT_ADD_ACCOUNT_OFFLINE_DESC", "Offline: pick a username (singleplayer and offline-mode servers only)."), "muted", wrap=True))
        result = {"value": None}
        row = QtWidgets.QHBoxLayout()
        row.addStretch(1)

        def choose(kind):
            result["value"] = kind
            dlg.accept()
        launcher = _QT_APP_REF[0]
        row.addWidget(_qt_button("Microsoft", lambda: choose("microsoft"), kind="accent", icon="microsoft", launcher=launcher))
        row.addWidget(_qt_button(_qt_t("QT_OFFLINE", "Offline"), lambda: choose("offline"), icon="offline", launcher=launcher))
        row.addWidget(_qt_button(_qt_t("QT_CANCEL", "Cancel"), dlg.reject))
        lay.addLayout(row)
        dlg.setMinimumWidth(420)
        dlg.exec()
        return result["value"]
    return _qt_blocking(run)


class LoginCancelled(Exception):
    pass

    # I know that this is illegal but I don't have my azure anymore because it got deleted.
def _ms_oauth_url():
    return ("https://login.live.com/oauth20_authorize.srf"
            f"?client_id={CLIENT_ID}"
            f"&redirect_uri={REDIRECT_URI}"
            "&response_type=code"
            f"&scope={SCOPE}")


def _qt_webengine():
    try:
        from PySide6 import QtWebEngineWidgets, QtWebEngineCore
        return QtWebEngineWidgets, QtWebEngineCore
    except Exception as e:
        print(f"[Accounts] embedded browser unavailable: {e}")
        return None, None


def _ms_device_code_start():
    r = requests.post("https://login.live.com/oauth20_connect.srf",
                      data={"client_id": DEVICE_CLIENT_ID, "scope": SCOPE, "response_type": "device_code"}, timeout=20)
    r.raise_for_status()
    data = r.json()
    if "device_code" not in data:
        raise Exception(data.get("error_description") or data.get("error") or "device code request failed")
    return data


def _ms_log(msg):
    try:
        print(f"[Accounts] {msg}", flush=True)
    except Exception:
        pass


def _ms_device_code_poll(device_code):
    r = requests.post("https://login.live.com/oauth20_token.srf", data={"client_id": DEVICE_CLIENT_ID, "device_code": device_code, "grant_type": "urn:ietf:params:oauth:grant-type:device_code"}, timeout=20)
    try:
        data = r.json()
    except Exception:
        data = {}
    if r.ok and data.get("access_token"):
        _ms_log("device code approved, got tokens")
        return "ok", data
    err = data.get("error", "")
    if err not in ("authorization_pending",):
        _ms_log(f"device poll: http {r.status_code} error={err!r} {data.get('error_description', '')[:120]}")
    if err in ("authorization_pending", "slow_down", ""):
        return "pending", data
    return "error", data


_XSTS_ERRORS = {
    "2148916233": "This Microsoft account has no Xbox profile. Sign in once at https://www.xbox.com to create one, then try again.",
    "2148916235": "Xbox Live is not available in your country or region.",
    "2148916236": "This account needs adult verification on xbox.com (South Korea).",
    "2148916237": "This account needs adult verification on xbox.com (South Korea).",
    "2148916238": "This is a child account. Add it to a Microsoft family group at https://account.microsoft.com/family first.",
}


def _ms_xbox_authenticate(microsoft_token):
    last = None
    for ticket in (microsoft_token, f"t={microsoft_token}", f"d={microsoft_token}"):
        r = requests.post("https://user.auth.xboxlive.com/user/authenticate", json={"Properties": {"AuthMethod": "RPS", "SiteName": "user.auth.xboxlive.com", "RpsTicket": ticket}, "RelyingParty": "http://auth.xboxlive.com", "TokenType": "JWT"}, timeout=30)
        if r.ok:
            try:
                return r.json()["Token"]
            except Exception:
                pass
        last = r
    raise Exception("Failed to get Xbox Live token: " + (last.text if last is not None else "?"))


def _ms_complete_with_tokens(tokens):
    microsoft_token = tokens["access_token"]
    microsoft_refresh_token = tokens.get("refresh_token", "")
    _ms_log("exchanging Microsoft token for Xbox Live token")
    xbl_token = _ms_xbox_authenticate(microsoft_token)
    _ms_log("got XBL token, requesting XSTS")
    r = requests.post("https://xsts.auth.xboxlive.com/xsts/authorize", json={"Properties": {"SandboxId": "RETAIL", "UserTokens": [xbl_token]}, "RelyingParty": "rp://api.minecraftservices.com/", "TokenType": "JWT"}, timeout=30)
    if not r.ok:
        try:
            xerr = str(r.json().get("XErr", ""))
        except Exception:
            xerr = ""
        raise Exception(_XSTS_ERRORS.get(xerr) or ("Failed to get XSTS token: " + r.text))
    xsts_userhash = r.json()["DisplayClaims"]["xui"][0]["uhs"]
    xsts_token = r.json()["Token"]
    _ms_log("got XSTS token, logging into Minecraft services")
    r = requests.post("https://api.minecraftservices.com/authentication/login_with_xbox", json={"identityToken": f"XBL3.0 x={xsts_userhash};{xsts_token}"}, timeout=30)
    if not r.ok:
        raise Exception("Failed to get Minecraft token: " + r.text)
    minecraft_token = r.json()["access_token"]
    _ms_log("got Minecraft token, fetching profile")
    r = _http_session.get("https://api.minecraftservices.com/minecraft/profile", headers={"Authorization": f"Bearer {minecraft_token}"}, timeout=30)
    if r.status_code == 404:
        raise Exception("This Microsoft account does not own Minecraft: Java Edition (no profile found).")
    if not r.ok:
        raise Exception("Failed to get Minecraft profile: " + r.text)
    profile_data = r.json()
    return {"microsoft_refresh_token": microsoft_refresh_token, "minecraft_token": minecraft_token,
            "username": profile_data["name"], "uuid": profile_data["id"], "last_refresh": time.time()}

    # the emmbeded browser login form
class QtMicrosoftLoginDialog(QtWidgets.QDialog):
    def __init__(self, parent=None, mode="embedded"):
        super().__init__(parent)
        self.setWindowTitle(_qt_t("QT_MS_LOGIN_TITLE", "Sign in with Microsoft"))
        self.resize(560, 720)
        self.auth_code = None
        self.tokens = None
        self.error = None
        self.view = None
        self.profile = None
        self._device = None
        self._poll_token = 0
        self._relogin = 0
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(12, 12, 12, 12)
        lay.setSpacing(8)
        self.stack = QtWidgets.QStackedWidget()
        lay.addWidget(self.stack, 1)
        self.embedded_page = QtWidgets.QWidget()
        el = QtWidgets.QVBoxLayout(self.embedded_page)
        el.setContentsMargins(0, 0, 0, 0)
        el.setSpacing(6)
        self.embedded_status = _qt_label(_qt_t("QT_MS_LOGIN_LOADING", "Loading the Microsoft sign-in page..."), "muted", wrap=True)
        el.addWidget(self.embedded_status)
        self.web_host = QtWidgets.QVBoxLayout()
        el.addLayout(self.web_host, 1)
        self.stack.addWidget(self.embedded_page)
        self.browser_page = QtWidgets.QWidget()
        bl = QtWidgets.QVBoxLayout(self.browser_page)
        bl.setContentsMargins(0, 0, 0, 0)
        bl.setSpacing(10)
        bl.addWidget(_qt_label(_qt_t("QT_MS_BROWSER_TITLE", "Sign in with your web browser"), "h2"))
        bl.addWidget(_qt_label(_qt_t("QT_MS_DEVICE_STEPS", "Your browser was opened on microsoft.com/link with this code already filled in. Sign in there and come back - the launcher finishes the login by itself, nothing to copy."), None, wrap=True))
        self.code_label = QtWidgets.QLabel("…")
        self.code_label.setAlignment(Qt.AlignCenter)
        self.code_label.setTextInteractionFlags(Qt.TextSelectableByMouse)
        self.code_label.setStyleSheet("font-size: 26pt; font-weight: 700; letter-spacing: 6px; padding: 10px;")
        bl.addWidget(self.code_label)
        self.device_status = _qt_label(_qt_t("QT_MS_DEVICE_WAITING", "Waiting for you to sign in..."), "muted", wrap=True)
        bl.addWidget(self.device_status)
        brow = QtWidgets.QHBoxLayout()
        brow.addWidget(_qt_button(_qt_t("QT_MS_OPEN_BROWSER_AGAIN", "Open the browser again"), self._open_device_browser))
        brow.addWidget(_qt_button(_qt_t("QT_COPY_CODE", "Copy code"), lambda: QtWidgets.QApplication.clipboard().setText(self.code_label.text())))
        brow.addStretch(1)
        bl.addLayout(brow)
        bl.addWidget(_qt_label(_qt_t("QT_MS_DEVICE_HINT", "If the browser did not open, go to https://www.microsoft.com/link yourself and type the code."), "hint", wrap=True))
        bl.addStretch(1)
        self.stack.addWidget(self.browser_page)
        foot = QtWidgets.QHBoxLayout()
        self.switch_btn = _qt_button(_qt_t("QT_MS_USE_BROWSER", "Use the browser instead"), self.use_browser, kind="flat")
        foot.addWidget(self.switch_btn)
        self.embed_btn = _qt_button(_qt_t("QT_MS_USE_EMBEDDED", "Use the embedded sign-in instead"), self.use_embedded, kind="flat")
        foot.addWidget(self.embed_btn)
        foot.addStretch(1)
        foot.addWidget(_qt_button(_qt_t("QT_CANCEL", "Cancel"), self.reject))
        lay.addLayout(foot)
        if mode == "browser":
            self.use_browser()
        else:
            self.use_embedded()

    # I still want the localhost redirect :|
    def _verification_url(self):
        if not self._device:
            return "https://www.microsoft.com/link"
        base = self._device.get("verification_uri") or "https://www.microsoft.com/link"
        return f"{base}?otc={self._device.get('user_code', '')}"

    def _open_device_browser(self):
        open_with_browser(self._verification_url())

    def use_browser(self):
        self._drop_view()
        self.stack.setCurrentWidget(self.browser_page)
        self.switch_btn.setVisible(False)
        self.embed_btn.setVisible(True)
        self.resize(560, 420)
        if self._device is None:
            self._start_device_flow()

    def _start_device_flow(self):
        self._poll_token += 1
        token = self._poll_token
        self.device_status.setText(_qt_t("QT_MS_DEVICE_REQUESTING", "Requesting a sign-in code..."))

        def done(data):
            if token != self._poll_token:
                return
            self._device = data
            self._device_started = time.time()
            _ms_log(f"device code {data.get('user_code')} issued, polling every {data.get('interval', 5)}s")
            self.code_label.setText(data.get("user_code", "?"))
            try:
                QtWidgets.QApplication.clipboard().setText(data.get("user_code", ""))
            except Exception:
                pass
            self.device_status.setText(_qt_t("QT_MS_DEVICE_WAITING", "Waiting for you to sign in..."))
            self._open_device_browser()
            self._schedule_poll(token, int(data.get("interval", 5)))

        def fail(err):
            if token != self._poll_token:
                return
            self.device_status.setText(_qt_t("QT_MS_DEVICE_FAIL", "Could not start the browser sign-in: {error}").format(error=err))
        _qt_run_bg(_ms_device_code_start, done, fail)

    def _schedule_poll(self, token, interval):
        QTimer.singleShot(max(2, interval) * 1000, lambda: self._poll(token))

    def _poll(self, token):
        if token != self._poll_token or self._device is None or not self.isVisible():
            return
        device_code = self._device["device_code"]

        def done(result):
            if token != self._poll_token:
                return
            state, data = result
            if state == "ok":
                self.tokens = data
                self.accept()
            elif state == "pending":
                if time.time() - getattr(self, "_device_started", time.time()) > 20:
                    self.device_status.setText(_qt_t("QT_MS_DEVICE_STILL_WAITING", "Still waiting. If the browser ended up on account.microsoft.com without asking about the code, open https://www.microsoft.com/link again, type the code above and confirm the Minecraft sign-in."))
                self._schedule_poll(token, int(self._device.get("interval", 5)) + (5 if data.get("error") == "slow_down" else 0))
            else:
                self.error = data.get("error_description") or data.get("error") or "?"
                self.device_status.setText(_qt_t("QT_MS_DEVICE_FAIL", "Could not start the browser sign-in: {error}").format(error=self.error))
                if data.get("error") == "expired_token":
                    self._device = None
                    self._start_device_flow()
        _qt_run_bg(lambda: _ms_device_code_poll(device_code), done, lambda e: self._schedule_poll(token, 10))

    def use_embedded(self):
        widgets, core = _qt_webengine()
        if widgets is None:
            self.embed_btn.setVisible(False)
            self.switch_btn.setVisible(False)
            self.use_browser()
            self.embed_btn.setVisible(False)
            messagebox.showwarning(_qt_t("QT_MS_LOGIN_TITLE", "Sign in with Microsoft"), _qt_t("QT_MS_NO_WEBENGINE", "The embedded browser (Qt WebEngine) is not installed, so the browser sign-in is used instead."), parent=self)
            return
        self._poll_token += 1
        self.stack.setCurrentWidget(self.embedded_page)
        self.switch_btn.setVisible(True)
        self.embed_btn.setVisible(False)
        self.resize(560, 720)
        if self.view is not None:
            return
        try:
            self.profile = core.QWebEngineProfile(self)
            self.profile.setHttpUserAgent("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36")
            dialog = self

            class _LoginPage(core.QWebEnginePage):
                def acceptNavigationRequest(self, url, nav_type, is_main_frame):
                    if is_main_frame and dialog._handle_nav(url.toString()):
                        return False
                    return super().acceptNavigationRequest(url, nav_type, is_main_frame)

                def createWindow(self, _type):
                    return self

            page = _LoginPage(self.profile, self)
            self.view = widgets.QWebEngineView()
            self.view.setPage(page)
            self.view.urlChanged.connect(self._on_url)
            self.view.loadFinished.connect(lambda ok: self.embedded_status.setText("" if ok else _qt_t("QT_MS_LOGIN_LOAD_FAIL", "The sign-in page could not be loaded. Check your connection or use the browser instead.")))
            self.web_host.addWidget(self.view, 1)
            self.view.load(QtCore.QUrl(_ms_oauth_url()))
        except Exception as e:
            print(f"[Accounts] embedded login failed: {e}")
            self._drop_view()
            self.use_browser()
            self.embed_btn.setVisible(False)

    def _drop_view(self):
        if self.view is not None:
            try:
                self.view.stop()
                self.view.setPage(None)
                self.web_host.removeWidget(self.view)
                self.view.deleteLater()
            except Exception:
                pass
            self.view = None

    def _handle_nav(self, text):
        low = text.lower()
        _ms_log("embedded nav: " + text.split("?", 1)[0])
        if low.startswith(REDIRECT_URI.lower()):
            code = _extract_code_from_redirect(text)
            if code:
                if self.auth_code is None:
                    self.auth_code = code
                    _qt_later(self.accept)
                return True
            query = urllib.parse.parse_qs(urllib.parse.urlsplit(text).query)
            if query.get("error"):
                self.error = (query.get("error_description") or query.get("error") or ["?"])[0]
                _qt_later(self.reject)
                return True
            return False
        host = urllib.parse.urlsplit(text).netloc.lower()
        if host in ("account.microsoft.com", "www.microsoft.com", "www.xbox.com", "www.minecraft.net") and self.view is not None and self._relogin < 3:
            self._relogin += 1
            self.embedded_status.setText(_qt_t("QT_MS_LOGIN_FINISHING", "Finishing the sign-in..."))
            _qt_later(lambda: self.view.load(QtCore.QUrl(_ms_oauth_url())) if self.view is not None else None)
            return True
        return False

    def _on_url(self, url):
        self._handle_nav(url.toString())

    def accept(self):
        _ms_log("login dialog accepted (%s)" % ("device tokens" if self.tokens else "auth code" if self.auth_code else "?"))
        super().accept()

    def done(self, result):
        self._poll_token += 1
        self._drop_view()
        super().done(result)


def _ms_token_flow_qt(mode="embedded"):
    def run():
        parent = QtWidgets.QApplication.activeWindow()
        dlg = QtMicrosoftLoginDialog(parent, mode=mode)
        dlg.exec()
        if dlg.tokens:
            return ("tokens", dlg.tokens)
        if dlg.auth_code:
            return ("code", dlg.auth_code)
        if dlg.error and dlg.result() == QtWidgets.QDialog.Accepted:
            raise Exception(dlg.error)
        return None
    result = _qt_blocking(run)
    if not result:
        raise LoginCancelled(_qt_t("QT_MS_LOGIN_CANCELLED", "Login cancelled"))
    kind, value = result
    if kind == "tokens":
        data = _ms_complete_with_tokens(value)
        data["ms_client_id"] = DEVICE_CLIENT_ID
        return data
    data = _complete_oauth_flow(value)
    data["ms_client_id"] = CLIENT_ID
    return data


def _qt_pixmap_from_path(path, size=None):
    try:
        pix = QtGui.QPixmap(str(path))
        if pix.isNull():
            return None
        if size:
            pix = pix.scaled(size[0], size[1], Qt.KeepAspectRatio, Qt.SmoothTransformation)
        return pix
    except Exception:
        return None


def _qt_pixmap_from_bytes(data, size=None):
    try:
        pix = QtGui.QPixmap()
        if not pix.loadFromData(data):
            return None
        if size:
            pix = pix.scaled(size[0], size[1], Qt.IgnoreAspectRatio, Qt.SmoothTransformation)
        return pix
    except Exception:
        return None


def _on_plasma():
    desk = (os.environ.get("XDG_CURRENT_DESKTOP", "") + ":" + os.environ.get("DESKTOP_SESSION", "")).lower()
    return "kde" in desk or "plasma" in desk


def _read_kdeglobals():
    candidates = [Path.home() / ".config" / "kdeglobals", Path("/etc/xdg/kdeglobals")]
    data = {}
    for path in candidates:
        if not path.exists():
            continue
        section = None
        try:
            for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
                line = line.strip()
                if not line or line.startswith("#"):
                    continue
                if line.startswith("[") and line.endswith("]"):
                    section = line[1:-1]
                    continue
                if "=" in line and section:
                    key, value = line.split("=", 1)
                    data.setdefault(section, {}).setdefault(key.strip(), value.strip())
        except Exception:
            continue
    return data


def _kde_rgb(value, fallback):
    try:
        parts = [int(x) for x in value.split(",")[:3]]
        if len(parts) == 3:
            return "#%02x%02x%02x" % tuple(max(0, min(255, p)) for p in parts)
    except Exception:
        pass
    return fallback


def _mix_hex(a, b, t):
    ca = QtGui.QColor(a)
    cb = QtGui.QColor(b)
    r = int(ca.red() + (cb.red() - ca.red()) * t)
    g = int(ca.green() + (cb.green() - ca.green()) * t)
    bl = int(ca.blue() + (cb.blue() - ca.blue()) * t)
    return QtGui.QColor(r, g, bl).name()


def _plasma_colors():
    kde = _read_kdeglobals()
    if not kde:
        return None
    win = kde.get("Colors:Window", {})
    view = kde.get("Colors:View", {})
    btn = kde.get("Colors:Button", {})
    sel = kde.get("Colors:Selection", {})
    bg = _kde_rgb(win.get("BackgroundNormal", ""), "#2a2e32")
    bg_alt = _kde_rgb(win.get("BackgroundAlternate", ""), bg)
    fg = _kde_rgb(win.get("ForegroundNormal", ""), "#fcfcfc")
    fg_inactive = _kde_rgb(win.get("ForegroundInactive", ""), "#a1a9b1")
    view_bg = _kde_rgb(view.get("BackgroundNormal", ""), "#1b1e20")
    button_bg = _kde_rgb(btn.get("BackgroundNormal", ""), "#31363b")
    focus = _kde_rgb(sel.get("DecorationFocus", ""), "#3daee9")
    accent = _kde_rgb(kde.get("General", {}).get("AccentColor", ""), focus)
    sel_bg = _kde_rgb(sel.get("BackgroundNormal", ""), accent)
    sel_fg = _kde_rgb(sel.get("ForegroundNormal", ""), "#fcfcfc")
    dark = QtGui.QColor(bg).lightness() < 128
    border = _mix_hex(bg, fg, 0.18)
    return {
        "bg_primary": bg,
        "bg_secondary": bg_alt if bg_alt != bg else _mix_hex(bg, view_bg, 0.5),
        "bg_tertiary": _mix_hex(bg, fg, 0.06),
        "bg_section": _mix_hex(bg, fg, 0.08),
        "bg_hover": _mix_hex(bg, fg, 0.14),
        "bg_pressed": _mix_hex(bg, fg, 0.04),
        "bg_input": view_bg,
        "fg_primary": fg,
        "fg_secondary": _mix_hex(fg, bg, 0.12),
        "fg_tertiary": fg_inactive,
        "fg_disabled": _mix_hex(fg, bg, 0.55),
        "accent_primary": accent,
        "accent_hover": _mix_hex(accent, fg, 0.2),
        "accent_pressed": _mix_hex(accent, bg, 0.2),
        "border": border,
        "scrollbar_thumb": _mix_hex(bg, fg, 0.3),
        "scrollbar_track": bg,
        "progress_bar": accent,
        "progress_track": _mix_hex(bg, fg, 0.1),
        "tab_selected": _mix_hex(bg, fg, 0.1),
        "tab_unselected": bg,
        "button_bg": button_bg,
        "button_fg": fg,
        "play_button_bg": accent,
        "play_button_fg": sel_fg,
        "play_button_hover": _mix_hex(accent, fg, 0.2),
        "play_button_pressed": _mix_hex(accent, bg, 0.2),
        "selection_bg": sel_bg,
        "selection_fg": sel_fg,
        "focus": focus,
        "is_dark": dark,
        "system": True,
    }


QT_THEME_SYSTEM = "System"
QT_THEME_OLED = "Dark Prism"


def _qt_check_icon_path(color, kind="check"):
    try:
        cache = Path.home() / ".cache" / "oranglauncher"
        cache.mkdir(parents=True, exist_ok=True)
        safe = color.strip("#")
        path = cache / f"{kind}_{safe}.png"
        if not path.exists():
            img = QtGui.QImage(32, 32, QtGui.QImage.Format_ARGB32)
            img.fill(Qt.transparent)
            painter = QtGui.QPainter(img)
            painter.setRenderHint(QtGui.QPainter.Antialiasing)
            if kind == "check":
                pen = QtGui.QPen(QtGui.QColor(color), 4)
                pen.setCapStyle(Qt.RoundCap)
                pen.setJoinStyle(Qt.RoundJoin)
                painter.setPen(pen)
                painter.drawPolyline([QtCore.QPointF(7, 16), QtCore.QPointF(13, 23), QtCore.QPointF(25, 9)])
            else:
                painter.setPen(Qt.NoPen)
                painter.setBrush(QtGui.QColor(color))
                painter.drawEllipse(QtCore.QRectF(10, 10, 12, 12))
            painter.end()
            img.save(str(path))
        return str(path).replace("\\", "/")
    except Exception:
        return ""


class QtTheme:
    def __init__(self, theme_manager):
        self.tm = theme_manager
        self.name = None
        self.colors = {}
        self.native = False

    def available(self):
        names = [QT_THEME_SYSTEM] if _on_plasma() else []
        names += [n for n in self.tm.get_available_themes() if n != QT_THEME_SYSTEM]
        return names

    def load(self, name):
        if name == QT_THEME_SYSTEM and not _on_plasma():
            name = QT_THEME_OLED
        if name == QT_THEME_SYSTEM:
            colors = _plasma_colors()
            if colors is None:
                colors = self._qt_default_colors()
            self.colors = colors
            self.native = True
            self.name = name
            self.tm.current_theme = name
            self.tm.theme_data = {"name": name, "colors": colors, "fonts": {"primary": "Segoe UI", "monospace": "monospace"}}
            return
        if not self.tm.load_theme(name):
            self.tm.load_theme("Arc")
        self.colors = dict(self.tm.theme_data.get("colors", {}))
        self.colors["selection_bg"] = self.colors.get("accent_primary")
        self.colors["selection_fg"] = "#ffffff"
        self.colors["is_dark"] = QtGui.QColor(self.colors.get("bg_primary", "#000")).lightness() < 128
        self.colors["system"] = False
        self.native = False
        self.name = self.tm.current_theme

    def _qt_default_colors(self):
        app = QtWidgets.QApplication.instance()
        pal = app.palette() if app else QtGui.QPalette()
        bg = pal.color(QtGui.QPalette.Window).name()
        fg = pal.color(QtGui.QPalette.WindowText).name()
        base = pal.color(QtGui.QPalette.Base).name()
        hl = pal.color(QtGui.QPalette.Highlight).name()
        hl_text = pal.color(QtGui.QPalette.HighlightedText).name()
        btn = pal.color(QtGui.QPalette.Button).name()
        dark = QtGui.QColor(bg).lightness() < 128
        return {
            "bg_primary": bg, "bg_secondary": _mix_hex(bg, base, 0.5), "bg_tertiary": _mix_hex(bg, fg, 0.06),
            "bg_section": _mix_hex(bg, fg, 0.08), "bg_hover": _mix_hex(bg, fg, 0.14), "bg_pressed": _mix_hex(bg, fg, 0.04),
            "bg_input": base, "fg_primary": fg, "fg_secondary": _mix_hex(fg, bg, 0.12), "fg_tertiary": _mix_hex(fg, bg, 0.35),
            "fg_disabled": _mix_hex(fg, bg, 0.55), "accent_primary": hl, "accent_hover": _mix_hex(hl, fg, 0.2),
            "accent_pressed": _mix_hex(hl, bg, 0.2), "border": _mix_hex(bg, fg, 0.18), "scrollbar_thumb": _mix_hex(bg, fg, 0.3),
            "scrollbar_track": bg, "progress_bar": hl, "progress_track": _mix_hex(bg, fg, 0.1), "tab_selected": _mix_hex(bg, fg, 0.1),
            "tab_unselected": bg, "button_bg": btn, "button_fg": fg, "play_button_bg": hl, "play_button_fg": hl_text,
            "play_button_hover": _mix_hex(hl, fg, 0.2), "play_button_pressed": _mix_hex(hl, bg, 0.2),
            "selection_bg": hl, "selection_fg": hl_text, "is_dark": dark, "system": True,
        }

    def c(self, key, default="#000000"):
        return self.colors.get(key, default)

    def palette(self):
        c = self.c
        pal = QtGui.QPalette()
        window = QtGui.QColor(c("bg_primary"))
        text = QtGui.QColor(c("fg_primary"))
        base = QtGui.QColor(c("bg_input"))
        button = QtGui.QColor(c("button_bg"))
        hl = QtGui.QColor(c("selection_bg", c("accent_primary")))
        hl_text = QtGui.QColor(c("selection_fg", "#ffffff"))
        disabled = QtGui.QColor(c("fg_disabled"))
        for group in (QtGui.QPalette.Active, QtGui.QPalette.Inactive, QtGui.QPalette.Disabled):
            pal.setColor(group, QtGui.QPalette.Window, window)
            pal.setColor(group, QtGui.QPalette.WindowText, text if group != QtGui.QPalette.Disabled else disabled)
            pal.setColor(group, QtGui.QPalette.Base, base)
            pal.setColor(group, QtGui.QPalette.AlternateBase, QtGui.QColor(c("bg_secondary")))
            pal.setColor(group, QtGui.QPalette.Text, text if group != QtGui.QPalette.Disabled else disabled)
            pal.setColor(group, QtGui.QPalette.Button, button)
            pal.setColor(group, QtGui.QPalette.ButtonText, QtGui.QColor(c("button_fg")) if group != QtGui.QPalette.Disabled else disabled)
            pal.setColor(group, QtGui.QPalette.Highlight, hl)
            pal.setColor(group, QtGui.QPalette.HighlightedText, hl_text)
            pal.setColor(group, QtGui.QPalette.ToolTipBase, QtGui.QColor(c("bg_tertiary")))
            pal.setColor(group, QtGui.QPalette.ToolTipText, text)
            pal.setColor(group, QtGui.QPalette.PlaceholderText, QtGui.QColor(c("fg_tertiary")))
            pal.setColor(group, QtGui.QPalette.Link, QtGui.QColor(c("accent_primary")))
            pal.setColor(group, QtGui.QPalette.Light, QtGui.QColor(c("bg_hover")))
            pal.setColor(group, QtGui.QPalette.Midlight, QtGui.QColor(c("bg_tertiary")))
            pal.setColor(group, QtGui.QPalette.Mid, QtGui.QColor(c("border")))
            pal.setColor(group, QtGui.QPalette.Dark, QtGui.QColor(c("bg_pressed")))
            pal.setColor(group, QtGui.QPalette.Shadow, QtGui.QColor(c("bg_secondary")))
        return pal

    def _object_stylesheet(self):
        c = self.c
        radius = "6px"
        card_bg = "transparent" if c("bg_primary").lower() == "#000000" else "rgba(128, 128, 128, 0.06)"
        return f"""
        QComboBox {{ combobox-popup: 0; }}
        QComboBox QAbstractItemView {{ padding: 4px; }}
        QComboBox QAbstractItemView::item {{ padding: 4px 8px; min-height: 22px; }}
        QComboBox QAbstractItemView {{ padding: 4px; }}
        QComboBox QAbstractItemView::item {{ padding: 4px 8px; min-height: 22px; }}
        QFrame#card {{ background: {card_bg}; border: 1px solid {c('border')}; border-radius: {radius}; }}
        QFrame#plain {{ background: transparent; border: none; }}
        QLabel#cardTitle {{ font-size: 13pt; font-weight: 600; color: {c('fg_primary')}; }}
        QLabel#h1 {{ font-size: 18pt; font-weight: 700; color: {c('fg_primary')}; }}
        QLabel#h2 {{ font-size: 14pt; font-weight: 600; color: {c('fg_primary')}; }}
        QLabel#h3 {{ font-size: 11pt; font-weight: 600; color: {c('fg_primary')}; }}
        QLabel#muted {{ color: {c('fg_tertiary')}; }}
        QLabel#hint {{ color: {c('fg_tertiary')}; font-size: 8pt; }}
        QLabel#accent {{ color: {c('accent_primary')}; font-weight: 600; }}
        QPushButton#accent {{ background: {c('accent_primary')}; color: {c('selection_fg', '#ffffff')}; border: 1px solid {c('accent_pressed')}; border-radius: 3px; padding: 5px 14px; font-weight: 600; }}
        QPushButton#accent:hover {{ background: {c('accent_hover')}; }}
        QPushButton#accent:pressed {{ background: {c('accent_pressed')}; }}
        QPushButton#accent:disabled {{ background: {c('bg_hover')}; color: {c('fg_disabled')}; border-color: {c('border')}; }}
        QPushButton#play {{ background: {c('play_button_bg')}; color: {c('play_button_fg')}; border: 1px solid {c('play_button_pressed')}; font-weight: 700; font-size: 11pt; padding: 7px 24px; border-radius: 3px; }}
        QPushButton#play:hover {{ background: {c('play_button_hover')}; }}
        QPushButton#play:pressed {{ background: {c('play_button_pressed')}; }}
        QPushButton#play:disabled {{ background: {c('bg_hover')}; color: {c('fg_disabled')}; }}
        QPushButton#danger {{ background: #b3261e; color: #ffffff; border: 1px solid #8e1d17; border-radius: 4px; padding: 5px 14px; }}
        QPushButton#danger:hover {{ background: #d13a31; }}
        QPushButton#flat {{ background: transparent; border: none; padding: 6px 10px; color: {c('fg_secondary')}; border-radius: 4px; }}
        QPushButton#flat:hover {{ background: {c('bg_hover')}; }}
        QPushButton#nav {{ background: transparent; border: none; text-align: left; padding: 9px 14px; color: {c('fg_secondary')}; font-size: 11pt; border-radius: {radius}; }}
        QPushButton#nav:hover {{ background: {c('bg_hover')}; color: {c('fg_primary')}; }}
        QPushButton#nav:checked {{ background: {c('bg_hover')}; color: {c('fg_primary')}; font-weight: 600; }}
        QPushButton#topnav {{ background: transparent; border: none; padding: 8px 18px; color: {c('fg_tertiary')}; font-size: 11pt; border-radius: {radius}; }}
        QPushButton#topnav:hover {{ background: {c('bg_hover')}; color: {c('fg_primary')}; }}
        QPushButton#topnav:checked {{ background: {c('tab_selected')}; color: {c('fg_primary')}; font-weight: 600; }}
        QFrame#instanceCard {{ background: {c('bg_secondary')}; border: 2px solid {c('border')}; border-radius: 10px; }}
        QFrame#instanceCard:hover {{ border-color: {c('bg_hover')}; }}
        QFrame#instanceCardSelected {{ background: {c('bg_tertiary')}; border: 2px solid {c('accent_primary')}; border-radius: 10px; }}
        QFrame#resultRow {{ background: {c('bg_secondary')}; border: 1px solid {c('border')}; border-radius: {radius}; }}
        QFrame#serverRow {{ background: {c('bg_secondary')}; border: 1px solid {c('border')}; border-radius: {radius}; }}
        QFrame#serverRowSelected {{ background: {c('bg_tertiary')}; border: 1px solid {c('accent_primary')}; border-radius: {radius}; }}
        QFrame#bottomBar {{ background: {c('bg_secondary')}; border-top: 1px solid {c('border')}; }}
        QFrame#sideNav {{ background: {c('bg_secondary')}; border-right: 1px solid {c('border')}; }}
        QFrame#topBar {{ background: {c('bg_secondary')}; border-bottom: 1px solid {c('border')}; }}
        QLabel#offline {{ background: #cc3333; color: #ffffff; padding: 2px 6px; border-radius: 4px; font-weight: 600; }}
        QScrollArea {{ border: none; background: transparent; }}
        QScrollArea > QWidget > QWidget {{ background: transparent; }}
        """

    def _widget_stylesheet(self):
        c = self.c
        radius = "6px"
        check = _qt_check_icon_path(c("selection_fg", "#ffffff"))
        dot = _qt_check_icon_path(c("selection_fg", "#ffffff"), "dot")
        return f"""
        QWidget {{ font-family: "Segoe UI", "Noto Sans", sans-serif; }}
        QMainWindow, QDialog {{ background: {c('bg_primary')}; }}
        QToolTip {{ background: {c('bg_tertiary')}; color: {c('fg_primary')}; border: 1px solid {c('border')}; padding: 4px; }}
        QPushButton {{ background: {c('button_bg')}; color: {c('button_fg')}; border: 1px solid {c('border')}; border-radius: {radius}; padding: 6px 14px; }}
        QPushButton:hover {{ background: {c('bg_hover')}; }}
        QPushButton:pressed {{ background: {c('bg_pressed')}; }}
        QPushButton:disabled {{ color: {c('fg_disabled')}; }}
        QLineEdit, QPlainTextEdit, QTextEdit, QSpinBox, QComboBox, QListWidget, QTreeWidget, QTableWidget, QTextBrowser {{ background: {c('bg_input')}; color: {c('fg_primary')}; border: 1px solid {c('border')}; border-radius: {radius}; padding: 4px 6px; selection-background-color: {c('selection_bg', c('accent_primary'))}; selection-color: {c('selection_fg', '#ffffff')}; }}
        QLineEdit:focus, QComboBox:focus, QPlainTextEdit:focus, QTextEdit:focus {{ border: 1px solid {c('accent_primary')}; }}
        QComboBox::drop-down {{ border: none; width: 22px; }}
        QComboBox QAbstractItemView {{ background: {c('bg_input')}; color: {c('fg_primary')}; selection-background-color: {c('selection_bg', c('accent_primary'))}; selection-color: {c('selection_fg', '#ffffff')}; border: 1px solid {c('border')}; }}
        QListWidget::item, QTreeWidget::item {{ padding: 3px; }}
        QListWidget::item:selected, QTreeWidget::item:selected, QTableWidget::item:selected {{ background: {c('selection_bg', c('accent_primary'))}; color: {c('selection_fg', '#ffffff')}; }}
        QHeaderView::section {{ background: {c('bg_tertiary')}; color: {c('fg_primary')}; border: none; border-bottom: 1px solid {c('border')}; padding: 5px; }}
        QTabWidget::pane {{ border: none; background: {c('bg_primary')}; }}
        QTabBar::tab {{ background: {c('tab_unselected')}; color: {c('fg_tertiary')}; padding: 7px 14px; border: 1px solid transparent; border-bottom: none; border-top-left-radius: {radius}; border-top-right-radius: {radius}; margin-right: 2px; }}
        QTabBar::tab:selected {{ background: {c('tab_selected')}; color: {c('fg_primary')}; border-color: {c('border')}; }}
        QTabBar::tab:hover {{ background: {c('bg_hover')}; color: {c('fg_primary')}; }}
        QProgressBar {{ background: {c('progress_track')}; border: none; border-radius: 4px; height: 8px; text-align: center; color: transparent; }}
        QProgressBar::chunk {{ background: {c('progress_bar')}; border-radius: 4px; }}
        QScrollBar:vertical {{ background: {c('scrollbar_track')}; width: 12px; margin: 0; border: none; }}
        QScrollBar::handle:vertical {{ background: {c('scrollbar_thumb')}; min-height: 24px; border-radius: 5px; margin: 2px; }}
        QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{ height: 0; }}
        QScrollBar:horizontal {{ background: {c('scrollbar_track')}; height: 12px; margin: 0; border: none; }}
        QScrollBar::handle:horizontal {{ background: {c('scrollbar_thumb')}; min-width: 24px; border-radius: 5px; margin: 2px; }}
        QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {{ width: 0; }}
        QCheckBox, QRadioButton {{ color: {c('fg_primary')}; spacing: 8px; }}
        QCheckBox::indicator, QRadioButton::indicator, QTreeView::indicator, QListView::indicator {{ width: 16px; height: 16px; border: 1px solid {c('scrollbar_thumb')}; background: {c('bg_input')}; }}
        QCheckBox::indicator, QTreeView::indicator, QListView::indicator {{ border-radius: 3px; }}
        QRadioButton::indicator {{ border-radius: 8px; }}
        QCheckBox::indicator:hover, QRadioButton::indicator:hover {{ border-color: {c('accent_primary')}; }}
        QCheckBox::indicator:checked, QTreeView::indicator:checked, QListView::indicator:checked {{ background: {c('accent_primary')}; border-color: {c('accent_primary')}; image: url({check}); }}
        QCheckBox::indicator:indeterminate, QTreeView::indicator:indeterminate {{ background: {c('bg_hover')}; border-color: {c('accent_primary')}; }}
        QRadioButton::indicator:checked {{ background: {c('accent_primary')}; border-color: {c('accent_primary')}; image: url({dot}); }}
        QSlider::groove:horizontal {{ height: 6px; background: {c('progress_track')}; border-radius: 3px; }}
        QSlider::handle:horizontal {{ background: {c('accent_primary')}; width: 16px; height: 16px; margin: -5px 0; border-radius: 8px; }}
        QSlider::sub-page:horizontal {{ background: {c('accent_primary')}; border-radius: 3px; }}
        QSplitter::handle {{ background: {c('border')}; }}
        QMenu {{ background: {c('bg_secondary')}; color: {c('fg_primary')}; border: 1px solid {c('border')}; }}
        QMenu::item:selected {{ background: {c('bg_hover')}; }}
        QGroupBox {{ border: 1px solid {c('border')}; border-radius: {radius}; margin-top: 10px; padding-top: 8px; color: {c('fg_primary')}; }}
        QGroupBox::title {{ subcontrol-origin: margin; left: 10px; padding: 0 4px; }}
        """

    def _native_polish_stylesheet(self):
        c = self.c
        focus = c("focus", c("accent_primary"))
        check = _qt_check_icon_path(c("selection_fg", "#ffffff"))
        dot = _qt_check_icon_path(c("selection_fg", "#ffffff"), "dot")
        return f"""
        QPushButton {{ background: {c('button_bg')}; color: {c('button_fg')}; border: 1px solid {c('border')}; border-radius: 3px; padding: 5px 12px; }}
        QPushButton:hover {{ border-color: {focus}; }}
        QPushButton:pressed {{ background: {c('bg_pressed')}; border-color: {focus}; }}
        QPushButton:disabled {{ color: {c('fg_disabled')}; }}
        QLineEdit, QPlainTextEdit, QTextEdit, QSpinBox, QComboBox, QListWidget, QTreeWidget, QTableWidget {{ background: {c('bg_input')}; color: {c('fg_primary')}; border: 1px solid {c('border')}; border-radius: 3px; padding: 4px 6px; selection-background-color: {c('selection_bg')}; selection-color: {c('selection_fg')}; }}
        QLineEdit:hover, QComboBox:hover, QPlainTextEdit:hover, QTextEdit:hover, QSpinBox:hover {{ border-color: {focus}; }}
        QLineEdit:focus, QComboBox:focus, QPlainTextEdit:focus, QTextEdit:focus, QSpinBox:focus {{ border: 1px solid {focus}; }}
        QComboBox::drop-down {{ border: none; width: 22px; }}
        QComboBox QAbstractItemView {{ background: {c('bg_input')}; color: {c('fg_primary')}; selection-background-color: {c('selection_bg')}; selection-color: {c('selection_fg')}; border: 1px solid {c('border')}; }}
        QHeaderView::section {{ background: {c('bg_tertiary')}; color: {c('fg_primary')}; border: none; border-bottom: 1px solid {c('border')}; padding: 5px; }}
        QTabWidget::pane {{ border: none; }}
        QTabBar::tab {{ background: transparent; color: {c('fg_tertiary')}; padding: 6px 14px; border: 1px solid transparent; border-bottom: none; border-top-left-radius: 3px; border-top-right-radius: 3px; margin-right: 1px; }}
        QTabBar::tab:selected {{ background: {c('bg_tertiary')}; color: {c('fg_primary')}; border-color: {c('border')}; }}
        QTabBar::tab:hover {{ color: {c('fg_primary')}; }}
        QCheckBox {{ spacing: 8px; }}
        QCheckBox::indicator, QRadioButton::indicator, QTreeView::indicator, QListView::indicator {{ width: 18px; height: 18px; border: 1px solid {c('scrollbar_thumb')}; background: {c('bg_input')}; }}
        QCheckBox::indicator, QTreeView::indicator, QListView::indicator {{ border-radius: 3px; }}
        QTreeView::indicator:checked, QListView::indicator:checked {{ background: {focus}; border-color: {focus}; image: url({check}); }}
        QTreeView::indicator:indeterminate {{ background: {c('bg_hover')}; border-color: {focus}; }}
        QRadioButton::indicator {{ border-radius: 9px; }}
        QCheckBox::indicator:hover, QRadioButton::indicator:hover {{ border-color: {focus}; }}
        QCheckBox::indicator:checked {{ background: {focus}; border-color: {focus}; image: url({check}); }}
        QCheckBox::indicator:indeterminate {{ background: {c('bg_hover')}; border-color: {focus}; }}
        QRadioButton::indicator:checked {{ background: {focus}; border-color: {focus}; image: url({dot}); }}
        QProgressBar {{ background: {c('progress_track')}; border: none; border-radius: 3px; text-align: center; color: transparent; }}
        QProgressBar::chunk {{ background: {focus}; border-radius: 3px; }}
        QSlider::groove:horizontal {{ height: 6px; background: {c('progress_track')}; border-radius: 3px; }}
        QSlider::handle:horizontal {{ background: {c('button_bg')}; border: 1px solid {focus}; width: 16px; height: 16px; margin: -6px 0; border-radius: 9px; }}
        QSlider::sub-page:horizontal {{ background: {focus}; border-radius: 3px; }}
        QScrollBar:vertical {{ background: transparent; width: 10px; margin: 0; border: none; }}
        QScrollBar::handle:vertical {{ background: {c('scrollbar_thumb')}; min-height: 24px; border-radius: 4px; margin: 1px; }}
        QScrollBar::handle:vertical:hover {{ background: {focus}; }}
        QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {{ height: 0; }}
        QScrollBar:horizontal {{ background: transparent; height: 10px; margin: 0; border: none; }}
        QScrollBar::handle:horizontal {{ background: {c('scrollbar_thumb')}; min-width: 24px; border-radius: 4px; margin: 1px; }}
        QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {{ width: 0; }}
        QSplitter::handle {{ background: {c('border')}; }}
        QToolTip {{ background: {c('bg_tertiary')}; color: {c('fg_primary')}; border: 1px solid {c('border')}; padding: 4px; }}
        """

    def stylesheet(self):
        if self.native:
            if _on_plasma() and getattr(self, "_breeze_loaded", False):
                return self._object_stylesheet()
            return self._native_polish_stylesheet() + self._object_stylesheet()
        return self._widget_stylesheet() + self._object_stylesheet()

    def _plasma_font(self):
        try:
            raw = _read_kdeglobals().get("General", {}).get("font", "")
            if raw:
                font = QtGui.QFont()
                if font.fromString(raw):
                    return font
        except Exception:
            pass
        return None

    def apply(self, app):
        try:
            keys = [k.lower() for k in QtWidgets.QStyleFactory.keys()]
            if self.native:
                self._breeze_loaded = False
                if _on_plasma() and "breeze" in keys:
                    app.setStyle(QtWidgets.QStyleFactory.create("Breeze"))
                    self._breeze_loaded = app.style().objectName().lower() == "breeze"
                    if app.style().objectName().lower() == "breeze" and QtGui.QGuiApplication.platformName() and _plasma_colors() is None:
                        app.setPalette(app.style().standardPalette())
                    else:
                        app.setPalette(self.palette())
                else:
                    app.setStyle(QtWidgets.QStyleFactory.create("Fusion"))
                    app.setPalette(self.palette())
                if _on_plasma():
                    font = self._plasma_font()
                    if font is not None:
                        app.setFont(font)
                app.setStyleSheet(self.stylesheet())
                return
            app.setStyle(QtWidgets.QStyleFactory.create("Fusion"))
            app.setPalette(self.palette())
            app.setStyleSheet(self.stylesheet())
        except Exception as e:
            print(f"[Qt] theme apply failed: {e}")


_QT_ICON_CACHE = {}


def _qt_icon(name, size=20, color=None):
    key = (name, size, color)
    if key in _QT_ICON_CACHE:
        return _QT_ICON_CACHE[key]
    path = find_resource(f"oranglauncher/images/icons/{name}.png")
    icon = QtGui.QIcon()
    if path:
        try:
            pix = QtGui.QPixmap(str(path)).scaled(size, size, Qt.KeepAspectRatio, Qt.SmoothTransformation)
            if color:
                tinted = QtGui.QPixmap(pix.size())
                tinted.fill(Qt.transparent)
                painter = QtGui.QPainter(tinted)
                painter.drawPixmap(0, 0, pix)
                painter.setCompositionMode(QtGui.QPainter.CompositionMode_SourceIn)
                painter.fillRect(tinted.rect(), QtGui.QColor(color))
                painter.end()
                pix = tinted
            icon = QtGui.QIcon(pix)
        except Exception:
            icon = QtGui.QIcon()
    _QT_ICON_CACHE[key] = icon
    return icon


def _qt_button(text, on_click=None, kind=None, icon=None, tooltip=None, launcher=None):
    btn = QtWidgets.QPushButton(text)
    if kind:
        btn.setObjectName(kind)
    if icon:
        fg = launcher.theme.c("fg_primary") if launcher is not None else None
        if kind == "accent" and launcher is not None:
            fg = launcher.theme.c("selection_fg", "#ffffff")
        btn.setIcon(_qt_icon(icon, 16, fg))
        btn.setIconSize(QSize(16, 16))
    if tooltip:
        btn.setToolTip(tooltip)
    if on_click:
        btn.clicked.connect(lambda *_: on_click())
    btn.setCursor(Qt.PointingHandCursor)
    return btn


def _qt_label(text, kind=None, wrap=False, align=None):
    lbl = QtWidgets.QLabel(text)
    if kind:
        lbl.setObjectName(kind)
    if wrap:
        lbl.setWordWrap(True)
    if align is not None:
        lbl.setAlignment(align)
    lbl.setTextInteractionFlags(Qt.TextSelectableByMouse)
    return lbl


class _ElideLabel(QtWidgets.QLabel):
    def __init__(self, text="", kind=None, parent=None):
        super().__init__(parent)
        self._full = text
        if kind:
            self.setObjectName(kind)
        self.setSizePolicy(QtWidgets.QSizePolicy.Expanding, QtWidgets.QSizePolicy.Preferred)
        self.setToolTip(text)
        self.setText(text)

    def minimumSizeHint(self):
        h = super().minimumSizeHint()
        return QSize(24, h.height())

    def paintEvent(self, event):
        rect = self.contentsRect()
        elided = self.fontMetrics().elidedText(self._full, Qt.ElideRight, rect.width())
        painter = QtGui.QPainter(self)
        painter.setFont(self.font())
        painter.setPen(self.palette().color(QtGui.QPalette.WindowText))
        painter.drawText(rect, int(self.alignment()) | Qt.TextSingleLine, elided)


class _Card(QtWidgets.QFrame):
    def __init__(self, title=None, parent=None):
        super().__init__(parent)
        self.setObjectName("card")
        outer = QtWidgets.QVBoxLayout(self)
        outer.setContentsMargins(16, 14, 16, 14)
        outer.setSpacing(8)
        if title:
            outer.addWidget(_qt_label(title, "cardTitle"))
        self.body = QtWidgets.QVBoxLayout()
        self.body.setSpacing(8)
        outer.addLayout(self.body)

    def add(self, widget):
        self.body.addWidget(widget)
        return widget

    def add_layout(self, layout):
        self.body.addLayout(layout)
        return layout


class _ToggleRow(QtWidgets.QWidget):
    def __init__(self, title, description, var, on_change=None, parent=None):
        super().__init__(parent)
        self.var = var
        lay = QtWidgets.QHBoxLayout(self)
        lay.setContentsMargins(0, 2, 0, 2)
        col = QtWidgets.QVBoxLayout()
        col.setSpacing(2)
        col.addWidget(_qt_label(title, "h3"))
        if description:
            col.addWidget(_qt_label(description, "muted", wrap=True))
        lay.addLayout(col, 1)
        self.check = QtWidgets.QCheckBox()
        self.check.setChecked(bool(var.get()))
        self.check.setCursor(Qt.PointingHandCursor)

        def changed(state):
            var.set(bool(self.check.isChecked()))
            if on_change:
                try:
                    on_change()
                except Exception as e:
                    print(f"[Toggle] {title}: {e}")
        self.check.toggled.connect(changed)
        lay.addWidget(self.check, 0, Qt.AlignVCenter)

    def refresh(self):
        self.check.blockSignals(True)
        self.check.setChecked(bool(self.var.get()))
        self.check.blockSignals(False)


def _qt_scroll(widget, horizontal=False):
    area = QtWidgets.QScrollArea()
    area.setWidgetResizable(True)
    area.setWidget(widget)
    area.setFrameShape(QtWidgets.QFrame.NoFrame)
    if not horizontal:
        area.setHorizontalScrollBarPolicy(Qt.ScrollBarAlwaysOff)
    return area


def _qt_page(spacing=14, margins=(28, 22, 28, 22)):
    page = QtWidgets.QWidget()
    lay = QtWidgets.QVBoxLayout(page)
    lay.setContentsMargins(*margins)
    lay.setSpacing(spacing)
    return page, lay


def _qt_form_row(label_text, widget, hint=None):
    box = QtWidgets.QWidget()
    lay = QtWidgets.QVBoxLayout(box)
    lay.setContentsMargins(0, 0, 0, 4)
    lay.setSpacing(3)
    lay.addWidget(_qt_label(label_text))
    if isinstance(widget, QtWidgets.QLayout):
        lay.addLayout(widget)
    else:
        lay.addWidget(widget)
    if hint:
        lay.addWidget(_qt_label(hint, "hint", wrap=True))
    return box


def _qt_line_with_browse(line_edit, on_browse, text="Browse"):
    lay = QtWidgets.QHBoxLayout()
    lay.setContentsMargins(0, 0, 0, 0)
    lay.addWidget(line_edit, 1)
    lay.addWidget(_qt_button(text, on_browse))
    return lay


def _tr(launcher, key, default=None):
    try:
        text = launcher._t(key)
    except Exception:
        text = key
    if text == key and default is not None:
        return default
    return text


def _qt_t(key, default=None):
    launcher = _QT_APP_REF[0]
    if launcher is None:
        return default if default is not None else key
    return _tr(launcher, key, default)


def _human_size(n):
    try:
        n = float(n)
    except Exception:
        return "?"
    for unit in ("B", "KB", "MB", "GB"):
        if n < 1024 or unit == "GB":
            return f"{n:.1f} {unit}" if unit != "B" else f"{int(n)} B"
        n /= 1024
    return f"{n:.1f} GB"


class _Worker(QObject):
    def __init__(self, fn, on_done=None, on_error=None):
        super().__init__()
        self.fn = fn
        self.on_done = on_done
        self.on_error = on_error

    def start(self):
        def run():
            try:
                result = self.fn()
            except Exception as e:
                err = e
                if not isinstance(e, LoginCancelled):
                    print(traceback.format_exc())
                if self.on_error:
                    _qt_later(lambda: self.on_error(err))
                return
            if self.on_done:
                _qt_later(lambda: self.on_done(result))
        threading.Thread(target=run, daemon=True).start()
        return self


def _qt_run_bg(fn, on_done=None, on_error=None):
    return _Worker(fn, on_done, on_error).start()

# this gets updated after launcher update or minecraft update.
NEWS_URL = "https://oranges.lt/launcher.html"

class QtNewsBrowser(QtWidgets.QTextBrowser):
    def __init__(self, launcher, parent=None):
        super().__init__(parent)
        self.launcher = launcher
        self.base_url = NEWS_URL
        self.setOpenLinks(False)
        self.setOpenExternalLinks(False)
        self.anchorClicked.connect(lambda url: open_with_browser(url.toString()))
        self.setFrameShape(QtWidgets.QFrame.NoFrame)
        self._bg = None
        self._resources = {}
        self.document().setDocumentMargin(18)

    def loadResource(self, rtype, url):
        key = url.toString()
        if key in self._resources:
            return self._resources[key]
        absolute = urllib.parse.urljoin(self.base_url, key)
        try:
            data = _cached_image_get(absolute)
            img = QtGui.QImage()
            img.loadFromData(data)
            if not img.isNull():
                self._resources[key] = img
                return img
        except Exception:
            pass
        return super().loadResource(rtype, url)

    def paintEvent(self, event):
        if self._bg is not None:
            painter = QtGui.QPainter(self.viewport())
            painter.drawTiledPixmap(self.viewport().rect(), self._bg)
            painter.end()
        super().paintEvent(event)

    def set_background(self, pixmap):
        self._bg = pixmap
        self.viewport().update()


def _news_html_transform(html, theme, base_url):
    css_match = re.search(r'<link[^>]+href="([^"]+\.css)"', html)
    css = ""
    if css_match:
        try:
            css = _http_session.get(urllib.parse.urljoin(base_url, css_match.group(1)), timeout=10).text
        except Exception:
            css = ""
    bg_img = None
    m = re.search(r"background-image:\s*url\('([^']+)'\)", html)
    if m:
        bg_img = urllib.parse.urljoin(base_url, m.group(1))
    body_bg = "#222222"
    body_fg = "#e0d0d0"
    link = "#aaaaff"
    mb = re.search(r"body\s*\{([^}]*)\}", css)
    if mb:
        block = mb.group(1)
        c1 = re.search(r"background-color:\s*([^;]+);", block)
        c2 = re.search(r"(?<![-\w])color:\s*([^;]+);", block)
        if c1:
            body_bg = c1.group(1).strip()
        if c2:
            body_fg = c2.group(1).strip()
    ma = re.search(r"(?:^|\n)a\s*\{[^}]*?(?<![-\w])color:\s*([^;]+);", css)
    if ma:
        link = ma.group(1).strip()
    html = re.sub(r"<link[^>]+>", "", html)
    html = re.sub(r'<body[^>]*>', '<body>', html)
    html = re.sub(r'width="(\d+)px"', r'width="\1"', html)

    def _img_style(m):
        tag = m.group(0)
        w = re.search(r"width:\s*(\d+)px", tag)
        tag = re.sub(r'\s*style="[^"]*"', "", tag)
        if w and 'width=' not in tag:
            tag = tag[:-1] + f' width="{w.group(1)}">'
        return tag
    html = re.sub(r"<img[^>]*>", _img_style, html)
    bg_css = "" if bg_img else f"background-color:{body_bg};"
    style = (f"<style>body{{{bg_css}color:{body_fg};font-family:sans-serif;font-size:10pt;}}"
             f"a{{color:{link};}} h1{{color:#ffffff;font-size:17pt;}} h3{{color:#ffffff;font-size:12pt;margin-top:12px;}}"
             f"p{{margin-top:3px;margin-bottom:8px;}} li{{margin-bottom:1px;}} td.sidebar{{padding:10px;}}"
             f"hr{{color:#111111;background-color:#111111;height:2px;}}</style>")
    html = html.replace("<head>", "<head>" + style, 1) if "<head>" in html else style + html
    return html, bg_img, body_bg, body_fg, link


class QtNewsPage(QtWidgets.QWidget):
    def __init__(self, launcher, parent=None):
        super().__init__(parent)
        self.launcher = launcher
        self.web = None
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(0)
        self.status = _qt_label(_tr(launcher, "LOADING_NEWS", "Loading news..."), "muted")
        self.status.setContentsMargins(12, 6, 12, 6)
        lay.addWidget(self.status)
        self.browser = QtNewsBrowser(launcher, self)
        lay.addWidget(self.browser, 1)
        self._loaded = False

    def ensure_loaded(self):
        if not self._loaded:
            self._loaded = True
            self.reload()

    def reload(self):
        self.status.setVisible(True)
        self.status.setText(_tr(self.launcher, "LOADING_NEWS", "Loading news..."))

        def work():
            r = _http_session.get(NEWS_URL, timeout=20)
            r.raise_for_status()
            html, bg_url, body_bg, body_fg, link = _news_html_transform(r.text, self.launcher.theme, NEWS_URL)
            bg_pix = None
            if bg_url:
                try:
                    bg_pix = _cached_image_get(bg_url)
                except Exception:
                    bg_pix = None
            return html, bg_pix, body_bg, body_fg, link

        def done(result):
            html, bg_bytes, body_bg, body_fg, link = result
            self.status.setVisible(False)
            self.browser.setStyleSheet(f"QTextBrowser {{ background: {body_bg}; color: {body_fg}; border: none; border-radius: 0; }}")
            self.browser.document().setDefaultStyleSheet(f"body, p, li, td, div {{ color: {body_fg}; }} a {{ color: {link}; }}")
            if bg_bytes:
                pix = QtGui.QPixmap()
                pix.loadFromData(bg_bytes)
                if not pix.isNull():
                    self.browser.set_background(pix)
                    self.browser.setStyleSheet(f"QTextBrowser {{ background: transparent; color: {body_fg}; border: none; border-radius: 0; }}")
                    self.browser.viewport().setAutoFillBackground(False)
            self.browser.setHtml(html)
            self.status.setText("")

        def fail(err):
            self.status.setVisible(True)
            self.status.setText(_tr(self.launcher, "FAILED_LOAD_NEWS", "Could not load news.") + f" ({err})")
            self.browser.setHtml(f"<h2>Minecraft News</h2><p>Could not load {NEWS_URL}</p><p>{err}</p>")
        _qt_run_bg(work, done, fail)


class QtLogPage(QtWidgets.QWidget):
    COLORS = {"error": "#f25c5c", "warning": "#ffc107", "info": "#74c0fc", "success": "#3bc652", None: None}

    def __init__(self, launcher, parent=None):
        super().__init__(parent)
        self.launcher = launcher
        self.filters = {"error": True, "warning": True, "info": True, "success": True, None: True}
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(10, 8, 10, 8)
        lay.setSpacing(6)
        bar = QtWidgets.QHBoxLayout()
        self.search = QtWidgets.QLineEdit()
        self.search.setPlaceholderText(_tr(launcher, "LOGS_SEARCH_PLACEHOLDER", "Type to filter logs..."))
        self.search.setClearButtonEnabled(True)
        self._debounce = QTimer(self)
        self._debounce.setSingleShot(True)
        self._debounce.setInterval(250)
        self._debounce.timeout.connect(self.rebuild)
        self.search.textChanged.connect(lambda *_: self._debounce.start())
        bar.addWidget(self.search, 1)
        for label_key, tag, color in ((("LOGS_FILTER_ERROR", "Errors"), "error", "#f25c5c"), (("LOGS_FILTER_WARN", "Warnings"), "warning", "#ffc107"), (("LOGS_FILTER_INFO", "Info"), "info", "#74c0fc"), (("LOGS_FILTER_SUCCESS", "Success"), "success", "#3bc652"), (("LOGS_FILTER_OTHER", "Other"), None, launcher.theme.c("fg_tertiary"))):
            cb = QtWidgets.QCheckBox(_tr(launcher, label_key[0], label_key[1]))
            cb.setChecked(True)
            cb.setStyleSheet(f"QCheckBox {{ color: {color}; }}")
            cb.toggled.connect(lambda checked, t=tag: self._set_filter(t, checked))
            bar.addWidget(cb)
        self.mclogs_btn = _qt_button(_tr(launcher, "LOGS_MCLOGS_BTN", "Upload to mclo.gs"), self.upload_mclogs, icon="update", launcher=launcher)
        bar.addWidget(self.mclogs_btn)
        bar.addWidget(_qt_button(_tr(launcher, "LOGS_SAVE_BTN", "Save log"), self.export_log, icon="file", launcher=launcher))
        bar.addWidget(_qt_button(_qt_t("QT_CLEAR", "Clear"), self.clear, icon="trash", launcher=launcher))
        lay.addLayout(bar)
        self.text = QtWidgets.QPlainTextEdit()
        self.text.setReadOnly(True)
        self.text.setMaximumBlockCount(20000)
        mono = QtGui.QFontDatabase.systemFont(QtGui.QFontDatabase.FixedFont)
        mono.setPointSize(9)
        self.text.setFont(mono)
        self.text.setLineWrapMode(QtWidgets.QPlainTextEdit.NoWrap)
        lay.addWidget(self.text, 1)

    def _set_filter(self, tag, checked):
        self.filters[tag] = checked
        self.rebuild()

    @staticmethod
    def classify(message):
        plain = re.sub(r'(?:\x1b|\033)\[([0-9;]*)m|\[([0-9;]+)m', '', message)
        low = plain.lower()
        m = re.search(r'\[(?:[^\]]+)/([A-Z]+)\]', plain)
        if m:
            level = m.group(1)
            if level in ("ERROR", "FATAL"):
                return plain, "error"
            if level == "WARN":
                return plain, "warning"
            if level == "INFO":
                return plain, "info"
            return plain, None
        if any(k in low for k in ("[error]", "error:", "exception", "traceback", "failed")):
            return plain, "error"
        if any(k in low for k in ("[warn]", "warning")):
            return plain, "warning"
        if any(k in low for k in ("success", "done", "finished", "installed", "complete")):
            return plain, "success"
        if any(k in low for k in ("[launcher]", "[forge]", "[fabric]", "[quilt]", "[install]", "[updater]", "[lwjgl]", "[java]", "[hook]")):
            return plain, "info"
        return plain, None

    def _visible(self, plain, tag):
        if not self.filters.get(tag, True):
            return False
        q = self.search.text().strip().lower()
        if q and q not in plain.lower():
            return False
        return True

    def _append_line(self, plain, tag):
        color = self.COLORS.get(tag)
        cursor = self.text.textCursor()
        cursor.movePosition(QtGui.QTextCursor.End)
        fmt = QtGui.QTextCharFormat()
        if color:
            fmt.setForeground(QtGui.QColor(color))
        else:
            fmt.setForeground(QtGui.QColor(self.launcher.theme.c("fg_secondary")))
        cursor.insertText(plain + "\n", fmt)
        self.text.setTextCursor(cursor)
        self.text.ensureCursorVisible()

    def append(self, message):
        message = (message or "").rstrip()
        if not message:
            return
        plain, tag = self.classify(message)
        self.launcher._log_buffer.append((plain, tag))
        if self._visible(plain, tag):
            self._append_line(plain, tag)

    def rebuild(self):
        self.text.clear()
        for plain, tag in list(self.launcher._log_buffer)[-5000:]:
            if self._visible(plain, tag):
                self._append_line(plain, tag)

    def clear(self):
        self.launcher._log_buffer.clear()
        self.text.clear()

    def export_log(self):
        if not self.launcher._log_buffer:
            messagebox.showinfo(_tr(self.launcher, "LOGS_EXPORT_TITLE", "Export log"), _tr(self.launcher, "LOGS_NO_ENTRIES", "No log entries."))
            return
        path = _pick_save_file(defaultextension=".txt", filetypes=[(_qt_t("QT_FT_TEXT", "Text files"), "*.txt"), (_qt_t("QT_FT_ALL", "All files"), "*.*")], initialfile="launcher_log.txt", title=_tr(self.launcher, "LOGS_EXPORT_TITLE", "Export log"))
        if not path:
            return
        try:
            with open(path, "w", encoding="utf-8") as f:
                for msg, _ in self.launcher._log_buffer:
                    f.write(msg + "\n")
            messagebox.showinfo(_tr(self.launcher, "LOGS_EXPORT_TITLE", "Export log"), _tr(self.launcher, "LOGS_EXPORT_SUCCESS", "Saved") + f"\n{path}")
        except Exception as e:
            messagebox.showerror(_tr(self.launcher, "LOGS_EXPORT_TITLE", "Export log"), str(e))

    # feature from modrinth
    def upload_mclogs(self):
        if not self.launcher._log_buffer:
            messagebox.showinfo("mclo.gs", _tr(self.launcher, "LOGS_NO_ENTRIES", "No log entries."))
            return
        lines = [msg for msg, _ in self.launcher._log_buffer][-25000:]
        content = "\n".join(lines)
        if len(content.encode("utf-8")) > 10 * 1024 * 1024:
            content = content.encode("utf-8")[-10 * 1024 * 1024:].decode("utf-8", "ignore")
        self.mclogs_btn.setEnabled(False)
        self.mclogs_btn.setText(_tr(self.launcher, "LOGS_MCLOGS_UPLOADING", "Uploading..."))

        def work():
            r = _http_session.post("https://api.mclo.gs/1/log", data={"content": content, "source": "OrangLauncher"}, timeout=20)
            return r.json()

        def done(data):
            self.mclogs_btn.setEnabled(True)
            self.mclogs_btn.setText(_tr(self.launcher, "LOGS_MCLOGS_BTN", "Upload to mclo.gs"))
            if data.get("success") and data.get("url"):
                url = data["url"]
                QtWidgets.QApplication.clipboard().setText(url)
                if messagebox.askyesno("mclo.gs", f"{_tr(self.launcher, 'LOGS_MCLOGS_SUCCESS', 'Uploaded, link copied to clipboard.')}\n\n{url}\n\n{_tr(self.launcher, 'LOGS_MCLOGS_OPEN', 'Open it in the browser?')}"):
                    open_with_browser(url)
            else:
                messagebox.showerror("mclo.gs", str(data.get("error", "?")))

        def fail(err):
            self.mclogs_btn.setEnabled(True)
            self.mclogs_btn.setText(_tr(self.launcher, "LOGS_MCLOGS_BTN", "Upload to mclo.gs"))
            messagebox.showerror("mclo.gs", str(err))
        _qt_run_bg(work, done, fail)


def _instance_icon_pixmap(launcher, instance, size=64):
    icon_file = instance.base_path / "icon.txt"
    path = None
    if icon_file.exists():
        try:
            path = icon_file.read_text(encoding="utf-8").strip()
        except Exception:
            path = None
    if not path or not Path(path).exists():
        for cand in ("icon.png", "pack.png"):
            p = instance.base_path / cand
            if p.exists():
                path = str(p)
                break
    if not path or not Path(path).exists():
        loader = (instance.mod_loader or "vanilla").lower()
        if loader in ("vanilla", "none", ""):
            res = find_resource("oranglauncher/images/minecraft-green.png")
        else:
            res = find_resource(f"oranglauncher/images/loaders/{loader}.png") or find_resource("oranglauncher/images/minecraft-blue.png")
        path = str(res) if res else None
    if not path:
        return QtGui.QPixmap()
    pix = _qt_pixmap_from_path(path, (size, size))
    return pix if pix is not None else QtGui.QPixmap()


class QtInstanceCard(QtWidgets.QFrame):
    def __init__(self, page, instance, selected):
        super().__init__()
        self.page = page
        self.instance = instance
        self.setObjectName("instanceCardSelected" if selected else "instanceCard")
        self.setCursor(Qt.PointingHandCursor)
        self.setFixedWidth(page.card_width())
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(14, 14, 14, 12)
        lay.setSpacing(6)
        top = QtWidgets.QHBoxLayout()
        icon = QtWidgets.QLabel()
        icon.setPixmap(_instance_icon_pixmap(page.launcher, instance, 48))
        icon.setFixedSize(48, 48)
        icon.setScaledContents(True)
        top.addWidget(icon)
        col = QtWidgets.QVBoxLayout()
        col.setSpacing(2)
        col.addWidget(_ElideLabel(instance.name, "h3"))
        loader = (instance.mod_loader or "vanilla").title()
        col.addWidget(_ElideLabel(f"{instance.version}  ·  {loader}", "muted"))
        top.addLayout(col, 1)
        lay.addLayout(top)
        meta = []
        try:
            meta.append(_qt_t("QT_MODS_COUNT", "{n} mods").format(n=instance.get_mod_count()))
            meta.append(_qt_t("QT_WORLDS_COUNT", "{n} worlds").format(n=instance.get_saves_count()))
        except Exception:
            pass
        if instance.play_time:
            h, rem = divmod(int(instance.play_time), 3600)
            meta.append(_qt_t("QT_PLAYED", "{h}h {m}m played").format(h=h, m=rem // 60))
        lay.addWidget(_ElideLabel("  ·  ".join(meta), "hint"))
        btns = QtWidgets.QHBoxLayout()
        btns.setSpacing(6)
        sel_btn = _qt_button(page.select_text(selected), lambda: page.select_instance(instance.instance_id), kind="accent" if selected else None)
        sel_btn.setEnabled(not selected)
        btns.addWidget(sel_btn)
        edit_btn = _qt_button(page.edit_text(), lambda: page.open_editor(instance), icon="settings", launcher=page.launcher)
        btns.addWidget(edit_btn)
        btns.addStretch(1)
        lay.addLayout(btns)
        for b in (sel_btn, edit_btn):
            b.ensurePolished()
            b.setMinimumWidth(b.sizeHint().width())
        need = sel_btn.minimumWidth() + edit_btn.minimumWidth() + 6 + 28 + 4
        if need > page._card_width:
            page._card_width = need
            self.setFixedWidth(need)

    def mouseDoubleClickEvent(self, event):
        self.page.open_editor(self.instance)

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.page.select_instance(self.instance.instance_id)
        super().mousePressEvent(event)


class QtInstanceFormDialog(QtWidgets.QDialog):
    def __init__(self, launcher, parent=None):
        super().__init__(parent)
        self.launcher = launcher
        self.setWindowTitle(_tr(launcher, "GAME_PROFILES_CREATE_TITLE", "New instance"))
        self.resize(460, 360)
        self.result_instance = None
        lay = QtWidgets.QVBoxLayout(self)
        lay.setSpacing(10)
        form = QtWidgets.QFormLayout()
        form.setLabelAlignment(Qt.AlignRight)
        self.name = QtWidgets.QLineEdit()
        base = "My Instance"
        candidate = base
        i = 1
        while launcher.instance_manager.get_instance_by_name(candidate):
            i += 1
            candidate = f"{base} {i}"
        self.name.setText(candidate)
        form.addRow(_tr(launcher, "GAME_PROFILES_NAME", _qt_t("QT_NAME", "Name")), self.name)
        self.loader = QtWidgets.QComboBox()
        self.loader.addItems(["vanilla", "forge", "neoforge", "fabric", "quilt", "optifine"])
        form.addRow(_tr(launcher, "GAME_PROFILES_LOADER", _qt_t("QT_MOD_LOADER", "Mod loader")), self.loader)
        self.version = QtWidgets.QComboBox()
        self.version.setMaxVisibleItems(14)
        self.version.setMinimumContentsLength(12)
        vrow = QtWidgets.QHBoxLayout()
        vrow.addWidget(self.version, 1)
        self.snapshots = QtWidgets.QCheckBox(_qt_t("QT_SHOW_SNAPSHOTS", "Snapshots"))
        vrow.addWidget(self.snapshots)
        form.addRow(_tr(launcher, "GAME_PROFILES_VERSION", "Minecraft version"), vrow)
        self.loader_version = QtWidgets.QComboBox()
        self.loader_version.setEnabled(False)
        form.addRow(_tr(launcher, "GAME_PROFILES_LOADER_VERSION", _qt_t("QT_LOADER_VERSION", "Loader version")), self.loader_version)
        self.ram = QtWidgets.QSpinBox()
        self.ram.setRange(1, max(2, (_get_system_ram_mb() - 1024) // 1024))
        self.ram.setValue(min(4, self.ram.maximum()))
        self.ram.setSuffix(" GB")
        form.addRow(_tr(launcher, "GAME_PROFILES_RAM", _qt_t("QT_MEMORY", "Memory")), self.ram)
        lay.addLayout(form)
        self.status = _qt_label("", "hint", wrap=True)
        lay.addWidget(self.status)
        lay.addStretch(1)
        btns = QtWidgets.QHBoxLayout()
        btns.addStretch(1)
        btns.addWidget(_qt_button(_tr(launcher, "CANCEL", "Cancel"), self.reject))
        btns.addWidget(_qt_button(_tr(launcher, "GAME_PROFILES_CREATE_BTN", "Create"), self.submit, kind="accent"))
        lay.addLayout(btns)
        self._token = 0
        self._fill_versions()
        self.loader.currentTextChanged.connect(lambda *_: self.refresh_loader_versions())
        self.version.currentTextChanged.connect(lambda *_: self.refresh_loader_versions())
        self.snapshots.toggled.connect(lambda *_: self._fill_versions())

    def _fill_versions(self):
        current = self.version.currentText()
        try:
            detailed = list(get_available_versions_detailed())
        except Exception:
            detailed = []
        if detailed:
            allowed = None if self.snapshots.isChecked() else ("release",)
            ids = [v.id for v in detailed if allowed is None or v.type in allowed]
        else:
            ids = self.launcher.version_values()
        self.version.blockSignals(True)
        self.version.clear()
        self.version.addItems(ids)
        idx = self.version.findText(current) if current else -1
        self.version.setCurrentIndex(idx if idx >= 0 else 0)
        self.version.blockSignals(False)
        if self.version.currentText() != current:
            self.refresh_loader_versions()

    def refresh_loader_versions(self):
        loader = self.loader.currentText().lower()
        mc = self.version.currentText().strip()
        self.loader_version.clear()
        if loader == "vanilla":
            self.loader_version.setEnabled(False)
            self.status.setText("")
            return
        self.loader_version.setEnabled(True)
        self.loader_version.addItem(_qt_t("QT_LOADING", "Loading…"))
        self._token += 1
        token = self._token

        def work():
            return self.launcher.fetch_loader_versions(loader, mc)

        def done(versions):
            if token != self._token:
                return
            self.loader_version.clear()
            if versions:
                self.loader_version.addItems(versions)
                self.status.setText("")
            else:
                self.loader_version.addItem(_tr(self.launcher, "LOADER_NOT_COMPATIBLE", "Not available for this version"))
                self.status.setText(_tr(self.launcher, "LOADER_NOT_COMPATIBLE_MSG", "{loader} has no build for Minecraft {version}.").format(loader=loader, version=mc))
        _qt_run_bg(work, done, lambda e: None)

    def submit(self):
        name = self.name.text().strip()
        version = self.version.currentText().strip()
        loader = self.loader.currentText().lower()
        if not name or not version:
            messagebox.showerror(_tr(self.launcher, "GAME_PROFILES_INVALID_TITLE", "Invalid"), _tr(self.launcher, "GAME_PROFILES_INVALID_MSG", "Name and version are required."), parent=self)
            return
        if not self.launcher.validate_version(version):
            return
        lv = self.loader_version.currentText().strip()
        if loader != "vanilla" and lv in ("", "Loading…", _qt_t("QT_LOADING", "Loading…"), _tr(self.launcher, "LOADER_NOT_COMPATIBLE", "Not available for this version")):
            messagebox.showerror(_tr(self.launcher, "LOADER_NOT_COMPATIBLE_TITLE", "Loader not available"),
                                 _tr(self.launcher, "LOADER_NOT_COMPATIBLE_MSG", "{loader} has no build for Minecraft {version}.").format(loader=loader, version=version), parent=self)
            return
        if loader == "vanilla":
            lv = None
        try:
            inst = self.launcher.instance_manager.create_instance(name, version, loader, ram=f"{self.ram.value()}G", loader_version=lv)
        except ValueError as e:
            messagebox.showerror(_tr(self.launcher, "GAME_PROFILES_CREATE_TITLE", "New instance"), str(e), parent=self)
            return
        except Exception as e:
            messagebox.showerror(_tr(self.launcher, "GAME_PROFILES_CREATE_TITLE", "New instance"), str(e), parent=self)
            return
        if inst is not None:
            try:
                self.launcher._apply_sharing_for_instance(inst)
            except Exception as e:
                print(f"[Sharing] {e}")
        self.result_instance = inst
        self.accept()


class QtInstancesPage(QtWidgets.QWidget):
    def __init__(self, launcher, parent=None):
        super().__init__(parent)
        self.launcher = launcher
        self.instance_manager = launcher.instance_manager
        self.stack = QtWidgets.QStackedLayout(self)
        self.stack.setContentsMargins(0, 0, 0, 0)
        self.list_page = QtWidgets.QWidget()
        self.stack.addWidget(self.list_page)
        self.editor = None
        self._build_list()
        self.instance_manager.register_callback(lambda: _qt_later(self.refresh))
        self._fingerprint = None

    def _build_list(self):
        lay = QtWidgets.QVBoxLayout(self.list_page)
        lay.setContentsMargins(18, 14, 18, 12)
        lay.setSpacing(10)
        head = QtWidgets.QHBoxLayout()
        head.addWidget(_qt_label(_tr(self.launcher, "GAME_PROFILES_TITLE", "Instances"), "h1"))
        head.addStretch(1)
        self.search = QtWidgets.QLineEdit()
        self.search.setPlaceholderText(_qt_t("QT_FILTER_INSTANCES", "Filter instances..."))
        self.search.setClearButtonEnabled(True)
        self.search.setFixedWidth(240)
        self.search.textChanged.connect(lambda *_: self.refresh(force=True))
        head.addWidget(self.search)
        lay.addLayout(head)
        self.grid_host = QtWidgets.QWidget()
        self.grid = QtWidgets.QGridLayout(self.grid_host)
        self.grid.setContentsMargins(0, 0, 0, 0)
        self.grid.setSpacing(12)
        self.grid.setAlignment(Qt.AlignTop | Qt.AlignLeft)
        self.scroll = _qt_scroll(self.grid_host)
        lay.addWidget(self.scroll, 1)
        bar = QtWidgets.QHBoxLayout()
        L = self.launcher
        bar.addWidget(_qt_button(_tr(L, "GAME_PROFILES_NEW", "New"), self.new_instance, kind="accent", icon="plus", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "GAME_PROFILES_DUPLICATE", "Duplicate"), self.duplicate_selected, icon="dublicate", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "GAME_PROFILES_DELETE", _qt_t("QT_DELETE", "Delete")), self.delete_selected, icon="trash", launcher=L))
        bar.addWidget(_qt_button(_qt_t("QT_IMPORT", "Import"), self.import_instance, icon="file", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "MODS_IMPORT_MRPACK_BTN", "Import .mrpack"), self.import_mrpack, icon="mrpack", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "GAME_PROFILES_EDIT", _qt_t("QT_EDIT", "Edit")), self.edit_selected, icon="settings", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "RES_SH_OPEN_FOLDER", "Open folder"), self.open_selected_folder, icon="folder", launcher=L))
        bar.addStretch(1)
        lay.addLayout(bar)

    def resizeEvent(self, event):
        super().resizeEvent(event)
        _qt_later(self.refresh)

    def select_text(self, selected):
        return _qt_t("QT_SELECTED", "Selected") if selected else _tr(self.launcher, "GAME_PROFILES_SELECT", "Select")

    def edit_text(self):
        return _tr(self.launcher, "GAME_PROFILES_EDIT", _qt_t("QT_EDIT", "Edit"))

    def card_width(self):
        cached = getattr(self, "_card_width", None)
        if cached:
            return cached
        probe = QtWidgets.QPushButton(self)
        probe.hide()
        probe.ensurePolished()
        widths = []
        for text, icon in ((self.select_text(False), False), (self.select_text(True), False), (self.edit_text(), True)):
            probe.setText(text)
            widths.append(probe.sizeHint().width() + (24 if icon else 0))
        probe.deleteLater()
        need = max(widths[0], widths[1]) + widths[2] + 28 + 6 + 12
        self._card_width = max(250, min(need, 420))
        return self._card_width

    def _columns(self):
        width = max(self.scroll.viewport().width(), 300)
        return max(1, width // (self.card_width() + 12))

    def refresh(self, force=False):
        q = self.search.text().strip().lower()
        instances = [i for i in self.instance_manager.instances.values() if not q or q in i.name.lower()]
        instances.sort(key=lambda i: (i.last_played or "", i.name), reverse=True)
        fp = (tuple((i.instance_id, i.name, i.version, i.mod_loader, i.play_time, i.last_played) for i in instances),
              self.instance_manager.selected_instance_id, self._columns())
        if fp == self._fingerprint and not force:
            return
        self._fingerprint = fp
        while self.grid.count():
            item = self.grid.takeAt(0)
            w = item.widget()
            if w is not None:
                w.deleteLater()
        if not instances:
            empty = _qt_label(_tr(self.launcher, "GAME_PROFILES_NO_PROFILES", _qt_t("QT_NO_INSTANCES", "No instances yet. Press New to create one, or Import to add a modpack.")), "muted", wrap=True)
            self.grid.addWidget(empty, 0, 0)
            return
        cards = [QtInstanceCard(self, inst, inst.instance_id == self.instance_manager.selected_instance_id) for inst in instances]
        cols = self._columns()
        for idx, card in enumerate(cards):
            card.setFixedWidth(self._card_width)
            self.grid.addWidget(card, idx // cols, idx % cols)

    def select_instance(self, instance_id):
        if self.instance_manager.selected_instance_id == instance_id:
            return
        self.instance_manager.set_selected_instance(instance_id)
        self.launcher.refresh_instance_display()
        self.refresh(force=True)

    def selected(self):
        return self.instance_manager.get_selected_instance()

    def new_instance(self):
        dlg = QtInstanceFormDialog(self.launcher, self)
        if dlg.exec() == QtWidgets.QDialog.Accepted and dlg.result_instance is not None:
            self.instance_manager.set_selected_instance(dlg.result_instance.instance_id)
            self.launcher.refresh_instance_display()
            self.refresh(force=True)

    def duplicate_selected(self):
        source = self.selected()
        if not source:
            messagebox.showinfo(_tr(self.launcher, "GAME_PROFILES_DUPLICATE", "Duplicate"), _tr(self.launcher, "GAME_PROFILES_DUPLICATE_SELECT_MSG", _qt_t("QT_SELECT_INSTANCE_FIRST", "Select an instance first.")))
            return
        base = f"{source.name} Copy"
        candidate = base
        i = 2
        while self.instance_manager.get_instance_by_name(candidate):
            candidate = f"{base} {i}"
            i += 1
        try:
            dup = self.instance_manager.create_instance(candidate, source.version, source.mod_loader, ram=source.ram, java_args=source.java_args, loader_version=source.loader_version)
            dup.java_path = source.java_path
            dup.env_vars = source.env_vars
            dup.opts = dict(source.opts)
            self.instance_manager.save_instances()
        except Exception as e:
            messagebox.showerror(_tr(self.launcher, "GAME_PROFILES_DUPLICATE", "Duplicate"), str(e))
            return
        self.launcher.set_status(_qt_t("QT_COPYING_FILES", "Copying files..."))

        def work():
            for rel in ("mods", "config", "resourcepacks", "shaderpacks", "saves", "options.txt", "servers.dat"):
                src = source.minecraft_dir / rel
                dst = dup.minecraft_dir / rel
                try:
                    if src.is_symlink() or not src.exists():
                        continue
                    if src.is_dir():
                        if dst.exists():
                            shutil.rmtree(dst)
                        shutil.copytree(src, dst)
                    else:
                        shutil.copy2(src, dst)
                except Exception as e:
                    print(f"[Duplicate] {rel}: {e}")
            for extra in ("icon.png", "icon.txt", "modpack.json"):
                src = source.base_path / extra
                if src.exists():
                    try:
                        shutil.copy2(src, dup.base_path / extra)
                    except Exception:
                        pass
            icon_txt = dup.base_path / "icon.txt"
            if icon_txt.exists() and (dup.base_path / "icon.png").exists():
                icon_txt.write_text(str(dup.base_path / "icon.png"), encoding="utf-8")

        def done(_):
            self.launcher.set_status(_qt_t("QT_READY", "Ready"))
            self.instance_manager.set_selected_instance(dup.instance_id)
            self.launcher.refresh_instance_display()
            self.refresh(force=True)
        _qt_run_bg(work, done, lambda e: (self.launcher.set_status(_qt_t("QT_READY", "Ready")), messagebox.showerror(_tr(self.launcher, "GAME_PROFILES_DUPLICATE", "Duplicate"), str(e))))

    def delete_selected(self):
        inst = self.selected()
        if not inst:
            messagebox.showinfo(_tr(self.launcher, "GAME_PROFILES_DELETE", _qt_t("QT_DELETE", "Delete")), _qt_t("QT_SELECT_INSTANCE_FIRST", "Select an instance first."))
            return
        self.delete_instance(inst)

    def delete_instance(self, inst):
        if not messagebox.askyesno(_tr(self.launcher, "GAME_PROFILES_DELETE_CONFIRM_TITLE", "Delete instance"),
                                   _tr(self.launcher, "GAME_PROFILES_DELETE_CONFIRM_MSG", "Delete '{name}' and all of its files?").format(name=inst.name)):
            return
        self.instance_manager.remove_instance(inst.instance_id)
        self.launcher.refresh_instance_display()
        self.show_list()
        self.refresh(force=True)

    def edit_selected(self):
        inst = self.selected()
        if not inst:
            messagebox.showinfo(_tr(self.launcher, "GAME_PROFILES_EDIT", _qt_t("QT_EDIT", "Edit")), _qt_t("QT_SELECT_INSTANCE_FIRST", "Select an instance first."))
            return
        self.open_editor(inst)

    def open_selected_folder(self):
        inst = self.selected()
        if not inst:
            messagebox.showinfo(_tr(self.launcher, "RES_SH_OPEN_FOLDER", "Open folder"), _qt_t("QT_SELECT_INSTANCE_FIRST", "Select an instance first."))
            return
        inst.minecraft_dir.mkdir(parents=True, exist_ok=True)
        open_path_native(inst.minecraft_dir)

    def open_editor(self, inst):
        if self.editor is None:
            self.editor = QtInstanceEditor(self.launcher, self)
            self.stack.addWidget(self.editor)
        self.editor.open(inst)
        self.stack.setCurrentWidget(self.editor)

    def show_list(self):
        self.stack.setCurrentWidget(self.list_page)
        self.refresh(force=True)

    def import_instance(self):
        path = _pick_open_file(title=_qt_t("QT_IMPORT_INSTANCE_OR_PACK", "Import instance or pack"), filetypes=[(_qt_t("QT_FT_INSTANCES_PACKS", "Instances and packs"), "*.zip *.orangpack *.mrpack"), (_qt_t("QT_FT_ALL", "All files"), "*.*")])
        if not path:
            return
        low = path.lower()
        if low.endswith((ORANGPACK_EXT, ".mrpack")):
            self.launcher._do_import_mrpack_path(path)
            return
        try:
            with zipfile.ZipFile(path, "r") as zf:
                names = set(zf.namelist())
            if "instance.json" not in names and ("modrinth.index.json" in names or ORANGPACK_INDEX in names):
                self.launcher._do_import_mrpack_path(path)
                return
            if "instance.json" not in names and "manifest.json" in names:
                self.launcher._do_import_curseforge_path(path)
                return
        except Exception:
            pass
        self.launcher.set_status(_qt_t("QT_IMPORTING_INSTANCE", "Importing instance..."))
        def work():
            new_id = str(uuid_module.uuid4())
            dest = InstanceManager.get_instances_dir() / new_id
            dest.mkdir(parents=True, exist_ok=True)
            with zipfile.ZipFile(path, "r") as zf:
                zf.extractall(dest)
            inst_json = dest / "instance.json"
            if not inst_json.exists():
                raise FileNotFoundError(_qt_t("QT_NOT_AN_EXPORT", "instance.json not found in zip - not a valid OrangLauncher export."))
            data = json.loads(inst_json.read_text(encoding="utf-8"))
            data["instance_id"] = new_id
            data["base_path"] = str(dest)
            data["minecraft_dir"] = str(dest / ".minecraft")
            base_name = data.get("name") or "Imported"
            candidate = base_name
            i = 2
            while self.instance_manager.get_instance_by_name(candidate):
                candidate = f"{base_name} {i}"
                i += 1
            data["name"] = candidate
            inst_json.write_text(json.dumps(data, indent=2), encoding="utf-8")
            instance = MinecraftInstance.from_dict(data)
            self.instance_manager.instances[new_id] = instance
            self.instance_manager.save_instances()
            self.launcher._apply_sharing_for_instance(instance)
            return instance
        def done(instance):
            self.launcher.set_status(_qt_t("QT_READY", "Ready"))
            self.launcher.refresh_instance_display()
            self.refresh(force=True)
            messagebox.showinfo(_qt_t("QT_IMPORT", "Import"), _qt_t("QT_IMPORTED_AS", "Imported: {name}").format(name=instance.name))
        _qt_run_bg(work, done, lambda e: (self.launcher.set_status(_qt_t("QT_READY", "Ready")), messagebox.showerror(_qt_t("QT_IMPORT_FAILED", "Import failed"), str(e))))
    def import_mrpack(self):
        path = _pick_open_file(title=_tr(self.launcher, "MODS_IMPORT_TITLE", "Import modpack"), filetypes=[(_qt_t("QT_FT_MODPACKS", "Modpacks"), "*.mrpack *.orangpack"), (_qt_t("QT_FT_ALL", "All files"), "*.*")])
        if path:
            self.launcher._do_import_mrpack_path(path)
_MOD_META_CACHE = {}

def _toml_loads_lenient(text):
    try:
        import tomllib
        return tomllib.loads(text)
    except Exception:
        pass
    mods = []
    current = None
    for line in text.splitlines():
        line = line.strip()
        if line.startswith("[[mods]]"):
            current = {}
            mods.append(current)
            continue
        if line.startswith("[") or current is None or "=" not in line:
            if line.startswith("["):
                current = None
            continue
        key, _, value = line.partition("=")
        value = value.strip()
        if value[:1] in ("'", '"'):
            value = value[1:].split(value[0])[0]
        current[key.strip()] = value
    return {"mods": mods}









def _manifest_value(zf, key):
    try:
        for line in zf.read("META-INF/MANIFEST.MF").decode("utf-8", "replace").splitlines():
            if line.lower().startswith(key.lower() + ":"):
                return line.split(":", 1)[1].strip()
    except Exception:
        pass
    return ""

def read_mod_metadata(path):
    path = Path(path)
    try:
        st = path.stat()
    except OSError:
        return {}
    key = (str(path), st.st_size, int(st.st_mtime))
    cached = _MOD_META_CACHE.get(key)
    if cached is not None:
        return cached
    meta = {}
    try:
        with zipfile.ZipFile(path) as zf:
            names = set(zf.namelist())

            def load_json(name):
                raw = zf.read(name).decode("utf-8-sig", "replace")
                try:
                    return json.loads(raw, strict=False)
                except Exception:
                    raw = re.sub(r",\s*([}\]])", r"\1", raw)
                    return json.loads(raw, strict=False)
            if "fabric.mod.json" in names:
                data = load_json("fabric.mod.json")
                meta = {"name": data.get("name") or data.get("id") or "", "version": str(data.get("version") or ""), "id": data.get("id") or "", "loader": "fabric", "description": data.get("description") or ""}
            elif "quilt.mod.json" in names:
                data = load_json("quilt.mod.json").get("quilt_loader", {})
                md = data.get("metadata", {}) or {}
                meta = {"name": md.get("name") or data.get("id") or "", "version": str(data.get("version") or ""), "id": data.get("id") or "", "loader": "quilt", "description": md.get("description") or ""}
            else:
                toml_name = next((n for n in ("META-INF/neoforge.mods.toml", "META-INF/mods.toml") if n in names), None)
                if toml_name:
                    data = _toml_loads_lenient(zf.read(toml_name).decode("utf-8-sig", "replace"))
                    mods = data.get("mods") or []
                    if mods:
                        first = mods[0]
                        version = str(first.get("version") or "")
                        if "${" in version or not version:
                            version = _manifest_value(zf, "Implementation-Version") or version.replace("${file.jarVersion}", "").strip("${}")
                        meta = {"name": first.get("displayName") or first.get("modId") or "", "version": version, "id": first.get("modId") or "",
                                "loader": "neoforge" if "neoforge" in toml_name else "forge", "description": str(first.get("description") or "").strip()}
                elif "mcmod.info" in names:
                    data = load_json("mcmod.info")
                    if isinstance(data, dict):
                        data = data.get("modList") or []
                    if isinstance(data, list) and data:
                        first = data[0] or {}
                        meta = {"name": first.get("name") or first.get("modid") or "", "version": str(first.get("version") or ""), "id": first.get("modid") or "",
                                "loader": "forge", "description": first.get("description") or ""}
                elif "META-INF/MANIFEST.MF" in names:
                    title = _manifest_value(zf, "Implementation-Title") or _manifest_value(zf, "Specification-Title")
                    if title:
                        meta = {"name": title, "version": _manifest_value(zf, "Implementation-Version"), "id": "", "loader": "", "description": ""}
    except Exception as e:
        print(f"[Mods] metadata read failed for {path.name}: {e}")
        meta = {}
    if meta.get("name"):
        meta["name"] = str(meta["name"]).strip()
    _MOD_META_CACHE[key] = meta
    return meta
# modding tab
class QtModsPanel(QtWidgets.QWidget):
    def __init__(self, editor, parent=None):
        super().__init__(parent)
        self.editor = editor
        self.launcher = editor.launcher
        self.instance = None
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(14, 12, 14, 12)
        lay.setSpacing(8)
        head = QtWidgets.QHBoxLayout()
        self.info = _qt_label("", "h3")
        head.addWidget(self.info)
        self.count = _qt_label("", "muted")
        head.addWidget(self.count)
        head.addStretch(1)
        self.search = QtWidgets.QLineEdit()
        self.search.setPlaceholderText(_qt_t("QT_FILTER_MODS", "Filter mods..."))
        self.search.setClearButtonEnabled(True)
        self.search.setFixedWidth(220)
        self.search.textChanged.connect(lambda *_: self.refresh())
        head.addWidget(self.search)
        lay.addLayout(head)
        self.warning = _qt_label(_tr(self.launcher, "MODS_LOADER_WARNING", "This instance runs vanilla Minecraft. Mods need Forge, NeoForge, Fabric or Quilt (Versions tab)."), "accent", wrap=True)
        lay.addWidget(self.warning)
        self.list = QtWidgets.QTreeWidget()
        self.list.setHeaderLabels([_qt_t("QT_MOD_NAME", "Mod"), _qt_t("QT_VERSION", "Version"), _qt_t("QT_MOD_FILE", "File")])
        self.list.setRootIsDecorated(False)
        self.list.setAlternatingRowColors(False)
        self.list.setSelectionMode(QtWidgets.QAbstractItemView.ExtendedSelection)
        self.list.setSelectionBehavior(QtWidgets.QAbstractItemView.SelectRows)
        hh = self.list.header()
        hh.setSectionResizeMode(0, QtWidgets.QHeaderView.Stretch)
        hh.setSectionResizeMode(1, QtWidgets.QHeaderView.ResizeToContents)
        hh.setSectionResizeMode(2, QtWidgets.QHeaderView.Stretch)
        self.list.itemSelectionChanged.connect(self._on_select)
        self.list.itemDoubleClicked.connect(lambda item, col: self.toggle_selected())
        lay.addWidget(self.list, 1)
        self.selected_info = _qt_label(_tr(self.launcher, "MODS_SELECT_MOD_INFO", "Select a mod to see details."), "hint")
        self.selected_info.setWordWrap(True)
        lay.addWidget(self.selected_info)
        self._meta_token = 0
        bar = QtWidgets.QHBoxLayout()
        L = self.launcher
        bar.addWidget(_qt_button(_tr(L, "MODS_ADD_BTN", "Add mods"), self.add_mods, kind="accent", icon="plus", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "MODS_REMOVE_BTN", "Remove"), self.remove_selected, icon="trash", launcher=L))
        bar.addWidget(_qt_button(_qt_t("QT_ENABLE_DISABLE", "Enable / disable"), self.toggle_selected, icon="switch", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "MODS_UPDATE_BTN", "Update mods"), self.update_mods, icon="update", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "MODS_RESTORE_BACKUPS", "Restore backups"), self.restore_backups, icon="refresh", launcher=L))
        bar.addWidget(_qt_button(_tr(L, "MODS_OPEN_FOLDER_BTN", "Open folder"), self.open_folder, icon="folder", launcher=L))
        bar.addWidget(_qt_button(_qt_t("QT_GET_MODS", "Get mods (Modrinth)"), self.go_content, icon="file", launcher=L))
        bar.addStretch(1)
        lay.addLayout(bar)

# btw the QT is new like addon from where it got to pyside, don't trust it fully

    def load(self, instance):
        self.instance = instance
        self.refresh()

    def _mods_dir(self):
        d = self.instance.mods_dir
        return d.resolve() if d.is_symlink() else d

    def refresh(self):
        self.list.clear()
        if self.instance is None:
            return
        inst = self.instance
        self.info.setText(f"{inst.name}  ·  {inst.version}  ·  {(inst.mod_loader or 'vanilla').title()}")
        self.warning.setVisible((inst.mod_loader or "vanilla").lower() in ("vanilla", "none", ""))
        d = self._mods_dir()
        q = self.search.text().strip().lower()
        mods = []
        if d.exists():
            for f in sorted(d.iterdir(), key=lambda p: p.name.lower()):
                if not f.is_file():
                    continue
                low = f.name.lower()
                if low.endswith(".jar") or low.endswith(".jar.disabled"):
                    mods.append(f)
        disabled_color = QtGui.QColor(self.launcher.theme.c("fg_disabled"))
        disabled_tip = _qt_t("QT_MOD_DISABLED_TIP", "Disabled (renamed to .jar.disabled)")
        rows = {}
        for f in mods:
            cached = _MOD_META_CACHE.get((str(f), f.stat().st_size, int(f.stat().st_mtime))) if f.exists() else None
            name = (cached or {}).get("name") or f.name
            version = (cached or {}).get("version") or ""
            if q and q not in f.name.lower() and q not in name.lower():
                continue
            item = QtWidgets.QTreeWidgetItem([name, version, f.name])
            item.setData(0, Qt.UserRole, str(f))
            font = item.font(0)
            font.setBold(True)
            item.setFont(0, font)
            if f.name.lower().endswith(".disabled"):
                for c in range(3):
                    item.setForeground(c, disabled_color)
                item.setToolTip(0, disabled_tip)
            self.list.addTopLevelItem(item)
            rows[str(f)] = item
        self.count.setText(_tr(self.launcher, "MODS_COUNT", "{n} mods").format(n=len(mods)) if len(mods) else _tr(self.launcher, "MODS_NONE_INSTALLED", "No mods installed"))
        self._meta_token += 1
        token = self._meta_token
        pending = [f for f in mods if str(f) in rows and _MOD_META_CACHE.get((str(f), f.stat().st_size, int(f.stat().st_mtime))) is None]
        if not pending:
            return

        def work():
            out = []
            for f in pending:
                if token != self._meta_token:
                    break
                out.append((str(f), read_mod_metadata(f)))
            return out

        def done(results):
            if token != self._meta_token:
                return
            for path, meta in results:
                item = rows.get(path)
                if item is None or not meta.get("name"):
                    continue
                try:
                    item.setText(0, meta["name"])
                    item.setText(1, meta.get("version") or "")
                    if meta.get("description"):
                        item.setToolTip(0, meta["description"][:400])
                except RuntimeError:
                    return
            if q:
                for path, meta in results:
                    item = rows.get(path)
                    if item is not None and q not in os.path.basename(path).lower() and q not in (meta.get("name") or "").lower():
                        item.setHidden(True)
        _qt_run_bg(work, done, lambda e: None)

    def _selected_paths(self):
        return [Path(item.data(0, Qt.UserRole)) for item in self.list.selectedItems() if not item.isHidden()]

    def _on_select(self):
        paths = self._selected_paths()
        if not paths:
            self.selected_info.setText(_tr(self.launcher, "MODS_SELECT_MOD_INFO", "Select a mod to see details."))
        elif len(paths) == 1:
            p = paths[0]
            meta = read_mod_metadata(p) if p.exists() else {}
            parts = [meta.get("name") or p.name]
            if meta.get("version"):
                parts.append("v" + meta["version"])
            if meta.get("id"):
                parts.append(meta["id"])
            if meta.get("loader"):
                parts.append(meta["loader"])
            parts.append(p.name)
            try:
                parts.append(_human_size(p.stat().st_size))
            except Exception:
                pass
            text = "  ·  ".join(parts)
            if meta.get("description"):
                text += "\n" + meta["description"][:300]
            self.selected_info.setText(text)
        else:
            self.selected_info.setText(_qt_t("QT_MODS_SELECTED", "{n} mods selected").format(n=len(paths)))

    # picker
    def add_mods(self):
        if (self.instance.mod_loader or "vanilla").lower() in ("vanilla", "none", ""):
            if not messagebox.askyesno(_tr(self.launcher, "MODS_NO_LOADER_TITLE", "No mod loader"), _tr(self.launcher, "MODS_NO_LOADER_MSG", "This instance has no mod loader, mods will not load. Add them anyway?")):
                return
        files = _pick_open_files(title=_tr(self.launcher, "MODS_FILE_SELECT_TITLE", "Select mods"), filetypes=[(_qt_t("QT_FT_MODS", "Mod files"), "*.jar *.zip"), (_qt_t("QT_FT_ALL", "All files"), "*.*")])
        if not files:
            return
        d = self._mods_dir()
        d.mkdir(parents=True, exist_ok=True)
        added = 0
        for f in files:
            try:
                shutil.copy2(f, d / os.path.basename(f))
                added += 1
            except Exception as e:
                print(f"[Mods] add {f}: {e}")
        self.refresh()
        self.launcher.set_status(_qt_t("QT_MODS_ADDED", "Added {n} mod(s)").format(n=added))

    def remove_selected(self):
        paths = self._selected_paths()
        if not paths:
            messagebox.showinfo(_tr(self.launcher, "MODS_REMOVE_TITLE", "Remove mods"), _tr(self.launcher, "MODS_REMOVE_NONE", "Select mods first."))
            return
        msg = _tr(self.launcher, "MODS_REMOVE_CONFIRM_SINGLE", "Remove {name}?").format(name=paths[0].name) if len(paths) == 1 else _tr(self.launcher, "MODS_REMOVE_CONFIRM_MULTI", "Remove {count} mods?").format(count=len(paths))
        if not messagebox.askyesno(_tr(self.launcher, "MODS_REMOVE_CONFIRM_TITLE", "Remove"), msg):
            return
        for p in paths:
            try:
                p.unlink()
            except Exception as e:
                print(f"[Mods] remove {p}: {e}")
        self.refresh()

    def toggle_selected(self):
        paths = self._selected_paths()
        if not paths:
            return
        for p in paths:
            try:
                if p.name.lower().endswith(".disabled"):
                    p.rename(p.with_name(p.name[:-len(".disabled")]))
                else:
                    p.rename(p.with_name(p.name + ".disabled"))
            except Exception as e:
                print(f"[Mods] toggle {p}: {e}")
        self.refresh()

    def open_folder(self):
        d = self._mods_dir()
        d.mkdir(parents=True, exist_ok=True)
        open_path_native(d)

    def go_content(self):
        self.launcher.instance_manager.set_selected_instance(self.instance.instance_id)
        self.launcher.show_content("mod")

    def restore_backups(self):
        d = self._mods_dir()
        baks = list(d.glob("*.jar.bak")) if d.exists() else []
        if not baks:
            messagebox.showinfo(_tr(self.launcher, "MODS_BACKUP_NONE", "No backups"), _tr(self.launcher, "MODS_BACKUP_NONE_MSG", "No .bak backup files found."))
            return
        if not messagebox.askyesno(_tr(self.launcher, "MODS_BACKUP_RESTORE_TITLE", "Restore backups"), _tr(self.launcher, "MODS_BACKUP_RESTORE_CONFIRM", "Restore {count} backup(s)?").format(count=len(baks))):
            return
        restored = 0
        for bak in baks:
            try:
                orig = bak.with_suffix("")
                if orig.exists():
                    orig.unlink()
                shutil.move(str(bak), str(orig))
                restored += 1
            except Exception as e:
                print(f"[Mods] restore {bak}: {e}")
        messagebox.showinfo(_tr(self.launcher, "MODS_BACKUP_RESTORE_DONE", "Restore complete"), _qt_t("QT_RESTORED_BACKUPS", "Restored {n} backup(s).").format(n=restored))
        self.refresh()

    def update_mods(self):
        inst = self.instance
        d = self._mods_dir()
        if not d.exists() or not any(d.glob("*.jar")):
            messagebox.showinfo(_tr(self.launcher, "MODS_UPDATE_TITLE", "Update mods"), _qt_t("QT_NO_MODS_TO_UPDATE", "No mods to update."))
            return
        QtPackUpdateDialog(self.launcher, d, (inst.mod_loader or "").lower(), inst.version, (".jar",), _tr(self.launcher, "MODS_UPDATE_TITLE", "Update mods"), on_done=self.refresh, parent=self).start()


class QtPackUpdateDialog(QtWidgets.QDialog):
    def __init__(self, launcher, directory, loader, game_version, exts, title, on_done=None, parent=None):
        super().__init__(parent or launcher)
        self.launcher = launcher
        self.directory = Path(directory)
        self.loader = loader
        self.game_version = game_version
        self.exts = exts
        self.title_text = title
        self.on_done = on_done
        self.setWindowTitle(title)
        self.resize(640, 460)
        self.cancel_event = threading.Event()
        self.updater = ModrinthUpdater(logger=lambda m: launcher._safe_append_log(m))
        lay = QtWidgets.QVBoxLayout(self)
        self.head = _qt_label(_qt_t("QT_CHECKING_ON_MODRINTH", "Checking {name} on Modrinth...").format(name=self.directory.name), "h2")
        lay.addWidget(self.head)
        self.bar = QtWidgets.QProgressBar()
        self.bar.setRange(0, 100)
        lay.addWidget(self.bar)
        self.status = _qt_label(_qt_t("QT_HASHING_FILES", "Hashing files..."), "muted", wrap=True)
        lay.addWidget(self.status)
        self.list = QtWidgets.QListWidget()
        self.list.setVisible(False)
        lay.addWidget(self.list, 1)
        self.force_check = QtWidgets.QCheckBox(_qt_t("QT_ALLOW_DOWNGRADES", "Allow downgrades / re-downloads (force)"))
        self.force_check.setVisible(False)
        lay.addWidget(self.force_check)
        btns = QtWidgets.QHBoxLayout()
        btns.addStretch(1)
        self.select_all_btn = _qt_button(_tr(launcher, "SELECT_ALL", "Select all"), lambda: self._set_all(True))
        self.deselect_btn = _qt_button(_tr(launcher, "DESELECT_ALL", "Deselect all"), lambda: self._set_all(False))
        self.apply_btn = _qt_button(_qt_t("QT_UPDATE_SELECTED", "Update selected"), self.apply_selected, kind="accent")
        for b in (self.select_all_btn, self.deselect_btn, self.apply_btn):
            b.setVisible(False)
            btns.addWidget(b)
        self.cancel_btn = _qt_button(_tr(launcher, "CANCEL", "Cancel"), self._cancel)
        btns.addWidget(self.cancel_btn)
        lay.addLayout(btns)
        self.candidates = {}
        self.downgrades = {}

    def _cancel(self):
        self.cancel_event.set()
        self.reject()

    def _set_all(self, checked):
        for i in range(self.list.count()):
            item = self.list.item(i)
            if item.flags() & Qt.ItemIsUserCheckable:
                item.setCheckState(Qt.Checked if checked else Qt.Unchecked)

    def start(self):
        self.show()

        def progress(cur, total, msg):
            _qt_later(lambda: (self.bar.setValue(int(cur / max(total, 1) * 100)), self.status.setText(str(msg))))

        def work():
            return self.updater.scan_dir(self.directory, self.loader, self.game_version, exts=self.exts, progress=progress, stop_event=self.cancel_event)

        def done(scan):
            if self.cancel_event.is_set():
                return
            self.candidates = scan.get("candidates", {}) or {}
            self.downgrades = scan.get("downgrades", {}) or {}
            if not self.candidates and not self.downgrades:
                self.accept()
                messagebox.showinfo(self.title_text, _qt_t("QT_ALL_UP_TO_DATE", "Everything is up to date (or not found on Modrinth)."))
                if self.on_done:
                    self.on_done()
                return
            self.head.setText(_qt_t("QT_UPDATES_AVAILABLE", "Updates available"))
            self.bar.setValue(100)
            self.status.setText(_qt_t("QT_UPDATES_FOUND", "{n} update(s) found").format(n=len(self.candidates)) + ((", " + _qt_t("QT_DOWNGRADES_SKIPPED", "{n} skipped by downgrade protection").format(n=len(self.downgrades))) if self.downgrades else ""))
            for name, info in self.candidates.items():
                item = QtWidgets.QListWidgetItem(f"{info.get('project_title')}: {info.get('current_version_number') or '?'}  →  {info.get('version_number')}    ({name})")
                item.setFlags(item.flags() | Qt.ItemIsUserCheckable)
                item.setCheckState(Qt.Checked)
                item.setData(Qt.UserRole, name)
                self.list.addItem(item)
            for name, info in self.downgrades.items():
                item = QtWidgets.QListWidgetItem(f"{info.get('project_title')}: {info.get('current_version_number') or '?'}  →  {info.get('version_number')}    ({name})  [{_qt_t('QT_OLDER_THAN_INSTALLED', 'older than installed')}]")
                item.setFlags(item.flags() | Qt.ItemIsUserCheckable)
                item.setCheckState(Qt.Unchecked)
                item.setData(Qt.UserRole, name)
                item.setForeground(QtGui.QColor(self.launcher.theme.c("fg_disabled")))
                self.list.addItem(item)
            self.list.setVisible(True)
            self.force_check.setVisible(bool(self.downgrades))
            for b in (self.select_all_btn, self.deselect_btn, self.apply_btn):
                b.setVisible(True)

        def fail(err):
            self.status.setText(_qt_t("QT_FAILED_WITH", "Failed: {error}").format(error=err))
            self.bar.setValue(0)
        _qt_run_bg(work, done, fail)

    def apply_selected(self):
        names = [self.list.item(i).data(Qt.UserRole) for i in range(self.list.count()) if self.list.item(i).checkState() == Qt.Checked]
        if not names:
            return
        force = self.force_check.isChecked()
        self.apply_btn.setEnabled(False)
        self.head.setText(_qt_t("QT_UPDATING", "Updating..."))
        self.bar.setValue(0)

        def work():
            done_n, errors = 0, []
            for idx, n in enumerate(names, 1):
                if self.cancel_event.is_set():
                    break
                _qt_later(lambda i=idx, nm=n: (self.bar.setValue(int((i - 1) / len(names) * 100)), self.status.setText(_qt_t("QT_UPDATING_NAME", "Updating {name}...").format(name=nm))))
                info = self.candidates.get(n) or self.downgrades.get(n)
                try:
                    ok, msg = self.updater.apply_candidate(info, force=force or n in self.downgrades)
                except Exception as e:
                    ok, msg = False, str(e)
                if ok:
                    done_n += 1
                else:
                    errors.append(f"{n}: {msg}")
            return done_n, errors

        def finished(result):
            done_n, errors = result
            self.accept()
            text = _qt_t("QT_UPDATED_N_OF", "Updated {done} of {total}.").format(done=done_n, total=len(names))
            if errors:
                text += "\n\n" + "\n".join(errors[:10])
            self.launcher._safe_append_log(f"[Updater] {text}")
            messagebox.showinfo(self.title_text, text)
            if self.on_done:
                self.on_done()
        _qt_run_bg(work, finished, lambda e: (self.accept(), messagebox.showerror(self.title_text, str(e))))

# servers and worlds tab
class QtServersWorldsPanel(QtWidgets.QWidget):
    def __init__(self, editor, parent=None):
        super().__init__(parent)
        self.editor = editor
        self.launcher = editor.launcher
        self.instance = None
        self.servers = []
        self.worlds = []
        self.rows = []
        self._ping_token = 0
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(14, 12, 14, 12)
        lay.setSpacing(8)
        head = QtWidgets.QHBoxLayout()
        head.addWidget(_qt_label(_qt_t("QT_TAB_SERVERS_WORLDS", "Servers and Worlds"), "h2"))
        self.info = _qt_label("", "muted")
        head.addWidget(self.info)
        head.addStretch(1)
        L = self.launcher
        head.addWidget(_qt_button(_qt_t("QT_REFRESH", "Refresh"), self.refresh, icon="refresh", launcher=L))
        head.addWidget(_qt_button(_tr(L, "SERVERS_ADD", "Add server"), self.add_server, icon="plus", launcher=L))
        self.join_btn = _qt_button(_qt_t("QT_JOIN", "Join"), self.quick_play, kind="accent")
        head.addWidget(self.join_btn)
        self.edit_btn = _qt_button(_qt_t("QT_EDIT", "Edit"), self.edit_server, icon="settings", launcher=L)
        head.addWidget(self.edit_btn)
        self.rename_btn = _qt_button(_qt_t("QT_RENAME", "Rename"), self.rename_world, icon="settings", launcher=L)
        head.addWidget(self.rename_btn)
        self.folder_btn = _qt_button(_qt_t("QT_FOLDER", "Folder"), self.open_folder, icon="folder", launcher=L)
        head.addWidget(self.folder_btn)
        self.del_btn = _qt_button(_qt_t("QT_DELETE", "Delete"), self.delete_selected, icon="trash", launcher=L)
        head.addWidget(self.del_btn)
        lay.addLayout(head)
        self.table = QtWidgets.QTableWidget(0, 5)
        self.table.setHorizontalHeaderLabels(["", _qt_t("QT_NAME", "Name"), _qt_t("QT_ADDRESS", "Address") + " / " + _qt_t("QT_FOLDER", "Folder"), _qt_t("QT_STATUS", "Status") + " / " + _qt_t("QT_SIZE", "Size"), _qt_t("QT_LAST_PLAYED", "Last played")])
        hh = self.table.horizontalHeader()
        hh.setSectionResizeMode(0, QtWidgets.QHeaderView.ResizeToContents)
        hh.setSectionResizeMode(1, QtWidgets.QHeaderView.Stretch)
        hh.setSectionResizeMode(2, QtWidgets.QHeaderView.Stretch)
        hh.setSectionResizeMode(3, QtWidgets.QHeaderView.ResizeToContents)
        hh.setSectionResizeMode(4, QtWidgets.QHeaderView.ResizeToContents)
        self.table.verticalHeader().setVisible(False)
        self.table.setIconSize(QSize(32, 32))
        self.table.setSelectionBehavior(QtWidgets.QAbstractItemView.SelectRows)
        self.table.setSelectionMode(QtWidgets.QAbstractItemView.SingleSelection)
        self.table.setEditTriggers(QtWidgets.QAbstractItemView.NoEditTriggers)
        self.table.setShowGrid(False)
        self.table.itemSelectionChanged.connect(self._on_select)
        self.table.doubleClicked.connect(lambda *_: self._activate())
        lay.addWidget(self.table, 1)
        self._on_select()

    def load(self, instance):
        self.instance = instance
        self.refresh()

    def _servers_path(self):
        return self.instance.minecraft_dir / "servers.dat" if self.instance else None

    def _selected(self):
        rows = self.table.selectionModel().selectedRows() if self.table.selectionModel() else []
        if not rows:
            return None
        return self.rows[rows[0].row()] if rows[0].row() < len(self.rows) else None

    def _on_select(self):
        sel = self._selected()
        is_srv = bool(sel and sel["kind"] == "server")
        is_world = bool(sel and sel["kind"] == "world")
        self.join_btn.setEnabled(is_srv)
        self.edit_btn.setEnabled(is_srv)
        self.rename_btn.setEnabled(is_world)
        self.folder_btn.setEnabled(is_world)
        self.del_btn.setEnabled(bool(sel))

    def _activate(self):
        sel = self._selected()
        if not sel:
            return
        if sel["kind"] == "server":
            self.quick_play()
        else:
            open_path_native(sel["world"]["path"])

    def refresh(self):
        self.table.setRowCount(0)
        self.rows = []
        if self.instance is None:
            return
        path = self._servers_path()
        self.servers = ServersNBT.read_servers_dat(path) if path and path.exists() else []
        self.worlds = list_instance_worlds(self.instance)
        server_icon = find_resource("oranglauncher/images/icons/server.png")
        world_icon = find_resource("oranglauncher/images/minecraft-green.png")
        for idx, srv in enumerate(self.servers):
            icon = None
            raw = srv.get("icon")
            if raw:
                try:
                    pix = _qt_pixmap_from_bytes(base64.b64decode(raw), (32, 32))
                    icon = QtGui.QIcon(pix) if pix else None
                except Exception:
                    icon = None
            if icon is None and server_icon:
                icon = QtGui.QIcon(str(server_icon))
            self._add_row("server", icon, srv.get("name", "Unknown"), srv.get("ip", ""), "⏳", "", {"kind": "server", "index": idx})
        for w in self.worlds:
            size = w["size"]
            size_text = f"{size / (1024 * 1024):.1f} MB" if size < 1024 ** 3 else f"{size / (1024 ** 3):.2f} GB"
            icon = QtGui.QIcon(str(world_icon)) if world_icon else None
            self._add_row("world", icon, w["name"], w["folder"], size_text, w["modified"].strftime("%Y-%m-%d %H:%M"), {"kind": "world", "world": w})
        self.info.setText(_qt_t("QT_SERVERS_COUNT", "{n} server(s)").format(n=len(self.servers)) + "  ·  " + _qt_t("QT_WORLDS_COUNT", "{n} worlds").format(n=len(self.worlds)))
        self._on_select()
        self._ping_all()

    def _add_row(self, kind, icon, name, second, third, fourth, meta):
        r = self.table.rowCount()
        self.table.insertRow(r)
        first = QtWidgets.QTableWidgetItem("")
        if icon is not None:
            first.setIcon(icon)
        first.setToolTip(_qt_t("QT_SERVER", "Server") if kind == "server" else _qt_t("QT_WORLD", "World"))
        self.table.setItem(r, 0, first)
        for c, text in enumerate((name, second, third, fourth), start=1):
            item = QtWidgets.QTableWidgetItem(text)
            if c == 1:
                font = item.font()
                font.setBold(True)
                item.setFont(font)
            self.table.setItem(r, c, item)
        self.table.setRowHeight(r, 38)
        self.rows.append(meta)

    def _ping_all(self):
        self._ping_token += 1
        token = self._ping_token
        for row_idx, meta in enumerate(self.rows):
            if meta["kind"] != "server":
                continue
            srv = self.servers[meta["index"]]
            host, _, port_str = srv.get("ip", "").partition(":")
            port = int(port_str) if port_str.isdigit() else 25565

            def work(h=host, p=port):
                return _slp_ping(h, p)

            def done(result, r=row_idx):
                if token != self._ping_token or r >= self.table.rowCount():
                    return
                status = self.table.item(r, 3)
                if status is None:
                    return
                if result:
                    ms = result.get("latency", 0)
                    status.setText(_qt_t("QT_ONLINE", "{online}/{max} online").format(online=result["online"], max=result["max"]) + f"  ·  {int(ms)} ms")
                    status.setForeground(QtGui.QColor("#3bc652" if ms < 80 else ("#ffc107" if ms < 200 else "#f25c5c")))
                    if result.get("motd"):
                        self.table.item(r, 2).setToolTip(_strip_mc_formatting(result["motd"]))
                    fav = result.get("favicon")
                    if fav:
                        try:
                            pix = _qt_pixmap_from_bytes(base64.b64decode(fav.split(",")[-1]), (32, 32))
                            if pix:
                                self.table.item(r, 0).setIcon(QtGui.QIcon(pix))
                        except Exception:
                            pass
                else:
                    status.setText(_qt_t("QT_OFFLINE", "Offline"))
                    status.setForeground(QtGui.QColor("#f25c5c"))
            _qt_run_bg(work, done, lambda e: None)
    # this works only from 1.20.1
    def quick_play(self):
        sel = self._selected()
        if not sel or sel["kind"] != "server":
            return
        ver = self.instance.version if self.instance else ""
        if not ver or _mc_version_tuple(ver) < (1, 20, 1):
            messagebox.showinfo(_tr(self.launcher, "SERVERS_QUICKPLAY", "Quick Play"), _qt_t("QT_QUICKPLAY_REQUIRES", "Quick Play requires Minecraft 1.20.1 or newer."))
            return
        ip = self.servers[sel["index"]].get("ip", "").strip()
        if not ip:
            return
        self.launcher.instance_manager.set_selected_instance(self.instance.instance_id)
        self.launcher._pending_quickplay = ip
        self.launcher._launch_game()

    def _dialog(self, edit_idx=None):
        srv = self.servers[edit_idx] if edit_idx is not None else {}
        dlg = QtWidgets.QDialog(self)
        dlg.setWindowTitle(_tr(self.launcher, "SERVERS_EDIT_TITLE", "Edit server") if edit_idx is not None else _tr(self.launcher, "SERVERS_ADD_TITLE", "Add server"))
        lay = QtWidgets.QFormLayout(dlg)
        name = QtWidgets.QLineEdit(srv.get("name", ""))
        ip = QtWidgets.QLineEdit(srv.get("ip", ""))
        lay.addRow(_tr(self.launcher, "SERVERS_NAME_LABEL", "Name"), name)
        lay.addRow(_tr(self.launcher, "SERVERS_ADDRESS_LABEL", "Address"), ip)
        btns = QtWidgets.QDialogButtonBox(QtWidgets.QDialogButtonBox.Save | QtWidgets.QDialogButtonBox.Cancel)
        btns.accepted.connect(dlg.accept)
        btns.rejected.connect(dlg.reject)
        lay.addRow(btns)
        dlg.resize(420, dlg.sizeHint().height())
        if dlg.exec() != QtWidgets.QDialog.Accepted:
            return
        n, a = name.text().strip(), ip.text().strip()
        if not n or not a:
            messagebox.showwarning(_tr(self.launcher, "SERVERS_INVALID_TITLE", "Invalid"), _tr(self.launcher, "SERVERS_INVALID_MSG", "Name and address are required."))
            return
        new = {"name": n, "ip": a}
        if edit_idx is not None:
            for k in ("icon", "hidden"):
                if k in self.servers[edit_idx]:
                    new[k] = self.servers[edit_idx][k]
            self.servers[edit_idx] = new
        else:
            self.servers.append(new)
        self._save_servers()

    def add_server(self):
        self._dialog()

    def edit_server(self):
        sel = self._selected()
        if sel and sel["kind"] == "server":
            self._dialog(sel["index"])

    def _save_servers(self):
        path = self._servers_path()
        if path:
            try:
                path.parent.mkdir(parents=True, exist_ok=True)
                ServersNBT.write_servers_dat(path, self.servers)
            except Exception as e:
                messagebox.showerror(_tr(self.launcher, "ERROR", "Error"), str(e))
        self.refresh()

    def open_folder(self):
        sel = self._selected()
        if sel and sel["kind"] == "world":
            open_path_native(sel["world"]["path"])
        elif self.instance:
            open_path_native(self.instance.saves_dir)

    def rename_world(self):
        sel = self._selected()
        if not sel or sel["kind"] != "world":
            messagebox.showinfo(_qt_t("QT_WORLDS", "Worlds"), _qt_t("QT_SELECT_WORLD", "Select a world first."))
            return
        w = sel["world"]
        new_name = _qt_askstring(_qt_t("QT_RENAME", "Rename"), _qt_t("QT_WORLD_NAME", "World name") + ":", initialvalue=w["name"])
        if not new_name or new_name.strip() == w["name"]:
            return
        new_name = new_name.strip()
        if not _set_levelname_in_level_dat(w["path"] / "level.dat", new_name):
            messagebox.showerror(_qt_t("QT_RENAME", "Rename"), _qt_t("QT_LEVEL_DAT_FAIL", "Could not update level.dat."))
            return
        safe = re.sub(r'[<>:"/\\|?*]', "_", new_name).strip() or w["folder"]
        target = w["path"].parent / safe
        if target != w["path"] and not target.exists():
            try:
                w["path"].rename(target)
            except Exception as e:
                print(f"[World] folder rename skipped: {e}")
        self.refresh()

    def delete_selected(self):
        sel = self._selected()
        if not sel:
            return
        if sel["kind"] == "server":
            srv = self.servers[sel["index"]]
            if messagebox.askyesno(_tr(self.launcher, "SERVERS_DELETE_TITLE", "Delete server"), _tr(self.launcher, "SERVERS_DELETE_CONFIRM", "Delete {name}?").format(name=srv.get("name", "?"))):
                self.servers.pop(sel["index"])
                self._save_servers()
            return
        w = sel["world"]
        if not messagebox.askyesno(_qt_t("QT_DELETE", "Delete"), _qt_t("QT_DELETE_WORLD_CONFIRM", "Permanently delete the world '{name}'?\n\nFolder: {path}\nThis cannot be undone.").format(name=w['name'], path=w['path'])):
            return
        try:
            shutil.rmtree(w["path"])
        except Exception as e:
            messagebox.showerror(_qt_t("QT_DELETE", "Delete"), str(e))
        self.refresh()

# sh and rs
class QtPacksPanel(QtWidgets.QWidget):
    def __init__(self, editor, parent=None):
        super().__init__(parent)
        self.editor = editor
        self.launcher = editor.launcher
        self.instance = None
        lay = QtWidgets.QHBoxLayout(self)
        lay.setContentsMargins(14, 12, 14, 12)
        lay.setSpacing(12)
        self.rp = self._column("resourcepacks", _tr(self.launcher, "RES_SH_RP_TITLE", "Resource packs"))
        self.sp = self._column("shaderpacks", _tr(self.launcher, "RES_SH_SP_TITLE", "Shader packs"))
        lay.addWidget(self.rp["box"], 1)
        lay.addWidget(self.sp["box"], 1)

    def _column(self, kind, title):
        box = QtWidgets.QWidget()
        lay = QtWidgets.QVBoxLayout(box)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(6)
        head = QtWidgets.QHBoxLayout()
        head.addWidget(_qt_label(title, "h2"))
        count = _qt_label("", "muted")
        head.addWidget(count)
        head.addStretch(1)
        lay.addLayout(head)
        lst = QtWidgets.QListWidget()
        lst.setSelectionMode(QtWidgets.QAbstractItemView.ExtendedSelection)
        lay.addWidget(lst, 1)
        info = _qt_label("", "hint")
        lay.addWidget(info)
        L = self.launcher
        b1 = QtWidgets.QHBoxLayout()
        b1.addWidget(_qt_button(_tr(L, "RES_SH_RP_ADD", _qt_t("QT_ADD", "Add")) if kind == "resourcepacks" else _tr(L, "RES_SH_SP_ADD", _qt_t("QT_ADD", "Add")), lambda: self.add(kind), kind="accent", icon="plus", launcher=L))
        b1.addWidget(_qt_button(_tr(L, "RES_SH_REMOVE_SELECTED", "Remove"), lambda: self.remove(kind), icon="trash", launcher=L))
        b1.addWidget(_qt_button(_tr(L, "RES_SH_OPEN_FOLDER", "Open folder"), lambda: self.open_folder(kind), icon="folder", launcher=L))
        b1.addStretch(1)
        lay.addLayout(b1)
        b2 = QtWidgets.QHBoxLayout()
        b2.addWidget(_qt_button(_qt_t("QT_UPDATE_MODRINTH", "Update (Modrinth)"), lambda: self.update(kind), icon="update", launcher=L))
        b2.addWidget(_qt_button(_qt_t("QT_BROWSE_MODRINTH", "Browse Modrinth"), lambda: self.browse(kind), icon="file", launcher=L))
        b2.addStretch(1)
        lay.addLayout(b2)
        col = {"box": box, "list": lst, "count": count, "info": info, "kind": kind}
        lst.itemSelectionChanged.connect(lambda c=col: self._on_select(c))
        return col

    def _dir(self, kind):
        d = self.instance.resourcepacks_dir if kind == "resourcepacks" else self.instance.shaderpacks_dir
        return d.resolve() if d.is_symlink() else d

    def load(self, instance):
        self.instance = instance
        self.refresh()

    def refresh(self):
        for col in (self.rp, self.sp):
            col["list"].clear()
            if self.instance is None:
                continue
            d = self._dir(col["kind"])
            names = []
            if d.exists():
                for item in sorted(d.iterdir(), key=lambda p: p.name.lower()):
                    if item.is_file() and item.suffix.lower() == ".zip":
                        names.append(item.name)
                    elif item.is_dir() and not item.name.startswith("."):
                        names.append(item.name)
            for n in names:
                col["list"].addItem(n)
            col["count"].setText(_qt_t("QT_PACKS_COUNT", "{n} pack(s)").format(n=len(names)))

    def _on_select(self, col):
        items = col["list"].selectedItems()
        if len(items) == 1:
            p = self._dir(col["kind"]) / items[0].text()
            try:
                size = p.stat().st_size if p.is_file() else sum(f.stat().st_size for f in p.rglob("*") if f.is_file())
                col["info"].setText(f"{items[0].text()}  ·  {_human_size(size)}")
            except Exception:
                col["info"].setText(items[0].text())
        elif items:
            col["info"].setText(_qt_t("QT_N_SELECTED", "{n} selected").format(n=len(items)))
        else:
            col["info"].setText("")

    def add(self, kind):
        files = _pick_open_files(title=_qt_t("QT_SELECT_PACKS", "Select packs"), filetypes=[(_qt_t("QT_FT_PACKS", "Packs"), "*.zip"), (_qt_t("QT_FT_ALL", "All files"), "*.*")])
        if not files:
            return
        d = self._dir(kind)
        d.mkdir(parents=True, exist_ok=True)
        for f in files:
            try:
                shutil.copy2(f, d / os.path.basename(f))
            except Exception as e:
                print(f"[Packs] add {f}: {e}")
        self.refresh()

    def remove(self, kind):
        col = self.rp if kind == "resourcepacks" else self.sp
        names = [i.text() for i in col["list"].selectedItems()]
        if not names:
            messagebox.showinfo(_qt_t("QT_PACKS", "Packs"), _qt_t("QT_SELECT_PACKS_FIRST", "Select packs first."))
            return
        if not messagebox.askyesno(_tr(self.launcher, "CONFIRM_REMOVE", "Remove"), _qt_t("QT_REMOVE_PACKS_CONFIRM", "Remove {n} pack(s)?").format(n=len(names))):
            return
        d = self._dir(kind)
        for n in names:
            p = d / n
            try:
                if p.is_dir():
                    shutil.rmtree(p)
                else:
                    p.unlink()
            except Exception as e:
                print(f"[Packs] remove {n}: {e}")
        self.refresh()

    def open_folder(self, kind):
        d = self._dir(kind)
        d.mkdir(parents=True, exist_ok=True)
        open_path_native(d)

    def update(self, kind):
        inst = self.instance
        d = self._dir(kind)
        loader = "minecraft" if kind == "resourcepacks" else ("iris" if (inst.mod_loader or "").lower() in ("fabric", "quilt", "neoforge", "forge") else "optifine")
        if not d.exists():
            messagebox.showinfo(_qt_t("QT_UPDATE", "Update"), _qt_t("QT_FOLDER_MISSING", "Folder does not exist yet."))
            return
        title = _tr(self.launcher, "RES_SH_RP_TITLE", "Resource packs") if kind == "resourcepacks" else _tr(self.launcher, "RES_SH_SP_TITLE", "Shader packs")
        QtPackUpdateDialog(self.launcher, d, loader, inst.version, (".zip",), _qt_t("QT_UPDATE", "Update") + ": " + title, on_done=self.refresh, parent=self).start()

    def browse(self, kind):
        self.launcher.instance_manager.set_selected_instance(self.instance.instance_id)
        self.launcher.show_content("resourcepack" if kind == "resourcepacks" else "shader")


class QtInstanceEditor(QtWidgets.QWidget):
    TABS = ["Customization", "Mem and Video", "Modpack Management", "Mods", "Servers and Worlds", "Resource packs and Shader packs", "Sharing", "Screenshots", "Other settings", "Versions"]
    TAB_KEYS = {"Customization": "QT_TAB_CUSTOMIZATION", "Mem and Video": "QT_TAB_MEM_VIDEO", "Modpack Management": "QT_TAB_MODPACK", "Mods": "QT_TAB_MODS", "Servers and Worlds": "QT_TAB_SERVERS_WORLDS", "Resource packs and Shader packs": "QT_TAB_PACKS", "Sharing": "QT_TAB_SHARING", "Screenshots": "QT_TAB_SCREENSHOTS", "Other settings": "QT_TAB_OTHER", "Versions": "QT_TAB_VERSIONS"}

    def __init__(self, launcher, page, parent=None):
        super().__init__(parent)
        self.launcher = launcher
        self.page = page
        self.instance_manager = launcher.instance_manager
        self.instance = None
        self._loaded = set()
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(18, 12, 18, 10)
        lay.setSpacing(10)
        self._build_header(lay)
        self.tabs = QtWidgets.QTabWidget()
        self.tabs.setDocumentMode(False)
        self.tabs.setUsesScrollButtons(True)
        self.tabs.tabBar().setExpanding(False)
        self.tabs.setStyleSheet("QTabBar::tab { padding: 6px 9px; }")
        lay.addWidget(self.tabs, 1)
        self.pages = {}
        builders = {
            "Customization": self._build_customization,
            "Mem and Video": self._build_mem_video,
            "Modpack Management": self._build_modpack,
            "Mods": self._build_mods,
            "Servers and Worlds": self._build_servers_worlds,
            "Resource packs and Shader packs": self._build_packs,
            "Sharing": self._build_sharing,
            "Screenshots": self._build_screenshots,
            "Other settings": self._build_other,
            "Versions": self._build_versions,
        }
        for name in self.TABS:
            w = builders[name]()
            self.pages[name] = w
            self.tabs.addTab(w, _qt_t(self.TAB_KEYS[name], name))
        self.tabs.currentChanged.connect(lambda idx: self._load_tab(self.TABS[idx]))

    def _build_header(self, lay):
        head = QtWidgets.QHBoxLayout()
        head.setSpacing(12)
        L = self.launcher
        head.addWidget(_qt_button(_tr(L, "GAME_PROFILES_GO_BACK", "← Back"), self.close_editor, kind="flat"))
        self.icon = QtWidgets.QLabel()
        self.icon.setFixedSize(56, 56)
        self.icon.setScaledContents(True)
        head.addWidget(self.icon)
        col = QtWidgets.QVBoxLayout()
        col.setSpacing(2)
        self.title = _qt_label("", "h1")
        self.subtitle = _qt_label("", "muted")
        col.addWidget(self.title)
        col.addWidget(self.subtitle)
        head.addLayout(col, 1)
        head.addWidget(_qt_button(_tr(L, "PLAY", "Play"), self.play, kind="accent"))
        head.addWidget(_qt_button(_tr(L, "RES_SH_OPEN_FOLDER", "Open folder"), self.open_folder, icon="folder", launcher=L))
        lay.addLayout(head)

    def open(self, instance):
        self.instance = instance
        self._loaded = set()
        self.refresh_header()
        self._load_tab(self.TABS[self.tabs.currentIndex()])

    def close_editor(self):
        self.page.show_list()

    def refresh_header(self):
        inst = self.instance
        if inst is None:
            return
        self.icon.setPixmap(_instance_icon_pixmap(self.launcher, inst, 56))
        self.title.setText(inst.name)
        ram_mb = self._ram_to_mb(inst.ram)
        ram_text = f"{ram_mb // 1024} GB" if ram_mb % 1024 == 0 else f"{ram_mb / 1024:.1f} GB"
        self.subtitle.setText(f"Minecraft {inst.version}  ·  {(inst.mod_loader or 'vanilla').title()}" + (f" {inst.loader_version}" if inst.loader_version else "") + f"  ·  {ram_text} " + _qt_t("QT_RAM", "RAM"))

    def _load_tab(self, name):
        if self.instance is None:
            return
        if name in self._loaded:
            return
        self._loaded.add(name)
        loaders = {
            "Customization": self._load_customization,
            "Mem and Video": self._load_mem_video,
            "Modpack Management": self._load_modpack,
            "Mods": lambda: self.mods.load(self.instance),
            "Servers and Worlds": lambda: self.servers_worlds.load(self.instance),
            "Resource packs and Shader packs": lambda: self.packs.load(self.instance),
            "Sharing": self._load_sharing,
            "Screenshots": self._load_screenshots,
            "Other settings": self._load_other,
            "Versions": self._load_versions,
        }
        try:
            loaders[name]()
        except Exception as e:
            print(f"[Editor] load {name}: {e}")
            traceback.print_exc()

    def play(self):
        if self.instance is None:
            return
        self.instance_manager.set_selected_instance(self.instance.instance_id)
        self.launcher.refresh_instance_display()
        self.launcher._launch_game()

    def open_folder(self):
        if self.instance is not None:
            self.instance.minecraft_dir.mkdir(parents=True, exist_ok=True)
            open_path_native(self.instance.minecraft_dir)

    def save(self, message=None):
        try:
            self.instance_manager.save_instances()
        except Exception as e:
            messagebox.showerror(_qt_t("QT_SAVE", "Save"), str(e))
            return False
        self.refresh_header()
        self.launcher.refresh_instance_display()
        if message:
            self.launcher.set_status(message)
        return True

    def _page(self):
        page, lay = _qt_page(spacing=12, margins=(18, 16, 18, 16))
        return page, lay

    def _build_customization(self):
        page, lay = self._page()
        card = _Card(_qt_t("QT_LOOK", "Look"))
        row = QtWidgets.QHBoxLayout()
        self.cust_icon = QtWidgets.QLabel()
        self.cust_icon.setFixedSize(64, 64)
        self.cust_icon.setScaledContents(True)
        row.addWidget(self.cust_icon)
        col = QtWidgets.QVBoxLayout()
        line = QtWidgets.QHBoxLayout()
        self.cust_icon_choice = QtWidgets.QComboBox()
        self.ICON_DEFAULT = _qt_t("QT_ICON_LOADER_DEFAULT", "Loader default")
        self.ICON_CUSTOM = _qt_t("QT_ICON_CUSTOM", "Custom image...")
        self.cust_icon_choice.addItems([self.ICON_DEFAULT, "vanilla", "forge", "fabric", "quilt", "neoforge", "optifine", self.ICON_CUSTOM])
        self.cust_icon_choice.activated.connect(lambda *_: self._on_icon_choice())
        line.addWidget(self.cust_icon_choice)
        line.addWidget(_qt_button(_qt_t("QT_BROWSE_IMAGE", "Browse image"), self._browse_icon, icon="folder", launcher=self.launcher))
        line.addStretch(1)
        col.addWidget(_qt_label(_qt_t("QT_ICON", "Icon")))
        col.addLayout(line)
        col.addWidget(_qt_label(_qt_t("QT_ICON_HINT", "PNG / JPG, shown on the instance card and in the header."), "hint"))
        row.addLayout(col, 1)
        card.add_layout(row)
        lay.addWidget(card)
        self.cust_icon_path = ""
        card2 = _Card(_qt_t("QT_IDENTITY", "Identity"))
        self.cust_name = QtWidgets.QLineEdit()
        card2.add(_qt_form_row(_qt_t("QT_INSTANCE_NAME", "Instance name"), self.cust_name))
        self.cust_path = QtWidgets.QLineEdit()
        self.cust_path.setReadOnly(True)
        pl = QtWidgets.QHBoxLayout()
        pl.addWidget(self.cust_path, 1)
        pl.addWidget(_qt_button(_tr(self.launcher, "GAME_PROFILES_OPEN_BTN", "Open"), self.open_folder, icon="folder", launcher=self.launcher))
        pl.addWidget(_qt_button(_qt_t("QT_MOVE", "Move..."), self._move_instance))
        card2.add(_qt_form_row(_qt_t("QT_INSTANCE_PATH", "Instance path"), pl))
        lay.addWidget(card2)
        btns = QtWidgets.QHBoxLayout()
        btns.addWidget(_qt_button(_qt_t("QT_SAVE", "Save"), self._save_customization, kind="accent"))
        btns.addStretch(1)
        lay.addLayout(btns)
        lay.addStretch(1)
        return _qt_scroll(page)

    def _on_icon_choice(self):
        choice = self.cust_icon_choice.currentText()
        if choice == self.ICON_CUSTOM:
            self._browse_icon()
            return
        if choice == self.ICON_DEFAULT:
            self.cust_icon_path = ""
        else:
            src = find_resource(f"oranglauncher/images/loaders/{choice}.png") if choice != "vanilla" else find_resource("oranglauncher/images/minecraft-green.png")
            self.cust_icon_path = str(src) if src else ""
        self._preview_icon()

    def _browse_icon(self):
        path = _pick_open_file(title=_qt_t("QT_SELECT_ICON", "Select icon image"), filetypes=[(_qt_t("QT_FT_IMAGES", "Images"), "*.png *.jpg *.jpeg *.webp *.gif"), (_qt_t("QT_FT_ALL", "All files"), "*.*")])
        if path:
            self.cust_icon_path = path
            self.cust_icon_choice.setCurrentText(self.ICON_CUSTOM)
            self._preview_icon()

    def _preview_icon(self):
        path = self.cust_icon_path
        pix = _qt_pixmap_from_path(path, (64, 64)) if path and Path(path).exists() else None
        if pix is None and self.instance is not None:
            pix = _instance_icon_pixmap(self.launcher, self.instance, 64)
        self.cust_icon.setPixmap(pix or QtGui.QPixmap())

    def _load_customization(self):
        inst = self.instance
        self.cust_name.setText(inst.name)
        self.cust_path.setText(str(inst.base_path))
        icon_file = inst.base_path / "icon.txt"
        path = ""
        if icon_file.exists():
            try:
                path = icon_file.read_text(encoding="utf-8").strip()
            except Exception:
                path = ""
        self.cust_icon_path = path
        self.cust_icon_choice.setCurrentText(self.ICON_CUSTOM if path else self.ICON_DEFAULT)
        self._preview_icon()

    def _save_customization(self):
        inst = self.instance
        name = self.cust_name.text().strip()
        if not name:
            messagebox.showerror(_qt_t("QT_TAB_CUSTOMIZATION", "Customization"), _qt_t("QT_NAME_EMPTY", "Name cannot be empty."))
            return
        other = self.instance_manager.get_instance_by_name(name)
        if other and other.instance_id != inst.instance_id:
            messagebox.showerror(_qt_t("QT_TAB_CUSTOMIZATION", "Customization"), _qt_t("QT_NAME_TAKEN", "An instance called '{name}' already exists.").format(name=name))
            return
        inst.name = name
        icon_file = inst.base_path / "icon.txt"
        path = self.cust_icon_path.strip()
        if path and Path(path).exists():
            try:
                img = _qimage_thumbnail(path, 128)
                dest = inst.base_path / "icon.png"
                if Path(path).resolve() != dest.resolve():
                    img.save(str(dest), "PNG")
                icon_file.write_text(str(dest), encoding="utf-8")
            except Exception as e:
                messagebox.showerror(_qt_t("QT_ICON", "Icon"), _qt_t("QT_ICON_FAIL", "Could not use that image: {error}").format(error=e))
        elif icon_file.exists():
            icon_file.unlink()
        self.save(_qt_t("QT_INSTANCE_SAVED", "Instance saved"))
        self._load_customization()

    def _move_instance(self):
        dest = _pick_directory(title=_qt_t("QT_MOVE_TO_FOLDER", "Move instance to folder"))
        if not dest:
            return
        inst = self.instance
        target = Path(dest) / inst.base_path.name
        title = _qt_t("QT_MOVE_TITLE", "Move")
        if target.exists():
            messagebox.showerror(title, _qt_t("QT_ALREADY_EXISTS", "{path} already exists.").format(path=target))
            return
        if not messagebox.askyesno(_qt_t("QT_MOVE_INSTANCE", "Move instance"), _qt_t("QT_MOVE_CONFIRM", "Move all files to\n{path}\nand leave a symlink behind?").format(path=target)):
            return
        try:
            shutil.move(str(inst.base_path), str(target))
            inst.base_path.symlink_to(target, target_is_directory=True)
            messagebox.showinfo(title, _qt_t("QT_MOVED_TO", "Moved to {path}").format(path=target))
        except Exception as e:
            messagebox.showerror(title, str(e))
        self._load_customization()

    def _slider_row(self, label, lo, hi, step, unit, zero_text=None):
        box = QtWidgets.QWidget()
        lay = QtWidgets.QVBoxLayout(box)
        lay.setContentsMargins(0, 0, 0, 4)
        lay.setSpacing(2)
        head = QtWidgets.QHBoxLayout()
        head.addWidget(_qt_label(label))
        head.addStretch(1)
        value = _qt_label("", "accent")
        head.addWidget(value)
        lay.addLayout(head)
        slider = QtWidgets.QSlider(Qt.Horizontal)
        slider.setRange(lo // step, hi // step)
        slider.setSingleStep(1)
        slider.setPageStep(max(1, 1024 // step))
        lay.addWidget(slider)

        def fmt(_=None):
            v = slider.value() * step
            if v == 0 and zero_text:
                value.setText(zero_text)
            elif unit == "MB" and v % 1024 == 0:
                value.setText(f"{v // 1024} GB")
            else:
                value.setText(f"{v} {unit}")
        slider.valueChanged.connect(fmt)
        fmt()
        box.slider = slider
        box.step = step
        box.get = lambda: slider.value() * step
        box.set = lambda v: slider.setValue(int(max(lo, min(hi, v)) // step))
        return box

    def _build_mem_video(self):
        page, lay = self._page()
        card = _Card(_qt_t("QT_WINDOW", "Window"))
        self.mv_fullscreen = _Var(False)
        card.add(_ToggleRow(_qt_t("QT_FULLSCREEN", "Launch in fullscreen"), _qt_t("QT_FULLSCREEN_DESC", "Writes fullscreen:true into this instance's options.txt before every launch."), self.mv_fullscreen))
        res = QtWidgets.QHBoxLayout()
        self.mv_width = QtWidgets.QLineEdit()
        self.mv_width.setFixedWidth(90)
        self.mv_width.setPlaceholderText("1920")
        self.mv_height = QtWidgets.QLineEdit()
        self.mv_height.setFixedWidth(90)
        self.mv_height.setPlaceholderText("1080")
        res.addWidget(self.mv_width)
        res.addWidget(_qt_label("  x  "))
        res.addWidget(self.mv_height)
        res.addWidget(_qt_label(_qt_t("QT_RESOLUTION_HINT", "(empty = Minecraft default)"), "hint"))
        res.addStretch(1)
        card.add(_qt_form_row(_qt_t("QT_RESOLUTION", "Custom resolution"), res))
        lay.addWidget(card)
        card = _Card(_qt_t("QT_MEMORY", "Memory"))
        total_mb = _get_system_ram_mb()
        usable = max(total_mb - 1024, 1024)
        self.mv_min = card.add(self._slider_row(_qt_t("QT_MEM_MIN", "Minimum memory (Xms)"), 256, usable, 128, "MB"))
        self.mv_max = card.add(self._slider_row(_qt_t("QT_MEM_MAX", "Maximum memory (Xmx)"), 512, usable, 256, "MB"))
        self.mv_perm = card.add(self._slider_row(_qt_t("QT_MEM_PERM", "PermGen / Metaspace"), 0, 2048, 64, "MB", zero_text=_qt_t("QT_DEFAULT", "default")))
        card.add(_qt_label(_qt_t("QT_MEM_HINT", "Detected system memory: {gb} GB. Sliders stop 1 GB below that so the desktop keeps breathing.").format(gb=total_mb // 1024), "hint", wrap=True))
        lay.addWidget(card)
        card = _Card(_qt_t("QT_GRAPHICS", "Graphics"))
        self.mv_zink = _Var(False)
        self.mv_prime = _Var(False)
        card.add(_ToggleRow(_qt_t("QT_ZINK", "Use Zink (OpenGL over Vulkan)"), _qt_t("QT_ZINK_DESC", "MESA_LOADER_DRIVER_OVERRIDE=zink. Helps on GPUs with weak OpenGL drivers, hurts on good ones."), self.mv_zink))
        card.add(_ToggleRow(_qt_t("QT_PRIME", "Run on the discrete GPU (PRIME offload)"), _qt_t("QT_PRIME_DESC", "DRI_PRIME=1, plus NVIDIA offload variables when the proprietary driver is present. For laptops with two GPUs."), self.mv_prime))
        lay.addWidget(card)
        card = _Card(_qt_t("QT_VARIABLES", "Variables"))
        self.mv_jvm = QtWidgets.QPlainTextEdit()
        self.mv_jvm.setFixedHeight(70)
        card.add(_qt_form_row(_qt_t("QT_JVM_ARGS", "Java (JVM) arguments"), self.mv_jvm, _qt_t("QT_JVM_ARGS_HINT", "Appended after the memory flags. One line or space separated, e.g. -XX:+UseG1GC -Dsome.prop=1")))
        self.mv_env = QtWidgets.QPlainTextEdit()
        self.mv_env.setFixedHeight(70)
        card.add(_qt_form_row(_qt_t("QT_ENV_VARS", "Environment variables"), self.mv_env, _qt_t("QT_ENV_VARS_HINT", "One KEY=VALUE per line, lines starting with # are ignored.")))
        self.mv_pre = QtWidgets.QLineEdit()
        card.add(_qt_form_row(_qt_t("QT_PRE_LAUNCH", "Pre-launch command"), self.mv_pre, _qt_t("QT_PRE_LAUNCH_HINT", "Runs in the .minecraft folder before the game starts; a non-zero exit aborts the launch. $INST_NAME, $INST_DIR, $INST_MC_DIR, $INST_JAVA, $INST_MC_VER are set.")))
        self.mv_wrap = QtWidgets.QLineEdit()
        card.add(_qt_form_row(_qt_t("QT_WRAPPER", "Wrapper command"), self.mv_wrap, _qt_t("QT_WRAPPER_HINT", "Prepended to the java command line, e.g. gamemoderun, mangohud, prime-run")))
        self.mv_post = QtWidgets.QLineEdit()
        card.add(_qt_form_row(_qt_t("QT_POST_EXIT", "Post-exit command"), self.mv_post, _qt_t("QT_POST_EXIT_HINT", "Runs after Minecraft closes. $INST_EXIT_CODE holds the exit code.")))
        lay.addWidget(card)
        btns = QtWidgets.QHBoxLayout()
        btns.addWidget(_qt_button(_qt_t("QT_SAVE", "Save"), self._save_mem_video, kind="accent"))
        btns.addStretch(1)
        lay.addLayout(btns)
        lay.addStretch(1)
        return _qt_scroll(page)

    @staticmethod
    def _ram_to_mb(ram):
        s = str(ram or "4G").strip().upper().replace(" ", "")
        try:
            if s.endswith("GB"):
                return int(float(s[:-2])) * 1024
            if s.endswith("G"):
                return int(float(s[:-1])) * 1024
            if s.endswith("MB"):
                return int(float(s[:-2]))
            if s.endswith("M"):
                return int(float(s[:-1]))
            return int(float(s)) * 1024
        except Exception:
            return 4096

    def _load_mem_video(self):
        inst = self.instance
        self.mv_fullscreen.set(bool(inst.opt("fullscreen", False)))
        self.mv_width.setText(str(inst.opt("res_width", "") or ""))
        self.mv_height.setText(str(inst.opt("res_height", "") or ""))
        self.mv_max.set(self._ram_to_mb(inst.ram))
        self.mv_min.set(int(inst.opt("min_ram_mb", 0) or min(512, self._ram_to_mb(inst.ram))))
        self.mv_perm.set(int(inst.opt("permgen_mb", 0) or 0))
        self.mv_zink.set(bool(inst.opt("use_zink", False)))
        self.mv_prime.set(bool(inst.opt("use_prime", False)))
        self.mv_jvm.setPlainText(inst.opt("jvm_args", "") or "")
        self.mv_env.setPlainText(inst.env_vars or "")
        self.mv_pre.setText(inst.opt("pre_launch_cmd", "") or "")
        self.mv_wrap.setText(inst.opt("wrapper_cmd", "") or "")
        self.mv_post.setText(inst.opt("post_exit_cmd", "") or "")
        for w in self.pages["Mem and Video"].findChildren(_ToggleRow):
            w.refresh()

    def _save_mem_video(self):
        inst = self.instance
        inst.set_opt("fullscreen", bool(self.mv_fullscreen.get()))
        w, h = self.mv_width.text().strip(), self.mv_height.text().strip()
        if (w or h) and not (w.isdigit() and h.isdigit() and int(w) > 0 and int(h) > 0):
            messagebox.showerror(_qt_t("QT_RESOLUTION", "Custom resolution"), _qt_t("QT_RESOLUTION_INVALID", "Width and height must both be positive numbers, e.g. 1920 x 1080."))
            return
        inst.set_opt("res_width", int(w) if w else None)
        inst.set_opt("res_height", int(h) if h else None)
        max_mb = int(self.mv_max.get())
        min_mb = min(int(self.mv_min.get()), max_mb)
        inst.ram = f"{max_mb // 1024}G" if max_mb % 1024 == 0 else f"{max_mb}M"
        inst.java_args = f"-Xmx{inst.ram}"
        inst.set_opt("min_ram_mb", min_mb)
        inst.set_opt("permgen_mb", int(self.mv_perm.get()))
        inst.set_opt("use_zink", bool(self.mv_zink.get()))
        inst.set_opt("use_prime", bool(self.mv_prime.get()))
        inst.set_opt("jvm_args", self.mv_jvm.toPlainText().strip())
        inst.env_vars = self.mv_env.toPlainText().strip()
        inst.set_opt("pre_launch_cmd", self.mv_pre.text().strip())
        inst.set_opt("wrapper_cmd", self.mv_wrap.text().strip())
        inst.set_opt("post_exit_cmd", self.mv_post.text().strip())
        if self.save(_qt_t("QT_MEM_VIDEO_SAVED", "Memory & video settings saved")):
            try:
                apply_video_options(inst)
            except Exception as e:
                print(f"[Editor] options.txt apply failed: {e}")

    def _build_modpack(self):
        page, lay = self._page()
        card = _Card(_qt_t("QT_MODPACK", "Modpack"))
        self.mp_info = _qt_label("", None, wrap=True)
        card.add(self.mp_info)
        row = QtWidgets.QHBoxLayout()
        L = self.launcher
        self.mp_check_btn = _qt_button(_qt_t("QT_CHECK_UPDATES", "Check for updates"), self._mp_check_updates, icon="update", launcher=L)
        self.mp_open_btn = _qt_button(_qt_t("QT_OPEN_MODRINTH", "Open on Modrinth"), self._mp_open_page, icon="github", launcher=L)
        self.mp_repair_btn = _qt_button(_qt_t("QT_REPAIR", "Repair (re-download missing files)"), self._mp_repair, icon="refresh", launcher=L)
        self.mp_unlink_btn = _qt_button(_qt_t("QT_UNLINK", "Unlink pack"), self._mp_unlink, icon="trash", launcher=L)
        for b in (self.mp_check_btn, self.mp_open_btn, self.mp_repair_btn, self.mp_unlink_btn):
            row.addWidget(b)
        row.addWidget(_qt_button(_qt_t("QT_IMPORT_INTO", "Import a different .mrpack / .orangpack into this instance"), self._mp_import_into, icon="mrpack", launcher=L))
        row.addStretch(1)
        card.add_layout(row)
        lay.addWidget(card)
        self.mp_versions_card = card2 = _Card(_qt_t("QT_AVAILABLE_VERSIONS", "Available versions"))
        card2.add(_qt_label(_qt_t("QT_MP_VERSIONS_HINT", "Pick a version and press Update. Mods from the old version are removed, your overrides and worlds stay."), "hint", wrap=True))
        self.mp_versions_box = QtWidgets.QListWidget()
        self.mp_versions_box.setMinimumHeight(180)
        card2.add(self.mp_versions_box)
        self.mp_versions = []
        row2 = QtWidgets.QHBoxLayout()
        row2.addWidget(_qt_button(_qt_t("QT_UPDATE_TO_SELECTED", "Update to selected version"), self._mp_update_selected, kind="accent"))
        row2.addStretch(1)
        card2.add_layout(row2)
        lay.addWidget(card2)
        lay.addStretch(1)
        return _qt_scroll(page)

    def _pack_meta(self):
        if self.instance is None:
            return {}
        p = self.instance.base_path / "modpack.json"
        if not p.exists():
            return {}
        try:
            return json.loads(p.read_text(encoding="utf-8"))
        except Exception:
            return {}

    def _load_modpack(self):
        meta = self._pack_meta()
        self.mp_versions_box.clear()
        self.mp_versions = []
        linked = bool(meta)
        on_modrinth = bool(meta.get("modrinth_project_id"))
        self.mp_unlink_btn.setVisible(linked)
        self.mp_repair_btn.setVisible(linked)
        self.mp_check_btn.setVisible(on_modrinth)
        self.mp_open_btn.setVisible(on_modrinth)
        self.mp_versions_card.setVisible(on_modrinth)
        if not meta:
            self.mp_info.setText(_qt_t("QT_MP_NOT_FROM_PACK", "This instance was not created from a modpack.\nImport an .mrpack or .orangpack below to link one."))
            return
        lines = [_qt_t("QT_MP_NAME", "Name: {name}").format(name=meta.get('modrinth_title') or meta.get('name') or '?'),
                 _qt_t("QT_MP_INSTALLED_VERSION", "Installed version: {version}").format(version=meta.get('modrinth_version_number') or meta.get('version_id') or '?'),
                 _qt_t("QT_MP_FILE_COUNT", "Files managed by the pack: {n}").format(n=meta.get('file_count', 0)),
                 _qt_t("QT_MP_IMPORTED", "Imported: {date}").format(date=(meta.get('imported') or '')[:19].replace('T', ' '))]
        if meta.get("summary"):
            lines.append(_qt_t("QT_MP_SUMMARY", "Summary: {summary}").format(summary=meta.get('summary')))
        if on_modrinth:
            lines.append(_qt_t("QT_MP_PROJECT", "Modrinth project: {project}").format(project=meta.get('modrinth_slug') or meta.get('modrinth_project_id')))
        else:
            lines.append(_qt_t("QT_MP_NOT_LINKED", "Not linked to a Modrinth project (imported from a local file)."))
        self.mp_info.setText("\n".join(lines))

    # the modrinth open in browser thing because implementing a emmbed is bad for maintainability
    def _mp_open_page(self):
        meta = self._pack_meta()
        slug = meta.get("modrinth_slug") or meta.get("modrinth_project_id")
        if slug:
            open_with_browser(f"https://modrinth.com/modpack/{slug}")
        else:
            messagebox.showinfo(_qt_t("QT_MODPACK", "Modpack"), _qt_t("QT_MP_NOT_ON_MODRINTH", "This instance is not linked to a Modrinth project."))

    def _mp_check_updates(self):
        meta = self._pack_meta()
        pid = meta.get("modrinth_project_id")
        if not pid:
            messagebox.showinfo(_qt_t("QT_MODPACK", "Modpack"), _qt_t("QT_MP_NOT_ON_MODRINTH", "This instance is not linked to a Modrinth project."))
            return
        self.mp_versions_box.clear()
        self.mp_versions_box.addItem(_qt_t("QT_LOADING_VERSIONS", "Loading versions..."))

        def work():
            r = _http_session.get(f"{MODRINTH_API_URL}/project/{pid}/version", timeout=20)
            r.raise_for_status()
            return r.json()

        def done(versions):
            self.mp_versions_box.clear()
            self.mp_versions = versions
            current = meta.get("modrinth_version_id")
            for v in versions:
                tag = f"  ({_qt_t('QT_INSTALLED_TAG', 'installed')})" if v.get("id") == current else ""
                self.mp_versions_box.addItem(f"{v.get('version_number')}  -  MC {', '.join(v.get('game_versions', [])[:3])}  -  {', '.join(v.get('loaders', []))}  -  {(v.get('date_published') or '')[:10]}{tag}")
            if versions and versions[0].get("id") != current:
                self.mp_info.setText(self.mp_info.text() + "\n" + _qt_t("QT_MP_UPDATE_AVAILABLE", "Update available: {version}").format(version=versions[0].get('version_number')))
        _qt_run_bg(work, done, lambda e: (self.mp_versions_box.clear(), messagebox.showerror(_qt_t("QT_MODPACK", "Modpack"), _qt_t("QT_MP_FETCH_FAIL", "Could not fetch versions: {error}").format(error=e))))

    def _mp_update_selected(self):
        row = self.mp_versions_box.currentRow()
        if row < 0 or row >= len(self.mp_versions):
            messagebox.showinfo(_qt_t("QT_MODPACK", "Modpack"), _qt_t("QT_MP_PICK_FIRST", "Check for updates first and pick a version from the list."))
            return
        version = self.mp_versions[row]
        primary = next((f for f in version.get("files", []) if f.get("primary")), None)
        if not primary and version.get("files"):
            primary = version["files"][0]
        if not primary:
            messagebox.showerror(_qt_t("QT_MODPACK", "Modpack"), _qt_t("QT_MP_NO_FILE", "That version has no downloadable file."))
            return
        if not messagebox.askyesno(_qt_t("QT_UPDATE_MODPACK", "Update modpack"), _qt_t("QT_MP_UPDATE_CONFIRM", "Update '{name}' to {version}?\n\nManaged mods from the current version are removed first, everything else stays.").format(name=self.instance.name, version=version.get('version_number'))):
            return
        self._mp_apply_pack_file(primary.get("url"), primary.get("filename"), remove_managed=True)

    def _mp_apply_pack_file(self, url, filename, remove_managed=True, local_path=None):
        inst = self.instance
        meta = self._pack_meta()
        self.launcher.set_status(_qt_t("QT_UPDATING_MODPACK", "Updating modpack..."))

        def work():
            if local_path:
                pack_path = Path(local_path)
            else:
                tmpdir = Path(tempfile.mkdtemp(prefix="orang_pack_"))
                pack_path = tmpdir / (filename or "pack.mrpack")
                r = _http_session.get(url, stream=True, timeout=120)
                r.raise_for_status()
                with open(pack_path, "wb") as f:
                    for chunk in r.iter_content(65536):
                        if chunk:
                            f.write(chunk)
            if remove_managed:
                for rel in meta.get("managed_files", []) or []:
                    if rel.startswith("mods/"):
                        target = inst.minecraft_dir / rel
                        try:
                            if target.exists():
                                target.unlink()
                        except Exception:
                            pass
            importer = ModrinthPackImporter(self.launcher)
            return importer.apply_pack_to_instance(pack_path, inst)

        def done(result):
            ok, msg = result
            self.launcher.set_status(_qt_t("QT_READY", "Ready"))
            (messagebox.showinfo if ok else messagebox.showerror)(_qt_t("QT_MODPACK", "Modpack"), msg)
            self._load_modpack()
            self.refresh_header()
            self._loaded.discard("Mods")
            self._loaded.discard("Resource packs and Shader packs")
            self._load_tab(self.TABS[self.tabs.currentIndex()])
        _qt_run_bg(work, done, lambda e: (self.launcher.set_status(_qt_t("QT_READY", "Ready")), messagebox.showerror(_qt_t("QT_MODPACK", "Modpack"), str(e))))

    def _mp_repair(self):
        meta = self._pack_meta()
        src = meta.get("source_file")
        if src and Path(src).exists():
            if messagebox.askyesno(_qt_t("QT_REPAIR_TITLE", "Repair"), _qt_t("QT_REPAIR_CONFIRM", "Re-download missing pack files using\n{path}?").format(path=src)):
                self._mp_apply_pack_file(None, None, remove_managed=False, local_path=src)
            return
        vid = meta.get("modrinth_version_id")
        if not vid:
            messagebox.showinfo(_qt_t("QT_REPAIR_TITLE", "Repair"), _qt_t("QT_REPAIR_NO_SOURCE", "The original pack file is gone and the instance is not linked to Modrinth.\nImport the pack file again to repair."))
            return

        def work():
            r = _http_session.get(f"{MODRINTH_API_URL}/version/{vid}", timeout=20)
            r.raise_for_status()
            version = r.json()
            return next((f for f in version.get("files", []) if f.get("primary")), None) or (version.get("files") or [None])[0]

        def done(primary):
            if primary:
                self._mp_apply_pack_file(primary.get("url"), primary.get("filename"), remove_managed=False)
        _qt_run_bg(work, done, lambda e: messagebox.showerror(_qt_t("QT_REPAIR_TITLE", "Repair"), str(e)))

    def _mp_unlink(self):
        p = self.instance.base_path / "modpack.json"
        if p.exists() and messagebox.askyesno(_qt_t("QT_UNLINK_TITLE", "Unlink"), _qt_t("QT_UNLINK_CONFIRM", "Forget the modpack link? Files stay, only update tracking is removed.")):
            p.unlink()
            self._load_modpack()

    def _mp_import_into(self):
        path = _pick_open_file(title=_qt_t("QT_IMPORT_INTO_TITLE", "Import modpack into this instance"), filetypes=[(_qt_t("QT_FT_MODPACKS", "Modpacks"), "*.mrpack *.orangpack"), (_qt_t("QT_FT_ALL", "All files"), "*.*")])
        if not path:
            return
        if messagebox.askyesno(_qt_t("QT_IMPORT", "Import"), _qt_t("QT_IMPORT_INTO_CONFIRM", "Apply this pack on top of the current instance?\nManaged mods of the previously linked pack are removed first.")):
            self._mp_apply_pack_file(None, None, remove_managed=True, local_path=path)

    def _build_mods(self):
        self.mods = QtModsPanel(self)
        return self.mods

    def _build_servers_worlds(self):
        self.servers_worlds = QtServersWorldsPanel(self)
        return self.servers_worlds

    def _build_packs(self):
        self.packs = QtPacksPanel(self)
        return self.packs

    # orangpack my beloved (it's just a zip, try with 7zip)
    def _build_sharing(self):
        page, lay = self._page()
        card = _Card(_qt_t("QT_EXPORT", "Export"))
        card.add(_qt_label(_qt_t("QT_EXPORT_HINT", "Tick what to include, choose a format, export. Files that exist on Modrinth are referenced by download link instead of being packed, which keeps .mrpack and .orangpack small."), "hint", wrap=True))
        self.sh_format = QtWidgets.QButtonGroup(self)
        for value, text in (("orangpack", _qt_t("QT_FMT_ORANGPACK", "OrangLauncher pack (.orangpack, smallest, keeps instance settings)")),
                            ("mrpack", _qt_t("QT_FMT_MRPACK", "Modrinth pack (.mrpack, works in every launcher)")),
                            ("zip", _qt_t("QT_FMT_ZIP", "Full zip (.zip, everything copied, biggest)"))):
            rb = QtWidgets.QRadioButton(text)
            rb.setProperty("fmt", value)
            if value == "orangpack":
                rb.setChecked(True)
            self.sh_format.addButton(rb)
            card.add(rb)
        self.sh_tree = QtWidgets.QTreeWidget()
        self.sh_tree.setHeaderHidden(True)
        self.sh_tree.setMinimumHeight(320)
        card.add(self.sh_tree)
        row = QtWidgets.QHBoxLayout()
        L = self.launcher
        row.addWidget(_qt_button(_qt_t("QT_REFRESH_LIST", "Refresh list"), self._load_sharing, icon="refresh", launcher=L))
        row.addWidget(_qt_button(_tr(L, "SELECT_ALL", "Select all"), lambda: self._sh_set_all(True)))
        row.addWidget(_qt_button(_qt_t("QT_SELECT_NONE", "Select none"), lambda: self._sh_set_all(False)))
        row.addWidget(_qt_button(_qt_t("QT_EXPORT_BTN", "Export..."), self._sh_export, kind="accent"))
        row.addStretch(1)
        card.add_layout(row)
        lay.addWidget(card)
        lay.addStretch(1)
        self._sh_updating = False
        return _qt_scroll(page)

    def _sh_set_recursive(self, item, state):
        item.setCheckState(0, state)
        for i in range(item.childCount()):
            self._sh_set_recursive(item.child(i), state)

    def _sh_set_all(self, checked):
        self._sh_updating = True
        try:
            for i in range(self.sh_tree.topLevelItemCount()):
                self._sh_set_recursive(self.sh_tree.topLevelItem(i), Qt.Checked if checked else Qt.Unchecked)
        finally:
            self._sh_updating = False

    def _load_sharing(self):
        if self.instance is None:
            return
        self._sh_updating = True
        try:
            self.sh_tree.clear()
            base = self.instance.base_path

            def skip(rel):
                return rel in DEFAULT_EXPORT_SKIP_DIRS or any(rel.startswith(d + "/") for d in DEFAULT_EXPORT_SKIP_DIRS)

            def make(parent, text, rel, checked, is_dir):
                item = QtWidgets.QTreeWidgetItem([text])
                item.setData(0, Qt.UserRole, rel)
                item.setData(0, Qt.UserRole + 1, is_dir)
                item.setFlags(item.flags() | Qt.ItemIsUserCheckable | Qt.ItemIsAutoTristate)
                item.setCheckState(0, Qt.Checked if checked else Qt.Unchecked)
                if parent is None:
                    self.sh_tree.addTopLevelItem(item)
                else:
                    parent.addChild(item)
                return item

            def add_dir(parent, path, rel, depth):
                item = make(parent, path.name, rel, not skip(rel), True)
                if depth >= 2:
                    return item
                try:
                    children = sorted(path.iterdir(), key=lambda x: (not x.is_dir(), x.name.lower()))
                except Exception:
                    return item
                for child in children[:400]:
                    if child.is_symlink():
                        continue
                    crel = f"{rel}/{child.name}"
                    if child.is_dir():
                        add_dir(item, child, crel, depth + 1)
                    else:
                        make(item, child.name, crel, not skip(rel) and not skip(crel), False)
                return item
            try:
                entries = sorted(base.iterdir(), key=lambda x: (not x.is_dir(), x.name.lower()))
            except Exception:
                entries = []
            for p in entries:
                if p.is_symlink() or p.name == "instance.json":
                    continue
                if p.is_dir():
                    item = add_dir(None, p, p.name, 0)
                    item.setExpanded(True)
                else:
                    make(None, p.name, p.name, p.name in ("icon.png", "icon.txt", "modpack.json"), False)
        finally:
            self._sh_updating = False

    def _sh_selected_paths(self):
        base = self.instance.base_path
        selected = []

        def walk(item):
            rel = item.data(0, Qt.UserRole)
            is_dir = item.data(0, Qt.UserRole + 1)
            state = item.checkState(0)
            if item.childCount():
                for i in range(item.childCount()):
                    walk(item.child(i))
                if state != Qt.Unchecked and is_dir:
                    listed = {item.child(i).data(0, Qt.UserRole) for i in range(item.childCount())}
                    depth = rel.count("/") + 2
                    for extra in (base / rel).rglob("*"):
                        if extra.is_file() and not extra.is_symlink():
                            erel = str(extra.relative_to(base)).replace("\\", "/")
                            first = "/".join(erel.split("/")[:depth])
                            if first not in listed and state == Qt.Checked:
                                selected.append(erel)
            elif state == Qt.Checked:
                path = base / rel
                if path.is_dir():
                    for f in path.rglob("*"):
                        if f.is_file() and not f.is_symlink():
                            selected.append(str(f.relative_to(base)).replace("\\", "/"))
                else:
                    selected.append(rel)
        for i in range(self.sh_tree.topLevelItemCount()):
            walk(self.sh_tree.topLevelItem(i))
        return sorted(set(selected))

    def _sh_export(self):
        inst = self.instance
        fmt = "orangpack"
        for b in self.sh_format.buttons():
            if b.isChecked():
                fmt = b.property("fmt")
        ext = {"orangpack": ORANGPACK_EXT, "mrpack": ".mrpack", "zip": ".zip"}[fmt]
        safe = re.sub(r"[^\w.-]+", "_", inst.name).strip("_") or "instance"
        path = _pick_save_file(title=_qt_t("QT_EXPORT_NAME", "Export {name}").format(name=inst.name), defaultextension=ext, filetypes=[(f"{ext[1:]} file", f"*{ext}")], initialfile=f"{safe}{ext}")
        if not path:
            return
        if not path.lower().endswith(ext):
            path += ext
        selected = self._sh_selected_paths()
        if not selected:
            messagebox.showinfo(_qt_t("QT_EXPORT", "Export"), _qt_t("QT_NOTHING_SELECTED", "Nothing selected."))
            return
        self.launcher.set_status(_qt_t("QT_EXPORTING", "Exporting..."))

        def work():
            return export_instance_pack(inst, path, fmt, selected, log_fn=self.launcher._safe_append_log,
                                        progress_fn=lambda i, t, n: self.launcher._submit_progress_update(int(i / max(t, 1) * 100), _qt_t("QT_EXPORTING_FILE", "Exporting {name}").format(name=n)))

        def done(out):
            self.launcher.set_status(_qt_t("QT_READY", "Ready"))
            self.launcher._submit_progress_update(0, _qt_t("QT_READY", "Ready"))
            messagebox.showinfo(_qt_t("QT_EXPORT", "Export"), _qt_t("QT_EXPORTED_TO", "Exported to:\n{path}").format(path=out))
        _qt_run_bg(work, done, lambda e: (self.launcher.set_status(_qt_t("QT_READY", "Ready")), messagebox.showerror(_qt_t("QT_EXPORT_FAILED", "Export failed"), str(e))))

    def _build_screenshots(self):
        page = QtWidgets.QWidget()
        lay = QtWidgets.QVBoxLayout(page)
        lay.setContentsMargins(14, 12, 14, 12)
        lay.setSpacing(8)
        head = QtWidgets.QHBoxLayout()
        head.addWidget(_qt_label(_qt_t("QT_TAB_SCREENSHOTS", "Screenshots"), "h2"))
        self.ss_count = _qt_label("", "muted")
        head.addWidget(self.ss_count)
        head.addStretch(1)
        L = self.launcher
        head.addWidget(_qt_button(_qt_t("QT_REFRESH", "Refresh"), self._load_screenshots, icon="refresh", launcher=L))
        head.addWidget(_qt_button(_tr(L, "RES_SH_OPEN_FOLDER", "Open folder"), lambda: self.instance and open_path_native(self._screenshot_dir()), icon="folder", launcher=L))
        head.addWidget(_qt_button(_qt_t("QT_COPY_PICTURES", "Copy selected to ~/Pictures"), self._ss_export_selected, icon="update", launcher=L))
        head.addWidget(_qt_button(_qt_t("QT_DELETE_SELECTED", "Delete selected"), self._ss_delete_selected, icon="trash", launcher=L))
        lay.addLayout(head)
        self.ss_list = QtWidgets.QListWidget()
        self.ss_list.setViewMode(QtWidgets.QListView.IconMode)
        self.ss_list.setIconSize(QSize(180, 110))
        self.ss_list.setGridSize(QSize(200, 150))
        self.ss_list.setResizeMode(QtWidgets.QListView.Adjust)
        self.ss_list.setMovement(QtWidgets.QListView.Static)
        self.ss_list.setSelectionMode(QtWidgets.QAbstractItemView.ExtendedSelection)
        self.ss_list.setWordWrap(True)
        self.ss_list.itemDoubleClicked.connect(lambda item: open_path_native(item.data(Qt.UserRole)))
        lay.addWidget(self.ss_list, 1)
        self._ss_token = 0
        return page

    def _screenshot_dir(self):
        d = self.instance.minecraft_dir / "screenshots"
        return d.resolve() if d.is_symlink() else d

    # I never used this and never use it. there is SUPER + SHIFT + S
    def _load_screenshots(self):
        if self.instance is None:
            return
        self.ss_list.clear()
        d = self._screenshot_dir()
        pngs = sorted(d.glob("*.png"), key=lambda p: p.stat().st_mtime, reverse=True) if d.exists() else []
        self.ss_count.setText(_qt_t("QT_SCREENSHOTS_COUNT", "{n} screenshot(s)").format(n=len(pngs)))
        if not pngs:
            # Bevare that there is a mod that makes ss in 2K quality, so f2 is not only way
            self.ss_list.addItem(_qt_t("QT_NO_SCREENSHOTS", "No screenshots yet. F2 in game takes one."))
            return
        self._ss_token += 1
        token = self._ss_token

        def work():
            out = []
            for png in pngs[:200]:
                if token != self._ss_token:
                    return out
                try:
                    img = QtGui.QImage(str(png))
                    if img.isNull():
                        continue
                    out.append((png, img.scaled(180, 110, Qt.KeepAspectRatio, Qt.SmoothTransformation)))
                except Exception:
                    continue
            return out

        def done(items):
            if token != self._ss_token:
                return
            for png, img in items:
                item = QtWidgets.QListWidgetItem(QtGui.QIcon(QtGui.QPixmap.fromImage(img)), png.name[:26])
                item.setData(Qt.UserRole, str(png))
                item.setToolTip(png.name)
                self.ss_list.addItem(item)
        _qt_run_bg(work, done, lambda e: None)

    def _ss_selected(self):
        return [Path(i.data(Qt.UserRole)) for i in self.ss_list.selectedItems() if i.data(Qt.UserRole)]

    def _ss_export_selected(self):
        sel = self._ss_selected()
        if not sel:
            messagebox.showinfo(_qt_t("QT_TAB_SCREENSHOTS", "Screenshots"), _qt_t("QT_SELECT_SCREENSHOTS_FIRST", "Click screenshots to select them first."))
            return
        dest = Path.home() / "Pictures" / "Minecraft" / re.sub(r"[^\w.-]+", "_", self.instance.name)
        dest.mkdir(parents=True, exist_ok=True)
        n = 0
        for png in sel:
            try:
                shutil.copy2(png, dest / png.name)
                n += 1
            except Exception as e:
                print(f"[Screenshots] copy failed: {e}")
        messagebox.showinfo(_qt_t("QT_TAB_SCREENSHOTS", "Screenshots"), _qt_t("QT_COPIED_FILES_TO", "Copied {n} file(s) to\n{path}").format(n=n, path=dest))

    def _ss_delete_selected(self):
        sel = self._ss_selected()
        if not sel:
            messagebox.showinfo(_qt_t("QT_TAB_SCREENSHOTS", "Screenshots"), _qt_t("QT_SELECT_SCREENSHOTS_FIRST", "Click screenshots to select them first."))
            return
        if not messagebox.askyesno(_qt_t("QT_DELETE_SCREENSHOTS", "Delete screenshots"), _qt_t("QT_DELETE_SCREENSHOTS_CONFIRM", "Delete {n} screenshot(s)? This cannot be undone.").format(n=len(sel))):
            return
        for png in sel:
            try:
                png.unlink()
            except Exception as e:
                print(f"[Screenshots] delete failed: {e}")
        self._load_screenshots()

    def _build_other(self):
        page, lay = self._page()
        card = _Card(_qt_t("QT_COMPAT", "Compatibility"))
        self.ot_legacy = _Var(False)
        card.add(_ToggleRow(_qt_t("QT_LEGACY_FIXES", "Legacy client fixes (old Minecraft versions)"),
                            _qt_t("QT_LEGACY_FIXES_DESC", "Adds the JVM flags old clients need (legacy merge sort, xrandr guard, macOS first-thread) and keeps Mojang's session endpoints reachable for skins on 1.7-1.16. Only affects versions below 1.13."), self.ot_legacy))
        self.ot_anti = _Var(True)
        card.add(_ToggleRow(_qt_t("QT_ANTI_BAN", "Bypass Mojang's blocked-server list"),
                            _qt_t("QT_ANTI_BAN_DESC", "Removes Mojang's server block list provider from the game's classpath, so servers on the secret sessionserver.mojang.com/blockedservers list can still be joined. Works on 1.16.4 and newer. Turn it off if a server refuses the connection."), self.ot_anti))
        self.ot_demo = _Var(False)
        card.add(_ToggleRow(_qt_t("QT_DEMO_MODE", "Launch the demo version (--demo)"),
                            _qt_t("QT_DEMO_MODE_DESC", "Passes --demo to the game. Use this if you want to try the Java Edition demo: it starts the demo world with the 5 in-game day limit, no account purchase needed."), self.ot_demo))
        if platform.system() == "Linux":
            self.ot_backend = QtWidgets.QComboBox()
            self.ot_backend.addItem(_qt_t("QT_BACKEND_X11", "X11 / XWayland (compatible, default)"), "x11")
            self.ot_backend.addItem(_qt_t("QT_BACKEND_WAYLAND", "Wayland (native, needs LWJGL 3.3.3+)"), "wayland")
            card.add(_qt_form_row(_qt_t("QT_DISPLAY_BACKEND", "Display backend"), self.ot_backend, _qt_t("QT_DISPLAY_BACKEND_HINT", "Native Wayland needs LWJGL 3.3.3+ and a client that tolerates GLFW's missing window-icon support (recent versions, or a Wayland-fix mod on 1.20.1 and older); XWayland is the safe default.")))
        lay.addWidget(card)
        card2 = _Card(_qt_t("QT_DANGER", "Danger zone"))
        row = QtWidgets.QHBoxLayout()
        row.addWidget(_qt_button(_qt_t("QT_DELETE_INSTANCE", "Delete this instance"), self._delete_instance, kind="danger"))
        row.addWidget(_qt_button(_tr(self.launcher, "GAME_PROFILES_DUPLICATE", "Duplicate"), self._duplicate_instance, icon="dublicate", launcher=self.launcher))
        row.addStretch(1)
        card2.add_layout(row)
        lay.addWidget(card2)
        btns = QtWidgets.QHBoxLayout()
        btns.addWidget(_qt_button(_qt_t("QT_SAVE", "Save"), self._save_other, kind="accent"))
        btns.addStretch(1)
        lay.addLayout(btns)
        lay.addStretch(1)
        return _qt_scroll(page)

    def _load_other(self):
        inst = self.instance
        self.ot_legacy.set(bool(inst.opt("legacy_fixes", False)))
        self.ot_anti.set(bool(inst.opt("anti_ban", True)))
        self.ot_demo.set(bool(inst.opt("demo", False)))
        if hasattr(self, "ot_backend"):
            idx = self.ot_backend.findData(inst.opt("display_backend", "") or "x11")
            self.ot_backend.setCurrentIndex(max(0, idx))
        for w in self.pages["Other settings"].findChildren(_ToggleRow):
            w.refresh()

    def _save_other(self):
        inst = self.instance
        inst.set_opt("legacy_fixes", bool(self.ot_legacy.get()))
        inst.opts["anti_ban"] = bool(self.ot_anti.get())
        inst.set_opt("demo", bool(self.ot_demo.get()))
        if hasattr(self, "ot_backend"):
            inst.set_opt("display_backend", self.ot_backend.currentData() or "")
        self.save(_qt_t("QT_OTHER_SAVED", "Other settings saved"))

    def _delete_instance(self):
        self.page.delete_instance(self.instance)

    def _duplicate_instance(self):
        self.instance_manager.set_selected_instance(self.instance.instance_id)
        self.page.duplicate_selected()
        self.page.show_list()

    JAVA_AUTO = "__auto__"
    JAVA_BROWSE = "__browse__"

    def _build_versions(self):
        page, lay = self._page()
        card = _Card(_qt_t("QT_COMPONENTS", "Components"))
        form = QtWidgets.QFormLayout()
        form.setLabelAlignment(Qt.AlignRight)
        self.vr_mc = QtWidgets.QComboBox()
        self.vr_mc.setEditable(True)
        self.vr_mc.setMaxVisibleItems(14)
        self.vr_mc.setMaximumWidth(360)
        self.vr_mc.addItems(self.launcher.version_values())
        self.vr_mc.currentTextChanged.connect(lambda *_: self._vr_loader_versions())
        form.addRow(_qt_t("QT_MINECRAFT", "Minecraft"), self.vr_mc)
        self.vr_loader = QtWidgets.QComboBox()
        self.vr_loader.setMaximumWidth(360)
        self.vr_loader.addItems(["vanilla", "forge", "neoforge", "fabric", "quilt", "optifine"])
        self.vr_loader.currentTextChanged.connect(lambda *_: self._vr_loader_versions())
        form.addRow(_qt_t("QT_MOD_LOADER", "Mod loader"), self.vr_loader)
        self.vr_loader_ver = QtWidgets.QComboBox()
        self.vr_loader_ver.setMaxVisibleItems(14)
        self.vr_loader_ver.setMaximumWidth(360)
        form.addRow(_qt_t("QT_LOADER_VERSION", "Loader version"), self.vr_loader_ver)
        jl = QtWidgets.QHBoxLayout()
        self.vr_java = QtWidgets.QComboBox()
        self.vr_java.setMaximumWidth(460)
        self.vr_java.activated.connect(lambda *_: self._vr_java_activated())
        jl.addWidget(self.vr_java, 1)
        jl.addStretch(1)
        form.addRow(_qt_t("QT_JAVA_RUNTIME", "Java runtime"), jl)
        card.add_layout(form)
        card.add(_qt_label(_qt_t("QT_JAVA_RUNTIME_HINT", "Automatic picks the Java version this Minecraft release needs (8, 17 or 21) and downloads it when missing."), "hint", wrap=True))
        lay.addWidget(card)
        card = _Card(_qt_t("QT_NATIVE_LIBS", "Native libraries (LWJGL)"))
        card.add(_qt_label(_qt_t("QT_LWJGL_EXPLAIN", "LWJGL is the library Minecraft uses to open its window, talk to the GPU and play sound. Minecraft ships its own copy; the old one bundled with 1.20.1 and earlier crashes on modern Linux desktops, so Automatic swaps in a newer one when needed."), "muted", wrap=True))
        self.vr_lwjgl_mode = QtWidgets.QComboBox()
        self.vr_lwjgl_mode.setMaximumWidth(460)
        self.vr_lwjgl_mode.addItem(_qt_t("QT_LWJGL_AUTO", "Automatic (recommended) - fix old versions, keep new ones"), LWJGL_MODE_AUTO)
        self.vr_lwjgl_mode.addItem(_qt_t("QT_LWJGL_STOCK", "Minecraft's own - never change anything"), LWJGL_MODE_STOCK)
        self.vr_lwjgl_mode.addItem(_qt_t("QT_LWJGL_PICK", "Pick a specific version"), "pick")
        self.vr_lwjgl_mode.currentIndexChanged.connect(lambda *_: self._vr_lwjgl_mode_changed())
        card.add(_qt_form_row(_qt_t("QT_LWJGL_MODE", "LWJGL mode"), self.vr_lwjgl_mode))
        self.vr_lwjgl = QtWidgets.QComboBox()
        self.vr_lwjgl.setMaximumWidth(300)
        self.vr_lwjgl_version_row = _qt_form_row(_qt_t("QT_LWJGL_VERSION", "LWJGL version"), self.vr_lwjgl)
        card.add(self.vr_lwjgl_version_row)
        self.vr_status = _qt_label("", "hint", wrap=True)
        card.add(self.vr_status)
        self.vr_adv_toggle = QtWidgets.QCheckBox(_qt_t("QT_LWJGL_ADVANCED", "Show advanced native library options"))
        self.vr_adv_toggle.toggled.connect(lambda on: self.vr_adv_box.setVisible(on))
        card.add(self.vr_adv_toggle)
        self.vr_adv_box = QtWidgets.QWidget()
        adv = QtWidgets.QVBoxLayout(self.vr_adv_box)
        adv.setContentsMargins(0, 0, 0, 0)
        adv.setSpacing(6)
        self.vr_lwjgl_dir = QtWidgets.QLineEdit()
        adv.addWidget(_qt_form_row(_qt_t("QT_CUSTOM_LWJGL_DIR", "Custom LWJGL folder (overrides everything above)"), _qt_line_with_browse(self.vr_lwjgl_dir, lambda: self.vr_lwjgl_dir.setText(_pick_directory(title=_qt_t("QT_SELECT_LWJGL_FOLDER", "Select LWJGL folder")) or self.vr_lwjgl_dir.text()), text=_qt_t("QT_BROWSE", "Browse")), _qt_t("QT_CUSTOM_LWJGL_DIR_HINT", "Put the LWJGL jars (lwjgl, lwjgl-glfw, lwjgl-openal, ... plus their natives jars or the raw .so/.dll files) in one folder.")))
        none_text = _qt_t("QT_NONE_DETECTED", "none")
        self.vr_glfw = QtWidgets.QLineEdit()
        adv.addWidget(_qt_form_row(_qt_t("QT_CUSTOM_GLFW", "System GLFW library (.so / .dylib / .dll)"), _qt_line_with_browse(self.vr_glfw, lambda: self.vr_glfw.setText(_pick_open_file(title=_qt_t("QT_SELECT_GLFW", "Select GLFW library")) or self.vr_glfw.text()), text=_qt_t("QT_BROWSE", "Browse")), _qt_t("QT_CUSTOM_GLFW_HINT", "Empty = use the LWJGL one. Detected on this system: {path}").format(path=detect_system_glfw() or none_text)))
        self.vr_openal = QtWidgets.QLineEdit()
        adv.addWidget(_qt_form_row(_qt_t("QT_CUSTOM_OPENAL", "System OpenAL library (.so / .dylib / .dll)"), _qt_line_with_browse(self.vr_openal, lambda: self.vr_openal.setText(_pick_open_file(title=_qt_t("QT_SELECT_OPENAL", "Select OpenAL library")) or self.vr_openal.text()), text=_qt_t("QT_BROWSE", "Browse")), _qt_t("QT_CUSTOM_OPENAL_HINT", "Empty = use the LWJGL one. Detected on this system: {path}").format(path=detect_system_openal() or none_text)))
        self.vr_adv_box.setVisible(False)
        card.add(self.vr_adv_box)
        lay.addWidget(card)
        btns = QtWidgets.QHBoxLayout()
        L = self.launcher
        btns.addWidget(_qt_button(_qt_t("QT_SAVE", "Save"), self._save_versions, kind="accent"))
        btns.addWidget(_qt_button(_qt_t("QT_REINSTALL", "Reinstall game files"), self._vr_reinstall, icon="refresh", launcher=L))
        btns.addWidget(_qt_button(_qt_t("QT_OPEN_VERSIONS", "Open versions folder"), lambda: self.instance and open_path_native(self.instance.minecraft_dir / "versions"), icon="folder", launcher=L))
        btns.addStretch(1)
        lay.addLayout(btns)
        lay.addWidget(_qt_label(_qt_t("QT_INSTALLED", "Installed"), "h3"))
        self.vr_installed = _qt_label("", "muted", wrap=True)
        lay.addWidget(self.vr_installed)
        lay.addStretch(1)
        self._vr_token = 0
        self._vr_loading = False
        self._vr_lwjgl_versions = []
        return _qt_scroll(page)

    def _vr_java_activated(self):
        if self.vr_java.currentData() != self.JAVA_BROWSE:
            return
        path = _pick_open_file(title=_qt_t("QT_SELECT_JAVA", "Select the java executable"))
        if path:
            self._vr_set_java(path)
        else:
            self.vr_java.setCurrentIndex(0)

    def _vr_set_java(self, path):
        idx = self.vr_java.findData(path)
        if idx < 0:
            self.vr_java.insertItem(self.vr_java.count() - 1, f"{_qt_t('QT_JAVA_CUSTOM', 'Custom')}  -  {path}", path)
            idx = self.vr_java.findData(path)
        self.vr_java.setCurrentIndex(idx)

    def _vr_fill_java(self, current):
        self.vr_java.blockSignals(True)
        self.vr_java.clear()
        self.vr_java.addItem(_qt_t("QT_JAVA_AUTO", "Automatic (recommended)"), self.JAVA_AUTO)
        seen = set()
        for major in (8, 11, 17, 21, 25):
            jp = find_java_executable(major)
            if jp and jp not in seen:
                seen.add(jp)
                self.vr_java.addItem(f"Java {major}  -  {jp}", jp)
        self.vr_java.addItem(_qt_t("QT_JAVA_BROWSE", "Choose a java executable..."), self.JAVA_BROWSE)
        if current:
            self._vr_set_java(current)
        else:
            self.vr_java.setCurrentIndex(0)
        self.vr_java.blockSignals(False)

    def _vr_lwjgl_mode_changed(self):
        pick = self.vr_lwjgl_mode.currentData() == "pick"
        self.vr_lwjgl_version_row.setVisible(pick)

    def _vr_loader_versions(self):
        if self._vr_loading or self.instance is None:
            return
        loader = self.vr_loader.currentText().lower()
        mc = self.vr_mc.currentText().strip()
        self.vr_loader_ver.clear()
        if loader in ("vanilla", ""):
            self.vr_loader_ver.addItem("N/A")
            self.vr_loader_ver.setEnabled(False)
            return
        self.vr_loader_ver.setEnabled(True)
        self.vr_loader_ver.addItem(_qt_t("QT_LOADING", "Loading…"))
        current = self.instance.loader_version or ""
        self._vr_token += 1
        token = self._vr_token

        def done(versions):
            if token != self._vr_token:
                return
            self.vr_loader_ver.clear()
            if versions:
                self.vr_loader_ver.addItems(versions)
                self.vr_loader_ver.setCurrentText(current if current in versions else versions[0])
            else:
                self.vr_loader_ver.addItem(_tr(self.launcher, "LOADER_NOT_COMPATIBLE", "Not available for this version"))
        _qt_run_bg(lambda: self.launcher.fetch_loader_versions(loader, mc), done, lambda e: None)

    def _vr_fill_lwjgl(self, mode):
        mode = mode or LWJGL_MODE_AUTO
        if mode in (LWJGL_MODE_AUTO, LWJGL_MODE_STOCK):
            self.vr_lwjgl_mode.setCurrentIndex(self.vr_lwjgl_mode.findData(mode))
            wanted = ""
        else:
            self.vr_lwjgl_mode.setCurrentIndex(self.vr_lwjgl_mode.findData("pick"))
            wanted = mode
        self._vr_lwjgl_mode_changed()
        self.vr_lwjgl.clear()
        base = list(self._vr_lwjgl_versions) or list(LWJGL_FALLBACK_VERSIONS)
        if wanted and wanted not in base:
            base.insert(0, wanted)
        self.vr_lwjgl.addItems(base)
        if wanted:
            self.vr_lwjgl.setCurrentText(wanted)

        def done(versions):
            if not versions:
                return
            self._vr_lwjgl_versions = list(versions)
            keep = self.vr_lwjgl.currentText()
            self.vr_lwjgl.clear()
            items = list(versions)
            if keep and keep not in items:
                items.insert(0, keep)
            self.vr_lwjgl.addItems(items)
            if keep:
                self.vr_lwjgl.setCurrentText(keep)
        if not self._vr_lwjgl_versions:
            _qt_run_bg(lambda: get_lwjgl_manager().list_versions(), done, lambda e: None)

    def _load_versions(self):
        inst = self.instance
        self._vr_loading = True
        try:
            self.vr_mc.setCurrentText(inst.version)
            self.vr_loader.setCurrentText((inst.mod_loader or "vanilla").lower())
            self._vr_fill_java(inst.java_path or "")
            self._vr_fill_lwjgl(inst.opt("lwjgl_mode", "") or LWJGL_MODE_AUTO)
            self.vr_lwjgl_dir.setText(inst.opt("custom_lwjgl_dir", "") or "")
            self.vr_glfw.setText(inst.opt("glfw_path", "") or "")
            self.vr_openal.setText(inst.opt("openal_path", "") or "")
            has_adv = bool(inst.opt("custom_lwjgl_dir") or inst.opt("glfw_path") or inst.opt("openal_path"))
            self.vr_adv_toggle.setChecked(has_adv)
        finally:
            self._vr_loading = False
        self._vr_loader_versions()
        stock, major = LwjglManager.stock_version(str(inst.minecraft_dir), inst.installed_version_id or inst.version)
        mode = inst.opt("lwjgl_mode", "") or LWJGL_MODE_AUTO
        effective = "stock" if mode == LWJGL_MODE_STOCK else (mode if mode != LWJGL_MODE_AUTO else (get_lwjgl_manager().choose_auto(stock, major) or "stock"))
        unknown = _qt_t("QT_LWJGL_UNKNOWN", "unknown (not installed yet)")
        self.vr_status.setText(_qt_t("QT_LWJGL_STATUS", "Minecraft's own LWJGL: {stock}. Used at launch: {effective}.").format(stock=stock or unknown, effective=effective if effective != 'stock' else (stock or unknown))
                               + (("  " + _qt_t("QT_JAVA_REQUIRED", "Java required: {major}").format(major=get_required_java_version(inst.version))) if inst.version else ""))
        installed = inst.installed_version_id or _qt_t("QT_NOTHING_INSTALLED", "nothing installed yet (installs on first launch)")
        versions_dir = inst.minecraft_dir / "versions"
        present = sorted(d.name for d in versions_dir.iterdir() if d.is_dir()) if versions_dir.exists() else []
        self.vr_installed.setText(_qt_t("QT_LAUNCH_ID", "Launch id: {id}").format(id=installed) + "\n" + _qt_t("QT_VERSION_FOLDERS", "Version folders: {folders}").format(folders=', '.join(present) if present else _qt_t("QT_NONE_DETECTED", "none")))

    def _save_versions(self):
        inst = self.instance
        mc = self.vr_mc.currentText().strip()
        loader = self.vr_loader.currentText().strip().lower()
        if not self.launcher.validate_version(mc):
            return
        lv = self.vr_loader_ver.currentText().strip()
        not_compat = _tr(self.launcher, "LOADER_NOT_COMPATIBLE", "Not available for this version")
        if loader != "vanilla" and lv == not_compat:
            messagebox.showerror(_tr(self.launcher, "LOADER_NOT_COMPATIBLE_TITLE", "Loader not available"), _tr(self.launcher, "LOADER_NOT_COMPATIBLE_MSG", "{loader} has no build for Minecraft {version}.").format(loader=loader, version=mc))
            return
        if lv in ("N/A", "Latest", "Loading…", _qt_t("QT_LOADING", "Loading…"), "", not_compat):
            lv = ""
        changed = (inst.version != mc) or ((inst.mod_loader or "").lower() != loader) or ((inst.loader_version or "") != lv)
        inst.version = mc
        inst.mod_loader = loader
        inst.loader_version = lv
        if changed:
            inst.installed_version_id = None
        mode = self.vr_lwjgl_mode.currentData()
        if mode == "pick":
            mode = self.vr_lwjgl.currentText().strip() or LWJGL_MODE_AUTO
        inst.set_opt("lwjgl_mode", mode if mode != LWJGL_MODE_AUTO else "")
        java = self.vr_java.currentData()
        inst.java_path = "" if java in (None, "", self.JAVA_AUTO, self.JAVA_BROWSE) else str(java)
        inst.set_opt("custom_lwjgl_dir", self.vr_lwjgl_dir.text().strip())
        inst.set_opt("glfw_path", self.vr_glfw.text().strip())
        inst.set_opt("openal_path", self.vr_openal.text().strip())
        inst.set_opt("use_system_glfw", bool(inst.opt("glfw_path")))
        inst.set_opt("use_system_openal", bool(inst.opt("openal_path")))
        if self.save(_qt_t("QT_VERSIONS_SAVED", "Versions saved")):
            self._load_versions()

    def _vr_reinstall(self):
        inst = self.instance
        if not messagebox.askyesno(_qt_t("QT_REINSTALL_TITLE", "Reinstall"), _qt_t("QT_REINSTALL_CONFIRM", "Delete the installed version folders (not mods, worlds or configs) so they are downloaded again on the next launch?")):
            return
        versions_dir = inst.minecraft_dir / "versions"
        try:
            if versions_dir.exists():
                shutil.rmtree(versions_dir)
        except Exception as e:
            messagebox.showerror(_qt_t("QT_REINSTALL_TITLE", "Reinstall"), str(e))
            return
        inst.installed_version_id = None
        self.save(_qt_t("QT_REINSTALL_DONE", "Version files removed, they will be reinstalled on launch"))
        self._load_versions()


class QtContentPage(QtWidgets.QWidget):
    PAGE_SIZE = 20
    TYPES = [("mod", "CONTENT_TYPE_MODS", "Mods"), ("modpack", "CONTENT_TYPE_MODPACKS", "Modpacks"), ("resourcepack", "CONTENT_TYPE_RESOURCEPACKS", "Resource packs"), ("shader", "CONTENT_TYPE_SHADERS", "Shaders"), ("datapack", "CONTENT_TYPE_DATAPACKS", "Data packs")]
    def __init__(self, launcher, parent=None):
        super().__init__(parent)
        self.launcher = launcher
        self.instance_manager = launcher.instance_manager
        self.content_type = "mod"
        self.offset = 0
        self.total_hits = 0
        self.instances = []
        self._search_token = 0
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(18, 12, 18, 10)
        lay.setSpacing(8)
        top = QtWidgets.QHBoxLayout()
        self.type_group = QtWidgets.QButtonGroup(self)
        self.type_buttons = {}
        for tag, key, default in self.TYPES:
            b = QtWidgets.QPushButton(_tr(launcher, key, default))
            b.setObjectName("topnav")
            b.setCheckable(True)
            b.setCursor(Qt.PointingHandCursor)
            b.clicked.connect(lambda _=False, t=tag: self.set_type(t))
            self.type_group.addButton(b)
            self.type_buttons[tag] = b
            top.addWidget(b)
        self.type_buttons["mod"].setChecked(True)
        top.addStretch(1)
        top.addWidget(_qt_label(_qt_t("QT_INSTANCE", "Instance") + ":", "muted"))
        self.instance_combo = QtWidgets.QComboBox()
        self.instance_combo.setMinimumWidth(220)
        self.instance_combo.currentIndexChanged.connect(lambda *_: self._on_instance_pick())
        top.addWidget(self.instance_combo)
        lay.addLayout(top)
        srow = QtWidgets.QHBoxLayout()
        self.search = QtWidgets.QLineEdit()
        self.search.setPlaceholderText(_tr(launcher, "CONTENT_SEARCH", "Search Modrinth..."))
        self.search.setClearButtonEnabled(True)
        self.search.returnPressed.connect(self.on_search)
        srow.addWidget(self.search, 1)
        self.sort = QtWidgets.QComboBox()
        for value, text in (("relevance", _qt_t("QT_SORT_RELEVANCE", "Relevance")), ("downloads", _qt_t("QT_SORT_DOWNLOADS", "Downloads")), ("follows", _qt_t("QT_SORT_FOLLOWS", "Follows")), ("newest", _qt_t("QT_SORT_NEWEST", "Newest")), ("updated", _qt_t("QT_SORT_UPDATED", "Recently updated"))):
            self.sort.addItem(text, value)
        self.sort.currentIndexChanged.connect(lambda *_: self.on_search())
        srow.addWidget(self.sort)
        srow.addWidget(_qt_button(_tr(launcher, "CONTENT_SEARCH", "Search"), self.on_search, kind="accent"))
        lay.addLayout(srow)
        frow = QtWidgets.QHBoxLayout()
        frow.addWidget(_qt_label(_qt_t("QT_FILTER_VERSION", "Game version") + ":", "muted"))
        self.version_filter = QtWidgets.QComboBox()
        self.version_filter.setMinimumWidth(150)
        frow.addWidget(self.version_filter)
        self.loader_label = _qt_label(_qt_t("QT_FILTER_LOADER", "Loader") + ":", "muted")
        frow.addWidget(self.loader_label)
        self.loader_filter = QtWidgets.QComboBox()
        self.loader_filter.setMinimumWidth(130)
        frow.addWidget(self.loader_filter)
        frow.addWidget(_qt_label(_qt_t("QT_FILTER_CATEGORY", "Category") + ":", "muted"))
        self.category_filter = QtWidgets.QComboBox()
        self.category_filter.setMinimumWidth(150)
        self.category_filter.addItem(_qt_t("QT_FILTER_ANY", "Any"), "")
        frow.addWidget(self.category_filter)
        frow.addStretch(1)
        lay.addLayout(frow)
        self._categories = []
        self._fill_version_filter()
        self._fill_loader_filter()
        self._apply_type_filter_defaults()
        for combo in (self.version_filter, self.loader_filter, self.category_filter):
            combo.currentIndexChanged.connect(lambda *_: self._on_filter_changed())
        _qt_run_bg(self._load_categories, self._on_categories_loaded, lambda e: None)
        self.status = _qt_label("", "muted", wrap=True)
        lay.addWidget(self.status)
        self.results_host = QtWidgets.QWidget()
        self.results = QtWidgets.QVBoxLayout(self.results_host)
        self.results.setContentsMargins(0, 0, 0, 0)
        self.results.setSpacing(6)
        self.results.addStretch(1)
        self.scroll = _qt_scroll(self.results_host)
        lay.addWidget(self.scroll, 1)
        pag = QtWidgets.QHBoxLayout()
        self.prev_btn = _qt_button(_tr(launcher, "CONTENT_PREV", "Previous"), self.prev_page)
        self.next_btn = _qt_button(_tr(launcher, "CONTENT_NEXT", _qt_t("QT_NEXT", "Next")), self.next_page)
        self.page_label = _qt_label("", "muted")
        pag.addWidget(self.prev_btn)
        pag.addWidget(self.page_label)
        pag.addWidget(self.next_btn)
        pag.addStretch(1)
        lay.addLayout(pag)
        self._searched = False
        self.refresh_instances()
        self.instance_manager.register_callback(lambda: _qt_later(self.refresh_instances))

    def refresh_instances(self):
        self.instance_combo.blockSignals(True)
        self.instance_combo.clear()
        self.instances = list(self.instance_manager.instances.values())
        for inst in self.instances:
            self.instance_combo.addItem(f"{inst.name}  ({inst.version}, {inst.mod_loader})")
        sel = self.instance_manager.selected_instance_id
        for i, inst in enumerate(self.instances):
            if inst.instance_id == sel:
                self.instance_combo.setCurrentIndex(i)
                break
        self.instance_combo.blockSignals(False)
        self._refresh_instance_filter_labels()

    def _on_instance_pick(self):
        self._refresh_instance_filter_labels()
        if self._searched:
            self.on_search()
    INSTANCE_FILTER = "__instance__"
    LOADERS = ["fabric", "forge", "neoforge", "quilt"]

    def _fill_version_filter(self):
        combo = self.version_filter
        combo.blockSignals(True)
        combo.clear()
        combo.addItem(_qt_t("QT_FILTER_ANY", "Any"), "")
        combo.addItem("", self.INSTANCE_FILTER)
        try:
            versions = [v.id for v in get_available_versions_detailed() if getattr(v, "type", "release") == "release"]
        except Exception:
            versions = []
        for vid in versions:
            combo.addItem(vid, vid)
        combo.blockSignals(False)
        self._refresh_instance_filter_labels()

    def _fill_loader_filter(self):
        combo = self.loader_filter
        combo.blockSignals(True)
        combo.clear()
        combo.addItem(_qt_t("QT_FILTER_ANY", "Any"), "")
        combo.addItem("", self.INSTANCE_FILTER)
        for name in self.LOADERS:
            combo.addItem(name.capitalize() if name != "neoforge" else "NeoForge", name)
        combo.blockSignals(False)

    def _refresh_instance_filter_labels(self):
        inst = self.target_instance()
        ver = inst.version if inst else "-"
        loader = (self._normalized_loader(inst) or "vanilla") if inst else "-"
        for combo, value in ((self.version_filter, ver), (self.loader_filter, loader)):
            idx = combo.findData(self.INSTANCE_FILTER)
            if idx >= 0:
                combo.setItemText(idx, _qt_t("QT_FILTER_INSTANCE", "Instance ({value})").format(value=value))

    def _apply_type_filter_defaults(self):
        is_modpack = self.content_type == "modpack"
        shows_loader = self.content_type in ("mod", "modpack")
        self.loader_label.setVisible(shows_loader)
        self.loader_filter.setVisible(shows_loader)
        for combo in (self.version_filter, self.loader_filter):
            combo.blockSignals(True)
            combo.setCurrentIndex(combo.findData("" if is_modpack else self.INSTANCE_FILTER))
            combo.blockSignals(False)
        self._fill_category_filter()

    def _on_filter_changed(self):
        if self._searched:
            self.on_search()

    def _load_categories(self):
        r = _http_session.get(f"{MODRINTH_API_URL}/tag/category", timeout=20)
        r.raise_for_status()
        return r.json() or []

    def _on_categories_loaded(self, cats):
        self._categories = [c for c in cats if c.get("header") == "categories"]
        self._fill_category_filter()

    def _fill_category_filter(self):
        combo = self.category_filter
        current = combo.currentData() or ""
        combo.blockSignals(True)
        combo.clear()
        combo.addItem(_qt_t("QT_FILTER_ANY", "Any"), "")
        names = sorted({c.get("name") for c in self._categories if c.get("project_type") == self.content_type and c.get("name")})
        for name in names:
            combo.addItem(name.replace("-", " ").capitalize(), name)
        idx = combo.findData(current)
        combo.setCurrentIndex(idx if idx >= 0 else 0)
        combo.blockSignals(False)

    def filter_version(self):
        value = self.version_filter.currentData() or ""
        if value == self.INSTANCE_FILTER:
            inst = self.target_instance()
            return inst.version if inst else None
        return value or None

    def filter_loader(self):
        if self.content_type not in ("mod", "modpack"):
            return None
        value = self.loader_filter.currentData() or ""
        if value == self.INSTANCE_FILTER:
            return self._normalized_loader(self.target_instance())
        return value or None

    def filter_category(self):
        return self.category_filter.currentData() or None

    def target_instance(self):
        idx = self.instance_combo.currentIndex()
        if 0 <= idx < len(self.instances):
            return self.instances[idx]
        return None

    @staticmethod
    def _normalized_loader(instance):
        loader = (instance.mod_loader or "").lower() if instance else ""
        return loader if loader in ("fabric", "forge", "quilt", "neoforge") else None

    @staticmethod
    def _is_vanilla(instance):
        loader = (instance.mod_loader or "vanilla").lower() if instance else "vanilla"
        return loader in ("vanilla", "none", "")

    def ensure_loaded(self):
        if not self._searched:
            self.on_search()

    def set_type(self, tag):
        self.content_type = tag
        self.type_buttons[tag].setChecked(True)
        self._apply_type_filter_defaults()
        self.offset = 0
        self.on_search()

    def on_search(self):
        self.offset = 0
        self.run_search()

    def prev_page(self):
        if self.offset > 0:
            self.offset = max(0, self.offset - self.PAGE_SIZE)
            self.run_search()

    def next_page(self):
        if self.offset + self.PAGE_SIZE < self.total_hits:
            self.offset += self.PAGE_SIZE
            self.run_search()

    def _update_paging(self):
        page = self.offset // self.PAGE_SIZE + 1
        pages = max(1, (self.total_hits + self.PAGE_SIZE - 1) // self.PAGE_SIZE)
        self.page_label.setText(_qt_t("QT_PAGE", "Page {page} / {pages}").format(page=page, pages=pages))
        self.prev_btn.setEnabled(self.offset > 0)
        self.next_btn.setEnabled(self.offset + self.PAGE_SIZE < self.total_hits)

    def _clear_results(self):
        while self.results.count() > 1:
            item = self.results.takeAt(0)
            w = item.widget()
            if w is not None:
                w.deleteLater()

    def run_search(self):
        self._searched = True
        self._clear_results()
        instance = self.target_instance()
        if self.content_type == "mod" and instance is not None and self._is_vanilla(instance):
            self.status.setText(_tr(self.launcher, "CONTENT_VANILLA_WARNING", "The selected instance is vanilla; pick a modded instance to browse mods."))
            self.total_hits = 0
            self._update_paging()
            return
        query = self.search.text().strip()
        sort = self.sort.currentData() or "relevance"
        version = self.filter_version()
        loader = self.filter_loader()
        category = self.filter_category()
        self.status.setText(_tr(self.launcher, "CONTENT_SEARCHING", "Searching..."))
        self._search_token += 1
        token = self._search_token
        offset = self.offset

        def work():
            facets = [[f"project_type:{self.content_type}"]]
            if version:
                facets.append([f"versions:{version}"])
            if loader:
                facets.append([f"categories:{loader}"])
            if category:
                facets.append([f"categories:{category}"])
            params = {"query": query, "facets": json.dumps(facets), "index": sort, "offset": offset, "limit": self.PAGE_SIZE}
            r = _http_session.get(f"{MODRINTH_API_URL}/search", params=params, timeout=25)
            r.raise_for_status()
            payload = r.json()
            return payload.get("hits", []), payload.get("total_hits", 0)

        def done(result):
            if token != self._search_token:
                return
            hits, total = result
            self.total_hits = total
            for hit in hits:
                self.results.insertWidget(self.results.count() - 1, self._make_row(hit))
            if total == 0:
                self.status.setText(_tr(self.launcher, "CONTENT_NO_RESULTS", "No results."))
            else:
                suffix = ((" " + _qt_t("QT_FOR_VERSION", "for {version}").format(version=version)) if version else "") + (f" ({loader})" if loader else "")
                self.status.setText(_tr(self.launcher, "CONTENT_RESULTS_COUNT", "{count} results").format(count=f"{total:,}") + suffix)
            self.scroll.verticalScrollBar().setValue(0)
            self._update_paging()
        _qt_run_bg(work, done, lambda e: self.status.setText(_qt_t("QT_SEARCH_FAILED", "Search failed: {error}").format(error=e)))

    def _make_row(self, hit):
        row = QtWidgets.QFrame()
        row.setObjectName("resultRow")
        h = QtWidgets.QHBoxLayout(row)
        h.setContentsMargins(10, 8, 10, 8)
        icon = QtWidgets.QLabel()
        icon.setFixedSize(56, 56)
        icon.setScaledContents(True)
        h.addWidget(icon)
        url = hit.get("icon_url")
        if url:
            def done(data, lbl=icon):
                pix = _qt_pixmap_from_bytes(data, (56, 56))
                if pix is not None:
                    try:
                        lbl.setPixmap(pix)
                    except RuntimeError:
                        pass
            _qt_run_bg(lambda: _cached_image_get(url), done, lambda e: None)
        col = QtWidgets.QVBoxLayout()
        col.setSpacing(2)
        title_row = QtWidgets.QHBoxLayout()
        title = _qt_label(hit.get("title", "?"), "h3")
        title_row.addWidget(title)
        if hit.get("author"):
            title_row.addWidget(_qt_label(_qt_t("QT_BY", "by {author}").format(author=hit.get("author")), "muted"))
        title_row.addStretch(1)
        col.addLayout(title_row)
        desc = (hit.get("description") or "").replace("\n", " ")
        if len(desc) > 220:
            desc = desc[:217] + "..."
        col.addWidget(_qt_label(desc, "muted", wrap=True))
        cats = ", ".join((hit.get("categories") or [])[:5])
        col.addWidget(_qt_label(_qt_t("QT_DOWNLOADS_COUNT", "{count} downloads").format(count=f"{hit.get('downloads', 0):,}") + (f"  ·  {cats}" if cats else ""), "hint"))
        h.addLayout(col, 1)
        slug = hit.get("slug") or hit.get("project_id")
        btn_col = QtWidgets.QVBoxLayout()
        install = _qt_button(_tr(self.launcher, "CONTENT_INSTALL_BTN", "Install"), None, kind="accent")
        install.clicked.connect(lambda *_: self.install(slug, install, hit.get("title", "?")))
        btn_col.addWidget(install)
        btn_col.addWidget(_qt_button(_qt_t("QT_CHOOSE_VERSION", "Choose version..."), lambda: self.install(slug, install, hit.get("title", "?"), choose=True), kind="flat"))
        ptype = hit.get("project_type") or self.content_type
        btn_col.addWidget(_qt_button(_qt_t("QT_PAGE_BTN", "Page"), lambda: open_with_browser(f"https://modrinth.com/{ptype}/{slug}"), kind="flat"))
        h.addLayout(btn_col)
        return row

    def _fetch_versions(self, slug, version, loader):
        params = {}
        if version:
            params["game_versions"] = json.dumps([version])
        if loader:
            params["loaders"] = json.dumps([loader])
        r = _http_session.get(f"{MODRINTH_API_URL}/project/{slug}/version", params=params, timeout=25)
        r.raise_for_status()
        return r.json() or []

    @staticmethod
    def _primary_file(version):
        files = (version or {}).get("files", [])
        return next((f for f in files if f.get("primary")), files[0] if files else None)

    def _is_mrpack_version(self, version):
        f = self._primary_file(version)
        return bool(f and f.get("filename", "").lower().endswith(".mrpack"))

    def install(self, slug, button, title, choose=False):
        is_modpack = self.content_type == "modpack"
        instance = None if is_modpack else self.target_instance()
        if not is_modpack:
            if instance is None:
                messagebox.showwarning(_tr(self.launcher, "CONTENT_TAB", "Content"), _tr(self.launcher, "CONTENT_NEED_PROFILE", "Create or select an instance first."))
                return
            if self.content_type == "mod" and self._is_vanilla(instance):
                messagebox.showwarning(_tr(self.launcher, "CONTENT_TAB", "Content"), _tr(self.launcher, "CONTENT_VANILLA_NO_MODS", "Vanilla instances cannot load mods."))
                return
        version = self.filter_version()
        loader = self.filter_loader()
        if choose:
            dlg = QtModrinthVersionDialog(self.launcher, title, lambda: self._fetch_versions(slug, version, loader),
                                          accept=(self._is_mrpack_version if is_modpack else None), parent=self)
            if dlg.exec() != QtWidgets.QDialog.Accepted or dlg.chosen is None:
                return
            self._install_version(dlg.chosen, instance, button, title)
            return
        button.setEnabled(False)
        button.setText(_tr(self.launcher, "CONTENT_INSTALLING_BTN", "Installing..."))

        def work():
            versions = self._fetch_versions(slug, version, loader)
            if is_modpack:
                versions = [v for v in versions if self._is_mrpack_version(v)]
            return pick_modrinth_version(versions)

        def done(chosen):
            if chosen is None:
                button.setEnabled(True)
                button.setText(_tr(self.launcher, "CONTENT_NOT_COMPATIBLE_BTN", "Not compatible"))
                if is_modpack:
                    self.status.setText(_tr(self.launcher, "CONTENT_NO_MRPACK", "{title} has no .mrpack file.").format(title=title))
                else:
                    target = (version or _qt_t("QT_FILTER_ANY", "Any")) + (f" ({loader})" if loader else "")
                    self.status.setText(_tr(self.launcher, "CONTENT_NOT_COMPATIBLE_MSG", "{title} has no version for {target}.").format(title=title, target=target))
                return
            self._install_version(chosen, instance, button, title)

        def fail(err):
            button.setEnabled(True)
            button.setText(_tr(self.launcher, "CONTENT_FAILED_BTN", "Failed"))
            self.status.setText(_tr(self.launcher, "CONTENT_INSTALL_FAILED", "Install failed: {error}").format(error=err))
        _qt_run_bg(work, done, fail)

    def _install_version(self, chosen, instance, button, title):
        if self.content_type == "modpack":
            self._install_modpack_version(chosen, button, title)
            return
        file_info = self._primary_file(chosen)
        if not file_info:
            self.status.setText(_tr(self.launcher, "CONTENT_INSTALL_FAILED", "Install failed: {error}").format(error="no file"))
            return
        dest_dir = {"mod": instance.mods_dir, "resourcepack": instance.resourcepacks_dir, "shader": instance.shaderpacks_dir}.get(self.content_type, instance.minecraft_dir / "datapacks")
        button.setEnabled(False)
        button.setText(_tr(self.launcher, "CONTENT_INSTALLING_BTN", "Installing..."))

        def work():
            dest_dir.mkdir(parents=True, exist_ok=True)
            dest_path = Path(dest_dir) / file_info["filename"]
            with _http_session.get(file_info["url"], stream=True, timeout=60) as dl:
                dl.raise_for_status()
                with open(dest_path, "wb") as out:
                    for chunk in dl.iter_content(chunk_size=1024 * 128):
                        if chunk:
                            out.write(chunk)
            if self.content_type in ("resourcepack", "shader"):
                try:
                    self.launcher._apply_sharing_for_instance(instance)
                except Exception:
                    pass
            return dest_path

        def done(dest_path):
            button.setText(_tr(self.launcher, "CONTENT_INSTALLED_BTN", _qt_t("QT_INSTALLED", "Installed")))
            self.status.setText(_tr(self.launcher, "CONTENT_INSTALLED_STATUS", "Installed {name}").format(name=dest_path.name))

        def fail(err):
            button.setEnabled(True)
            button.setText(_tr(self.launcher, "CONTENT_FAILED_BTN", "Failed"))
            self.status.setText(_tr(self.launcher, "CONTENT_INSTALL_FAILED", "Install failed: {error}").format(error=err))
        _qt_run_bg(work, done, fail)

    def _install_modpack_version(self, chosen, button, title):
        file_info = self._primary_file(chosen)
        if not file_info or not self._is_mrpack_version(chosen):
            button.setEnabled(True)
            button.setText(_tr(self.launcher, "CONTENT_NOT_COMPATIBLE_BTN", "Not compatible"))
            self.status.setText(_tr(self.launcher, "CONTENT_NO_MRPACK", "{title} has no .mrpack file.").format(title=title))
            return
        button.setEnabled(False)
        button.setText(_tr(self.launcher, "CONTENT_INSTALLING_BTN", "Installing..."))

        def work():
            _qt_later(lambda: self.status.setText(_tr(self.launcher, "CONTENT_DOWNLOADING", "Downloading {title}...").format(title=title)))
            dest_path = Path(tempfile.gettempdir()) / file_info["filename"]
            with _http_session.get(file_info["url"], stream=True, timeout=120) as dl:
                dl.raise_for_status()
                with open(dest_path, "wb") as out:
                    for chunk in dl.iter_content(chunk_size=1024 * 128):
                        if chunk:
                            out.write(chunk)
            _qt_later(lambda: self.status.setText(_tr(self.launcher, "CONTENT_INSTALLING_STATUS", "Installing {title}...").format(title=title)))
            result = import_modpack(str(dest_path), self.launcher)
            try:
                dest_path.unlink()
            except Exception:
                pass
            return result

        def done(result):
            success, message, profile_name = result
            self.launcher.set_status(_qt_t("QT_READY", "Ready"))
            if success:
                button.setText(_tr(self.launcher, "CONTENT_INSTALLED_BTN", _qt_t("QT_INSTALLED", "Installed")))
                self.status.setText(_tr(self.launcher, "CONTENT_INSTALLED_STATUS", "Installed {name}").format(name=profile_name))
                self.launcher.refresh_instance_display()
                self.launcher.instances_page.refresh(force=True)
                messagebox.showinfo(_tr(self.launcher, "CONTENT_TAB", "Content"), _tr(self.launcher, "CONTENT_MODPACK_DONE", "Imported as '{profile}'.\n{message}").format(profile=profile_name, message=message))
            else:
                button.setEnabled(True)
                button.setText(_tr(self.launcher, "CONTENT_FAILED_BTN", "Failed"))
                messagebox.showerror(_tr(self.launcher, "CONTENT_TAB", "Content"), _tr(self.launcher, "CONTENT_MODPACK_FAIL", "Import failed: {message}").format(message=message))

        def fail(err):
            button.setEnabled(True)
            button.setText(_tr(self.launcher, "CONTENT_FAILED_BTN", "Failed"))
            self.status.setText(_tr(self.launcher, "CONTENT_INSTALL_FAILED", "Install failed: {error}").format(error=err))
        _qt_run_bg(work, done, fail)


class QtModrinthVersionDialog(QtWidgets.QDialog):
    def __init__(self, launcher, title, fetch, accept=None, parent=None):
        super().__init__(parent or launcher)
        self.launcher = launcher
        self.chosen = None
        self.setWindowTitle(_qt_t("QT_VERSION_PICKER_TITLE", "{title} - versions").format(title=title))
        self.resize(720, 480)
        lay = QtWidgets.QVBoxLayout(self)
        lay.addWidget(_qt_label(title, "h2"))
        self.status = _qt_label(_qt_t("QT_VERSION_PICKER_LOADING", "Loading versions..."), "muted", wrap=True)
        lay.addWidget(self.status)
        self.list = QtWidgets.QListWidget()
        self.list.setSelectionMode(QtWidgets.QAbstractItemView.SingleSelection)
        self.list.itemDoubleClicked.connect(lambda *_: self._accept())
        lay.addWidget(self.list, 1)
        btns = QtWidgets.QHBoxLayout()
        btns.addStretch(1)
        self.install_btn = _qt_button(_tr(launcher, "CONTENT_INSTALL_BTN", "Install"), self._accept, kind="accent")
        self.install_btn.setEnabled(False)
        btns.addWidget(self.install_btn)
        btns.addWidget(_qt_button(_tr(launcher, "CANCEL", "Cancel"), self.reject))
        lay.addLayout(btns)
        self.versions = []

        def work():
            versions = fetch()
            if accept:
                versions = [v for v in versions if accept(v)]
            recommended = pick_modrinth_version(versions)
            ordered = sorted(versions, key=lambda v: v.get("date_published") or "", reverse=True)
            return ordered, recommended
        _qt_run_bg(work, self._loaded, lambda e: self.status.setText(_qt_t("QT_SEARCH_FAILED", "Search failed: {error}").format(error=e)))

    def _loaded(self, result):
        ordered, recommended = result
        self.versions = ordered
        if not ordered:
            self.status.setText(_qt_t("QT_VERSION_PICKER_EMPTY", "No versions match the current filters."))
            return
        self.status.setText(_qt_t("QT_VERSION_PICKER_COUNT", "{count} versions - newest first").format(count=len(ordered)))
        rec_id = (recommended or {}).get("id")
        for v in ordered:
            name = v.get("name") or v.get("version_number") or "?"
            number = v.get("version_number") or ""
            head = name if not number or number in name else f"{name}  ({number})"
            if v.get("id") == rec_id:
                head += "  ·  " + _qt_t("QT_VERSION_RECOMMENDED", "Recommended")
            gv = ", ".join(v.get("game_versions") or [])
            if len(gv) > 60:
                gv = gv[:57] + "..."
            loaders = ", ".join(v.get("loaders") or [])
            date = (v.get("date_published") or "")[:10]
            channel = (v.get("version_type") or "release").lower()
            size = self._size(v)
            detail = "  ·  ".join(x for x in (gv, loaders, channel, date, size) if x)
            item = QtWidgets.QListWidgetItem(f"{head}\n{detail}")
            item.setData(Qt.UserRole, v)
            self.list.addItem(item)
            if v.get("id") == rec_id:
                self.list.setCurrentItem(item)
        if self.list.currentItem() is None:
            self.list.setCurrentRow(0)
        self.install_btn.setEnabled(True)

    @staticmethod
    def _size(version):
        files = version.get("files") or []
        primary = next((f for f in files if f.get("primary")), files[0] if files else None)
        return _human_size(primary.get("size")) if primary and primary.get("size") else ""

    def _accept(self):
        item = self.list.currentItem()
        if item is None:
            return
        self.chosen = item.data(Qt.UserRole)
        self.accept()


class QtSkinPreview(QtWidgets.QLabel):
    def __init__(self, parent=None):
        super().__init__(parent)
        self.setFixedSize(_SKIN_PREVIEW_W, _SKIN_PREVIEW_H)
        self.setAlignment(Qt.AlignCenter)
        self.state = None
        self._last = None
        self.setCursor(Qt.OpenHandCursor)

    def set_skin(self, skin, slim, cape):
        if skin is None:
            self.state = None
            self.setPixmap(QtGui.QPixmap())
            self.setText(_qt_t("QT_NO_SKIN_PREVIEW", "No skin preview available"))
            return
        prev = self.state or {}
        self.state = {"skin": skin, "slim": slim, "cape": cape, "yaw": prev.get("yaw", 25.0), "pitch": prev.get("pitch", 10.0)}
        self.render()

    def render(self):
        st = self.state
        if not st:
            return
        try:
            img = _render_skin_3d(st["skin"], _SKIN_PREVIEW_W, _SKIN_PREVIEW_H, yaw_deg=st["yaw"], pitch_deg=st["pitch"], slim=st["slim"], cape=st.get("cape"))
            self.setText("")
            self.setPixmap(QtGui.QPixmap.fromImage(img))
        except Exception as e:
            print(f"[Skin] render failed: {e}")

    def mousePressEvent(self, event):
        self._last = event.position()
        self.setCursor(Qt.ClosedHandCursor)

    def mouseReleaseEvent(self, event):
        self._last = None
        self.setCursor(Qt.OpenHandCursor)

    def mouseMoveEvent(self, event):
        if self._last is None or not self.state:
            return
        pos = event.position()
        self.state["yaw"] += (pos.x() - self._last.x()) * 0.8
        self.state["pitch"] = max(-60.0, min(60.0, self.state["pitch"] + (pos.y() - self._last.y()) * 0.4))
        self._last = pos
        self.render()


class QtSettingsPage(QtWidgets.QWidget):
    def __init__(self, launcher, parent=None):
        super().__init__(parent)
        self.launcher = launcher
        lay = QtWidgets.QHBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(0)
        nav = QtWidgets.QFrame()
        nav.setObjectName("sideNav")
        nav.setFixedWidth(220)
        nl = QtWidgets.QVBoxLayout(nav)
        nl.setContentsMargins(12, 20, 12, 12)
        nl.setSpacing(2)
        nl.addWidget(_qt_label(_tr(launcher, "SETTINGS", "Settings"), "h1"))
        nl.addSpacing(10)
        self.stack = QtWidgets.QStackedWidget()
        self.nav_group = QtWidgets.QButtonGroup(self)
        self.pages = {}
        for key, default, icon, builder in (("SETTINGS_NAV_GENERAL", "General", "general", self._build_general), ("SETTINGS_NAV_ACCOUNTS", "Accounts", "accounts", self._build_accounts), ("SETTINGS_NAV_ADVANCED", "Advanced", "advanced", self._build_advanced), ("SETTINGS_NAV_ABOUT", "About", "about", self._build_about)):
            b = QtWidgets.QPushButton(_tr(launcher, key, default))
            b.setObjectName("nav")
            b.setCheckable(True)
            b.setIcon(_qt_icon(icon, 18, launcher.theme.c("fg_secondary")))
            b.setIconSize(QSize(18, 18))
            b.setCursor(Qt.PointingHandCursor)
            page = builder()
            idx = self.stack.addWidget(page)
            self.pages[default] = page
            b.clicked.connect(lambda _=False, i=idx: self.stack.setCurrentIndex(i))
            self.nav_group.addButton(b)
            nl.addWidget(b)
            if idx == 0:
                b.setChecked(True)
        nl.addStretch(1)
        lay.addWidget(nav)
        lay.addWidget(self.stack, 1)

    def _page(self):
        page, lay = _qt_page(spacing=16, margins=(32, 26, 32, 26))
        return page, lay
        #Settings general page
    def _build_general(self):
        page, lay = self._page()
        L = self.launcher
        card = _Card(_tr(L, "SETTINGS_CARD_LANGUAGE", "Language"))
        card.add(_qt_label(_tr(L, "SETTINGS_LANG_LABEL", "Launcher language"), "muted"))
        self.lang_combo = QtWidgets.QComboBox()
        names = {'en-US': 'English (United States)', 'lt-LT': 'Lietuvių (Lithuania)', 'ru-RU': 'Русский (Russia)', 'pl-PL': 'Polski (Poland)', 'de-DE': 'Deutsch (Germany)', 'lv-LV': 'Latviešu (Latvia)', 'na-NA': 'For Translators'}
        for code in L.locales:
            self.lang_combo.addItem(names.get(code, code), code)
        idx = self.lang_combo.findData(L.current_locale)
        if idx >= 0:
            self.lang_combo.setCurrentIndex(idx)
        self.lang_combo.setMaximumWidth(320)

        def on_lang():
            code = self.lang_combo.currentData()
            if code and code != L.current_locale:
                _save_language_preference(code)
                L.current_locale = code
                L.translations = L.locales.get(code, {})
                _save_settings(L)
                messagebox.showinfo(_tr(L, "LANGUAGE_CHANGED_TITLE", "Language"), _qt_t("QT_LANGUAGE_SAVED", "Language preference saved. Restart the launcher to apply the change."))
        self.lang_combo.activated.connect(lambda *_: on_lang())
        card.add(self.lang_combo)
        card.add(_qt_label(_tr(L, "SETTINGS_LANG_WARNING", "Restart the launcher to apply."), "hint"))
        lay.addWidget(card)
        card = _Card(_tr(L, "SETTINGS_CARD_THEME", "Theme"))
        card.add(_qt_label(_tr(L, "SETTINGS_THEME_DESC", "Pick how the launcher looks."), "muted"))
        col = QtWidgets.QVBoxLayout()
        col.setSpacing(6)
        self.theme_group = QtWidgets.QButtonGroup(self)
        descriptions = {QT_THEME_SYSTEM: _qt_t("QT_THEME_SYSTEM_DESC", "Follows your desktop color"), "Arc": _tr(L, "THEME_ARC_DESC", "Balanced gray"), "Dark Prism": _tr(L, "THEME_DARK_PRISM_DESC", "Pure black"), "Light Mode": _tr(L, "THEME_LIGHT_DESC", "Bright")}
        swatches = {QT_THEME_SYSTEM: L.theme.c("accent_primary") if L.theme.name == QT_THEME_SYSTEM else "#5a5a5a", "Arc": "#2b2b2b", "Dark Prism": "#000000", "Light Mode": "#f0f0f0"}
        
        for name in L.theme.available():
            row = QtWidgets.QHBoxLayout()
            row.setSpacing(10)
            rb = QtWidgets.QRadioButton(_qt_t("QT_THEME_SYSTEM", "System") if name == QT_THEME_SYSTEM else name)
            rb.setProperty("theme", name)
            rb.setChecked(name == L.theme.name)
            rb.setCursor(Qt.PointingHandCursor)
            rb.clicked.connect(lambda _=False, n=name: L.apply_theme(n))
            self.theme_group.addButton(rb)
            sw = QtWidgets.QFrame()
            sw.setFixedSize(22, 22)
            sw.setStyleSheet(f"background: {swatches.get(name, '#888')}; border: 1px solid {L.theme.c('border')}; border-radius: 4px;")
            row.addWidget(sw)
            row.addWidget(rb)
            row.addWidget(_qt_label(descriptions.get(name, ""), "muted"), 1)
            col.addLayout(row)

        card.add_layout(col)
        lay.addWidget(card)
        card = _Card(_tr(L, "SETTINGS_GPU_SECTION", _qt_t("QT_GRAPHICS", "Graphics")))
        card.add(_ToggleRow(_tr(L, "SETTINGS_GPU_DRI_PRIME", "Use the discrete GPU (DRI_PRIME)"), _tr(L, "SETTINGS_GPU_DRI_PRIME_DESC", "Launch Minecraft on the dedicated GPU on hybrid laptops."), L.use_dri_prime, lambda: _save_settings(L)))
        lay.addWidget(card)
        card = _Card(_tr(L, "SETTINGS_SHARED_FILES", "Shared files"))
        card.add(_qt_label(_tr(L, "SETTINGS_SHARED_FILES_DESC", "Share these between all instances via symlinks."), "muted", wrap=True))
        for attr, key, dkey in (("share_options", "SETTINGS_SHARED_OPTIONS_TXT", "SETTINGS_SHARED_OPTIONS_DESC"), ("share_resourcepacks", "SETTINGS_SHARED_RESOURCEPACKS", "SETTINGS_SHARED_RESOURCEPACKS_DESC"), ("share_shaderpacks", "SETTINGS_SHARED_SHADERPACKS", "SETTINGS_SHARED_SHADERPACKS_DESC"), ("share_servers", "SETTINGS_SHARED_SERVERS", "SETTINGS_SHARED_SERVERS_DESC"), ("share_screenshots", "SETTINGS_SHARED_SCREENSHOTS", "SETTINGS_SHARED_SCREENSHOTS_DESC")):
            card.add(_ToggleRow(_tr(L, key, attr.replace("share_", "").title()), _tr(L, dkey, ""), getattr(L, attr), lambda: _on_share_toggle(L)))
        b = _qt_button(_tr(L, "SETTINGS_SHARED_APPLY_ALL", "Apply to all instances"), lambda: _qt_run_bg(L._apply_sharing_all, lambda _: messagebox.showinfo(_tr(L, "SETTINGS_SHARED_FILES", "Shared files"), _tr(L, "SETTINGS_SHARED_UPDATED", "Sharing updated."))))
        hb = QtWidgets.QHBoxLayout()
        hb.addWidget(b)
        hb.addStretch(1)
        card.add_layout(hb)
        lay.addWidget(card)
        lay.addStretch(1)
        return _qt_scroll(page)

        # account page
    def _build_accounts(self):
        page, lay = self._page()
        L = self.launcher
        card = _Card(_tr(L, "SETTINGS_ACCOUNTS_TITLE", "Accounts"))
        card.add(_qt_label(_tr(L, "SETTINGS_ACCOUNTS_DESC", "Manage Microsoft and offline accounts."), "muted", wrap=True))
        row = QtWidgets.QHBoxLayout()
        self.acc_list = QtWidgets.QListWidget()
        self.acc_list.setMinimumHeight(220)
        self.acc_list.currentRowChanged.connect(self._on_account_row)
        row.addWidget(self.acc_list, 1)
        pv = QtWidgets.QVBoxLayout()
        self.skin = QtSkinPreview()
        pv.addWidget(self.skin, 0, Qt.AlignHCenter)
        self.skin_caption = _qt_label("", "h3", align=Qt.AlignCenter)
        pv.addWidget(self.skin_caption)
        pv.addWidget(_qt_label(_qt_t("QT_DRAG_ROTATE", "Drag to rotate"), "hint", align=Qt.AlignCenter))
        self.cape_combo = QtWidgets.QComboBox()
        self.cape_combo.setToolTip(_qt_t("QT_CAPE_TIP", "Choose which cape is shown in-game"))
        self.cape_combo.setIconSize(QSize(20, 32))
        self.cape_combo.hide()
        self.cape_combo.activated.connect(self._on_cape_pick)
        pv.addWidget(self.cape_combo)
        pv.addStretch(1)
        row.addLayout(pv)
        card.add_layout(row)
        btns = QtWidgets.QHBoxLayout()
        btns.addWidget(_qt_button(_tr(L, "SETTINGS_ACCOUNTS_ADD_MS", "Add Microsoft account"), lambda: L.add_account("microsoft"), kind="accent", icon="microsoft", launcher=L))
        btns.addWidget(_qt_button(_qt_t("QT_MS_BROWSER_OPTION", "Microsoft via browser"), lambda: L.add_account("microsoft", mode="browser"), icon="microsoft", launcher=L, tooltip=_qt_t("QT_MS_BROWSER_OPTION_TIP", "Opens microsoft.com/link in your web browser with a one-time code already filled in; the launcher finishes the login by itself once you signed in there.")))
        btns.addWidget(_qt_button(_tr(L, "SETTINGS_ACCOUNTS_ADD_OFFLINE", "Add offline account"), lambda: L.add_account("offline"), icon="offline", launcher=L))
        btns.addWidget(_qt_button(_tr(L, "REMOVE", "Remove"), self._remove_account, icon="trash", launcher=L))
        btns.addStretch(1)
        card.add_layout(btns)
        card.add(_qt_label(_qt_t("QT_MS_TWO_WAYS", "Two ways to sign in: the embedded window logs you in inside the launcher; the browser option opens your default browser with a one-time code and the launcher picks the login up automatically."), "hint", wrap=True))
        lay.addWidget(card)
        lay.addStretch(1)
        self.refresh_accounts()
        return _qt_scroll(page)

    def refresh_accounts(self):
        self.acc_list.clear()
        try:
            accounts = load_profiles()
        except Exception:
            accounts = []
        self._accounts = accounts
        for acc in accounts:
            self.acc_list.addItem(f"{acc.get('username', 'Unknown')}  ({acc.get('type', '?').title()})")
        if not accounts:
            self.acc_list.addItem(_tr(self.launcher, "NO_ACCOUNTS", "No accounts yet"))
            self.skin.set_skin(None, False, None)
            self.skin_caption.setText("")
        else:
            self.acc_list.setCurrentRow(0)

    def _on_account_row(self, row):
        if row < 0 or row >= len(getattr(self, "_accounts", [])):
            return
        acc = self._accounts[row]
        self.skin_caption.setText(acc.get("username", ""))

        self.cape_combo.hide()

        def work():
            skin, slim, cape, capes = _fetch_skin_texture(acc)
            for c in capes:
                try:
                    c["img"] = _qimage_rgba(_cached_image_get(c["url"]))
                except Exception:
                    pass
            return skin, slim, cape, capes

        def done(result):
            skin, slim, cape, capes = result
            self.skin.set_skin(skin, slim, cape)
            self._fill_capes(capes)
        _qt_run_bg(work, done, lambda e: self.skin.set_skin(None, False, None))

    def _fill_capes(self, capes):
        cb = self.cape_combo
        cb.clear()
        cb.addItem(_qt_t("QT_CAPE_NONE", "No cape"), None)
        for c in capes:
            img = c.get("img")
            if img is not None:
                px = QtGui.QPixmap.fromImage(img.copy(1, 1, 10, 16).scaled(20, 32, Qt.IgnoreAspectRatio, Qt.FastTransformation))
                cb.addItem(QtGui.QIcon(px), c.get("alias") or c.get("id", "?"), c.get("id"))
            else:
                cb.addItem(c.get("alias") or c.get("id", "?"), c.get("id"))
            if c.get("state") == "ACTIVE":
                cb.setCurrentIndex(cb.count() - 1)
        cb.setVisible(bool(capes))

    def _on_cape_pick(self, index):
        row = self.acc_list.currentRow()
        if row < 0 or row >= len(self._accounts):
            return
        acc = self._accounts[row]
        cape_id = self.cape_combo.itemData(index)
        self.cape_combo.setEnabled(False)

        def done(_):
            self.cape_combo.setEnabled(True)
            self._on_account_row(row)

        def fail(e):
            self.cape_combo.setEnabled(True)
            messagebox.showerror(_qt_t("QT_CAPE_TITLE", "Cape"), _qt_t("QT_CAPE_FAIL", "Could not change cape: {error}").format(error=e))
            self._on_account_row(row)
        _qt_run_bg(lambda: _set_active_cape(acc, cape_id), done, fail)

    def _remove_account(self):
        row = self.acc_list.currentRow()
        if row < 0 or row >= len(self._accounts):
            return
        username = self._accounts[row].get("username", "?")
        if not messagebox.askyesno(_tr(self.launcher, "ACCOUNT_REMOVE_TITLE", "Remove account"), _tr(self.launcher, "ACCOUNT_REMOVE_CONFIRM", "Remove {username}?").format(username=username)):
            return
        accounts = load_profiles()
        if row < len(accounts):
            del accounts[row]
            save_profiles(accounts)
        self.refresh_accounts()
        self.launcher.refresh_accounts()

        # intresting section of the settings page
    def _build_advanced(self):
        
        page, lay = self._page()
        L = self.launcher
        card = _Card(_tr(L, "SETTINGS_CARD_DISCORD", "Discord"))
        card.add(_ToggleRow(_tr(L, "SETTINGS_DISCORD_ENABLE", "Discord Rich Presence"), _tr(L, "SETTINGS_DISCORD_DESC", "Show what you play on Discord."), L.discord_rpc_enabled, lambda: _save_and_apply(L, lambda: _toggle_discord_rpc(L))))
        lay.addWidget(card)
        card = _Card(_tr(L, "SETTINGS_CARD_TELEMETRY", "Privacy"))
        card.add(_ToggleRow(_tr(L, "SETTINGS_TELEMETRY_DELETE", "Delete telemetry on startup"), _tr(L, "SETTINGS_TELEMETRY_DESC", "Removes Mojang telemetry logs from every instance when the launcher starts."), L.delete_telemetry_on_startup, lambda: _save_settings(L)))
        lay.addWidget(card)
        card = _Card(_tr(L, "SETTINGS_PROGRESS_BAR", "Interface"))
        card.add(_ToggleRow(_tr(L, "SETTINGS_SHOW_PROGRESS_BAR", "Show the progress bar"), _tr(L, "SETTINGS_SHOW_PROGRESS_BAR_DESC", "Shows download/installation progress under the Play button."), L.show_progress_bar, lambda: (_save_settings(L), L.update_bottom_visibility())))
        card.add(_ToggleRow(_tr(L, "SHOW_STATUS_BAR", "Show the status bar"), _qt_t("QT_STATUS_BAR_DESC", "Status text with the current instance and account."), L.show_status_bar, lambda: (_save_settings(L), L.update_bottom_visibility())))
        lay.addWidget(card)
        card = _Card(_tr(L, "SETTINGS_CARD_DEBUG", "Debug"))
        card.add(_ToggleRow(_tr(L, "SETTINGS_DEBUG_ENABLE", "Debug mode"), _tr(L, "SETTINGS_DEBUG_DESC", "Writes launcher output to ~/.local/share/oranglauncher/logs."), L.debug_mode_enabled, lambda: _toggle_debug_mode(L)))
        card.add(_qt_label(_qt_t("QT_DEBUG_CRASH_HINT", "Crashes are always written to crash_*.log in that folder, even with debug mode off."), "hint", wrap=True))
        hb = QtWidgets.QHBoxLayout()
        hb.addWidget(_qt_button(_qt_t("QT_DEBUG_OPEN_LOGS", "Open log folder"), lambda: open_path_native(_launcher_log_dir()), icon="folder", launcher=L))
        hb.addWidget(_qt_button(_qt_t("QT_SYSTEM_INFO", "Show system info"), self._show_debug_info, icon="logs", launcher=L))
        hb.addStretch(1)
        card.add_layout(hb)
        lay.addWidget(card)
        card = _Card(_tr(L, "SETTINGS_CARD_PLUGINS", "Plugins"))
        card.add(_qt_label(_tr(L, "SETTINGS_PLUGINS_DESC", "Python plugins from ~/.local/share/oranglauncher/plugins."), "muted", wrap=True))
        self.plugin_list = QtWidgets.QListWidget()
        self.plugin_list.setMaximumHeight(120)
        card.add(self.plugin_list)
        self._refresh_plugins()
        hb = QtWidgets.QHBoxLayout()
        hb.addWidget(_qt_button(_tr(L, "SETTINGS_PLUGIN_ADD", "Add plugin"), lambda: _add_plugin_file(L), icon="plus", launcher=L))
        hb.addWidget(_qt_button(_tr(L, "SETTINGS_PLUGIN_REFRESH", "Reload plugins"), lambda: (_refresh_plugins_runtime_qt(L), self._refresh_plugins()), icon="refresh", launcher=L))
        hb.addWidget(_qt_button(_qt_t("QT_PLUGINS_FOLDER", "Open plugins folder"), lambda: open_path_native(Path.home() / ".local" / "share" / "oranglauncher" / "plugins"), icon="folder", launcher=L))
        hb.addStretch(1)
        card.add_layout(hb)
        lay.addWidget(card)
        card = _Card(_qt_t("QT_JAVA_RUNTIMES", "Java runtimes"))
        card.add(_qt_label(_qt_t("QT_JAVA_RUNTIMES_DESC", "Install or update Java runtimes. The launcher checks pacman / apt first, then downloads directly from Adoptium if needed."), "muted", wrap=True))
        not_found = _qt_t("QT_NOT_FOUND", "not found")
        
        for major in (8, 17, 21, 25):
            row = QtWidgets.QHBoxLayout()
            path = find_java_executable(major)
            lbl = _qt_label(f"Java {major}  ✓  {path}" if path else f"Java {major}  -  {not_found}", "accent" if path else "muted")
            row.addWidget(lbl, 1)

            def make_install(m, label):
                def on_install():
                    label.setText(f"Java {m}  …  {_qt_t('QT_WORKING', 'working')}")

                    def set_status(msg):
                        _qt_later(lambda: label.setText(f"Java {m}  -  {msg}"))

                    def on_done(ok, msg):
                        p = find_java_executable(m)
                        _qt_later(lambda: label.setText(f"Java {m}  {'✓' if ok else '✗'}  {p or msg}"))
                    _install_java_pm_or_download(m, set_status, on_done)
                return on_install
            row.addWidget(_qt_button(_qt_t("QT_INSTALL_UPDATE", "Install / Update"), make_install(major, lbl)))
            card.add_layout(row)
        lay.addWidget(card)
        card = _Card(_qt_t("QT_LAUNCHER_NETWORK", "Launcher network"))
        proxy = QtWidgets.QLineEdit(str(_adv_get("proxy_url", "") or ""))
        proxy.setPlaceholderText("http://host:port   socks5h://host:port   http://user:pass@host:port")
        card.add(_qt_form_row(_qt_t("QT_PROXY_LABEL", "Proxy for the launcher only"), proxy, _qt_t("QT_PROXY_HINT", "Used for Modrinth, Mojang, Java and news downloads. Minecraft itself is launched without it. SOCKS needs the PySocks package.")))
        self.proxy_status = _qt_label("", "hint", wrap=True)
        card.add(self.proxy_status)
        # I never tested this.
        def apply_proxy():
            value = proxy.text().strip()
            _adv_set("proxy_url", value)
            ok, msg = apply_launcher_proxy()
            self.proxy_status.setText(msg)
            if not ok:
                messagebox.showerror(_qt_t("QT_PROXY", "Proxy"), msg)
            else:
                messagebox.showinfo(_qt_t("QT_PROXY", "Proxy"), _qt_t("QT_PROXY_APPLIED", "Proxy applied to launcher downloads.") if value else _qt_t("QT_PROXY_CLEARED", "Proxy cleared."))

        def test_proxy():
            value = proxy.text().strip()
            self.proxy_status.setText(_qt_t("QT_PROXY_TESTING", "Testing proxy..."))

            def work():
                return test_launcher_proxy(value)

            def done(result):
                ok, msg = result
                self.proxy_status.setText(msg)
                (messagebox.showinfo if ok else messagebox.showerror)(_qt_t("QT_PROXY", "Proxy"), msg)
            _qt_run_bg(work, done, lambda e: self.proxy_status.setText(str(e)))
        hb = QtWidgets.QHBoxLayout()
        hb.addWidget(_qt_button(_qt_t("QT_PROXY_APPLY", "Apply proxy"), apply_proxy, kind="accent"))
        hb.addWidget(_qt_button(_qt_t("QT_PROXY_TEST", "Test proxy"), test_proxy))
        hb.addStretch(1)
        card.add_layout(hb)
        lay.addWidget(card)
        card = _Card(_qt_t("QT_OPEN_WITH", "Open with"))
        browser = QtWidgets.QLineEdit(str(_adv_get("custom_browser", "") or ""))
        browser.editingFinished.connect(lambda: _adv_set("custom_browser", browser.text().strip()))
        card.add(_qt_form_row(_qt_t("QT_BROWSER_OVERRIDE", "Browser command override (empty = system default)"), browser, _qt_t("QT_BROWSER_OVERRIDE_HINT", "Example: firefox   or   flatpak run org.chromium.Chromium")))
        editor = QtWidgets.QLineEdit(str(_adv_get("custom_editor", "") or ""))
        editor.editingFinished.connect(lambda: _adv_set("custom_editor", editor.text().strip()))
        card.add(_qt_form_row(_qt_t("QT_EDITOR_OVERRIDE", "Text editor command override (empty = system default)"), editor, _qt_t("QT_EDITOR_OVERRIDE_HINT", "Used for opening logs and config files. Example: kate   or   code --wait")))
        
        if platform.system() == "Linux":
            picker_var = _Var(bool(_adv_get("native_file_picker", True)))
            detected = _native_picker_tool() or _qt_t("QT_PICKER_NONE", "none, the Qt dialog is used")
            card.add(_ToggleRow(_qt_t("QT_NATIVE_PICKER", "Use the desktop's file picker"), _qt_t("QT_NATIVE_PICKER_DESC", "kdialog on Plasma, zenity elsewhere. Detected: {tool}").format(tool=detected), picker_var, lambda: _adv_set("native_file_picker", bool(picker_var.get()))))
        lay.addWidget(card)
        lay.addStretch(1)
        return _qt_scroll(page)

    def _refresh_plugins(self):
        self.plugin_list.clear()
        plugins = getattr(self.launcher, "loaded_plugins", []) or []
        if not plugins:
            self.plugin_list.addItem(_tr(self.launcher, "PLUGINS_NONE", "No plugins loaded"))
        for p in plugins:
            if isinstance(p, dict):
                self.plugin_list.addItem(f"{p.get('name', '?')}  ({p.get('type', '?')})  -  {p.get('path', '')}")
            else:
                self.plugin_list.addItem(str(p))

    def _show_debug_info(self):
        L = self.launcher
        lines = ["=== SYSTEM INFO ===", f"Platform: {platform.platform()}", f"Python: {sys.version}", f"Qt: {QtCore.qVersion()} (PySide6)", f"Desktop: {os.environ.get('XDG_CURRENT_DESKTOP', '?')} / {os.environ.get('XDG_SESSION_TYPE', '?')}", f"Theme: {L.theme.name}", f"Locale: {L.current_locale}", f"Debug mode: {'ON' if L.debug_mode_enabled.get() else 'OFF'}"]
        if getattr(L, "_current_log_file", None):
            lines.append(f"Log file: {L._current_log_file}")
        lines.append(f"Instances: {len(L.instance_manager.instances)}")
        lines.append(f"Java: " + ", ".join(f"{m}={find_java_executable(m) or '-'}" for m in (8, 17, 21, 25)))
        dlg = QtWidgets.QDialog(self)
        dlg.setWindowTitle(_qt_t("QT_DEBUG_INFO", "Debug info"))
        dlg.resize(700, 420)
        v = QtWidgets.QVBoxLayout(dlg)
        txt = QtWidgets.QPlainTextEdit("\n".join(lines))
        txt.setReadOnly(True)
        v.addWidget(txt)
        hb = QtWidgets.QHBoxLayout()
        hb.addStretch(1)
        hb.addWidget(_qt_button(_qt_t("QT_COPY", "Copy"), lambda: QtWidgets.QApplication.clipboard().setText(txt.toPlainText())))
        hb.addWidget(_qt_button(_qt_t("QT_CLOSE", "Close"), dlg.accept, kind="accent"))
        v.addLayout(hb)
        dlg.exec()
    # credits basicly, this is about page of the settings page (OrangLauncher)
    def _build_about(self):
        page, lay = self._page()
        L = self.launcher
        card = _Card(_tr(L, "SETTINGS_ABOUT_TITLE", "About"))
        row = QtWidgets.QHBoxLayout()
        logo = QtWidgets.QLabel()
        logo_path = find_resource("oranglauncher/images/orange.png")
        if logo_path:
            logo.setPixmap(_qt_pixmap_from_path(str(logo_path), (72, 72)) or QtGui.QPixmap())
        logo.setFixedSize(72, 72)
        logo.setScaledContents(True)
        logo.setCursor(Qt.PointingHandCursor)
        logo.mousePressEvent = lambda e: L.toggle_music()
        row.addWidget(logo, 0, Qt.AlignTop)
        col = QtWidgets.QVBoxLayout()
        
        col.addWidget(_qt_label("OrangLauncher", "h1"))
        col.addWidget(_qt_label(_qt_t("QT_VERSION", "Version") + f": {CURRENT_VERSION}  ·  PySide6 {QtCore.qVersion()}", "muted"))
        col.addWidget(_qt_label(_tr(L, "SETTINGS_ABOUT_AUTHOR", "by Orang Studio"), "muted"))
        col.addWidget(_qt_label(_tr(L, "SETTINGS_ABOUT_DESC", "A modular, open source Minecraft launcher."), "hint", wrap=True))
        
        row.addLayout(col, 1)
        card.add_layout(row)
        btns = QtWidgets.QHBoxLayout()
        
        btns.addWidget(_qt_button(_tr(L, "SETTINGS_ABOUT_CHECK_UPDATES", _qt_t("QT_CHECK_UPDATES", "Check for updates")), L.check_updates, kind="accent", icon="update", launcher=L))
        btns.addWidget(_qt_button(_tr(L, "SETTINGS_ABOUT_GITHUB", "GitHub"), lambda: open_with_browser("https://github.com/Orang-Studio/OrangLaunch"), icon="github", launcher=L))
        btns.addWidget(_qt_button(_qt_t("QT_LICENSE", "License"), lambda: open_with_browser("https://github.com/Orang-Studio/OrangLaunch?tab=GPL-3.0-1-ov-file"), kind="flat"))
        btns.addStretch(1)
        card.add_layout(btns)
        lay.addWidget(card)
        card = _Card(_qt_t("QT_SOURCE_PACKAGE", "Build-from-source package"))
        card.add(_qt_label(_qt_t("QT_SOURCE_PACKAGE_DESC", "There's an oranglauncher AUR package that builds the launcher with Nuitka on your machine instead of shipping a prebuilt binary. Prefer compiling yourself? Install that one. Want the quick prebuilt? Keep oranglauncher-bin."), "muted", wrap=True))
        hb = QtWidgets.QHBoxLayout()
        hb.addWidget(_qt_button(_qt_t("QT_VIEW_ON_AUR", "View on AUR"), lambda: open_with_browser("https://aur.archlinux.org/packages/oranglauncher"), kind="accent"))
        hb.addStretch(1)
        card.add_layout(hb)
        lay.addWidget(card)
        lay.addStretch(1)
        return _qt_scroll(page)


def _refresh_plugins_runtime_qt(launcher):
    try:
        launcher._initialize_plugins()
        messagebox.showinfo(_tr(launcher, "SETTINGS_CARD_PLUGINS", "Plugins"), _qt_t("QT_PLUGINS_RELOADED", "Plugins have been reloaded."))
    except Exception as e:
        messagebox.showerror(_tr(launcher, "SETTINGS_CARD_PLUGINS", "Plugins"), _qt_t("QT_PLUGINS_RELOAD_FAIL", "Error refreshing plugins: {error}").format(error=e))

# still hate this annoyance, but it is for new people, so it is needed. I guess.
class QtWelcomeWizard(QtWidgets.QDialog):
    def __init__(self, launcher):
        super().__init__(launcher)
        self.launcher = launcher
        self.setWindowTitle(_qt_t("QT_WELCOME_TITLE", "Welcome to OrangLauncher"))
        self.setModal(True)
        self.resize(720, 560)
        self.page = 0
        self.profile_created = False
        self.java_choice = "Auto"
        self.rec_vars = {}
        lay = QtWidgets.QVBoxLayout(self)
        lay.setContentsMargins(0, 0, 0, 0)
        lay.setSpacing(0)
        head = QtWidgets.QFrame()
        head.setObjectName("sideNav")
        hl = QtWidgets.QHBoxLayout(head)
        hl.setContentsMargins(22, 16, 22, 16)
        logo = QtWidgets.QLabel()
        lp = find_resource("oranglauncher/images/orange.png")
        if lp:
            logo.setPixmap(_qt_pixmap_from_path(str(lp), (40, 40)) or QtGui.QPixmap())
        hl.addWidget(logo)
        col = QtWidgets.QVBoxLayout()
        col.addWidget(_qt_label("OrangLauncher", "h2"))
        self.subtitle = _qt_label("", "muted")
        col.addWidget(self.subtitle)
        hl.addLayout(col, 1)
        self.steps_label = _qt_label("", "muted")
        hl.addWidget(self.steps_label)
        lay.addWidget(head)
        self.body_host = QtWidgets.QWidget()
        self.body = QtWidgets.QVBoxLayout(self.body_host)
        self.body.setContentsMargins(26, 20, 26, 12)
        self.body.setSpacing(10)
        lay.addWidget(_qt_scroll(self.body_host), 1)
        foot = QtWidgets.QHBoxLayout()
        foot.setContentsMargins(22, 10, 22, 16)
        self.back_btn = _qt_button(_qt_t("QT_BACK", "Back"), self._go_back)
        foot.addWidget(self.back_btn)
        foot.addStretch(1)
        self.next_btn = _qt_button(_qt_t("QT_NEXT", "Next"), self._go_next, kind="accent")
        foot.addWidget(self.next_btn)
        lay.addLayout(foot)
        self.pages = [self._page_greet, self._page_account, self._page_java, self._page_profile, self._page_settings]
        self._render()

    def _t(self, key, default):
        return _tr(self.launcher, key, default)

    def _clear(self):
        while self.body.count():
            item = self.body.takeAt(0)
            w = item.widget()
            if w is not None:
                w.deleteLater()

    def _render(self):
        self._clear()
        self.steps_label.setText(_qt_t("QT_WIZARD_STEP", "Step {step} / {total}").format(step=self.page + 1, total=len(self.pages)))
        self.back_btn.setVisible(self.page > 0)
        self.next_btn.setText(_qt_t("QT_FINISH", "Finish") if self.page == len(self.pages) - 1 else _qt_t("QT_NEXT", "Next"))
        self.pages[self.page]()
        self.body.addStretch(1)

    def _go_back(self):
        if self.page > 0:
            self.page -= 1
            self._render()

    def _go_next(self):
        if self.page == 3 and not self.profile_created:
            if not self._create_profile():
                return
        if self.page == len(self.pages) - 1:
            self._finish()
            return
        self.page += 1
        self._render()

    def _finish(self):
        self._apply_recommended_settings()
        mark_setup_done(True)
        self.launcher.refresh_accounts()
        self.launcher.refresh_instance_display()
        self.launcher.instances_page.refresh(force=True)
        self.accept()

    def _heading(self, text, sub=None):
        self.body.addWidget(_qt_label(text, "h1"))
        if sub:
            self.body.addWidget(_qt_label(sub, "muted", wrap=True))

    def _page_greet(self):
        self.subtitle.setText(self._t("WIZARD_STEP_GREET", "Welcome"))
        self._heading(self._t("WIZARD_GREET_TAGLINE", "Let's get you playing in a minute."))
        for i, (t_key, d_key, t_def, d_def) in enumerate([("WIZARD_GREET_STEP_ACCOUNT", "WIZARD_GREET_STEP_ACCOUNT_DESC", _qt_t("QT_ACCOUNT", "Account"), _qt_t("QT_WIZ_ACCOUNT_DESC", "Sign in with Microsoft or use an offline name")), ("WIZARD_GREET_STEP_JAVA", "WIZARD_GREET_STEP_JAVA_DESC", "Java", _qt_t("QT_WIZ_JAVA_DESC", "Pick a Java runtime (auto works fine)")), ("WIZARD_GREET_STEP_PROFILE", "WIZARD_GREET_STEP_PROFILE_DESC", _qt_t("QT_INSTANCE", "Instance"), _qt_t("QT_WIZ_PROFILE_DESC", "Create your first instance or grab a modpack")), ("WIZARD_GREET_STEP_SETTINGS", "WIZARD_GREET_STEP_SETTINGS_DESC", _tr(self.launcher, "SETTINGS", "Settings"), _qt_t("QT_WIZ_SETTINGS_DESC", "A few recommended toggles"))]):
            row = QtWidgets.QHBoxLayout()
            num = QtWidgets.QLabel(str(i + 1))
            num.setFixedSize(28, 28)
            num.setAlignment(Qt.AlignCenter)
            num.setStyleSheet(f"background: {self.launcher.theme.c('accent_primary')}; color: {self.launcher.theme.c('selection_fg', '#fff')}; border-radius: 14px; font-weight: 700;")
            row.addWidget(num)
            col = QtWidgets.QVBoxLayout()
            col.addWidget(_qt_label(self._t(t_key, t_def), "h3"))
            col.addWidget(_qt_label(self._t(d_key, d_def), "muted"))
            row.addLayout(col, 1)
            box = QtWidgets.QWidget()
            box.setLayout(row)
            self.body.addWidget(box)
        skip = _qt_button(self._t("WIZARD_SKIP_ALL", "Skip setup"), lambda: (mark_setup_done(True), self.reject()), kind="flat")
        self.body.addWidget(skip, 0, Qt.AlignLeft)

    def _page_account(self):
        self.subtitle.setText(self._t("WIZARD_STEP_ACCOUNT", _qt_t("QT_ACCOUNT", "Account")))
        self._heading(self._t("WIZARD_ACCOUNT_TITLE", "Sign in"), self._t("WIZARD_ACCOUNT_DESC", "A Microsoft account is needed for online servers. Offline names work for singleplayer."))
        self.acc_status = _qt_label("", "muted")
        self.body.addWidget(self.acc_status)
        self._update_account_status()
        row = QtWidgets.QHBoxLayout()
        row.addWidget(_qt_button(self._t("WIZARD_ACCOUNT_LOGIN_MS", "Sign in with Microsoft"), lambda: self._add("microsoft"), kind="accent", icon="microsoft", launcher=self.launcher))
        row.addWidget(_qt_button(_qt_t("QT_OFFLINE_ACCOUNT", "Offline account"), lambda: self._add("offline"), icon="offline", launcher=self.launcher))
        row.addWidget(_qt_button(self._t("WIZARD_ACCOUNT_SKIP", "Skip"), self._go_next_plain, kind="flat"))
        row.addStretch(1)
        box = QtWidgets.QWidget()
        box.setLayout(row)
        self.body.addWidget(box)

    def _go_next_plain(self):
        self.page += 1
        self._render()

    def _add(self, kind):
        self.launcher.add_account(kind)
        self._update_account_status()

    def _update_account_status(self):
        try:
            accounts = load_profiles()
        except Exception:
            accounts = []
        if accounts:
            self.acc_status.setText(self._t("WIZARD_ACCOUNT_SIGNED_IN", "Signed in: {names}").format(names=", ".join(a.get("username", "?") for a in accounts)))
            self.acc_status.setObjectName("accent")
        else:
            self.acc_status.setText(self._t("WIZARD_ACCOUNT_NO_ACCOUNTS", "No accounts yet."))
        self.acc_status.style().unpolish(self.acc_status)
        self.acc_status.style().polish(self.acc_status)

    def _page_java(self):
        self.subtitle.setText(self._t("WIZARD_STEP_JAVA", "Java"))
        self._heading(self._t("WIZARD_JAVA_TITLE", _qt_t("QT_JAVA_RUNTIME", "Java runtime")), self._t("WIZARD_JAVA_DESC", "Auto picks the right Java for each Minecraft version and downloads it when missing."))
        installed = _wizard_detect_javas()
        self.java_group = QtWidgets.QButtonGroup(self)
        first = True
        for major, path in installed:
            rb = QtWidgets.QRadioButton(f"Java {major}  -  {path}" + (f"  ({self._t('WIZARD_JAVA_RECOMMENDED', 'recommended')})" if first else ""))
            rb.setProperty("java", path)
            self.java_group.addButton(rb)
            self.body.addWidget(rb)
            first = False
        rb = QtWidgets.QRadioButton(self._t("WIZARD_JAVA_AUTO", "Auto (recommended)"))
        rb.setProperty("java", "Auto")
        rb.setChecked(True)
        self.java_group.addButton(rb)
        self.body.addWidget(rb)

    def _page_profile(self):
        self.subtitle.setText(self._t("WIZARD_STEP_PROFILE", _qt_t("QT_INSTANCE", "Instance")))
        self._heading(self._t("WIZARD_PROFILE_TITLE", "First instance"), self._t("WIZARD_PROFILE_DESC", "Pick a Minecraft version and, optionally, a mod loader."))
        form = QtWidgets.QFormLayout()
        self.p_name = QtWidgets.QLineEdit()
        base = "My Profile"
        name = base
        i = 1
        while self.launcher.instance_manager.get_instance_by_name(name):
            i += 1
            name = f"{base} {i}"
        self.p_name.setText(name)
        form.addRow(_qt_t("QT_NAME", "Name"), self.p_name)
        self.p_loader = QtWidgets.QComboBox()
        self.p_loader.addItems(["vanilla", "forge", "neoforge", "fabric", "quilt"])
        form.addRow(_qt_t("QT_LOADER", "Loader"), self.p_loader)
        self.p_version = QtWidgets.QComboBox()
        self.p_version.setEditable(True)
        self.p_version.setMaxVisibleItems(14)
        self.p_version.addItems(self.launcher.version_values())
        form.addRow(_qt_t("QT_VERSION", "Version"), self.p_version)
        self.p_loader_version = QtWidgets.QComboBox()
        self.p_loader_version.setEnabled(False)
        form.addRow(_qt_t("QT_LOADER_VERSION", "Loader version"), self.p_loader_version)
        self.p_ram = QtWidgets.QSpinBox()
        self.p_ram.setRange(1, max(2, (_get_system_ram_mb() - 1024) // 1024))
        self.p_ram.setValue(min(4, self.p_ram.maximum()))
        self.p_ram.setSuffix(" GB")
        form.addRow(_qt_t("QT_RAM", "RAM"), self.p_ram)
        box = QtWidgets.QWidget()
        box.setLayout(form)
        self.body.addWidget(box)
        self.p_loader.currentTextChanged.connect(lambda *_: self._refresh_loader_versions())
        self.p_version.currentTextChanged.connect(lambda *_: self._refresh_loader_versions())
        self.body.addWidget(_qt_label(_qt_t("QT_NOT_SURE", "Not sure yet? You can skip this step, or grab a ready-made modpack instead:"), "muted"))
        row = QtWidgets.QHBoxLayout()
        row.addWidget(_qt_button(_qt_t("QT_SKIP_PROFILE", "Skip, no instance for now"), self._skip_profile))
        row.addWidget(_qt_button(_qt_t("QT_GET_MODPACK", "Get a modpack from Modrinth"), self._goto_content, kind="accent"))
        row.addWidget(_qt_button(_qt_t("QT_IMPORT_PACK_FILE", "Import pack file..."), self._import_pack_file, icon="mrpack", launcher=self.launcher))
        row.addStretch(1)
        b2 = QtWidgets.QWidget()
        b2.setLayout(row)
        self.body.addWidget(b2)

    def _refresh_loader_versions(self):
        loader = self.p_loader.currentText().lower()
        self.p_loader_version.clear()
        if loader == "vanilla":
            self.p_loader_version.setEnabled(False)
            return
        self.p_loader_version.setEnabled(True)
        self.p_loader_version.addItem(_qt_t("QT_LOADING", "Loading…"))
        mc = self.p_version.currentText().strip()

        def done(versions):
            try:
                self.p_loader_version.clear()
                self.p_loader_version.addItems(versions or ["Latest"])
            except RuntimeError:
                pass
        _qt_run_bg(lambda: _wizard_loader_versions(loader, mc), done, lambda e: None)

    def _skip_profile(self):
        self.profile_created = True
        self.page += 1
        self._render()

    def _goto_content(self):
        self._apply_recommended_settings()
        mark_setup_done(True)
        self.launcher.refresh_accounts()
        self.accept()
        self.launcher.show_content("modpack")

    def _import_pack_file(self):
        path = _pick_open_file(title=_tr(self.launcher, "MODS_IMPORT_TITLE", "Import modpack"), filetypes=[(_qt_t("QT_FT_MODPACKS", "Modpacks"), "*.mrpack *.orangpack *.zip"), (_qt_t("QT_FT_ALL", "All files"), "*.*")])
        if not path:
            return
        self.profile_created = True
        self._finish()
        _qt_later(lambda: self.launcher._open_file_from_cli(path), 300)

    def _create_profile(self):
        name = self.p_name.text().strip()
        version = self.p_version.currentText().strip()
        loader = self.p_loader.currentText().lower()
        if not name or not version:
            messagebox.showerror(_tr(self.launcher, "GAME_PROFILES_INVALID_TITLE", "Invalid"), _tr(self.launcher, "GAME_PROFILES_INVALID_MSG", "Name and version are required."), parent=self)
            return False
        if self.launcher.instance_manager.get_instance_by_name(name):
            messagebox.showerror(_tr(self.launcher, "GAME_PROFILES_INVALID_TITLE", "Invalid"), _qt_t("QT_NAME_TAKEN", "An instance called '{name}' already exists.").format(name=name), parent=self)
            return False
        lv = self.p_loader_version.currentText().strip()
        if loader == "vanilla" or lv in ("", "N/A", "Latest", "Loading…", _qt_t("QT_LOADING", "Loading…")):
            lv = ""
        try:
            inst = self.launcher.instance_manager.create_instance(name=name, version=version, mod_loader=loader, ram=f"{self.p_ram.value()}G", loader_version=lv or None)
            if inst is None:
                raise RuntimeError("instance creation returned None")
            checked = self.java_group.checkedButton() if hasattr(self, "java_group") else None
            java_val = checked.property("java") if checked else "Auto"
            if java_val and java_val != "Auto":
                inst.java_path = java_val
                self.launcher.instance_manager.save_instances()
            self.launcher.instance_manager.set_selected_instance(inst.instance_id)
        except Exception as e:
            messagebox.showerror(_tr(self.launcher, "ERROR", "Error"), _qt_t("QT_CREATE_INSTANCE_FAIL", "Could not create the instance:\n{error}").format(error=e), parent=self)
            return False
        self.profile_created = True
        return True

    def _page_settings(self):
        self.subtitle.setText(self._t("WIZARD_STEP_SETTINGS", "Settings"))
        self._heading(self._t("WIZARD_SETTINGS_TITLE", "Recommended settings"), self._t("WIZARD_SETTINGS_DESC", "You can change all of these later in Settings."))
        for key, tkey, default_text, default in (("show_progress_bar", "WIZARD_SETTINGS_SHOW_PROGRESS", "Show the progress bar", True), ("discord_rpc_enabled", "WIZARD_SETTINGS_DISCORD", "Discord Rich Presence", True), ("delete_telemetry_on_startup", "WIZARD_SETTINGS_TELEMETRY", "Delete telemetry on startup", True), ("show_status_bar", "WIZARD_SETTINGS_STATUS_BAR", "Show the status bar", True)):
            var = _Var(default)
            self.rec_vars[key] = var
            self.body.addWidget(_ToggleRow(self._t(tkey, default_text), "", var))
        row = QtWidgets.QHBoxLayout()
        row.addWidget(_qt_label(self._t("WIZARD_SETTINGS_THEME", "Theme")))
        self.theme_combo = QtWidgets.QComboBox()
        self.theme_combo.addItems(self.launcher.theme.available())
        self.theme_combo.setCurrentText(self.launcher.theme.name or QT_THEME_SYSTEM)
        self.theme_combo.activated.connect(lambda *_: self.launcher.apply_theme(self.theme_combo.currentText()))
        row.addWidget(self.theme_combo)
        row.addStretch(1)
        box = QtWidgets.QWidget()
        box.setLayout(row)
        self.body.addWidget(box)
        self.body.addWidget(_qt_label(self._t("WIZARD_SETTINGS_FINISH_HINT", "Press Finish to start using the launcher."), "hint"))

    def _apply_recommended_settings(self):
        try:
            for key, var in self.rec_vars.items():
                target = getattr(self.launcher, key, None)
                if target is not None:
                    target.set(var.get())
            _save_settings(self.launcher)
            _toggle_discord_rpc(self.launcher)
            self.launcher.update_bottom_visibility()
        except Exception as e:
            print(f"[setup] applying recommended settings failed: {e}")

    # My version of CrashAssistant mod
class QtCrashDialog(QtWidgets.QDialog):
    def __init__(self, launcher, exit_code, report_path, summary):
        super().__init__(launcher)
        self.setWindowTitle(_qt_t("QT_CRASHED", "Minecraft Crashed"))
        self.resize(720, 460)
        lay = QtWidgets.QVBoxLayout(self)
        title = _qt_label(_qt_t("QT_EXITED_WITH", "Minecraft exited with code {code}").format(code=exit_code), "h2")
        title.setStyleSheet("color: #f25c5c;")
        lay.addWidget(title)
        lay.addWidget(_qt_label(_qt_t("QT_LATEST_CRASH_REPORT", "Latest crash report: {name}").format(name=report_path.name), "muted"))
        txt = QtWidgets.QPlainTextEdit(summary)
        txt.setReadOnly(True)
        txt.setFont(QtGui.QFontDatabase.systemFont(QtGui.QFontDatabase.FixedFont))
        lay.addWidget(txt, 1)
        row = QtWidgets.QHBoxLayout()
        row.addWidget(_qt_button(_qt_t("QT_OPEN_REPORT", "Open full report"), lambda: open_with_editor(report_path)))
        row.addWidget(_qt_button(_qt_t("QT_OPEN_CRASH_FOLDER", "Open crash-reports folder"), lambda: open_path_native(report_path.parent)))
        row.addWidget(_qt_button(_qt_t("QT_SHOW_LOG", "Show launcher log"), lambda: (launcher.show_page("logs"), self.accept())))
        row.addStretch(1)
        row.addWidget(_qt_button(_qt_t("QT_CLOSE", "Close"), self.accept, kind="accent"))
        lay.addLayout(row)


class LauncherCore:
    def _load_locales(self):
        self.locales = {}
        self.locale_names = {}
        locale_dir_path = find_resource("oranglauncher/locales")
        if not locale_dir_path:
            print("WARNING: locales directory not found")
            return
        locale_dir = str(locale_dir_path)
        for path in glob.glob(os.path.join(locale_dir, '*.locale')):
            code = os.path.splitext(os.path.basename(path))[0]
            with open(path, encoding="utf-8") as f:
                lines = f.readlines()
            d = {}
            for line in lines:
                if '=' in line:
                    k, v = line.strip().split('=', 1)
                    v = v.replace("\\n", "\n")
                    d[k] = v
            self.locales[code] = d
            self.locale_names[code] = {
                'en-US': 'English',
                'lt-LT': 'Lietuvių',
                'ru-RU': 'Русский',
                'pl-PL': 'Polski',
                'de-DE': 'Deutsch',
            }.get(code, code)
        self.current_locale = 'en-US'
        self.translations = self.locales.get(self.current_locale, {})
    def _t(self, key, **kwargs):
        text = self.translations.get(key, key)
        if kwargs:
            try:
                return text.format(**kwargs)
            except Exception:
                return text
        return text
    def _run_launcher_thread(self, current_instance, launch_name, version, mod_loader, ram, selected_profile, username, uuid, quick_play_server=None):
        try:
            if selected_profile.get("type") == "microsoft":
                try:
                    updated = ensure_mc_profile_valid(selected_profile)
                    if updated:
                        selected_profile = updated
                except Exception as e:
                    err_msg = str(e)
                    try:
                        self.after(0, lambda m=err_msg: (
                            messagebox.showerror(self._t("MS_AUTH_ERROR"), self._t("MS_AUTH_FAIL") + f"\n{m}"),
                            self._restore_ui()
                        ))
                    except RuntimeError:
                        pass
                    return
            access_token = selected_profile.get("minecraft_token", "0")
            if current_instance:
                minecraft_directory = str(current_instance.minecraft_dir)
            else:
                minecraft_directory = str(Path.home() / ".minecraft")
            Path(minecraft_directory).mkdir(parents=True, exist_ok=True)
            java_exe = resolve_java_for_instance(current_instance, version, log_fn=self._safe_append_log)
            self._safe_append_log(f"[Java] Using: {java_exe}")
            jvm_args = memory_jvm_args(current_instance, ram)
            jvm_args += extra_jvm_args(current_instance)
            jvm_args += native_library_jvm_args(current_instance)
            options = {
                'username': username,
                'uuid': uuid,
                'token': access_token,
                'executablePath': java_exe,
                'jvmArguments': jvm_args,
                'launcherName': 'OrangLauncher',
                'launcherVersion': CURRENT_VERSION
            }
            if current_instance is not None:
                res_w = current_instance.opt("res_width")
                res_h = current_instance.opt("res_height")
                if res_w and res_h:
                    options['customResolution'] = True
                    options['resolutionWidth'] = str(int(res_w))
                    options['resolutionHeight'] = str(int(res_h))
                if current_instance.opt("demo", False):
                    options['demo'] = True
                    self._safe_append_log("[Launcher] Demo mode: passing --demo to the game")
            if quick_play_server:
                options['quickPlayMultiplayer'] = quick_play_server
                self._safe_append_log(f"[Launcher] Quick Play -> joining {quick_play_server}")
            self._safe_append_log(f"[Launcher] Installing Minecraft {version}...")
            needs_loader_install = False
            loader_installed = False
            if mod_loader and mod_loader.lower() != "vanilla" and mod_loader.lower() != "none":
                if current_instance.installed_version_id and current_instance.installed_version_id not in ['Latest', 'N/A', '']:
                    local_versions_dir = Path(minecraft_directory) / "versions" / current_instance.installed_version_id
                    version_exists = local_versions_dir.exists() and (local_versions_dir / f"{current_instance.installed_version_id}.json").exists()
                    if not version_exists:
                        recovered = find_installed_loader_version(minecraft_directory, mod_loader, version,
                                                                  getattr(current_instance, 'loader_version', '') or '')
                        if recovered:
                            self._safe_append_log(f"[Launcher] Recovered installed version id: {recovered}")
                            current_instance.installed_version_id = recovered
                            self.instance_manager.save_instances()
                            local_versions_dir = Path(minecraft_directory) / "versions" / recovered
                            version_exists = True
                    if version_exists:
                        version = current_instance.installed_version_id
                        self._safe_append_log(f"[Launcher] Using installed version: {version}")
                    else:
                        needs_loader_install = True
                        self._safe_append_log(f"[Launcher] Version {current_instance.installed_version_id} not found locally, will install...")
                else:
                    recovered = find_installed_loader_version(minecraft_directory, mod_loader, version,
                                                              getattr(current_instance, 'loader_version', '') or '')
                    if recovered and getattr(current_instance, 'loader_version', ''):
                        self._safe_append_log(f"[Launcher] Using already installed version: {recovered}")
                        current_instance.installed_version_id = recovered
                        self.instance_manager.save_instances()
                        version = recovered
                    else:
                        needs_loader_install = True
                if needs_loader_install:
                    if mod_loader.lower() == "forge":
                        stored_lv = getattr(current_instance, 'loader_version', '') or ''
                        forge_version = f"{version}-{stored_lv}" if stored_lv else minecraft_launcher_lib.forge.find_forge_version(version)
                        if forge_version:
                            self._safe_append_log(f"[Launcher] Installing Forge {forge_version}...")
                            _forg_max = [1]
                            def _forg_set_max(m): _forg_max[0] = max(m, 1)
                            def _forg_progress(c): self._submit_progress_update(min(int((c/_forg_max[0])*100),100), f"Installing Forge... {min(int((c/_forg_max[0])*100),100)}%")
                            minecraft_launcher_lib.forge.install_forge_version(
                                forge_version,
                                minecraft_directory,
                                callback={"setStatus": lambda x: self._safe_append_log(f"[Forge] {x}"), "setProgress": _forg_progress, "setMax": _forg_set_max}
                            )
                            parts = forge_version.split('-', 1)
                            if len(parts) == 2:
                                mc_ver, loader_ver = parts
                                version = f"{mc_ver}-forge-{loader_ver}"
                            else:
                                version = forge_version
                            current_instance.installed_version_id = version
                            self.instance_manager.save_instances()
                            loader_installed = True
                    elif mod_loader.lower() == "fabric":
                        fabric_version = getattr(current_instance, 'loader_version', '') or minecraft_launcher_lib.fabric.get_latest_loader_version()
                        if fabric_version:
                            self._safe_append_log(f"[Launcher] Installing Fabric {fabric_version}...")
                            _fab_max = [1]
                            def _fab_set_max(m): _fab_max[0] = max(m, 1)
                            def _fab_progress(c): self._submit_progress_update(min(int((c/_fab_max[0])*100),100), f"Installing Fabric... {min(int((c/_fab_max[0])*100),100)}%")
                            minecraft_launcher_lib.fabric.install_fabric(
                                version,
                                minecraft_directory,
                                loader_version=fabric_version,
                                callback={"setStatus": lambda x: self._safe_append_log(f"[Fabric] {x}"), "setProgress": _fab_progress, "setMax": _fab_set_max}
                            )
                            version = f"fabric-loader-{fabric_version}-{version}"
                            current_instance.installed_version_id = version
                            self.instance_manager.save_instances()
                            loader_installed = True
                    elif mod_loader.lower() == "quilt":
                        self._safe_append_log(f"[Launcher] Installing Quilt...")
                        try:
                            _qlt_max = [1]
                            def _qlt_set_max(m): _qlt_max[0] = max(m, 1)
                            def _qlt_progress(c): self._submit_progress_update(min(int((c/_qlt_max[0])*100),100), f"Installing Quilt... {min(int((c/_qlt_max[0])*100),100)}%")
                            quilt_loader = getattr(current_instance, 'loader_version', '') or minecraft_launcher_lib.quilt.get_latest_loader_version()
                            minecraft_launcher_lib.quilt.install_quilt(
                                version,
                                minecraft_directory,
                                loader_version=quilt_loader,
                                callback={"setStatus": lambda x: self._safe_append_log(f"[Quilt] {x}"), "setProgress": _qlt_progress, "setMax": _qlt_set_max}
                            )
                            version = f"quilt-loader-{quilt_loader}-{version}"
                            current_instance.installed_version_id = version
                            self.instance_manager.save_instances()
                            loader_installed = True
                        except Exception as e:
                            self._safe_append_log(f"[Launcher] Quilt install failed: {e}")
                    elif mod_loader.lower() == "neoforge":
                        self._safe_append_log(f"[Launcher] Installing NeoForge...")
                        try:
                            nf = _NeoforgeCompat()
                            stored_nf = getattr(current_instance, 'loader_version', '') or ''
                            nf_versions = [stored_nf] if stored_nf else (nf.get_loader_versions(version, True) or nf.get_loader_versions(version, False))
                            if nf_versions:
                                nf_loader_ver = nf_versions[0]
                                _nf_max = [1]
                                def _nf_set_max(m): _nf_max[0] = max(m, 1)
                                def _nf_progress(c): self._submit_progress_update(min(int((c/_nf_max[0])*100),100), f"Installing NeoForge... {min(int((c/_nf_max[0])*100),100)}%")
                                java_path = java_exe
                                nf.install(
                                    version,
                                    minecraft_directory,
                                    callback={"setStatus": lambda x: self._safe_append_log(f"[NeoForge] {x}"), "setProgress": _nf_progress, "setMax": _nf_set_max},
                                    java=java_path,
                                    loader_version=nf_loader_ver
                                )
                                version = nf.get_installed_version(version, nf_loader_ver)
                                current_instance.installed_version_id = version
                                self.instance_manager.save_instances()
                                loader_installed = True
                            else:
                                self._safe_append_log(f"[Launcher] No NeoForge versions found for {version}")
                        except Exception as e:
                            self._safe_append_log(f"[Launcher] NeoForge install failed: {e}")
                    elif mod_loader.lower() == "optifine":
                        self._safe_append_log(f"[Launcher] Installing OptiFine...")
                        try:
                            base_mc = current_instance.version
                            of_file = getattr(current_instance, 'loader_version', '') or None
                            def _of_progress(p, msg): self._submit_progress_update(p, msg)
                            version = install_optifine(base_mc, minecraft_directory, java_exe, filename=of_file, log_fn=self._safe_append_log, progress_fn=_of_progress)
                            current_instance.installed_version_id = version
                            self.instance_manager.save_instances()
                            loader_installed = True
                        except Exception as e:
                            self._safe_append_log(f"[Launcher] OptiFine install failed: {e}")
            if not loader_installed:
                self._safe_append_log(f"[Launcher] Preparing {version}...")
                _install_max = [1]
                def install_set_max(maximum):
                    _install_max[0] = max(maximum, 1)
                def install_progress(current):
                    try:
                        percent = min(int((current / _install_max[0]) * 100), 100)
                        self._submit_progress_update(percent, f"Installing {version}... {percent}%")
                    except:
                        pass
                minecraft_launcher_lib.install.install_minecraft_version(
                    version,
                    minecraft_directory,
                    callback={"setProgress": install_progress, "setMax": install_set_max, "setStatus": lambda x: self._safe_append_log(f"[Install] {x}")}
                )
            self._submit_progress_update(100, "Installation complete!")
            try:
                apply_video_options(current_instance)
            except Exception as e:
                self._safe_append_log(f"[Launcher] options.txt update skipped: {e}")
            java_major = 0
            try:
                java_major = get_required_java_version(current_instance.version if current_instance else version)
            except Exception:
                pass
            options['jvmArguments'] = options['jvmArguments'] + legacy_jvm_args(current_instance, current_instance.version if current_instance else version, java_major)
            self._safe_append_log(f"[Launcher] Starting Minecraft...")
            command = minecraft_launcher_lib.command.get_minecraft_command(version, minecraft_directory, options)
            command = [arg for arg in command if arg != "--sun-misc-unsafe-memory-access=allow"]
            command = strip_server_blocklist(command, current_instance, current_instance.version if current_instance else version, log_fn=self._safe_append_log)
            try:
                def _lwjgl_progress(cur, total, name):
                    self._submit_progress_update(min(int(cur / max(total, 1) * 100), 100), f"Preparing LWJGL... {name}")
                lwjgl_override = resolve_lwjgl_override(current_instance, minecraft_directory, version,
                                                        log_fn=self._safe_append_log, progress_fn=_lwjgl_progress)
                if lwjgl_override:
                    command = LwjglManager.rewrite_command(command, lwjgl_override["jars"], lwjgl_override["natives_dir"])
                    self._safe_append_log(f"[LWJGL] Active LWJGL: {lwjgl_override['version']}")
                else:
                    stock_lwjgl, _lw_major = LwjglManager.stock_version(minecraft_directory, version)
                    if stock_lwjgl:
                        self._safe_append_log(f"[LWJGL] Using stock LWJGL {stock_lwjgl}")
                if platform.system() == "Darwin":
                    _stock, _major = LwjglManager.stock_version(minecraft_directory, version)
                    if _major == 3 and "-XstartOnFirstThread" not in command:
                        command.insert(1, "-XstartOnFirstThread")
            except Exception as e:
                self._safe_append_log(f"[LWJGL] override failed, using stock libraries: {e}")
            launch_env = build_launch_env(current_instance, self)
            if platform.system() == "Linux" and _is_wayland_session():
                self._safe_append_log(f"[Launcher] Display backend: {'X11 (XWayland)' if launch_env.get('XDG_SESSION_TYPE') == 'x11' else 'Wayland (native)'}")
                mc_tuple = _version_tuple(current_instance.version if current_instance else version)
                if launch_env.get('XDG_SESSION_TYPE') != 'x11' and mc_tuple and mc_tuple < (1, 20, 2):
                    self._safe_append_log("[Launcher] Warning: native Wayland on this version fails at glfwSetWindowIcon unless a Wayland-fix mod is installed; switch the display backend to X11 in Other settings if the window never appears")
            hook_env = dict(launch_env)
            hook_env.update(instance_command_vars(current_instance, java_exe, version))
            if current_instance is not None and current_instance.opt("pre_launch_cmd"):
                self._safe_append_log("[Hook] Running pre-launch command...")
                rc = run_hook_command(current_instance.opt("pre_launch_cmd"), minecraft_directory, hook_env, log_fn=self._safe_append_log)
                if rc != 0:
                    self._safe_append_log(f"[Hook] Pre-launch command exited with {rc}, aborting launch")
                    return
            if current_instance is not None:
                command = apply_wrapper_command(command, current_instance.opt("wrapper_cmd"))
            self.after(0, self._on_mc_started)
            _launch_start = time.time()
            self.mc_process = subprocess.Popen(
                command,
                cwd=minecraft_directory,
                stdout=subprocess.PIPE,
                stderr=subprocess.STDOUT,
                text=True,
                bufsize=1,
                env=launch_env
            )
            game_log = None
            try:
                game_log_path = _launcher_log_dir() / f"game_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
                game_log = open(game_log_path, "w", encoding="utf-8", errors="replace")
                game_log.write(f"# {version} ({mod_loader}) in {minecraft_directory}\n")
                self._safe_append_log(f"[Launcher] Game output is saved to {game_log_path}")
            except Exception:
                game_log = None
            for line in self.mc_process.stdout:
                if self.cancel_requested:
                    self.mc_process.terminate()
                    break
                self._safe_append_log(line.rstrip())
                if game_log is not None:
                    try:
                        game_log.write(line)
                    except Exception:
                        pass
            self.mc_process.wait()
            exit_code = self.mc_process.returncode
            if game_log is not None:
                try:
                    game_log.write(f"# exit code {exit_code}\n")
                    game_log.close()
                except Exception:
                    pass
            if current_instance is not None and current_instance.opt("post_exit_cmd"):
                hook_env["INST_EXIT_CODE"] = str(exit_code)
                self._safe_append_log("[Hook] Running post-exit command...")
                run_hook_command(current_instance.opt("post_exit_cmd"), minecraft_directory, hook_env, log_fn=self._safe_append_log)
            elapsed = int(time.time() - _launch_start)
            if current_instance and elapsed > 5:
                current_instance.play_time = (current_instance.play_time or 0) + elapsed
                current_instance.last_played = datetime.now().isoformat()
                try:
                    self.instance_manager.save_instances()
                except Exception:
                    pass
            self._safe_append_log(f"[Launcher] Minecraft exited with code {exit_code}")
            if exit_code != 0:
                self.after(0, lambda: self._check_crash_report(minecraft_directory, exit_code))
        except minecraft_launcher_lib.exceptions.VersionNotFound as e:
            bad_ver = str(e)
            try:
                all_versions = get_available_versions()
                suggestions = [v for v in all_versions if bad_ver[:4] in v][:5]
                hint = f"\nDid you mean one of: {', '.join(suggestions)}?" if suggestions else ""
            except Exception:
                hint = ""
            self._safe_append_log(f"[ERROR] Version '{bad_ver}' does not exist.{hint}")
            self.after(0, lambda: messagebox.showerror("Version Not Found", f"Minecraft version '{bad_ver}' does not exist.{hint}\n\nPlease check the version name and try again."
            ))
        except Exception as e:
            self._safe_append_log(f"[ERROR] Launch failed: {e}")
            self._safe_append_log(traceback.format_exc())
        finally:
            self.after(0, self._restore_ui)
    def install_loader_for_instance(self, instance, loader, loader_version=None):
        def worker():
            try:
                minecraft_directory = str(instance.minecraft_dir) if instance and instance.minecraft_dir else str(Path.home() / ".minecraft")
                Path(minecraft_directory).mkdir(parents=True, exist_ok=True)
                self._safe_append_log(f"[Installer] Installing {loader} ({loader_version}) for instance {instance.name}...")
                installed_id = None
                if loader.lower() == "forge":
                    forge_version = loader_version
                    if not forge_version:
                        forge_version = minecraft_launcher_lib.forge.find_forge_version(instance.version)
                    if forge_version:
                        _forg2_max = [1]
                        def _forg2_set_max(m): _forg2_max[0] = max(m, 1)
                        def _forg2_progress(c): self._submit_progress_update(min(int((c/_forg2_max[0])*100),100), f"Installing Forge... {min(int((c/_forg2_max[0])*100),100)}%")
                        minecraft_launcher_lib.forge.install_forge_version(
                            forge_version,
                            minecraft_directory,
                            callback={"setStatus": lambda x: self._safe_append_log(f"[Forge] {x}"), "setProgress": _forg2_progress, "setMax": _forg2_set_max}
                        )
                        parts = forge_version.split('-', 1)
                        if len(parts) == 2:
                            mc_ver, loader_ver = parts
                            installed_id = f"{mc_ver}-forge-{loader_ver}"
                        else:
                            installed_id = forge_version
                elif loader.lower() == "fabric":
                    fabric_ver = loader_version or minecraft_launcher_lib.fabric.get_latest_loader_version()
                    if fabric_ver:
                        _fab2_max = [1]
                        def _fab2_set_max(m): _fab2_max[0] = max(m, 1)
                        def _fab2_progress(c): self._submit_progress_update(min(int((c/_fab2_max[0])*100),100), f"Installing Fabric... {min(int((c/_fab2_max[0])*100),100)}%")
                        minecraft_launcher_lib.fabric.install_fabric(
                            instance.version,
                            minecraft_directory,
                            loader_version=fabric_ver,
                            callback={"setStatus": lambda x: self._safe_append_log(f"[Fabric] {x}"), "setProgress": _fab2_progress, "setMax": _fab2_set_max}
                        )
                        installed_id = f"fabric-loader-{fabric_ver}-{instance.version}"
                elif loader.lower() == "quilt":
                    _qlt2_max = [1]
                    def _qlt2_set_max(m): _qlt2_max[0] = max(m, 1)
                    def _qlt2_progress(c): self._submit_progress_update(min(int((c/_qlt2_max[0])*100),100), f"Installing Quilt... {min(int((c/_qlt2_max[0])*100),100)}%")
                    minecraft_launcher_lib.quilt.install_quilt(
                        instance.version,
                        minecraft_directory,
                        callback={"setStatus": lambda x: self._safe_append_log(f"[Quilt] {x}"), "setProgress": _qlt2_progress, "setMax": _qlt2_set_max}
                    )
                    try:
                        quilt_loader = minecraft_launcher_lib.quilt.get_latest_loader_version()
                        installed_id = f"quilt-loader-{quilt_loader}-{instance.version}"
                    except Exception:
                        installed_id = None
                elif loader.lower() == "neoforge":
                    nf = _NeoforgeCompat()
                    nf_loader_ver = loader_version
                    if not nf_loader_ver:
                        nf_versions = nf.get_loader_versions(instance.version, True) or nf.get_loader_versions(instance.version, False)
                        nf_loader_ver = nf_versions[0] if nf_versions else None
                    if nf_loader_ver:
                        _nf2_max = [1]
                        def _nf2_set_max(m): _nf2_max[0] = max(m, 1)
                        def _nf2_progress(c): self._submit_progress_update(min(int((c/_nf2_max[0])*100),100), f"Installing NeoForge... {min(int((c/_nf2_max[0])*100),100)}%")
                        java_path = resolve_java_for_instance(instance, instance.version, log_fn=self._safe_append_log)
                        nf.install(
                            instance.version,
                            minecraft_directory,
                            callback={"setStatus": lambda x: self._safe_append_log(f"[NeoForge] {x}"), "setProgress": _nf2_progress, "setMax": _nf2_set_max},
                            java=java_path,
                            loader_version=nf_loader_ver
                        )
                        installed_id = nf.get_installed_version(instance.version, nf_loader_ver)
                elif loader.lower() == "optifine":
                    java_path = resolve_java_for_instance(instance, instance.version, log_fn=self._safe_append_log)
                    def _of2_progress(p, msg): self._submit_progress_update(p, msg)
                    installed_id = install_optifine(instance.version, minecraft_directory, java_path,
                                                    filename=loader_version or None,
                                                    log_fn=self._safe_append_log, progress_fn=_of2_progress)

                if installed_id:
                    instance.installed_version_id = installed_id
                    self.instance_manager.save_instances()
                    self._safe_append_log(f"[Installer] Installed {loader}: {installed_id}")
                    self.after(0, lambda: messagebox.showinfo(self._t("SUCCESS"), f"Installed {loader.title()}: {installed_id}"))
                else:
                    self._safe_append_log(f"[Installer] Failed to determine installed id for {loader} {loader_version}")
                    self.after(0, lambda: messagebox.showerror(self._t("ERROR"), f"Failed to install {loader.title()} {loader_version or ''}"))
            except Exception as e:
                self._safe_append_log(f"[Installer] Error installing {loader}: {e}")
                self.after(0, lambda m=str(e): messagebox.showerror(self._t("ERROR"), f"Failed to install {loader.title()} {loader_version or ''}: {m}"))
            finally:
                self._submit_progress_update(100, "Installation complete!")
                if hasattr(self, '_progress_queue') and self._progress_queue is not None:
                    try:
                        while True:
                            self._progress_queue.get_nowait()
                    except Exception:
                        pass
                self.after(0, lambda: self._apply_progress_update(0, ""))
        thread = threading.Thread(target=worker, daemon=True)
        thread.start()
    def _apply_sharing_for_instance(self, instance):
        shared_dir = Path.home() / ".config" / "oranglauncher" / "shared"
        shared_dir.mkdir(parents=True, exist_ok=True)
        for attr, shared_name, rel_path, is_file in self._SHARE_TARGETS:
            shared_target = shared_dir / shared_name
            inst_path = instance.minecraft_dir / rel_path
            enabled = getattr(self, attr, None)
            enabled = enabled.get() if enabled else False
            try:
                if enabled:
                    if shared_name == "servers.dat" and shared_target.exists() and shared_target.stat().st_size == 0:
                        # empty
                        ServersNBT.write_servers_dat(shared_target, [])
                    if not shared_target.exists() and not shared_target.is_symlink():
                        if inst_path.exists() and not inst_path.is_symlink():
                            if is_file:
                                shutil.copy2(inst_path, shared_target)
                            else:
                                shutil.copytree(str(inst_path), str(shared_target))
                        else:
                            if shared_name == "servers.dat":
                                ServersNBT.write_servers_dat(shared_target, [])
                            elif is_file:
                                shared_target.touch()
                            else:
                                shared_target.mkdir(parents=True, exist_ok=True)
                    if inst_path.is_symlink():
                        try:
                            if inst_path.resolve() == shared_target.resolve():
                                continue
                        except Exception:
                            pass
                        inst_path.unlink()
                    elif inst_path.exists():
                        if is_file:
                            inst_path.unlink()
                        else:
                            # merge this instance's packs into the shared folder so nothing lost
                            try:
                                for item in inst_path.iterdir():
                                    dest = shared_target / item.name
                                    if dest.exists():
                                        continue
                                    if item.is_dir():
                                        shutil.copytree(str(item), str(dest))
                                    else:
                                        shutil.copy2(str(item), str(dest))
                            except Exception as e:
                                self._safe_append_log(f"[Sharing] merge {shared_name} / {instance.name}: {e}")
                            shutil.rmtree(str(inst_path))
                    inst_path.symlink_to(shared_target)
                else:
                    if inst_path.is_symlink():
                        inst_path.unlink()
                        if shared_target.exists():
                            if is_file:
                                shutil.copy2(shared_target, inst_path)
                            else:
                                shutil.copytree(str(shared_target), str(inst_path))
                        elif not is_file:
                            inst_path.mkdir(parents=True, exist_ok=True)
            except Exception as e:
                self._safe_append_log(f"[Sharing] {shared_name} / {instance.name}: {e}")
    def _apply_sharing_all(self):
        for instance in self.instance_manager.instances.values():
            self._apply_sharing_for_instance(instance)
    _SHARE_TARGETS = [
        ('share_options',       'options.txt',    'options.txt',         True),
        ('share_resourcepacks', 'resourcepacks',  'resourcepacks',       False),
        ('share_shaderpacks',   'shaderpacks',    'shaderpacks',         False),
        ('share_servers',       'servers.dat',    'servers.dat',         True),
        ('share_screenshots',   'screenshots',    'screenshots',         False),
    ]
    def _progress_callback(self, current, total, message=None):
        if self.cancel_requested:
            raise Exception("Operation cancelled by user.")
        try:
            if isinstance(current, dict):
                status = current
                current = status.get("task", 0)
                total = status.get("total", 100)
                message = status.get("status", message)
        except Exception:
            pass
        total = max(total or 1, 1)
        percent = max(0.0, min(100.0, (current / total) * 100.0))
        files_done = None
        files_total = None
        base_msg = message
        if isinstance(message, dict):
            base_msg = message.get("text") or "Preparing..."
            files_done = message.get("files_done")
            files_total = message.get("files_total")
        else:
            base_msg = message or "Preparing..."
        message_parts = []
        trimmed_msg = (base_msg or "").strip()
        if trimmed_msg:
            message_parts.append(trimmed_msg)
        if isinstance(files_done, int) and isinstance(files_total, int) and files_total > 0:
            files_left = max(files_total - files_done, 0)
            files_total_str = str(files_total)
            if len(files_total_str) > 8:
                files_total_str = f"{files_total_str[:5]}..."
            files_done_str = str(files_done)
            if len(files_done_str) > 8:
                files_done_str = f"{files_done_str[:5]}..."
            message_parts.append(f"{files_done_str}/{files_total_str} files ({files_left} left)")
        status_body = " - ".join(message_parts) if message_parts else "Preparing..."
        status_text = f"{status_body} ({percent:.1f}%)"
        self._submit_progress_update(percent, status_text)
    def _submit_progress_update(self, percent, status_text):
        if threading.get_ident() == getattr(self, "_main_thread_id", None):
            self._apply_progress_update(percent, status_text)
            return
        if not hasattr(self, "_progress_queue") or self._progress_queue is None:
            return
        was_empty = self._progress_queue.empty()
        try:
            self._progress_queue.put_nowait((percent, status_text))
        except queue.Full:
            try:
                self._progress_queue.get_nowait()
            except queue.Empty:
                pass
            try:
                self._progress_queue.put_nowait((percent, status_text))
            except queue.Full:
                pass
        if was_empty and not getattr(self, '_progress_polling_active', False):
            try:
                self.after(0, self._process_progress_events)
            except Exception:
                pass
    def _process_progress_events(self):
        if not hasattr(self, "_progress_queue") or self._progress_queue is None:
            self._progress_polling_active = False
            return
        self._progress_polling_active = True
        try:
            while True:
                percent, status_text = self._progress_queue.get_nowait()
                self._apply_progress_update(percent, status_text)
        except queue.Empty:
            pass
        try:
            if not self._progress_queue.empty() and self.winfo_exists():
                self.after(100, self._process_progress_events)
            else:
                self._progress_polling_active = False
        except Exception:
            self._progress_polling_active = False
    def _start_discord_rpc(self):
        try:
            if self.discord_rpc_mgr is None:
                app_id = '1411624079701573703'
                self.discord_start_time = int(time.time())
                self.discord_rpc_mgr = DiscordRPCManager(
                    app_id,
                    on_connected=lambda: self.after(0, lambda: self._update_discord_rpc("Idling in launcher"))
                )
                self.discord_rpc_mgr.start()
        except Exception:
            self.discord_rpc_mgr = None
    def _stop_discord_rpc(self):
        try:
            if self.discord_rpc_mgr:
                self.discord_rpc_mgr.stop()
                self.discord_rpc_mgr = None
        except Exception:
            pass
    def _update_discord_rpc(self, state, details=None):
        if not self.discord_rpc_enabled.get() or not getattr(self, 'discord_rpc_mgr', None):
            return
        try:
            presence_data = {
                "state": state,
                "start": self.discord_start_time,
            }
            if details:
                presence_data["details"] = details
            self.discord_rpc_mgr.update(**presence_data)
        except Exception:
            pass
    def _music_player(self):
        if self._music is None:
            try:
                from PySide6.QtMultimedia import QMediaPlayer, QAudioOutput
            except Exception as e:
                print(f"[Music] QtMultimedia unavailable: {e}")
                return None
            player = QMediaPlayer()
            output = QAudioOutput()
            player.setAudioOutput(output)
            player.mediaStatusChanged.connect(self._on_music_status)
            player.errorOccurred.connect(lambda err, msg: print(f"[Music] error: {msg}"))
            self._music = (player, output)
        return self._music[0]

    def _on_music_status(self, status):
        try:
            from PySide6.QtMultimedia import QMediaPlayer
            if status == QMediaPlayer.MediaStatus.EndOfMedia and self.music_playing:
                self._play_next_song()
        except Exception as e:
            print(f"[Music] status error: {e}")

    def _toggle_music(self):
        player = self._music_player()
        if player is None:
            messagebox.showerror(self._t("MUSIC_ERROR"), _qt_t("QT_AUDIO_UNAVAILABLE", "Audio playback is unavailable (QtMultimedia missing)."))
            return
        if self.music_playing:
            try:
                player.stop()
            except Exception as e:
                print(f"Music stop error: {e}")
            self.music_playing = False
            self.music_playlist = []
            self.current_music_index = 0
            if self.music_btn:
                try:
                    self.music_btn.config(text=self._t("PLAY_MUSIC"))
                except Exception as e:
                    print(f"Music button config error (stop): {e}")
        else:
            music_dir = find_resource("oranglauncher/music")
            if not music_dir or not music_dir.exists():
                base_dir = get_resource_path()
                music_dir = base_dir / "oranglauncher" / "music"
            if not music_dir or not music_dir.exists():
                messagebox.showwarning(self._t("MUSIC_ERROR"),"Music folder not found at oranglauncher/music/")
                return
            music_files = []
            for ext in ['*.mp3', '*.ogg', '*.wav', '*.flac']:
                music_files.extend(list(music_dir.glob(ext)))
            if not music_files:
                messagebox.showwarning(self._t("MUSIC_ERROR"),"No music files found in oranglauncher/music/\nSupported formats: .mp3, .ogg, .wav, .flac")
                return
            random.shuffle(music_files)
            self.music_playlist = music_files
            self.current_music_index = 0
            self._play_next_song()
    def _play_next_song(self):
        if not self.music_playlist or self.current_music_index >= len(self.music_playlist):
            self.current_music_index = 0
        if not self.music_playlist:
            return
        music_path = self.music_playlist[self.current_music_index]
        player = self._music_player()
        if player is None:
            return
        try:
            player.setSource(QtCore.QUrl.fromLocalFile(str(music_path)))
            player.play()
            self.music_playing = True
            if self.music_btn:
                try:
                    self.music_btn.config(text=self._t("STOP_MUSIC"))
                except Exception as e:
                    print(f"Music button config error (play): {e}")
            print(f"[Music] Now playing ({self.current_music_index + 1}/{len(self.music_playlist)}): {music_path.name}")
            self.current_music_index += 1
        except Exception as e:
            print(f"Music play error: {e}")
            messagebox.showerror(
                self._t("MUSIC_ERROR"),
                self._t("MUSIC_PLAY_FAILED") + f"\n{e}"
            )
    def _initialize_plugins(self):
        try:
            
            #
            # Time wasted here: 5 h now
            # add more if you encounter issues with loading of plugins
            #
            builtin_plugin_dir = Path(__file__).parent / "oranglauncher" / "plugin"
            launcher_root = Path.home() / ".local" / "share" / "oranglauncher"
            user_plugin_dir = launcher_root / "plugins"
            self.loaded_plugins = []
            if builtin_plugin_dir.exists():
                for plugin_path in builtin_plugin_dir.glob("*.py"):
                    if plugin_path.name.startswith("_"):
                        continue
                    try:
                        spec = importlib.util.spec_from_file_location(plugin_path.stem, plugin_path)
                        if spec and spec.loader:
                            module = importlib.util.module_from_spec(spec)
                            spec.loader.exec_module(module)
                            if hasattr(module, 'init_plugin'):
                                module.init_plugin(self)
                                plugin_info = {
                                    'name': plugin_path.stem,
                                    'type': 'builtin',
                                    'module': module,
                                    'path': str(plugin_path)
                                }
                                self.loaded_plugins.append(plugin_info)
                                print(f"[Plugins] Loaded built-in plugin: {plugin_path.stem}")
                    except Exception as e:
                        print(f"[Plugins] Error loading plugin {plugin_path.name}: {e}")
                        traceback.print_exc()
            user_plugin_dir.mkdir(parents=True, exist_ok=True)
            
            for plugin_path in user_plugin_dir.glob("*.py"):
                if plugin_path.name.startswith("_"):
                    continue
                try:
                    spec = importlib.util.spec_from_file_location(plugin_path.stem, plugin_path)
                    if spec and spec.loader:
                        module = importlib.util.module_from_spec(spec)
                        spec.loader.exec_module(module)
                        if hasattr(module, 'init_plugin'):
                            module.init_plugin(self)
                            plugin_info = {
                                'name': plugin_path.stem,
                                'type': 'user',
                                'module': module,
                                'path': str(plugin_path)
                            }
                            self.loaded_plugins.append(plugin_info)
                            print(f"[Plugins] Loaded user plugin: {plugin_path.stem}")
                except Exception as e:
                    print(f"[Plugins] Error loading plugin {plugin_path.name}: {e}")
                    traceback.print_exc()
            print(f"[Plugins] Loaded {len(self.loaded_plugins)} plugin(s) total")
            for plugin in self.loaded_plugins:
                print(f"  - {plugin['name']} ({plugin['type']})")
        except Exception as e:
            print(f"[Plugins] Error initializing plugin system: {e}")
            traceback.print_exc()
    def _open_file_from_cli(self, path: str):
        p = path.lower()
        if p.endswith('.mrpack') or p.endswith(ORANGPACK_EXT):
            self._do_import_mrpack_path(path)
        elif p.endswith('.zip'):
            self._do_import_curseforge_path(path)
    def _do_import_mrpack_path(self, mrpack_path: str):
        if hasattr(self, 'status_label'):
            self.status_label.config(text="Importing modpack...")
        if hasattr(self, 'status_bar_progress'):
            self.status_bar_progress.config(mode='indeterminate')
            self.status_bar_progress.start(15)
        def _restore():
            if hasattr(self, 'status_bar_progress'):
                self.status_bar_progress.stop()
                self.status_bar_progress.config(mode='determinate')
                if hasattr(self, 'progress'):
                    self.progress.set(0)
            if hasattr(self, 'status_label'):
                self.status_label.config(text="Ready")
        def _do():
            try:
                success, message, profile_name = import_modpack(mrpack_path, self)
                def done():
                    _restore()
                    if success:
                        messagebox.showinfo("Modpack Imported", f"Imported as '{profile_name}'\n{message}")
                        if hasattr(self, '_refresh_game_profiles'):
                            self._refresh_game_profiles()
                        if hasattr(self, 'game_profiles_tab'):
                            try:
                                self.game_profiles_tab._profile_fingerprints = {}
                                self.game_profiles_tab._refresh_profiles_list()
                            except Exception:
                                pass
                    else:
                        messagebox.showerror("Import Failed", message)
                self.after(0, done)
            except Exception as e:
                self.after(0, lambda m=str(e): (_restore(), messagebox.showerror("Import Error", m)))
        threading.Thread(target=_do, daemon=True).start()
    def _do_import_curseforge_path(self, zip_path: str):
        if hasattr(self, 'status_label'):
            self.status_label.config(text="Importing CurseForge pack...")
        if hasattr(self, 'status_bar_progress'):
            self.status_bar_progress.config(mode='indeterminate')
            self.status_bar_progress.start(15)
        def _restore():
            if hasattr(self, 'status_bar_progress'):
                self.status_bar_progress.stop()
                self.status_bar_progress.config(mode='determinate')
                if hasattr(self, 'progress'):
                    self.progress.set(0)
            if hasattr(self, 'status_label'):
                self.status_label.config(text="Ready")
        def _do():
            try:
                success, message, profile_name = import_curseforge_pack(zip_path, self)
                def done():
                    _restore()
                    if success:
                        messagebox.showinfo("Pack Imported", f"Imported as '{profile_name}'\n{message}")
                        if hasattr(self, '_refresh_game_profiles'):
                            self._refresh_game_profiles()
                    else:
                        messagebox.showerror("Import Failed", message)
                self.after(0, done)
            except Exception as e:
                self.after(0, lambda m=str(e): (_restore(), messagebox.showerror("Import Error", m)))
        threading.Thread(target=_do, daemon=True).start()
    def _startup_sync_sharing(self):
        # re-link shared folders for every instance so sharing is live from launch
        try:
            if any(getattr(self, a, None) and getattr(self, a).get()
                   for a in ('share_options', 'share_resourcepacks', 'share_shaderpacks', 'share_servers', 'share_screenshots')):
                threading.Thread(target=self._apply_sharing_all, daemon=True).start()
        except Exception as e:
            print(f"[Sharing] startup sync failed: {e}")
    @property
    def profiles(self):
        if self._profiles_cache is None:
            self._profiles_cache = load_profiles_safe()
        return self._profiles_cache


class QtLauncher(LauncherCore, QtWidgets.QMainWindow):
    PAGES = [("news", "UPDATE_NOTES", "News", "news"), ("logs", "LAUNCHER_LOG", "Log", "logs"), ("instances", "GAME_PROFILES_TITLE", "Instances", "instances"), ("content", "CONTENT_TAB", "Content", "file"), ("settings", "SETTINGS", "Settings", "settings")]
    def __init__(self):
        super().__init__()
        _QT_APP_REF[0] = self
        global _APP_REF
        try:
            _APP_REF = weakref.ref(self)
        except Exception:
            pass
        self._main_thread_id = threading.get_ident()
        self._progress_queue = queue.Queue(maxsize=32)
        self._progress_polling_active = False
        self._log_buffer = deque(maxlen=20000)
        self.use_default_args = _Var(True)
        self.custom_args = _Var("")
        self.show_status_bar = _Var(True)
        self.discord_rpc_enabled = _Var(True)
        self.delete_telemetry_on_startup = _Var(False)
        self.custom_layout_enabled = _Var(False)
        self.debug_mode_enabled = _Var(False)
        self.show_progress_bar = _Var(True)
        self.use_dri_prime = _Var(False)
        for attr in ("share_options", "share_resourcepacks", "share_shaderpacks", "share_servers", "share_screenshots"):
            setattr(self, attr, _Var(False))
        self.selected_profile = _Var("")
        self.progress = _Var(0)
        self.discord_rpc_mgr = None
        self.discord_start_time = None
        self.launch_thread = None
        self.cancel_requested = False
        self.mc_process = None
        self.profiles_list = []
        self.loaded_plugins = []
        self._profiles_cache = None
        self._pending_quickplay = None
        self._pending_open_file = None
        self._offline = False
        self._music = None
        self.music_playing = False
        self.music_playlist = []
        self.current_music_index = 0
        self.music_btn = None
        self.os_type = platform.system()
        self.game_profile_manager = get_game_profile_manager()
        self.instance_manager = get_instance_manager()
        self.theme_manager = get_theme_manager()
        self.theme = QtTheme(self.theme_manager)
        self._load_locales()
        try:
            saved_language = load_saved_language()
            if saved_language in self.locales:
                self.current_locale = saved_language
                self.translations = self.locales.get(saved_language, {})
        except Exception as e:
            print(f"[DEBUG] Error loading saved language: {e}")
        _load_settings(self)
        self.theme.load(self._initial_theme_name())
        self.theme.apply(QtWidgets.QApplication.instance())
        self.setWindowTitle("OrangLauncher")
        self.resize(1200, 860)
        self.setMinimumSize(960, 700)
        icon_path = find_resource("oranglauncher/images/orange.png")
        if icon_path:
            self.setWindowIcon(QtGui.QIcon(str(icon_path)))
        self._version_cache = None
        self._build_ui()
        self.refresh_accounts()
        self.refresh_instance_display()
        self.update_bottom_visibility()
        if self.discord_rpc_enabled.get():
            self._start_discord_rpc()
        if self.debug_mode_enabled.get():
            try:
                self._current_log_file = _start_debug_capture()
            except Exception as e:
                print(f"[DEBUG] {e}")
        QTimer.singleShot(100, self._initialize_plugins)
        QTimer.singleShot(1500, self._startup_sync_sharing)
        QTimer.singleShot(2000, self._check_connectivity)
        QTimer.singleShot(400, self._maybe_show_welcome)
        QTimer.singleShot(3000, self._delete_telemetry_if_enabled)
        QTimer.singleShot(5000, self._auto_check_updates)
        QTimer.singleShot(1200, _trim_memory)

    def _initial_theme_name(self):
        default = QT_THEME_SYSTEM if _on_plasma() else QT_THEME_OLED
        if not _adv_get("qt_theme_set", False):
            return default
        try:
            config_path = Path.home() / ".config" / "oranglauncher" / "launcher_config.json"
            if config_path.exists():
                data = json.loads(config_path.read_text(encoding="utf-8"))
                name = data.get("theme")
                if name in self.theme.available():
                    return name
        except Exception:
            pass
        return default

    def apply_theme(self, name):
        self.theme.load(name)
        self.theme.apply(QtWidgets.QApplication.instance())
        save_theme_preference(name)
        _adv_set("qt_theme_set", True)
        self.theme_manager.current_theme = name
        if self.page_widgets.get("settings") is not None:
            for b in self.settings_page.theme_group.buttons():
                b.setChecked(b.property("theme") == name)
        self.set_status(_qt_t("QT_THEME_APPLIED", "Theme: {name}").format(name=name))

    def after(self, ms, fn, *args):
        if args:
            _qt_later(lambda: fn(*args), ms)
        else:
            _qt_later(fn, ms)
        return None

    def after_cancel(self, ident):
        pass

    def winfo_exists(self):
        return True

    def _get_theme_color(self, key):
        return self.theme.c(key)

    def version_values(self):
        if self._version_cache is None:
            try:
                self._version_cache = list(get_available_versions())
            except Exception:
                self._version_cache = []
        return self._version_cache

    def validate_version(self, version):
        values = self.version_values()
        if version in values or not values:
            return True
        suggestions = [v for v in values if version[:4] in v][:5]
        hint = ("\n\n" + _qt_t("QT_DID_YOU_MEAN", "Did you mean: {options}?").format(options=', '.join(suggestions))) if suggestions else ""
        messagebox.showerror(_qt_t("QT_UNKNOWN_VERSION", "Unknown version"), _qt_t("QT_UNKNOWN_VERSION_MSG", "'{version}' is not a valid Minecraft version.").format(version=version) + hint)
        return False

    def fetch_loader_versions(self, loader, mc_version):
        if not mc_version:
            return []
        try:
            if loader == "forge":
                forge_versions = minecraft_launcher_lib.forge.list_forge_versions()
                return [v for v in reversed(forge_versions) if v.startswith(f"{mc_version}-")]
            if loader == "neoforge":
                nf = _NeoforgeCompat()
                return nf.get_loader_versions(mc_version, True) or nf.get_loader_versions(mc_version, False)
            if loader == "fabric":
                return [v["version"] for v in minecraft_launcher_lib.fabric.get_all_loader_versions()]
            if loader == "quilt":
                return [v["version"] for v in minecraft_launcher_lib.quilt.get_all_loader_versions()]
            if loader == "optifine":
                return get_optifine_files_for(mc_version)
        except Exception as e:
            print(f"[DEBUG] Error fetching loader versions: {e}")
        return []

    def _build_ui(self):
        central = QtWidgets.QWidget()
        self.setCentralWidget(central)
        root = QtWidgets.QVBoxLayout(central)
        root.setContentsMargins(0, 0, 0, 0)
        root.setSpacing(0)
        topbar = QtWidgets.QFrame()
        topbar.setObjectName("topBar")
        tl = QtWidgets.QHBoxLayout(topbar)
        tl.setContentsMargins(12, 6, 12, 6)
        tl.setSpacing(4)
        logo = QtWidgets.QLabel()
        lp = find_resource("oranglauncher/images/orange.png")
        if lp:
            logo.setPixmap(_qt_pixmap_from_path(str(lp), (26, 26)) or QtGui.QPixmap())
        logo.setToolTip(_qt_t("QT_LOGO_TOOLTIP", "OrangLauncher"))
        tl.addWidget(logo)
        tl.addSpacing(8)
        self.nav_group = QtWidgets.QButtonGroup(self)
        self.nav_buttons = {}
        self.stack = QtWidgets.QStackedWidget()
        self.page_widgets = {}
        self._page_builders = {"news": lambda: QtNewsPage(self), "logs": lambda: QtLogPage(self), "instances": lambda: QtInstancesPage(self),
                               "content": lambda: QtContentPage(self), "settings": lambda: QtSettingsPage(self)}
        for key, tkey, default, icon in self.PAGES:
            b = QtWidgets.QPushButton(_tr(self, tkey, default))
            b.setObjectName("topnav")
            b.setCheckable(True)
            b.setIcon(_qt_icon(icon, 18, self.theme.c("fg_secondary")))
            b.setIconSize(QSize(18, 18))
            b.setCursor(Qt.PointingHandCursor)
            b.clicked.connect(lambda _=False, k=key: self.show_page(k))
            self.nav_group.addButton(b)
            self.nav_buttons[key] = b
            tl.addWidget(b)
        self.log_page = self._page("logs")
        self.instances_page = self._page("instances")
        tl.addStretch(1)
        self.offline_label = QtWidgets.QLabel(" ⚠ " + _qt_t("QT_OFFLINE", "Offline") + " ")
        self.offline_label.setObjectName("offline")
        self.offline_label.setVisible(False)
        tl.addWidget(self.offline_label)
        root.addWidget(topbar)
        root.addWidget(self.stack, 1)
        self._build_bottom(root)
        self.show_page("instances" if self.instance_manager.instances else "news")

    def _build_bottom(self, root):
        bar = QtWidgets.QFrame()
        bar.setObjectName("bottomBar")
        outer = QtWidgets.QVBoxLayout(bar)
        outer.setContentsMargins(16, 8, 16, 8)
        outer.setSpacing(4)
        grid = QtWidgets.QGridLayout()
        grid.setContentsMargins(0, 0, 0, 0)
        grid.setHorizontalSpacing(20)
        grid.setColumnStretch(1, 1)
        left = QtWidgets.QVBoxLayout()
        left.setSpacing(5)
        left.addWidget(_qt_label(_tr(self, "PROFILE", "Profile"), "h3"))
        self.account_combo = QtWidgets.QComboBox()
        self.account_combo.setFixedWidth(230)
        self.account_combo.currentIndexChanged.connect(lambda *_: self._on_account_selected())
        left.addWidget(self.account_combo)
        left.addWidget(_qt_button(_tr(self, "NEW_PROFILE", "New Profile"), lambda: self.add_account(), icon="plus", launcher=self), 0, Qt.AlignLeft)
        left.addStretch(1)
        left_box = QtWidgets.QWidget()
        left_box.setLayout(left)
        grid.addWidget(left_box, 0, 0, Qt.AlignTop | Qt.AlignLeft)
        center = QtWidgets.QVBoxLayout()
        center.setSpacing(5)
        center.addWidget(_qt_label(_tr(self, "GAME_PROFILES", "Game Profiles"), "h3", align=Qt.AlignHCenter))
        self.version_label_widget = _qt_label("", "h3", wrap=True, align=Qt.AlignHCenter)
        self.version_label_widget.setMinimumWidth(400)
        self.version_label_widget.setMaximumWidth(460)
        center.addWidget(self.version_label_widget, 0, Qt.AlignHCenter)
        self.play_button = QtWidgets.QPushButton(_tr(self, "PLAY", "Play"))
        self.play_button.setObjectName("play")
        self.play_button.setCursor(Qt.PointingHandCursor)
        self.play_button.setFixedWidth(170)
        self.play_button.clicked.connect(lambda *_: self._on_play_clicked())
        center.addSpacing(6)
        center.addWidget(self.play_button, 0, Qt.AlignHCenter)
        center_box = QtWidgets.QWidget()
        center_box.setLayout(center)
        grid.addWidget(center_box, 0, 1, Qt.AlignTop | Qt.AlignHCenter)
        right = QtWidgets.QVBoxLayout()
        right.setSpacing(5)
        self.status_label_widget = _qt_label(_tr(self, "WELCOME", "Welcome"), "muted", align=Qt.AlignRight)
        right.addWidget(self.status_label_widget, 0, Qt.AlignRight)
        self.instance_combo = QtWidgets.QComboBox()
        self.instance_combo.setFixedWidth(270)
        self.instance_combo.currentIndexChanged.connect(lambda *_: self._on_instance_combo())
        right.addWidget(self.instance_combo, 0, Qt.AlignRight)
        right.addStretch(1)
        right_box = QtWidgets.QWidget()
        right_box.setLayout(right)
        grid.addWidget(right_box, 0, 2, Qt.AlignTop | Qt.AlignRight)
        outer.addLayout(grid)
        self.status_row = QtWidgets.QWidget()
        self.status_row.setVisible(False)
        outer.addWidget(self.status_row)
        self.progress_bar = QtWidgets.QProgressBar()
        self.progress_bar.setRange(0, 100)
        self.progress_bar.setValue(0)
        self.progress_bar.setTextVisible(False)
        self.progress_bar.setFixedHeight(8)
        outer.addWidget(self.progress_bar)
        root.addWidget(bar)
        self.status_label = _LabelAdapter(self.status_label_widget)
        self.version_label = _LabelAdapter(self.version_label_widget)
        self.status_bar_progress = _ProgressAdapter(self.progress_bar)
        self._play_mode = "play"

    def update_bottom_visibility(self):
        self.progress_bar.setVisible(bool(self.show_progress_bar.get()))
        self.status_label_widget.setVisible(bool(self.show_status_bar.get()))

    def _page(self, key):
        w = self.page_widgets.get(key)
        if w is None:
            w = self._page_builders[key]()
            self.page_widgets[key] = w
            self.stack.addWidget(w)
        return w

    @property
    def news_page(self):
        return self._page("news")
    @property
    def content_page(self):
        return self._page("content")
    @property
    def settings_page(self):
        return self._page("settings")

    def show_page(self, key):
        self.nav_buttons[key].setChecked(True)
        self.stack.setCurrentWidget(self._page(key))
        if key == "news":
            self.news_page.ensure_loaded()
        elif key == "content":
            self.content_page.refresh_instances()
            self.content_page.ensure_loaded()
        elif key == "instances":
            self.instances_page.refresh()

    def show_content(self, content_type):
        self.show_page("content")
        self.content_page.refresh_instances()
        self.content_page.set_type(content_type)

    def _select_tab_by_text(self, text):
        for key, tkey, default, _icon in self.PAGES:
            if _tr(self, tkey, default).strip() == str(text).strip():
                self.show_page(key)
                return True
        return False

    def set_status(self, text):
        self.status_label_widget.setText(str(text))

    def _apply_progress_update(self, percent, status_text):
        try:
            self.progress_bar.setRange(0, 100)
            self.progress_bar.setValue(int(min(max(percent, 0), 100)))
            self.progress.set(percent)
            if status_text:
                self.status_label_widget.setText(str(status_text))
        except Exception:
            pass

    def refresh_accounts(self):
        self._profiles_cache = None
        try:
            data = load_profiles()
        except Exception:
            data = []
        self.profiles_list = [f"{p['username']} ({p['type']})" for p in data]
        current = self.selected_profile.get()
        self.account_combo.blockSignals(True)
        self.account_combo.clear()
        if self.profiles_list:
            self.account_combo.addItems(self.profiles_list)
            idx = self.profiles_list.index(current) if current in self.profiles_list else 0
            self.account_combo.setCurrentIndex(idx)
            self.selected_profile.set(self.profiles_list[idx])
            self.set_status(_tr(self, "WELCOME_USER", "Welcome, {username}").format(username=data[idx]["username"]))
        else:
            self.account_combo.addItem(_tr(self, "NO_ACCOUNTS", _qt_t("QT_NO_ACCOUNTS_ADD", "No accounts yet - press Add")))
            self.selected_profile.set("")
        self.account_combo.blockSignals(False)
        if self.page_widgets.get("settings") is not None:
            self.settings_page.refresh_accounts()

    def _on_account_selected(self):
        if not self.profiles_list:
            return
        idx = self.account_combo.currentIndex()
        if 0 <= idx < len(self.profiles_list):
            self.selected_profile.set(self.profiles_list[idx])
            self.set_status(_tr(self, "WELCOME_USER", "Welcome, {username}").format(username=self.profiles_list[idx].split(" (")[0]))
    # I miss mojang accounts
    def add_account(self, kind=None, mode="embedded"):
        if kind is None:
            kind = _qt_ask_profile_type(self)
        if kind not in ("offline", "microsoft"):
            return
        profile = {"type": kind}
        if kind == "offline":
            username = _qt_askstring(_tr(self, "OFFLINE_ACCOUNT_TITLE", _qt_t("QT_OFFLINE_ACCOUNT", "Offline account")), _tr(self, "OFFLINE_ACCOUNT_PROMPT", "Username:"))
            if not username or not username.strip():
                return
            profile["username"] = username.strip()
            profile["uuid"] = "00000000-0000-0000-0000-000000000000"
            self._store_account(profile)
            return
        self.set_status(_qt_t("QT_MS_SIGNING_IN", "Signing in with Microsoft..."))

        def work():
            return _ms_token_flow_qt(mode)

        def done(tokens):
            profile.update(tokens)
            self._store_account(profile)

        def fail(err):
            self.set_status(_qt_t("QT_READY", "Ready"))
            if isinstance(err, LoginCancelled):
                return
            messagebox.showerror(_tr(self, "MS_AUTH_ERROR", "Microsoft sign-in"), f"{_tr(self, 'MS_AUTH_FAIL', 'Authentication failed')}\n{err}")
        _qt_run_bg(work, done, fail)

    def _store_account(self, profile):
        try:
            data = load_profiles()
            if profile.get("type") == "microsoft":
                data = [p for p in data if not (p.get("type") == "microsoft" and p.get("uuid") == profile.get("uuid"))]
            else:
                data = [p for p in data if not (p.get("type") == profile.get("type") and p.get("username") == profile.get("username"))]
            data.append(profile)
            save_profiles(data)
        except Exception as e:
            messagebox.showerror(_tr(self, "ERROR", "Error"), str(e))
            return
        self.selected_profile.set(f"{profile.get('username', '')} ({profile.get('type', '')})")
        self.refresh_accounts()
        self.set_status(_qt_t("QT_ACCOUNT_ADDED", "Added account {name}").format(name=profile.get('username', '')))

    def refresh_instance_display(self):
        instances = list(self.instance_manager.instances.values())
        self.instance_combo.blockSignals(True)
        self.instance_combo.clear()
        for inst in instances:
            self.instance_combo.addItem(f"{inst.name}  ·  {inst.version} ({inst.mod_loader})", inst.instance_id)
        sel = self.instance_manager.get_selected_instance()
        if sel is None and instances:
            self.instance_manager.selected_instance_id = instances[0].instance_id
            self.instance_manager.save_instances()
            sel = instances[0]
        if sel is not None:
            idx = self.instance_combo.findData(sel.instance_id)
            if idx >= 0:
                self.instance_combo.setCurrentIndex(idx)
            self.version_label_widget.setText(_qt_t("QT_INSTANCE", "Instance") + f": {sel.name} | {sel.version} ({sel.mod_loader})")
        else:
            self.instance_combo.addItem(_tr(self, "NO_PROFILE_SELECTED", _qt_t("QT_NO_INSTANCE_YET", "No instance yet - create one in Instances")))
            self.version_label_widget.setText(_tr(self, "NO_PROFILE_SELECTED", "No instance selected"))
        self.instance_combo.blockSignals(False)
        if self.page_widgets.get("content") is not None:
            self.content_page.refresh_instances()

    def _on_instance_combo(self):
        iid = self.instance_combo.currentData()
        if iid and iid != self.instance_manager.selected_instance_id:
            self.instance_manager.set_selected_instance(iid)
            sel = self.instance_manager.get_selected_instance()
            if sel is not None:
                self.version_label_widget.setText(_qt_t("QT_INSTANCE", "Instance") + f": {sel.name} | {sel.version} ({sel.mod_loader})")
            if self.page_widgets.get("content") is not None:
                self.content_page.refresh_instances()
            if self.page_widgets.get("instances") is not None:
                self.instances_page.refresh(force=True)

    def _edit_current_instance(self):
        inst = self.instance_manager.get_selected_instance()
        if inst is None:
            self.show_page("instances")
            return
        self.show_page("instances")
        self.instances_page.open_editor(inst)

    def toggle_music(self):
        self._toggle_music()


    def _on_play_clicked(self):
        if self._play_mode == "play":
            self._launch_game()
        else:
            self._cancel_launch()

    def _set_play_mode(self, mode):
        self._play_mode = mode
        if mode == "play":
            self.play_button.setText(_tr(self, "PLAY", "Play"))
            self.play_button.setEnabled(True)
        elif mode == "stop":
            self.play_button.setText(_tr(self, "STOP", "Stop"))
            self.play_button.setEnabled(True)
        else:
            self.play_button.setText(_tr(self, "CANCELLING", "Cancelling..."))
            self.play_button.setEnabled(False)

    def _launch_game(self):
        if self.launch_thread is not None and self.launch_thread.is_alive():
            messagebox.showinfo(_tr(self, "LAUNCHER_BUSY", "Busy"), _tr(self, "LAUNCHER_BUSY_MSG", "A launch is already in progress."))
            return
        selected_name = self.selected_profile.get()
        if not selected_name:
            messagebox.showinfo(_qt_t("QT_NO_ACCOUNT_TITLE", "No account added"), _qt_t("QT_NO_ACCOUNT_MSG", "You have not added any account yet.\n\nPress 'New Profile' under the account box and choose:\n  - Microsoft: sign in with the account that owns Minecraft\n  - Offline: pick a username (singleplayer and offline-mode servers only)\n\nAccounts can also be managed in Settings > Accounts."))
            return
        selected_profile = None
        try:
            for prof in load_profiles():
                if f"{prof['username']} ({prof['type']})" == selected_name:
                    selected_profile = prof
                    break
        except Exception as e:
            messagebox.showerror(_tr(self, "PROFILE_ERROR", _qt_t("QT_ACCOUNT", "Account")), f"{_tr(self, 'ERROR_LOADING_PROFILES', 'Could not load accounts')}\n{e}")
            return
        if not selected_profile:
            messagebox.showerror(_tr(self, "PROFILE_ERROR", _qt_t("QT_ACCOUNT", "Account")), _tr(self, "SELECTED_PROFILE_NOT_FOUND", "Selected account not found."))
            return
        current_instance = self.instance_manager.get_selected_instance()
        if current_instance is None:
            messagebox.showinfo(_tr(self, "NO_PROFILE_SELECTED", "No instance selected"), _qt_t("QT_NO_INSTANCE_MSG", "There is no instance to play yet.\n\nOpen the Instances tab and press New to create one, or use Import to add a modpack (.mrpack / .orangpack / .zip)."))
            self.show_page("instances")
            return
        version = current_instance.version
        mod_loader = current_instance.mod_loader
        ram = current_instance.ram
        launch_name = current_instance.name
        if not version or not version.strip():
            messagebox.showerror(_qt_t("QT_VERSION_ERROR", "Version error"), _qt_t("QT_VERSION_ERROR_MSG", "Selected instance has no Minecraft version set. Edit the instance to set a valid version."))
            return
        username = selected_profile.get("username", "")
        uuid = selected_profile.get("uuid", "")
        if not uuid or uuid == "0-0-0-0":
            uuid = str(uuid_module.uuid4())
        if not ram.endswith("G") and not ram.endswith("M"):
            ram = f"{ram}G"
        print(f"Playing Minecraft {version} ({mod_loader}) as {username} with {ram} RAM...")
        if getattr(self, "discord_rpc_mgr", None) and self.discord_rpc_enabled.get():
            self._update_discord_rpc("Playing Minecraft", f"{version} ({mod_loader})")
        self.progress_bar.setValue(0)
        self.cancel_requested = False
        self._set_play_mode("stop")
        self.account_combo.setEnabled(False)
        self.instance_combo.setEnabled(False)
        self.set_status(_qt_t("QT_LAUNCHING", "Launching {version} ({loader})...").format(version=version, loader=mod_loader) + " 0%")
        self.log_page.clear()
        self._append_log(f"[Launcher] Launching Minecraft {version} ({mod_loader}) as {username}...")
        self.mc_process = None
        quick_play_server = self._pending_quickplay
        self._pending_quickplay = None
        self.launch_thread = threading.Thread(target=self._run_launcher_thread, args=(current_instance, launch_name, version, mod_loader, ram, selected_profile, username, uuid, quick_play_server), daemon=True)
        self.launch_thread.start()

    def _cancel_launch(self):
        if self.mc_process is not None:
            try:
                self.mc_process.terminate()
                try:
                    self.mc_process.wait(timeout=2)
                except subprocess.TimeoutExpired:
                    self.mc_process.kill()
                    self.mc_process.wait(timeout=5)
                except Exception:
                    pass
            except Exception as e:
                print(f"Error stopping Minecraft: {e}")
            self.mc_process = None
            self.set_status(_tr(self, "STOPPED", _qt_t("QT_STOPPED", "Stopped")))
            self._restore_ui()
        else:
            self.cancel_requested = True
            self._set_play_mode("cancelling")
            self.set_status(_tr(self, "CANCELLING_LAUNCH", "Cancelling launch..."))

    def _on_mc_started(self):
        inst = self.instance_manager.get_selected_instance()
        username = self.selected_profile.get().split(" (")[0]
        if inst:
            self.set_status(_qt_t("QT_RUNNING_AS", "Minecraft {version} ({loader}) running as {username}").format(version=inst.version, loader=inst.mod_loader, username=username))
            if getattr(self, "discord_rpc_mgr", None) and self.discord_rpc_enabled.get():
                self._update_discord_rpc("Playing Minecraft", f"{inst.version} ({inst.mod_loader})")
        self._set_play_mode("stop")

    def _restore_ui(self):
        self._set_play_mode("play")
        self.account_combo.setEnabled(True)
        self.instance_combo.setEnabled(True)
        try:
            while True:
                self._progress_queue.get_nowait()
        except Exception:
            pass
        self.progress_bar.setRange(0, 100)
        self.progress_bar.setValue(0)
        self.launch_thread = None
        self.mc_process = None
        name = self.selected_profile.get()
        if name:
            self.set_status(_tr(self, "WELCOME_USER", "Welcome, {username}").format(username=name.split(" (")[0]))
        if getattr(self, "discord_rpc_mgr", None) and self.discord_rpc_enabled.get():
            self._update_discord_rpc("Idling in Launcher")
        try:
            self.instances_page.refresh(force=True)
        except Exception:
            pass

    def _append_log(self, message):
        self.log_page.append(message)
    def _safe_append_log(self, line):
        _qt_later(lambda: self._append_log(line))
    def log_message(self, message):
        self._append_log(message)

    def _check_crash_report(self, minecraft_directory, exit_code):
        try:
            crash_dir = Path(minecraft_directory) / "crash-reports"
            if not crash_dir.exists():
                return
            reports = sorted(crash_dir.glob("crash-*.txt"), key=lambda p: p.stat().st_mtime, reverse=True)
            if not reports:
                return
            latest = reports[0]
            if time.time() - latest.stat().st_mtime > 600:
                return
            lines = latest.read_text(encoding="utf-8", errors="replace").splitlines()
            summary = "\n".join([l for l in lines[:80] if l.strip()][:30])
            QtCrashDialog(self, exit_code, latest, summary).show()
        except Exception as e:
            print(f"[DEBUG] Error reading crash report: {e}")

    def _check_connectivity(self):
        def work():
            try:
                _http_session.get("https://launchermeta.mojang.com/mc/game/version_manifest.json", timeout=5, stream=True).close()
                return True
            except Exception:
                return False

        def done(online):
            self._offline = not online
            self.offline_label.setVisible(not online)
        _qt_run_bg(work, done, lambda e: None)
        QTimer.singleShot(60000, self._check_connectivity)

    def _delete_telemetry_if_enabled(self):
        if not self.delete_telemetry_on_startup.get():
            return

        def work():
            n = 0
            for inst in list(self.instance_manager.instances.values()):
                for sub in ("logs/telemetry", "telemetry"):
                    d = inst.minecraft_dir / sub
                    if d.exists() and not d.is_symlink():
                        try:
                            shutil.rmtree(d)
                            n += 1
                        except Exception:
                            pass
            return n
        _qt_run_bg(work, lambda n: n and self._safe_append_log(f"[Privacy] Removed telemetry from {n} folder(s)"), lambda e: None)

    def _auto_check_updates(self):
        def done(result):
            available, version, url, notes = result
            if available:
                self.set_status(_qt_t("QT_UPDATE_AVAILABLE", "Update available: {version} (Settings > About)").format(version=version))
        _qt_run_bg(check_for_updates, done, lambda e: None)

    def check_updates(self):
        self.set_status(_qt_t("QT_CHECKING_UPDATES", "Checking for updates..."))

        def done(result):
            available, version, url, notes = result
            if available:
                if url == "AUR":
                    msg = _qt_t("QT_UPDATE_AUR_MSG", "A new version ({version}) is available via AUR.\nDo you want to run 'yay -S oranglauncher-bin'?").format(version=version)
                else:
                    msg = _qt_t("QT_UPDATE_MSG", "A new version ({version}) is available!\n\nRelease notes:\n{notes}\n\nDo you want to update now?").format(version=version, notes=(notes or '')[:1500])
                if messagebox.askyesno(_qt_t("QT_UPDATE_AVAILABLE_TITLE", "Update available"), msg):
                    threading.Thread(target=perform_update, args=(url, str(Path(__file__).parent)), daemon=True).start()
            else:
                messagebox.showinfo(_qt_t("QT_NO_UPDATES", "No updates"), _qt_t("QT_LATEST_VERSION", "You are using the latest version ({version}).").format(version=CURRENT_VERSION))
            self.set_status(_qt_t("QT_READY", "Ready"))
        _qt_run_bg(check_for_updates, done, lambda e: (self.set_status(_qt_t("QT_READY", "Ready")), messagebox.showerror(_qt_t("QT_UPDATES", "Updates"), str(e))))

    def _maybe_show_welcome(self):
        try:
            if not is_setup_done():
                QtWelcomeWizard(self).exec()
        except Exception as e:
            print(f"[setup] welcome wizard failed: {e}")
            traceback.print_exc()
        if self._pending_open_file:
            path = self._pending_open_file
            self._pending_open_file = None
            QTimer.singleShot(800, lambda: self._open_file_from_cli(path))

    def _refresh_game_profiles(self):
        self.refresh_instance_display()
        try:
            self.instances_page.refresh(force=True)
        except Exception:
            pass

    def _refresh_profiles(self):
        self.refresh_accounts()

    def closeEvent(self, event):
        try:
            self._stop_discord_rpc()
        except Exception:
            pass
        try:
            if self._music is not None:
                self.music_playing = False
                self._music[0].stop()
        except Exception:
            pass
        try:
            _save_settings(self)
        except Exception:
            pass
        event.accept()


def qt_main(open_file_arg=None):
    os.environ.setdefault("QT_ENABLE_HIGHDPI_SCALING", "1")
    if _on_plasma() and not os.environ.get("QT_QPA_PLATFORMTHEME"):
        os.environ["QT_QPA_PLATFORMTHEME"] = "kde"
    if platform.system() == "Darwin":
        os.environ.setdefault("QT_MAC_WANTS_LAYER", "1")
    QtCore.QCoreApplication.setAttribute(Qt.AA_ShareOpenGLContexts, True)
    app = QtWidgets.QApplication(sys.argv)
    app.setApplicationName("OrangLauncher")
    app.setDesktopFileName("oranglauncher")
    app.setOrganizationName("Orang Studio")
    QT_ACTIVE[0] = True
    _QT_MAIN_THREAD[0] = threading.get_ident()
    _QT_INVOKER[0] = _QtInvoker()
    window = QtLauncher()
    if open_file_arg:
        window._pending_open_file = open_file_arg
    window.show()
    return app.exec()


def _reexec_as_module():
    if getattr(sys, "frozen", False) or "__compiled__" in globals():
        return
    if __name__ != "__main__" or os.environ.get("ORANG_IMPORT_MODE") == "1":
        return
    try:
        here = str(Path(__file__).resolve().parent)
        code = ("import sys; sys.path.insert(0, %r); import launcher; sys.argv = ['launcher.py'] + sys.argv[1:]; launcher.main()" % here)
        env = dict(os.environ)
        env["ORANG_IMPORT_MODE"] = "1"
        os.execve(sys.executable, [sys.executable, "-c", code] + sys.argv[1:], env)
    except Exception as e:
        print(f"[startup] module re-exec skipped: {e}")

# defenitly main.....
def main():
    try:
        if "--terminal" in sys.argv:
            terminal_main()
            return
        if not QT_AVAILABLE:
            print("[ui] PySide6 is not installed (pip install PySide6)")
            sys.exit(1)
        _reexec_as_module()
        print("Welcome to the Orange launcher! You can use --terminal to save ram!")
        install_crash_handlers()
        if _debug_mode_saved():
            _start_debug_capture()
        if "--testing" in sys.argv:
            mark_setup_done(False)
            print("[testing] reset setup.mark → setup_done=false")
        open_file_arg = None
        for arg in sys.argv[1:]:
            if not arg.startswith('-') and (arg.endswith('.mrpack') or arg.endswith('.zip') or arg.endswith('.orangpack')):
                open_file_arg = arg
                break
        if platform.system() == "Linux":
            _register_mrpack_association()
        sys.exit(qt_main(open_file_arg))
    except KeyboardInterrupt:
        print("\\cya :>")
        sys.exit(0)
if __name__ == "__main__":
    main()