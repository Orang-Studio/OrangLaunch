import sys
if sys.platform.startswith("linux") and os.environ.get("ORANG_FC_PRELOAD") != "1":
    _fc = next((p for p in ("/usr/lib/libfontconfig.so.1", "/usr/lib64/libfontconfig.so.1",
                            "/usr/lib/x86_64-linux-gnu/libfontconfig.so.1") if os.path.exists(p)), None)
    if _fc:
        os.environ["ORANG_FC_PRELOAD"] = "1"
        _pre = os.environ.get("LD_PRELOAD", "")
        os.environ["LD_PRELOAD"] = _fc + (":" + _pre if _pre else "")
        try:
            if "__compiled__" in globals() or getattr(sys, "frozen", False):
                os.execv(sys.executable, sys.argv)
            else:
                os.execv(sys.executable, [sys.executable] + sys.argv)
        except Exception:
            pass
os.environ.setdefault("GDK_BACKEND", "x11")
os.environ['PYGAME_HIDE_SUPPORT_PROMPT'] = "hide"
import builtins
import tkinter as tk
import platform
import asyncio
import atexit
import weakref
import threading
import queue
import time
import glob
import subprocess
import traceback as tb
import json
import base64
import urllib.parse
import importlib.util
import os
import minecraft_launcher_lib
import requests
import uuid as uuid_module
import re
import shutil
import zipfile
import tarfile
import tempfile
import traceback
import copy
import tkinterweb
import webbrowser
import webview
import random
from tkinter import ttk, messagebox, scrolledtext, filedialog, simpledialog
from datetime import datetime, timedelta
from PIL import Image, ImageTk, ImageDraw, ImageOps
import io
from typing import List, Dict, Any, Optional, Union, Callable, Tuple
try:
    from pypresence.presence import Presence
except ImportError:
    Presence = None
from collections import deque
from pathlib import Path


import pygame as _pygame
import pygame as _pg
import platform as _platform
import uuid as _uuid
import gi
import socket, json, struct
import re as _re_ansi
import stat
from minecraft_launcher_lib.mod_loader import Neoforge
import subprocess as _sp
import re as _re

pygame = None
pygame_available = False
pygame_mixer_initialized = False
pygame = _pygame
pygame_available = True
_http_session = requests.Session()
_http_session.headers.update({"User-Agent": "OrangLauncher"})
_image_cache: Dict[str, bytes] = {}
_image_cache_lock = threading.Lock()

def _cached_image_get(url: str, timeout: int = 8) -> bytes:
    with _image_cache_lock:
        if url in _image_cache:
            return _image_cache[url]
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
            "base_path": str(self.base_path),
            "minecraft_dir": str(self.minecraft_dir)
        }
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
    

CURRENT_VERSION = "6.1.7"
REPO_OWNER = "Orang-Studio"
REPO_NAME = "OrangLaunch"
GITHUB_API_URL = f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases/latest"
ORANGLIB_API_URL = os.environ.get("ORANGLIB_API_URL", "https://api.oranges.lt")
ORANGLIB_DESKTOP_DIR = Path.home() / "Desktop"
ORANGLIB_TEMP_DIR = Path(tempfile.gettempdir()) / "oranglauncher" / "oranglib_downloads"
def check_for_updates():
    def _parse_version(v):
        clean = v.split("-")[0].lstrip("v")
        try:
            return [int(x) for x in clean.split(".")]
        except ValueError:
            return []

    if shutil.which("yay") and (Path("/etc/arch-release").exists() or Path("/etc/manjaro-release").exists()):
        try:
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
def show_update_dialog(parent, launcher):
    def _check():
        result = check_for_updates()
        parent.after(0, lambda: _show_result(result))
    def _show_result(result):
        available, version, url, notes = result
        if available:
            if url == "AUR":
                msg = f"A new version ({version}) is available via AUR.\\nDo you want to run 'yay -S oranglauncher-bin'?"
            else:
                msg = f"A new version ({version}) is available!\\n\\nRelease Notes:\\n{notes}\\n\\nDo you want to update now?"
            if messagebox.askyesno("Update Available", msg, parent=parent):
                launcher_root = Path(__file__).parent
                threading.Thread(target=perform_update, args=(url, str(launcher_root)), daemon=True).start()
        else:
            messagebox.showinfo("No Updates", f"You are using the latest version ({CURRENT_VERSION}).", parent=parent)
    threading.Thread(target=_check, daemon=True).start()

# ui components
class ToggleSwitch(tk.Canvas):
    def __init__(self, parent, variable, command=None, width=44, height=24, **kwargs):
        self.variable = variable
        self.command = command
        self.width = width
        self.height = height
        if 'bg' not in kwargs:
            try:
                kwargs['bg'] = parent['bg']
            except:
                kwargs['bg'] = parent.cget('bg')
        super().__init__(parent, width=width, height=height, highlightthickness=0, bd=0, **kwargs)
        
        self.bind("<Button-1>", self._on_click)
        self.bind("<Button-4>", lambda e: "break")
        self.bind("<Button-5>", lambda e: "break")
        self.bind("<MouseWheel>", lambda e: "break")
        self.bind("<Destroy>", self._on_destroy)
        self._trace_name = self.variable.trace_add("write", self._update_graphics)
        self.after(10, self._update_graphics)

    def _on_destroy(self, event):
        try:
            self.variable.trace_remove("write", self._trace_name)
        except Exception:
            pass

    def _on_click(self, event):
        self.variable.set(not self.variable.get())
        if self.command:
            self.command()

    def _update_graphics(self, *args):
        try:
            self.delete("all")
        except Exception:
            return
        val = self.variable.get()
        tm = get_theme_manager()
        bg_color = "#F8961E" if val else "#4B4B4B"
        handle_color = "#ffffff"
        if tm:
            bg_color = "#F8961E" if val else "#4B4B4B"
            handle_color = "#ffffff"

        radius = self.height / 2
        
        self.create_oval(0, 0, self.height, self.height, fill=bg_color, outline=bg_color)
        self.create_oval(self.width-self.height, 0, self.width, self.height, fill=bg_color, outline=bg_color)
        self.create_rectangle(radius, 0, self.width-radius, self.height, fill=bg_color, outline=bg_color)
        
        handle_pad = 4
        handle_size = self.height - (handle_pad * 2)
        if val:
            x = self.width - handle_size - handle_pad
        else:
            x = handle_pad
        y = handle_pad
        self.create_oval(x, y, x+handle_size, y+handle_size, fill=handle_color, outline=handle_color)

def build_settings_tab(launcher, notebook):
    tab = ttk.Frame(notebook)
    notebook.add(tab, text=launcher._t('SETTINGS'))
    launcher.settings_frame = tab
    _configure_modern_styles(launcher.style)

    _load_settings(launcher)
    bg_primary = launcher._get_theme_color('bg_primary')
    
    main_container = tk.Frame(tab, bg=bg_primary)
    main_container.pack(fill="both", expand=True)
    
    sidebar = tk.Frame(main_container, bg=bg_primary, width=240)
    sidebar.pack(side="left", fill="y", padx=0, pady=0)
    sidebar.pack_propagate(False)

    header_frame = tk.Frame(sidebar, bg=bg_primary)
    header_frame.pack(fill="x", padx=24, pady=(32, 24))
    
    title_label = tk.Label(header_frame, text="Settings", font=("Segoe UI", 18, "bold"),
                           bg=bg_primary, fg=launcher._get_theme_color('fg_primary'))
    title_label.pack(side="left", anchor="center")

    nav_buttons_frame = tk.Frame(sidebar, bg=bg_primary)
    nav_buttons_frame.pack(fill="x", padx=12)

    content_container = tk.Frame(main_container, bg=bg_primary)
    content_container.pack(side="left", fill="both", expand=True)

    launcher._settings_current_content = None
    launcher._settings_nav_buttons = []
    launcher._settings_nav_buttons_data = []
    
    def show_content(content_func, tab_name):
        if hasattr(launcher, '_settings_canvas') and launcher._settings_canvas.winfo_exists():
            launcher._settings_canvas.destroy()
        if hasattr(launcher, '_settings_scrollbar') and launcher._settings_scrollbar.winfo_exists():
            launcher._settings_scrollbar.destroy()
            
        launcher._settings_canvas = tk.Canvas(content_container, bg=bg_primary, highlightthickness=0, bd=0)
        launcher._settings_scrollbar = ttk.Scrollbar(content_container, orient="vertical", command=launcher._settings_canvas.yview, style="Modern.Vertical.TScrollbar")
        
        launcher._settings_current_content = tk.Frame(launcher._settings_canvas, bg=bg_primary)
        launcher._settings_current_content.bind(
            "<Configure>",
            lambda e: launcher._settings_canvas.configure(scrollregion=launcher._settings_canvas.bbox("all"))
        )
        
        launcher._settings_canvas.create_window((0, 0), window=launcher._settings_current_content, anchor="nw")
        launcher._settings_canvas.configure(yscrollcommand=launcher._settings_scrollbar.set)
        
        def on_mousewheel(event):
            launcher._settings_canvas.yview_scroll(int(-1*(event.delta/120)), "units")
        def on_enter(event):
            launcher._settings_canvas.focus_set()
            launcher._settings_canvas.bind_all("<MouseWheel>", on_mousewheel)

        def on_leave(event):
            try:
                launcher._settings_canvas.unbind_all("<MouseWheel>")
            except: pass

        launcher._settings_canvas.bind("<Enter>", on_enter)
        launcher._settings_canvas.bind("<Leave>", on_leave)
        
        launcher._settings_canvas.pack(side="left", fill="both", expand=True)
        launcher._settings_scrollbar.pack(side="right", fill="y", padx=(0, 4), pady=4)

        padded_content = tk.Frame(launcher._settings_current_content, bg=bg_primary)
        padded_content.pack(fill="both", expand=True, padx=40, pady=30)
        
        content_func(padded_content, launcher)
        launcher._settings_current_content.update_idletasks()
        launcher._settings_canvas.configure(scrollregion=launcher._settings_canvas.bbox("all"))

    def show_content_no_scroll(content_func, tab_name):
        show_content(content_func, tab_name)

    def create_modern_nav_button(parent, text_key, command, icon_name=""):
        text = launcher._t(text_key)
        btn = tk.Button(parent, text=f"  {text}", anchor="w",
                       bg=bg_primary,
                       fg=launcher._get_theme_color('fg_secondary'),
                       activebackground=launcher._get_theme_color('bg_hover'),
                       activeforeground=launcher._get_theme_color('fg_primary'),
                       relief="flat", font=("Segoe UI", 11),
                       padx=16, pady=10, cursor="hand2", bd=0)
        
        if icon_name:
             try:
                 icon_img = launcher._load_themed_icon(icon_name)
                 if icon_img:
                     btn.config(image=icon_img, compound="left")  # type: ignore
                     btn._icon_ref = icon_img  # type: ignore
             except Exception as e:
                 print(f"Error loading icon {icon_name}: {e}")

        def on_click():
            command()
            highlight_modern_button(btn)
            
        btn.config(command=on_click)
        btn.pack(fill="x", pady=1)
        launcher._settings_nav_buttons.append(btn)
        launcher._settings_nav_buttons_data.append((btn, icon_name, text_key))
        return btn

    def highlight_modern_button(active_btn):
        for btn in launcher._settings_nav_buttons:
            if btn == active_btn:
                btn.config(bg=launcher._get_theme_color('bg_hover'),
                          fg=launcher._get_theme_color('fg_primary'),
                          font=("Segoe UI", 11, "bold"))
            else:
                btn.config(bg=bg_primary,
                          fg=launcher._get_theme_color('fg_secondary'),
                          font=("Segoe UI", 11, "normal"))
    
    general_btn = create_modern_nav_button(nav_buttons_frame, "SETTINGS_NAV_GENERAL", 
                                          lambda: show_content(_build_general_page, "general"), "general")
    accounts_btn = create_modern_nav_button(nav_buttons_frame, "SETTINGS_NAV_ACCOUNTS", 
                                           lambda: show_content(_build_accounts_page, "accounts"), "accounts")
    advanced_btn = create_modern_nav_button(nav_buttons_frame, "SETTINGS_NAV_ADVANCED", 
                                           lambda: show_content(_build_advanced_page, "advanced"), "advanced")
    about_btn = create_modern_nav_button(nav_buttons_frame, "SETTINGS_NAV_ABOUT", 
                                        lambda: show_content(_build_about_page, "about"), "about")
    
    show_content(_build_general_page, "general")
    highlight_modern_button(general_btn)

def _build_general_page(parent, launcher):
    bg_primary = launcher._get_theme_color('bg_primary')
    
    lang_card = _create_modern_card(parent, launcher._t("SETTINGS_CARD_LANGUAGE"), launcher)
    lang_label = tk.Label(lang_card, text=launcher._t("SETTINGS_LANG_LABEL"),
                         bg=bg_primary, 
                         fg=launcher._get_theme_color('fg_secondary'), 
                         font=("Segoe UI", 10, "bold"))
    lang_label.pack(anchor="w", pady=(0, 8))
    launcher.language_var = tk.StringVar(value=launcher.current_locale)
    lang_cb = ttk.Combobox(lang_card,
                          textvariable=launcher.language_var,
                          state="readonly",
                          width=40,
                          style="Modern.TCombobox",
                          font=("Segoe UI", 10))
    lang_names = {
        'en-US': 'English (United States)',
        'lt-LT': 'Lietuvių (Lithuania)', 
        'ru-RU': 'Русский (Russia)',
        'pl-PL': 'Polski (Poland)',
        'de-DE': 'Deutsch (Germany)',
        'lv-LV': 'Latviešu (Latvia)',
        'na-NA': 'For Translators'
    }
    display_values = []
    launcher._lang_code_map = {}
    for code in launcher.locales:
        display_name = lang_names.get(code, code)
        display_values.append(display_name)
        launcher._lang_code_map[display_name] = code
    lang_cb['values'] = display_values
    current_display = lang_names.get(launcher.current_locale, launcher.current_locale)
    lang_cb.set(current_display)
    lang_cb.pack(anchor="w", pady=(0, 8))
    def on_lang_change(event=None):
        selected_display = lang_cb.get()
        selected_code = launcher._lang_code_map.get(selected_display)
        if selected_code and selected_code != launcher.current_locale:
            try:
                _save_language_preference(selected_code)
                messagebox.showinfo(
                    launcher._t("LANGUAGE_CHANGED_TITLE"),
                    "Language preference saved. Restart the launcher to apply the change."
                )
            except Exception as e:
                messagebox.showerror(launcher._t("ERROR"), str(e))
    lang_cb.bind("<<ComboboxSelected>>", on_lang_change)
    warning = tk.Label(lang_card,
                      text=launcher._t("SETTINGS_LANG_WARNING"),
                      bg=bg_primary,
                      fg=launcher._get_theme_color('accent_primary'),
                      font=("Segoe UI", 9, "italic"))
    warning.pack(anchor="w", pady=(0, 16))
    theme_card = _create_modern_card(parent, launcher._t("SETTINGS_CARD_THEME"), launcher)
    theme_desc = tk.Label(theme_card, text=launcher._t("SETTINGS_THEME_DESC"),
                         bg=bg_primary, 
                         fg=launcher._get_theme_color('fg_secondary'), 
                         font=("Segoe UI", 10))
    theme_desc.pack(anchor="w", pady=(0, 16))
    if not hasattr(launcher, 'selected_theme'):
        launcher.selected_theme = tk.StringVar(value=_load_theme_preference())
    themes_grid = tk.Frame(theme_card, bg=bg_primary)
    themes_grid.pack(fill="x", pady=(0, 16))
    themes = [
        ("Arc", launcher._t("THEME_ARC"), "#363636"),
        ("Dark Prism", launcher._t("THEME_DARK_PRISM"), "#000000"),
        ("Light Mode", launcher._t("THEME_LIGHT"), "#f0f0f0")
    ]
    for i, (theme_name, description, bg_color) in enumerate(themes):
        _create_modern_theme_button(themes_grid, launcher, theme_name, description, bg_color)

    gpu_card = _create_modern_card(parent, launcher._t("SETTINGS_GPU_SECTION"), launcher)
    gpu_row = tk.Frame(gpu_card, bg=bg_primary)
    gpu_row.pack(fill="x", pady=(0, 16))
    gpu_info = tk.Frame(gpu_row, bg=bg_primary)
    gpu_info.pack(side="left", fill="both", expand=True, padx=(0, 16))
    tk.Label(gpu_info, text=launcher._t("SETTINGS_GPU_DRI_PRIME"), bg=bg_primary,
             fg=launcher._get_theme_color('fg_primary'),
             font=("Segoe UI", 11, "bold"), anchor="w").pack(anchor="w")
    tk.Label(gpu_info, text=launcher._t("SETTINGS_GPU_DRI_PRIME_DESC"),
             bg=bg_primary, fg=launcher._get_theme_color('fg_secondary'),
             font=("Segoe UI", 9), anchor="w", wraplength=450, justify="left").pack(anchor="w", pady=(2, 0))
    if not hasattr(launcher, 'use_dri_prime'):
        launcher.use_dri_prime = tk.BooleanVar(value=False)
    ToggleSwitch(gpu_row, variable=launcher.use_dri_prime,
                 command=lambda: _save_settings(launcher), bg=bg_primary).pack(side="right", anchor="center")

    sharing_card = _create_modern_card(parent, launcher._t("SETTINGS_SHARED_FILES"), launcher)
    tk.Label(sharing_card,
             text=launcher._t("SETTINGS_SHARED_FILES_DESC"),
             bg=bg_primary, fg=launcher._get_theme_color('fg_secondary'),
             font=("Segoe UI", 9), justify="left", wraplength=500).pack(anchor="w", pady=(0, 12))

    _share_rows = [
        ("share_options",       launcher._t("SETTINGS_SHARED_OPTIONS_TXT"),       launcher._t("SETTINGS_SHARED_OPTIONS_DESC")),
        ("share_resourcepacks", launcher._t("SETTINGS_SHARED_RESOURCEPACKS"),    launcher._t("SETTINGS_SHARED_RESOURCEPACKS_DESC")),
        ("share_shaderpacks",   launcher._t("SETTINGS_SHARED_SHADERPACKS"),      launcher._t("SETTINGS_SHARED_SHADERPACKS_DESC")),
        ("share_servers",       launcher._t("SETTINGS_SHARED_SERVERS"),          launcher._t("SETTINGS_SHARED_SERVERS_DESC")),
    ]
    for attr, title, desc in _share_rows:
        if not hasattr(launcher, attr):
            setattr(launcher, attr, tk.BooleanVar(value=False))
        row = tk.Frame(sharing_card, bg=bg_primary)
        row.pack(fill="x", pady=(0, 8))
        info = tk.Frame(row, bg=bg_primary)
        info.pack(side="left", fill="both", expand=True, padx=(0, 16))
        tk.Label(info, text=title, bg=bg_primary,
                 fg=launcher._get_theme_color('fg_primary'),
                 font=("Segoe UI", 10, "bold"), anchor="w").pack(anchor="w")
        tk.Label(info, text=desc, bg=bg_primary,
                 fg=launcher._get_theme_color('fg_secondary'),
                 font=("Segoe UI", 9), anchor="w").pack(anchor="w", pady=(2, 0))
        ToggleSwitch(row, variable=getattr(launcher, attr),
                     command=lambda: _on_share_toggle(launcher),
                     bg=bg_primary).pack(side="right", anchor="center")

    apply_btn = tk.Button(sharing_card, text=launcher._t("SETTINGS_SHARED_APPLY_ALL"),
                          bg=launcher._get_theme_color('bg_tertiary'),
                          fg=launcher._get_theme_color('fg_primary'),
                          font=("Segoe UI", 9), bd=0, padx=12, pady=6, cursor="hand2", relief="flat",
                          command=lambda: threading.Thread(
                              target=lambda: (launcher._apply_sharing_all(),
                                              launcher.after(0, lambda: messagebox.showinfo(launcher._t("SETTINGS_SHARED_FILES"), launcher._t("SETTINGS_SHARED_UPDATED")))),
                              daemon=True).start())
    apply_btn.pack(anchor="w", pady=(8, 0))

def _build_advanced_page(parent, launcher):
    bg_color = launcher._get_theme_color('bg_primary')
    
    def create_setting_row(parent_card, title, description, variable, command=None):
        row = tk.Frame(parent_card, bg=bg_color)
        row.pack(fill="x", pady=(0, 16))
        
        info_col = tk.Frame(row, bg=bg_color)
        info_col.pack(side="left", fill="both", expand=True, padx=(0, 16))
        
        lbl = tk.Label(info_col, text=title, bg=bg_color, fg=launcher._get_theme_color('fg_primary'),
                      font=("Segoe UI", 11, "bold"), anchor="w")
        lbl.pack(anchor="w")
        
        if description:
            desc = tk.Label(info_col, text=description, bg=bg_color, fg=launcher._get_theme_color('fg_secondary'),
                           font=("Segoe UI", 9), anchor="w", justify="left", wraplength=450)
            desc.pack(anchor="w", pady=(2, 0))
            
        switch_col = tk.Frame(row, bg=bg_color)
        switch_col.pack(side="right", anchor="center")
        
        def on_toggle():
            if command: command()
            
        switch = ToggleSwitch(switch_col, variable=variable, command=on_toggle, bg=bg_color)
        switch.pack()
        return row

    discord_card = _create_modern_card(parent, launcher._t("SETTINGS_CARD_DISCORD"), launcher)
    if not hasattr(launcher, 'discord_rpc_enabled'):
        launcher.discord_rpc_enabled = tk.BooleanVar(value=True)
        
    create_setting_row(discord_card, 
                      launcher._t("SETTINGS_DISCORD_ENABLE"),
                      launcher._t("SETTINGS_DISCORD_DESC"),
                      launcher.discord_rpc_enabled,
                      lambda: _save_and_apply(launcher, lambda: _toggle_discord_rpc(launcher)))

    telemetry_card = _create_modern_card(parent, launcher._t("SETTINGS_CARD_TELEMETRY"), launcher)
    if not hasattr(launcher, 'delete_telemetry_on_startup'):
        launcher.delete_telemetry_on_startup = tk.BooleanVar(value=False)
        
    create_setting_row(telemetry_card,
                      launcher._t("SETTINGS_TELEMETRY_DELETE"),
                      launcher._t("SETTINGS_TELEMETRY_DESC"),
                      launcher.delete_telemetry_on_startup,
                      lambda: _save_settings(launcher))

    plugins_card = _create_modern_card(parent, launcher._t("SETTINGS_CARD_PLUGINS"), launcher)
    plugins_desc = tk.Label(plugins_card,
                           text=launcher._t("SETTINGS_PLUGINS_DESC"),
                           bg=bg_color,
                           fg=launcher._get_theme_color('fg_secondary'),
                           font=("Segoe UI", 10))
    plugins_desc.pack(anchor="w", pady=(0, 12))
    
    plugins_container = tk.Frame(plugins_card, 
                                bg=launcher._get_theme_color('bg_secondary'),
                                relief="flat", bd=1)
    plugins_container.pack(fill="x", pady=(0, 12))
    
    plugins_canvas = tk.Canvas(plugins_container, height=120, 
                              bg=launcher._get_theme_color('bg_secondary'), highlightthickness=0)
    plugins_scrollbar = ttk.Scrollbar(plugins_container, orient="vertical", command=plugins_canvas.yview, style="Modern.Vertical.TScrollbar")
    
    launcher._plugins_list_frame = tk.Frame(plugins_canvas, bg=launcher._get_theme_color('bg_secondary'))
    launcher._plugins_list_frame.bind("<Configure>", lambda e: plugins_canvas.configure(scrollregion=plugins_canvas.bbox("all")))
    
    plugins_canvas.create_window((0, 0), window=launcher._plugins_list_frame, anchor="nw")
    plugins_canvas.configure(yscrollcommand=plugins_scrollbar.set)
    plugins_canvas.pack(side="left", fill="both", expand=True, padx=8, pady=8)
    
    plugin_buttons = tk.Frame(plugins_container, bg=launcher._get_theme_color('bg_secondary'))
    plugin_buttons.pack(fill="x", padx=8, pady=(0, 8))

    add_plugin_icon = launcher._load_themed_icon("plus", size=(16, 16))
    add_plugin_btn = tk.Button(plugin_buttons, 
                               text=f"  {launcher._t('SETTINGS_PLUGIN_ADD')}", 
                               image=add_plugin_icon,
                               compound="left",
                               bg=launcher._get_theme_color('accent_primary'),
                               fg=launcher._get_theme_color('fg_primary'),
                               activebackground=launcher._get_theme_color('accent_hover'),
                               activeforeground=launcher._get_theme_color('fg_primary'),
                               font=("Segoe UI", 10), relief="flat", bd=0, padx=12, pady=8, cursor="hand2",
                               command=lambda: _add_plugin_file(launcher))
    add_plugin_btn.image = add_plugin_icon  # type: ignore
    add_plugin_btn.pack(side="left", padx=(0, 8))
    
    refresh_plugin_icon = launcher._load_themed_icon("refresh", size=(16, 16))
    refresh_plugins_btn = tk.Button(plugin_buttons, 
                                    text=f"  {launcher._t('SETTINGS_PLUGIN_REFRESH')}", 
                                    image=refresh_plugin_icon,
                                    compound="left",
                                    bg=launcher._get_theme_color('bg_hover'),
                                    fg=launcher._get_theme_color('fg_primary'),
                                    activebackground=launcher._get_theme_color('bg_pressed'),
                                    activeforeground=launcher._get_theme_color('fg_primary'),
                                    font=("Segoe UI", 9), relief="flat", bd=0, padx=10, pady=6, cursor="hand2",
                                    command=lambda: _refresh_plugins_runtime(launcher))
    refresh_plugins_btn.image = refresh_plugin_icon  # type: ignore
    refresh_plugins_btn.pack(side="left")
    _refresh_plugins_list(launcher)

    progress_card = _create_modern_card(parent, launcher._t("SETTINGS_PROGRESS_BAR"), launcher)
    if not hasattr(launcher, 'show_progress_bar'):
        launcher.show_progress_bar = tk.BooleanVar(value=False)
    
    def toggle_progress_bar():
        _save_settings(launcher)
        if hasattr(launcher, 'status_bar_frame') and launcher.status_bar_frame is not None:
            if launcher.show_progress_bar.get():
                launcher.status_bar_frame.pack(fill="x", side="bottom", pady=(0, 4))
            else:
                launcher.status_bar_frame.pack_forget()
            
    create_setting_row(progress_card,
                      launcher._t("SETTINGS_SHOW_PROGRESS_BAR"),
                      launcher._t("SETTINGS_SHOW_PROGRESS_BAR_DESC"),
                      launcher.show_progress_bar,
                      toggle_progress_bar)

    debug_card = _create_modern_card(parent, launcher._t("SETTINGS_CARD_DEBUG"), launcher)
    if not hasattr(launcher, 'debug_mode_enabled'):
        launcher.debug_mode_enabled = tk.BooleanVar(value=False)
        
    create_setting_row(debug_card,
                      launcher._t("SETTINGS_DEBUG_ENABLE"),
                      launcher._t("SETTINGS_DEBUG_DESC"),
                      launcher.debug_mode_enabled,
                      lambda: _toggle_debug_mode(launcher))
                      
    debug_info_frame = tk.Frame(debug_card, bg=bg_color)
    debug_info_frame.pack(fill="x")
    
    if launcher.debug_mode_enabled.get():
        debug_info = tk.Text(debug_info_frame, 
                           height=8, 
                           bg=launcher._get_theme_color('bg_secondary'),
                           fg=launcher._get_theme_color('fg_primary'),
                           font=("Consolas", 8),
                           wrap=tk.WORD,
                           state=tk.DISABLED,
                           bd=0, highlightthickness=0)
        debug_info.pack(fill="x", pady=(0, 8))
        launcher._debug_text_widget = debug_info
        
        refresh_btn = ttk.Button(debug_info_frame, 
                               text=launcher._t("SETTINGS_DEBUG_REFRESH"), 
                               style="Secondary.TButton",
                               command=lambda: _update_debug_info(launcher))
        refresh_btn.pack(anchor="w", pady=(0, 8))
        
        show_spawn_btn = ttk.Button(debug_info_frame,
                       text=launcher._t("SETTINGS_DEBUG_SHOW_SPAWN_LOG"),
                       style="Secondary.TButton",
                       command=lambda: _show_spawn_log(launcher))
        show_spawn_btn.pack(anchor="w", pady=(0, 4))
        
        _update_debug_info(launcher)
    
    def _show_spawn_log(launcher):
        try:
            if not hasattr(launcher, "debug_mode_enabled"):
                launcher.debug_mode_enabled = tk.BooleanVar(value=False)

            debug_capture_active = isinstance(sys.stdout, StreamCapture) and hasattr(launcher, "_current_log_file")
            if not debug_capture_active:
                launcher.debug_mode_enabled.set(True)
                _toggle_debug_mode(launcher)

            log_path = Path(getattr(launcher, "_current_log_file", "") or "")
            if not log_path:
                log_dir = Path.home() / ".local" / "share" / "oranglauncher" / "logs"
                log_dir.mkdir(parents=True, exist_ok=True)
                log_path = log_dir / f"launcher_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
                launcher._current_log_file = str(log_path)

            log_path.parent.mkdir(parents=True, exist_ok=True)
            with open(log_path, "a", encoding="utf-8") as f:
                f.write(f"\n=== DEBUG SNAPSHOT {datetime.now().isoformat()} ===\n")
                if getattr(launcher, "_log_buffer", None):
                    for msg, _ in launcher._log_buffer:
                        f.write(msg.rstrip("\n") + "\n")
                else:
                    f.write("No buffered log entries available.\n")

            if not _open_terminal_tailing_file(log_path):
                messagebox.showerror(
                    launcher._t("ERROR"),
                    f"Could not open a terminal to show debug logs.\n{log_path}"
                )
        except Exception as e:

            messagebox.showerror(launcher._t("ERROR"), f"Failed to read spawn log: {e}")
            pass

    java_card = _create_modern_card(parent, "Java Runtimes", launcher)
    _build_java_management(java_card, launcher)

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
        # check apt
        if shutil.which("apt-get"):
            pkg = "default-jre" if major == 8 else f"openjdk-{major}-jre"
            status_fn(f"Trying apt-get install {pkg}…")
            r = subprocess.run(["pkexec", "apt-get", "install", "-y", pkg],
                               capture_output=True, text=True)
            if r.returncode == 0:
                done_fn(True, f"Installed via apt ({pkg})")
                return
        # Adoptium download
        status_fn(f"Downloading Java {major} from Adoptium…")
        path = download_java_runtime(major, progress_callback=lambda p, m: status_fn(m))
        if path:
            done_fn(True, f"Downloaded to {path}")
        else:
            done_fn(False, f"Failed — check your internet connection")
    threading.Thread(target=work, daemon=True).start()

def _build_java_management(card, launcher):
    bg = launcher._get_theme_color('bg_primary')
    acc = launcher._get_theme_color('accent_primary')
    fg = launcher._get_theme_color('fg_primary')
    fgs = launcher._get_theme_color('fg_secondary')

    tk.Label(card, text="Install or update Java runtimes. The launcher checks pacman / apt first,\n"
             "then downloads directly from Adoptium if needed.",
             bg=bg, fg=fgs, font=("Segoe UI", 9), justify="left").pack(anchor="w", pady=(0, 12))

    rows_frame = tk.Frame(card, bg=bg)
    rows_frame.pack(fill="x")

    versions = [8, 17, 21, 25]
    for major in versions:
        row = tk.Frame(rows_frame, bg=bg)
        row.pack(fill="x", pady=5)

        # detect status
        path = find_java_executable(major)
        if path:
            status_text = f"Java {major}  ✓  {path}"
            status_color = acc
        else:
            status_text = f"Java {major}  —  not found"
            status_color = fgs

        status_lbl = tk.Label(row, text=status_text, bg=bg, fg=status_color,
                              font=("Segoe UI", 10), anchor="w")
        status_lbl.pack(side="left", fill="x", expand=True)

        def make_install(m, lbl):
            def on_install():
                lbl.config(text=f"Java {m}  …  working", fg=fgs)
                def set_status(msg):
                    try: lbl.config(text=f"Java {m}  —  {msg}")
                    except Exception: pass
                def on_done(ok, msg):
                    color = acc if ok else launcher._get_theme_color('fg_disabled')
                    try:
                        p = find_java_executable(m)
                        lbl.config(text=f"Java {m}  {'✓' if ok else '✗'}  {p or msg}", fg=color)
                    except Exception: pass
                _install_java_pm_or_download(m, set_status, on_done)
            return on_install

        tk.Button(row, text="Install / Update", command=make_install(major, status_lbl),
                  bg=launcher._get_theme_color('bg_tertiary'), fg=fg,
                  font=("Segoe UI", 9), bd=0, relief="flat", padx=12, pady=4,
                  cursor="hand2", activebackground=launcher._get_theme_color('bg_hover'),
                  activeforeground=fg).pack(side="right")

def _build_accounts_page(parent, launcher):
    bg_primary = launcher._get_theme_color('bg_primary')
    account_card = _create_modern_card(parent, launcher._t("SETTINGS_ACCOUNTS_TITLE"), launcher)
    account_desc = tk.Label(account_card, text=launcher._t("SETTINGS_ACCOUNTS_DESC"),
                           bg=bg_primary, 
                           fg=launcher._get_theme_color('fg_secondary'), 
                           font=("Segoe UI", 10))
    account_desc.pack(anchor="w", pady=(0, 20))
    accounts_container = tk.Frame(account_card, 
                                bg=launcher._get_theme_color('bg_secondary'),
                                relief="flat",
                                bd=1,
                                highlightthickness=1,
                                highlightcolor=launcher._get_theme_color('border_primary'),
                                highlightbackground=launcher._get_theme_color('border_primary'))
    accounts_container.pack(fill="x", pady=(0, 12))
    canvas = tk.Canvas(accounts_container, height=300, bg=launcher._get_theme_color('bg_secondary'), highlightthickness=0)
    scrollbar = ttk.Scrollbar(accounts_container, orient="vertical", command=canvas.yview, style="Modern.Vertical.TScrollbar")
    launcher._accounts_list_frame = tk.Frame(canvas, bg=launcher._get_theme_color('bg_secondary'))
    launcher._accounts_list_frame.bind(
        "<Configure>",
        lambda e: canvas.configure(scrollregion=canvas.bbox("all"))
    )
    canvas.create_window((0, 0), window=launcher._accounts_list_frame, anchor="nw")
    canvas.configure(yscrollcommand=scrollbar.set)
    canvas.pack(side="left", fill="both", expand=True, padx=8, pady=8)
    scrollbar.pack(side="right", fill="y", pady=8, padx=(0, 4))
    _refresh_accounts_list(launcher)
    btn_frame = tk.Frame(account_card, bg=launcher._get_theme_color('bg_primary'))
    btn_frame.pack(fill="x")
    
    ms_icon = launcher._load_themed_icon("microsoft", size=(16, 16))
    add_ms_btn = tk.Button(btn_frame, 
                           text=f"  {launcher._t('SETTINGS_ACCOUNTS_ADD_MS')}",
                           image=ms_icon,
                           compound="left",
                           bg=launcher._get_theme_color('bg_hover'),
                           fg=launcher._get_theme_color('fg_primary'),
                           activebackground=launcher._get_theme_color('bg_pressed'),
                           activeforeground=launcher._get_theme_color('fg_primary'),
                           font=("Segoe UI", 10),
                           relief="flat",
                           bd=0,
                           padx=12,
                           pady=8,
                           cursor="hand2",
                           command=lambda: _add_microsoft_account(launcher))
    add_ms_btn.image = ms_icon  # type: ignore
    add_ms_btn.pack(side="left", padx=(0, 8))
    
    offline_icon = launcher._load_themed_icon("offline", size=(16, 16))
    add_offline_btn = tk.Button(btn_frame, 
                               text=f"  {launcher._t('SETTINGS_ACCOUNTS_ADD_OFFLINE')}",
                               image=offline_icon,
                               compound="left",
                               bg=launcher._get_theme_color('bg_hover'),
                               fg=launcher._get_theme_color('fg_primary'),
                               activebackground=launcher._get_theme_color('bg_pressed'),
                               activeforeground=launcher._get_theme_color('fg_primary'),
                               font=("Segoe UI", 10),
                               relief="flat",
                               bd=0,
                               padx=12,
                               pady=8,
                               cursor="hand2",
                               command=lambda: _add_offline_account(launcher))
    add_offline_btn.image = offline_icon  # type: ignore
    add_offline_btn.pack(side="left")
def _build_about_page(parent, launcher):
    bg_primary = launcher._get_theme_color('bg_primary')
    about_card = _create_modern_card(parent, launcher._t("SETTINGS_ABOUT_TITLE"), launcher)
    
    about_inner = tk.Frame(about_card, bg=bg_primary)
    about_inner.pack(fill="x")
    
    try:
        logo_path = find_resource("oranglauncher/images/orange.png")
        if logo_path:
            img = Image.open(logo_path).resize((64, 64), Image.Resampling.LANCZOS)
            launcher._about_logo_img = ImageTk.PhotoImage(img)
            logo_frame = tk.Frame(about_inner, bg=bg_primary)
            logo_frame.pack(side="left", anchor="n", padx=(0, 24))
            
            logo_lbl = tk.Label(logo_frame, image=launcher._about_logo_img, bg=bg_primary, cursor="hand2")
            logo_lbl.pack()
            
            def on_logo_click(event):
                launcher._toggle_music()
            
            logo_lbl.bind("<Button-1>", on_logo_click)
            logo_lbl.bind("<Enter>", lambda e: logo_lbl.config(bg=launcher._get_theme_color('bg_hover')))
            logo_lbl.bind("<Leave>", lambda e: logo_lbl.config(bg=bg_primary))
    except Exception:
        pass

    info_frame = tk.Frame(about_inner, bg=bg_primary)
    info_frame.pack(side="left", fill="both", expand=True)

    name_label = tk.Label(info_frame, text="OrangLauncher", 
                         bg=bg_primary, 
                         fg=launcher._get_theme_color('fg_primary'),
                         font=("Segoe UI", 16, "bold"))
    name_label.pack(anchor="w", pady=(0, 8))
    
    version_label = tk.Label(info_frame, text=f"Version: {CURRENT_VERSION}", 
                           bg=bg_primary, 
                           fg=launcher._get_theme_color('fg_secondary'),
                           font=("Segoe UI", 11))
    version_label.pack(anchor="w", pady=(0, 4))
    
    author_label = tk.Label(info_frame, text=launcher._t("SETTINGS_ABOUT_AUTHOR"), 
                          bg=bg_primary, 
                          fg=launcher._get_theme_color('fg_secondary'),
                          font=("Segoe UI", 10))
    author_label.pack(anchor="w", pady=(0, 16))
    
    desc_label = tk.Label(info_frame, 
                         text=launcher._t("SETTINGS_ABOUT_DESC"),
                         bg=bg_primary, 
                         fg=launcher._get_theme_color('fg_disabled'),
                         font=("Segoe UI", 9),
                         wraplength=400,
                         justify="left")
    desc_label.pack(anchor="w", pady=(0, 20))
    
    buttons_frame = tk.Frame(about_card, bg=bg_primary)
    buttons_frame.pack(fill="x", pady=(16, 0))
    
    update_icon = launcher._load_themed_icon("update", size=(16, 16))
    update_btn = tk.Button(buttons_frame, 
                          text=f"  {launcher._t('SETTINGS_ABOUT_CHECK_UPDATES')}", 
                          image=update_icon,
                          compound="left",
                          bg=launcher._get_theme_color('accent_primary'),
                          fg=launcher._get_theme_color('fg_primary'),
                          activebackground=launcher._get_theme_color('accent_hover'),
                          activeforeground=launcher._get_theme_color('fg_primary'),
                          font=("Segoe UI", 10),
                          relief="flat",
                          bd=0,
                          padx=12,
                          pady=8,
                          cursor="hand2",
                          command=lambda: show_update_dialog(parent, launcher))
    update_btn.image = update_icon  # type: ignore
    update_btn.pack(side="left", padx=(0, 8))
    
    github_icon = launcher._load_themed_icon("github", size=(16, 16))
    github_btn = tk.Button(buttons_frame, 
                          text=f"  {launcher._t('SETTINGS_ABOUT_GITHUB')}", 
                          image=github_icon,
                          compound="left",
                          bg=launcher._get_theme_color('bg_hover'),
                          fg=launcher._get_theme_color('fg_primary'),
                          activebackground=launcher._get_theme_color('bg_pressed'),
                          activeforeground=launcher._get_theme_color('fg_primary'),
                          font=("Segoe UI", 9),
                          relief="flat",
                          bd=0,
                          padx=10,
                          pady=6,
                          cursor="hand2",
                          command=lambda: webbrowser.open("https://github.com/adasjusk/OrangLaunch"))
    github_btn.image = github_icon  # type: ignore
    github_btn.pack(side="left")

    _build_source_package_note(parent, launcher)

def _build_source_package_note(parent, launcher):
    bg_primary = launcher._get_theme_color('bg_primary')
    accent = launcher._get_theme_color('accent_primary')
    container = tk.Frame(parent, bg=bg_primary)
    container.pack(fill="x", pady=(0, 20), padx=4)
    card = tk.Frame(container, bg=launcher._get_theme_color('bg_secondary'),
                    highlightthickness=0, bd=0)
    card.pack(fill="x")
    strip = tk.Frame(card, bg=accent, width=4)
    strip.pack(side="left", fill="y")
    inner = tk.Frame(card, bg=launcher._get_theme_color('bg_secondary'))
    inner.pack(side="left", fill="both", expand=True, padx=16, pady=14)
    tk.Label(inner, text="New in 6.1.5  ·  Build-from-source package",
             bg=launcher._get_theme_color('bg_secondary'), fg=accent,
             font=("Segoe UI", 11, "bold")).pack(anchor="w")
    tk.Label(inner,
             text=("There's now an "
                   "oranglauncher"
                   " AUR package that builds the launcher with Nuitka on your "
                   "machine instead of shipping a prebuilt binary. Prefer compiling "
                   "yourself? Install that one. Want the quick prebuilt? Keep "
                   "oranglauncher-bin."),
             bg=launcher._get_theme_color('bg_secondary'),
             fg=launcher._get_theme_color('fg_secondary'),
             font=("Segoe UI", 9), wraplength=440, justify="left").pack(anchor="w", pady=(6, 10))
    aur_btn = tk.Button(inner, text="View on AUR",
                        bg=accent, fg="#ffffff",
                        activebackground=launcher._get_theme_color('accent_hover'),
                        activeforeground="#ffffff",
                        font=("Segoe UI", 9, "bold"), relief="flat", bd=0,
                        padx=12, pady=6, cursor="hand2",
                        command=lambda: webbrowser.open("https://aur.archlinux.org/packages/oranglauncher"))
    aur_btn.pack(anchor="w")

def _build_experimental_page(parent, launcher):
    title = tk.Label(parent, text=launcher._t("SETTINGS_EXP_PAGE_TITLE"), bg=launcher._get_theme_color('bg_primary'), fg=launcher._get_theme_color('fg_primary'),
                    font=("Segoe UI", 18, "bold"))
    title.pack(anchor="w", pady=(0, 20))
    subtitle = tk.Label(parent, text=launcher._t("SETTINGS_EXP_PAGE_DESC"),
                       bg=launcher._get_theme_color('bg_primary'), fg=launcher._get_theme_color('accent_primary'), font=("Segoe UI", 10, "italic"))
    subtitle.pack(anchor="w", pady=(0, 20))
    telemetry_section = _create_modern_section(parent, launcher._t("SETTINGS_EXP_TELEMETRY"), launcher)
    if not hasattr(launcher, 'delete_telemetry_on_startup'):
        launcher.delete_telemetry_on_startup = tk.BooleanVar(value=False)
    telemetry_check = tk.Checkbutton(
        telemetry_section,
        text=launcher._t("DELETE_TELEMETRY"),
        variable=launcher.delete_telemetry_on_startup,
        bg=launcher._get_theme_color('bg_section'),
        fg=launcher._get_theme_color('fg_primary'),
        selectcolor=launcher._get_theme_color('bg_input'),
        activebackground=launcher._get_theme_color('bg_section'),
        activeforeground=launcher._get_theme_color('fg_primary'),
        font=("Segoe UI", 10),
        command=lambda: _save_settings(launcher)
    )
    telemetry_check.pack(anchor="w", pady=(0, 5))
    telemetry_desc = tk.Label(
        telemetry_section,
        text=launcher._t("DELETE_TELEMETRY_DESC"),
        bg=launcher._get_theme_color('bg_section'),
        fg=launcher._get_theme_color('fg_tertiary'),
        font=("Segoe UI", 9),
        wraplength=500,
        justify="left"
    )
    telemetry_desc.pack(anchor="w", pady=(0, 10))
    plugins_section = _create_modern_section(parent, launcher._t("SETTINGS_EXP_PLUGINS"), launcher)
    plugins_container = tk.Frame(plugins_section, bg=launcher._get_theme_color('bg_section'))
    plugins_container.pack(fill="both", expand=True, pady=(0, 10))
    canvas = tk.Canvas(plugins_container, height=200, bg=launcher._get_theme_color('bg_section'), highlightthickness=0)
    scrollbar = ttk.Scrollbar(plugins_container, orient="vertical", command=canvas.yview, style="Modern.Vertical.TScrollbar")
    launcher._plugins_list_frame = tk.Frame(canvas, bg=launcher._get_theme_color('bg_section'))
    launcher._plugins_list_frame.bind(
        "<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all"))
    )
    canvas.create_window((0, 0), window=launcher._plugins_list_frame, anchor="nw")
    canvas.configure(yscrollcommand=scrollbar.set)
    canvas.pack(side="left", fill="both", expand=True)
    scrollbar.pack(side="right", fill="y")
    _refresh_plugins_list(launcher)
    btn_frame = tk.Frame(plugins_section, bg=launcher._get_theme_color('bg_section'))
    btn_frame.pack(fill="x", pady=(10, 0))
    add_btn = ttk.Button(btn_frame, text=launcher._t("SETTINGS_EXP_ADD_PLUGIN"), style="Settings.TButton", width=14,
                        command=lambda: _add_plugin_file(launcher))
    add_btn.pack(side="left", padx=(0, 8))
    refresh_btn = ttk.Button(btn_frame, text=launcher._t("SETTINGS_EXP_REFRESH_PLUGINS"), style="Settings.TButton", width=14,
                            command=lambda: _refresh_plugins_runtime(launcher))
    refresh_btn.pack(side="left")
def _create_modern_section(parent, title_text, launcher):
    bg_tertiary = launcher._get_theme_color('bg_tertiary')
    fg_primary = launcher._get_theme_color('fg_primary')
    section_frame = tk.Frame(parent, bg=bg_tertiary, bd=0)
    section_frame.pack(fill="x", pady=(0, 15))
    content = tk.Frame(section_frame, bg=bg_tertiary)
    content.pack(fill="x", padx=15, pady=15)
    if title_text:
        title_label = tk.Label(content, text=title_text, bg=bg_tertiary, fg=fg_primary,
                              font=("Segoe UI", 12, "bold"))
        title_label.pack(anchor="w", pady=(0, 10))
    return content
def _create_theme_button(parent, launcher, theme_name, image_path, bg_color):
    frame = tk.Frame(parent, bg="#363636")
    frame.pack(side="left", padx=5, pady=5)
    btn_frame = tk.Frame(frame, bg="#404040", bd=2, relief="solid")
    btn_frame.pack()
    try:
        images_dir = find_resource("images")
        if images_dir:
            full_path = os.path.join(str(images_dir), image_path)
        else:
            full_path = None
        if full_path and os.path.exists(full_path):
            try:
                
                img = Image.open(full_path)
                img = img.resize((100, 80), Image.Resampling.LANCZOS)
                photo = ImageTk.PhotoImage(img)
                img_label = tk.Label(btn_frame, image=photo, bg=bg_color, cursor="hand2")
                img_label.image = photo  # type: ignore
                img_label.pack()
                img_label.bind("<Button-1>", lambda e: _apply_theme(launcher, theme_name))
            except ImportError:
                color_label = tk.Label(btn_frame, bg=bg_color, width=12, height=4, cursor="hand2")
                color_label.pack()
                color_label.bind("<Button-1>", lambda e: _apply_theme(launcher, theme_name))
        else:
            color_label = tk.Label(btn_frame, bg=bg_color, width=12, height=4, cursor="hand2")
            color_label.pack()
            color_label.bind("<Button-1>", lambda e: _apply_theme(launcher, theme_name))
    except Exception as e:
        print(f"Error loading theme image: {e}")
        color_label = tk.Label(btn_frame, bg=bg_color, width=12, height=4, cursor="hand2")
        color_label.pack()
        color_label.bind("<Button-1>", lambda e: _apply_theme(launcher, theme_name))
    name_label = tk.Label(frame, text=theme_name, bg="#363636", fg="#e8e8e8",
                         font=("Segoe UI", 9))
    name_label.pack(pady=(5, 0))
    if launcher.selected_theme.get() == theme_name:
        btn_frame.config(bg="#ff8c00", bd=3)
def _apply_theme(launcher, theme_name):
    launcher.selected_theme.set(theme_name)
    if save_theme_preference(theme_name):
        try:
            messagebox.showinfo(
                launcher._t("THEME_CHANGED_TITLE"),
                "Theme preference saved. Restart the launcher to apply the change."
            )
        except Exception as e:
            print(f"Error notifying theme change: {e}")
    else:
        messagebox.showerror(launcher._t("ERROR"), launcher._t("THEME_SAVE_ERROR"))
def _load_theme_preference_legacy():
    return load_saved_theme()
def _add_microsoft_account(launcher):
    try:
        result = add_profile(parent=launcher)
        _refresh_accounts_list(launcher)
        if hasattr(launcher, '_refresh_profiles'):
            launcher._refresh_profiles()
    except Exception as e:
        messagebox.showerror(launcher._t("ERROR"), f"{launcher._t('MS_AUTH_FAIL')}\n{e}")
def _add_offline_account(launcher):
    username = themed_askstring(launcher._t("OFFLINE_ACCOUNT_TITLE"), launcher._t("OFFLINE_ACCOUNT_PROMPT"), parent=launcher, launcher=launcher)
    
    if username:
        try:
            offline_profile = {
                'username': username,
                'type': 'offline',
                'uuid': '00000000-0000-0000-0000-000000000000'
            }
            accounts = load_profiles()
            accounts.append(offline_profile)
            save_profiles(accounts)
            _refresh_accounts_list(launcher)
            if hasattr(launcher, '_refresh_profiles'):
                launcher._refresh_profiles()
            messagebox.showinfo(launcher._t("SUCCESS"), launcher._t("OFFLINE_ACCOUNT_ADDED").format(username=username))
        except Exception as e:
            messagebox.showerror(launcher._t("ERROR"), launcher._t("OFFLINE_ACCOUNT_FAIL").format(e=e))
def _get_plugins_config_path():
    config_dir = Path.home() / ".config" / "oranglauncher"
    config_dir.mkdir(parents=True, exist_ok=True)
    return config_dir / "launcher_plugins.json"

def _load_plugins():
    return []

def _save_plugins(plugins):
    pass
def _refresh_plugins_runtime(launcher):
    try:
        launcher._initialize_plugins()
        _refresh_plugins_list(launcher)
        messagebox.showinfo("Plugins Refreshed", "Native plugins have been reloaded successfully!")
    except Exception as e:
        messagebox.showerror("Plugin Error", f"Error refreshing plugins: {e}")
        print(f"[PLUGIN ERROR] Full traceback:\n{traceback.format_exc()}")
def _refresh_plugins_list(launcher):
    for widget in launcher._plugins_list_frame.winfo_children():
        widget.destroy()
    plugins = []
    if hasattr(launcher, 'loaded_plugins') and launcher.loaded_plugins:
        plugins = launcher.loaded_plugins
    
    if not plugins:
        no_plugin_label = tk.Label(launcher._plugins_list_frame,
                                   text=launcher._t("PLUGINS_NONE"),
                                   bg=launcher._get_theme_color('bg_secondary'),
                                   fg=launcher._get_theme_color('fg_disabled'),
                                   font=("Segoe UI", 9, "italic"))
        no_plugin_label.pack(anchor="w", pady=8, padx=8)
    else:
        for i, plugin_info in enumerate(plugins):
            if isinstance(plugin_info, dict):
                plugin_name = plugin_info.get('name', 'Unknown')
                plugin_type = plugin_info.get('type', 'unknown')
                plugin_path = plugin_info.get('path', 'N/A')
            else:
                plugin_name = str(plugin_info[0]) if isinstance(plugin_info, (tuple, list)) else str(plugin_info)
                plugin_type = "native"
                plugin_path = "built-in"
            
            plugin_container = tk.Frame(launcher._plugins_list_frame, 
                                       bg=launcher._get_theme_color('bg_tertiary'), 
                                       relief="flat",
                                       bd=1,
                                       highlightthickness=1,
                                       highlightcolor=launcher._get_theme_color('border_primary'),
                                       highlightbackground=launcher._get_theme_color('border_primary'))
            plugin_container.pack(fill="x", pady=2, padx=4)
            plugin_frame = tk.Frame(plugin_container, bg=launcher._get_theme_color('bg_tertiary'))
            plugin_frame.pack(fill="x", padx=8, pady=6)
            info_frame = tk.Frame(plugin_frame, bg=launcher._get_theme_color('bg_tertiary'))
            info_frame.pack(side="left", fill="x", expand=True)
            
            name_label = tk.Label(info_frame,
                                 text=f"{plugin_name} ({plugin_type})",
                                 bg=launcher._get_theme_color('bg_tertiary'),
                                 fg=launcher._get_theme_color('fg_primary'),
                                 font=("Segoe UI", 10, "bold"))
            name_label.pack(anchor="w")
            
            status_label = tk.Label(info_frame,
                                   text=f"Type: {plugin_type} | Active",
                                   bg=launcher._get_theme_color('bg_tertiary'),
                                   fg=launcher._get_theme_color('fg_secondary'),
                                   font=("Segoe UI", 8))
            status_label.pack(anchor="w")
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

def _remove_plugin(launcher, idx):
    info_text = (
        "To remove a native plugin:\n\n"
        "1. Go to your plugins directory:\n"
        f"   {Path.home() / '.local' / 'share' / 'oranglauncher' / 'plugins'}\n\n"
        "2. Delete the plugin's .py file\n\n"
        "3. Restart the launcher\n\n"
        "Built-in plugins cannot be removed."
    )
    messagebox.showinfo("Remove Plugin", info_text)

def _toggle_plugin_enabled(launcher, idx):
    messagebox.showinfo("Plugin Status", "Native plugins are managed by file system.\n\nPlace/remove .py files in the plugins directory to enable/disable.")
def _configure_modern_styles(style, tm=None):
    if tm is None:
        tm = get_theme_manager()
    style.configure("Modern.TFrame", 
                   background=tm.get_color('bg_tertiary'),
                   borderwidth=0,
                   relief="flat")
    style.configure("Modern.TButton",
                   background=tm.get_color('accent_primary'),
                   foreground=tm.get_color('fg_primary'),
                   borderwidth=0,
                   focuscolor="none",
                   font=("Segoe UI", 10, "normal"),
                   padding=(12, 8))
    style.map("Modern.TButton",
             background=[("active", tm.get_color('accent_hover')), 
                        ("pressed", tm.get_color('accent_pressed'))],
             foreground=[("active", tm.get_color('fg_primary')),
                        ("pressed", tm.get_color('fg_primary'))])
    style.configure("Secondary.TButton",
                   background=tm.get_color('bg_hover'),
                   foreground=tm.get_color('fg_primary'),
                   borderwidth=1,
                   focuscolor="none",
                   font=("Segoe UI", 9),
                   padding=(10, 6))
    style.map("Secondary.TButton",
             background=[("active", tm.get_color('bg_pressed')), 
                        ("pressed", tm.get_color('bg_section'))],
             foreground=[("active", tm.get_color('fg_primary')),
                        ("pressed", tm.get_color('fg_primary'))])
    style.configure("Modern.TCheckbutton",
                   background=tm.get_color('bg_tertiary'),
                   foreground=tm.get_color('fg_primary'),
                   focuscolor="none",
                   font=("Segoe UI", 10))
    style.map("Modern.TCheckbutton",
             background=[("active", tm.get_color('bg_tertiary'))],
             foreground=[("active", tm.get_color('fg_primary'))])
    style.configure("Modern.TEntry",
                   fieldbackground=tm.get_color('bg_input'),
                   background=tm.get_color('bg_input'),
                   foreground=tm.get_color('fg_primary'),
                   borderwidth=1,
                   relief="solid",
                   insertcolor=tm.get_color('fg_primary'),
                   font=("Segoe UI", 10))
    style.configure("Modern.TCombobox",
                   fieldbackground=tm.get_color('bg_input'),
                   background=tm.get_color('bg_input'),
                   foreground=tm.get_color('fg_primary'),
                   borderwidth=1,
                   arrowcolor=tm.get_color('fg_primary'),
                   font=("Segoe UI", 10))
    style.map("Modern.TCombobox",
             fieldbackground=[('readonly', tm.get_color('bg_input'))],
             selectbackground=[('readonly', tm.get_color('accent_primary'))],
             selectforeground=[('readonly', tm.get_color('fg_primary'))],
             foreground=[('readonly', tm.get_color('fg_primary'))],
             arrowcolor=[('disabled', tm.get_color('fg_disabled'))])
    try:
        if not hasattr(style, '_rounded_scrollbar_assets_loaded'):
            sb_width = 8
            sb_radius = sb_width // 2
            def create_pill_image(width, height, color, alpha=255):
                scale = 2
                w, h = width * scale, height * scale
                r = sb_radius * scale
                
                if color.startswith('#'):
                     color_rgb = tuple(int(color[i:i+2], 16) for i in (1, 3, 5)) 
                     fill_color = color_rgb + (alpha,)
                else:
                     fill_color = color
                
                image = Image.new('RGBA', (w, h), (0, 0, 0, 0))
                draw = ImageDraw.Draw(image)
                draw.rounded_rectangle([0, 0, w-1, h-1], radius=r, fill=fill_color)
                
                image = image.resize((width, height), Image.Resampling.LANCZOS)
                return ImageTk.PhotoImage(image)
            trough_color = tm.get_color('bg_secondary') 
            if trough_color.startswith('#'):
                pass
            thumb_color = "#606060" 
            active_thumb_color = "#909090"
            base_h = 32 
            
            style._trough_img = create_pill_image(sb_width, base_h, trough_color, alpha=0) # Invisible trough primarily
            style._thumb_img = create_pill_image(sb_width, base_h, thumb_color, alpha=180)
            style._thumb_active_img = create_pill_image(sb_width, base_h, active_thumb_color, alpha=220)
            
            style.element_create("Rounded.Vertical.Scrollbar.trough", "image", style._trough_img,
                               border=[0, sb_radius, 0, sb_radius], sticky="ns", padding=0)
            
            style.element_create("Rounded.Vertical.Scrollbar.thumb", "image", style._thumb_img,
                               ('active', style._thumb_active_img),
                               border=[0, sb_radius, 0, sb_radius], sticky="ns")
                               
            style._rounded_scrollbar_assets_loaded = True

    except Exception as e:
        print(f"Failed to create rounded scrollbar, falling back: {e}")

    try:
        style.layout("Modern.Vertical.TScrollbar", [
            ('Rounded.Vertical.Scrollbar.trough', {'children': [
                ('Rounded.Vertical.Scrollbar.thumb', {'unit': '1', 'children': [], 'sticky': 'nswe'})
            ], 'sticky': 'ns'})
        ])
    except tk.TclError:
         pass

    style.configure("Modern.Vertical.TScrollbar",
                   background=tm.get_color('bg_section'),
                   troughcolor=tm.get_color('bg_tertiary'),
                   borderwidth=0,
                   relief="flat",
                   width=8)
    
    style.layout("Modern.Horizontal.TScrollbar", [
        ('Horizontal.Scrollbar.trough', {'children': [
            ('Horizontal.Scrollbar.thumb', {'expand': '1', 'sticky': 'nswe'})
        ], 'sticky': 'ew'})
    ])
    style.configure("Modern.Horizontal.TScrollbar",
                   background=tm.get_color('bg_section'),
                   troughcolor=tm.get_color('bg_tertiary'),
                   borderwidth=0,
                   relief="flat",
                   width=8)
def _configure_enhanced_styles(style):
    _configure_modern_styles(style)
def _create_header(parent, launcher):
    header_frame = tk.Frame(parent, bg=launcher._get_theme_color('bg_primary'))
    header_frame.pack(fill="x", pady=(0, 16))
    title = tk.Label(header_frame, 
                    text=launcher._t("LAUNCHER_SETTINGS"),
                    bg=launcher._get_theme_color('bg_primary'),
                    fg=launcher._get_theme_color('fg_primary'),
                    font=("Segoe UI", 16, "bold"))
    title.pack(anchor="w")
    subtitle = tk.Label(header_frame,
                       text=launcher._t("LAUNCHER_SETTINGS_DESC"),
                       bg=launcher._get_theme_color('bg_primary'),
                       fg=launcher._get_theme_color('fg_tertiary'),
                       font=("Segoe UI", 10))
    subtitle.pack(anchor="w", pady=(2, 0))
    separator = tk.Frame(header_frame, height=2, bg="#ff8c00")
    separator.pack(fill="x", pady=(8, 0))
def _create_section_frame(parent, title_text):
    section_frame = tk.Frame(parent, bg="#363636", bd=1, relief="solid")
    section_frame.pack(fill="x", pady=(0, 12))
    title_bar = tk.Frame(section_frame, bg="#ff8c00", height=24)
    title_bar.pack(fill="x")
    title_bar.pack_propagate(False)
    title_label = tk.Label(title_bar,
                          text=title_text,
                          bg="#ff8c00",
                          fg="#000000",
                          font=("Segoe UI", 9, "bold"))
    title_label.pack(anchor="w", padx=10, pady=4)
    content_area = tk.Frame(section_frame, bg="#363636")
    content_area.pack(fill="x", padx=12, pady=12)
    return content_area
def _create_display_section(parent, launcher):
    content = _create_section_frame(parent, launcher._t("DISPLAY_OPTIONS"))
    status_cb = tk.Checkbutton(content,
                               text=launcher._t("SHOW_STATUS_BAR"),
                               variable=launcher.show_status_bar,
                               bg="#363636",
                               fg="#e8e8e8",
                               selectcolor="#404040",
                               activebackground="#363636",
                               activeforeground="#ffffff",
                               font=("Segoe UI", 9),
                               bd=0,
                               highlightthickness=0,
                               command=lambda: _save_and_apply(launcher, launcher._toggle_status_bar))
    status_cb.pack(anchor="w", pady=(0, 4))
    helper = tk.Label(content,
                     text=launcher._t("SHOW_PROGRESS_BAR"),
                     bg="#363636",
                     fg="#a0a0a0",
                     font=("Segoe UI", 8))
    helper.pack(anchor="w")
def _create_discord_section(parent, launcher):
    content = _create_section_frame(parent, launcher._t("DISCORD_RICH_PRESENCE"))
    launcher.discord_rpc_enabled = tk.BooleanVar(value=True)
    discord_cb = tk.Checkbutton(content,
                                text=launcher._t("ENABLE_DISCORD_RPC"),
                                variable=launcher.discord_rpc_enabled,
                                bg="#363636",
                                fg="#e8e8e8",
                                selectcolor="#404040",
                                activebackground="#363636",
                                activeforeground="#ffffff",
                                font=("Segoe UI", 9),
                                bd=0,
                                highlightthickness=0,
                                command=lambda: _save_and_apply(launcher, lambda: _toggle_discord_rpc(launcher)))
    discord_cb.pack(anchor="w", pady=(0, 4))
    helper = tk.Label(content,
                     text=launcher._t("DISCORD_RPC_DESC"),
                     bg="#363636",
                     fg="#a0a0a0",
                     font=("Segoe UI", 8))
    helper.pack(anchor="w")
def _toggle_discord_rpc(launcher):
    if launcher.discord_rpc_enabled.get():
        launcher._start_discord_rpc()
    else:
        launcher._stop_discord_rpc()
        # Vakarux, I removed the section and made it as easteregg
def _create_audio_section(launcher, parent):
    content = _create_section_frame(parent, launcher._t("MUSIC"))
    launcher.music_btn = ttk.Button(content,
                                   text=launcher._t("PLAY_MUSIC"),
                                   command=launcher._toggle_music,
                                   style="Settings.TButton",
                                   width=18)
    launcher.music_btn.pack(anchor="w", pady=(0, 4))
    helper = tk.Label(content,
                     text=launcher._t("PLAY_BACKGROUND_MUSIC"),
                     bg="#363636",
                     fg="#a0a0a0",
                     font=("Segoe UI", 8))
    helper.pack(anchor="w")
def _create_language_section(parent, launcher):
    content = _create_section_frame(parent, launcher._t("LANGUAGE_SETTINGS"))
    lang_label = tk.Label(content,
                         text=launcher._t("LANGUAGE") + ":",
                         bg="#363636",
                         fg="#e8e8e8",
                         font=("Segoe UI", 9, "bold"))
    lang_label.pack(anchor="w", pady=(0, 4))
    launcher.language_var = tk.StringVar(value=launcher.current_locale)
    lang_cb = ttk.Combobox(content,
                          textvariable=launcher.language_var,
                          state="readonly",
                          width=22,
                          font=("Segoe UI", 8))
    lang_names = {
        'en-US': 'English (United States)',
        'lt-LT': 'Lietuvių (Lithuania)', 
        'ru-RU': 'Русский (Russia)',
        'lv-LV': 'Latviešu (Latvia)',
        'pl-PL': 'Polski (Poland)',
        'de-DE': 'Deutsch (Germany)',
        'na-NA': 'Debug Locale (For Developers)'
    }
    display_values = []
    launcher._lang_code_map = {}
    for code in launcher.locales:
        display_name = lang_names.get(code, code)
        display_values.append(display_name)
        launcher._lang_code_map[display_name] = code
    lang_cb['values'] = display_values
    current_display = lang_names.get(launcher.current_locale, launcher.current_locale)
    lang_cb.set(current_display)
    lang_cb.pack(anchor="w", pady=(0, 8))
    def on_lang_change(event=None):
        selected_display = lang_cb.get()
        selected_code = launcher._lang_code_map.get(selected_display)
        if selected_code and selected_code != launcher.current_locale:
            try:
                _save_language_preference(selected_code)
                messagebox.showinfo(
                    launcher._t("LANGUAGE_CHANGED_TITLE"),
                    "Language preference saved. Restart the launcher to apply the change."
                )
            except Exception as e:
                messagebox.showerror(launcher._t("ERROR"), str(e))
    lang_cb.bind("<<ComboboxSelected>>", on_lang_change)
    warning = tk.Label(content,
                      text=launcher._t("LANGUAGE_RESTART_WARNING"),
                      bg="#363636",
                      fg="#ff8c00",
                      font=("Segoe UI", 8, "italic"))
    warning.pack(anchor="w")
def _create_accounts_section(parent, launcher):
    content = _create_section_frame(parent, launcher._t("ACCOUNTS"))
    accounts_container = tk.Frame(content, bg="#363636")
    accounts_container.pack(fill="x", pady=(0, 4))
    canvas = tk.Canvas(accounts_container, height=80, bg="#363636", highlightthickness=0)
    scrollbar = ttk.Scrollbar(accounts_container, orient="vertical", command=canvas.yview, style="Modern.Vertical.TScrollbar")
    launcher._accounts_list_frame = tk.Frame(canvas, bg="#363636")
    launcher._accounts_list_frame.bind(
        "<Configure>",
        lambda e: canvas.configure(scrollregion=canvas.bbox("all"))
    )
    canvas.create_window((0, 0), window=launcher._accounts_list_frame, anchor="nw")
    canvas.configure(yscrollcommand=scrollbar.set)
    canvas.pack(side="left", fill="both", expand=True)
    scrollbar.pack(side="right", fill="y")
    _refresh_accounts_list(launcher)
def _create_mojang_section(parent, launcher):
    content = _create_section_frame(parent, launcher._t("MOJANG_ACCOUNTS"))
    info = tk.Label(content,
                   text=launcher._t("MOJANG_DISCONTINUED"),
                   bg="#363636",
                   fg="#e8e8e8",
                   font=("Segoe UI", 8),
                   wraplength=220,
                   justify="left")
    info.pack(anchor="w", pady=(0, 8))
    add_btn = ttk.Button(content,
                        text=launcher._t("ADD_MOJANG_ACCOUNT"),
                        style="Settings.TButton",
                        state="disabled",
                        width=18)
    add_btn.pack(anchor="w")
def _refresh_accounts_list(launcher):
    for widget in launcher._accounts_list_frame.winfo_children():
        widget.destroy()
    try:
        accounts = load_profiles()
    except Exception as e:
        print(f"Error loading accounts: {e}")
        accounts = []
    if not accounts:
        no_acc_label = tk.Label(launcher._accounts_list_frame,
                               text=launcher._t("NO_ACCOUNTS"),
                               bg=launcher._get_theme_color('bg_secondary'),
                               fg=launcher._get_theme_color('fg_disabled'),
                               font=("Segoe UI", 8, "italic"))
        no_acc_label.pack(anchor="w", pady=4)
    else:
        for i, acc in enumerate(accounts):
            acc_container = tk.Frame(launcher._accounts_list_frame, 
                                    bg=launcher._get_theme_color('bg_hover'),
                                    bd=1, relief="solid",
                                    highlightthickness=1,
                                    highlightbackground=launcher._get_theme_color('border_primary'))
            acc_container.pack(fill="x", pady=2)
            acc_frame = tk.Frame(acc_container, bg=launcher._get_theme_color('bg_hover'))
            acc_frame.pack(fill="x", padx=8, pady=4)
            info_frame = tk.Frame(acc_frame, bg=launcher._get_theme_color('bg_hover'))
            info_frame.pack(side="left", fill="x", expand=True)
            username = acc.get('username', 'Unknown')
            acc_type = acc.get('type', 'unknown').title()
            name_label = tk.Label(info_frame,
                                 text=f"{username} ({acc_type})",
                                 bg=launcher._get_theme_color('bg_hover'),
                                 fg=launcher._get_theme_color('fg_primary'),
                                 font=("Segoe UI", 8, "bold"))
            name_label.pack(anchor="w")
            btn = ttk.Button(acc_frame,
                           text=launcher._t("REMOVE"),
                           style="Settings.TButton",
                           width=8,
                           command=lambda idx=i: _remove_account_persistent(launcher, idx))
            btn.pack(side="right")
def _get_settings_path():
    config_dir = Path.home() / ".config" / "oranglauncher"
    config_dir.mkdir(parents=True, exist_ok=True)
    return config_dir / "launcher_config.json"
def _load_settings(launcher):
    try:
        config_path = _get_settings_path()
        if config_path.exists():
            with open(config_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            launcher.show_status_bar.set(data.get('show_status_bar', False))
            launcher.discord_rpc_enabled.set(data.get('discord_rpc_enabled', True))
            if not hasattr(launcher, 'delete_telemetry_on_startup'):
                launcher.delete_telemetry_on_startup = tk.BooleanVar(value=False)
            launcher.delete_telemetry_on_startup.set(data.get('delete_telemetry_on_startup', False))
            if not hasattr(launcher, 'custom_layout_enabled'):
                launcher.custom_layout_enabled = tk.BooleanVar(value=False)
            launcher.custom_layout_enabled.set(data.get('custom_layout_enabled', False))
            if not hasattr(launcher, 'debug_mode_enabled'):
                launcher.debug_mode_enabled = tk.BooleanVar(value=False)
            launcher.debug_mode_enabled.set(data.get('debug_mode_enabled', False))
            if not hasattr(launcher, 'show_progress_bar'):
                launcher.show_progress_bar = tk.BooleanVar(value=False)
            launcher.show_progress_bar.set(data.get('show_progress_bar', False))
            if not hasattr(launcher, 'use_dri_prime'):
                launcher.use_dri_prime = tk.BooleanVar(value=False)
            launcher.use_dri_prime.set(data.get('use_dri_prime', False))
            for attr, key in [('share_options', 'share_options'),
                               ('share_resourcepacks', 'share_resourcepacks'),
                               ('share_shaderpacks', 'share_shaderpacks'),
                               ('share_servers', 'share_servers'),
                               ('share_screenshots', 'share_screenshots')]:
                if not hasattr(launcher, attr):
                    setattr(launcher, attr, tk.BooleanVar(value=False))
                getattr(launcher, attr).set(data.get(key, False))
    except Exception as e:
        print(f"Error loading settings: {e}")
def _on_share_toggle(launcher):
    # persist the new state, then re-link every instance so sharing takes effect now
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
def _restart_application():
    try:
        os.execv(sys.executable, [sys.executable] + sys.argv)
    except Exception as e:
        print(f"Error restarting application: {e}")
        messagebox.showerror("Restart Failed", "Please manually restart the application.")
def _save_and_apply(launcher, apply_func):
    _save_settings(launcher)
    if apply_func:
        apply_func()
def _remove_account_persistent(launcher, idx):
    try:
        accounts = load_profiles()
        if 0 <= idx < len(accounts):
            username = accounts[idx].get('username', 'Unknown')
            result = messagebox.askyesno(
                launcher._t("ACCOUNT_REMOVE_TITLE"),
                launcher._t("ACCOUNT_REMOVE_CONFIRM").format(username=username)
            )
            if result:
                del accounts[idx]
                save_profiles(accounts)
                _refresh_accounts_list(launcher)
                launcher._refresh_profiles()
    except Exception as e:
        messagebox.showerror(launcher._t("ERROR"), launcher._t("ACCOUNT_REMOVE_FAIL").format(e=e))
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
def initialize_settings_on_startup(launcher):
    try:
        _load_settings(launcher)
    except Exception as e:
        print(f"[DEBUG] Error initializing settings: {e}")
def update_language_ui(launcher):
    try:
        if hasattr(launcher, 'language_var') and hasattr(launcher, '_lang_code_map'):
            lang_names = {
                'en-US': 'English (United States)',
                'lt-LT': 'Lietuvių (Lithuania)',
                'lv-LV': 'Latvian (Latvia)',
                'ru-RU': 'Русский (Russia)',
                'pl-PL': 'Polski (Poland)',
                'de-DE': 'Deutsch (Germany)',
                'na-NA': 'For Translators'
            }
            current_display = lang_names.get(launcher.current_locale, launcher.current_locale)
            launcher.language_var.set(current_display)
    except Exception as e:
        print(f"[DEBUG] Error updating language UI: {e}")
def _create_modern_card(parent, title, launcher):
    card_container = tk.Frame(parent, bg=launcher._get_theme_color('bg_primary'))
    card_container.pack(fill="x", pady=(0, 20), padx=4)
    
    card_frame = tk.Frame(card_container, 
                         bg=launcher._get_theme_color('bg_primary'),
                         relief="flat", 
                         bd=0,
                         highlightthickness=0)
    card_frame.pack(fill="x", padx=0, pady=2)
    
    header_frame = tk.Frame(card_frame, bg=launcher._get_theme_color('bg_primary'))
    header_frame.pack(fill="x", padx=0, pady=(16, 8))
    
    title_label = tk.Label(header_frame, text=title,
                          bg=launcher._get_theme_color('bg_primary'), 
                          fg=launcher._get_theme_color('fg_primary'),
                          font=("Segoe UI", 12, "bold"))
    title_label.pack(anchor="w")
    
    content_frame = tk.Frame(card_frame, bg=launcher._get_theme_color('bg_primary'))
    content_frame.pack(fill="x", padx=0, pady=(0, 16))
    return content_frame
def _create_modern_theme_button(parent, launcher, theme_name, description, bg_color):
    btn_container = tk.Frame(parent, bg=launcher._get_theme_color('bg_primary'))
    btn_container.pack(fill="x", pady=(0, 12))
    theme_frame = tk.Frame(btn_container, 
                          bg=launcher._get_theme_color('bg_secondary'),
                          relief="flat",
                          bd=1,
                          highlightthickness=1,
                          highlightcolor=launcher._get_theme_color('border_primary'),
                          highlightbackground=launcher._get_theme_color('border_primary'))
    theme_frame.pack(fill="x", padx=2, pady=2)
    preview_frame = tk.Frame(theme_frame, bg=bg_color, width=40, height=40)
    preview_frame.pack(side="left", padx=12, pady=12)
    preview_frame.pack_propagate(False)
    info_frame = tk.Frame(theme_frame, bg=launcher._get_theme_color('bg_secondary'))
    info_frame.pack(side="left", fill="both", expand=True, padx=(0, 12), pady=12)
    name_label = tk.Label(info_frame, text=theme_name,
                         bg=launcher._get_theme_color('bg_secondary'),
                         fg=launcher._get_theme_color('fg_primary'),
                         font=("Segoe UI", 11, "bold"))
    name_label.pack(anchor="w")
    desc_label = tk.Label(info_frame, text=description,
                         bg=launcher._get_theme_color('bg_secondary'),
                         fg=launcher._get_theme_color('fg_disabled'),
                         font=("Segoe UI", 9))
    desc_label.pack(anchor="w")

    switch_icon = launcher._load_themed_icon("switch", size=(24, 24))
    
    def _update_selection_indicator(*args):
        if not indicator_btn.winfo_exists():
            return
        is_selected = (launcher.selected_theme.get() == theme_name)
        color = launcher._get_theme_color('accent_primary') if is_selected else launcher._get_theme_color('fg_disabled')
        
        icon = launcher._load_themed_icon("switch", size=(24, 24), force_color=color)
        try:
            indicator_btn.config(image=icon)
            indicator_btn.image = icon  # type: ignore
        except Exception:
            return
    indicator_btn = tk.Button(theme_frame,
                             bg=launcher._get_theme_color('bg_secondary'),
                             bd=0,
                             activebackground=launcher._get_theme_color('bg_secondary'),
                             relief="flat",
                             cursor="hand2",
                             command=lambda: _on_theme_select(launcher, theme_name))
    indicator_btn.pack(side="right", padx=12)
    
    _update_selection_indicator()
    
    launcher.selected_theme.trace_add("write", lambda *a: _update_selection_indicator())

    return theme_frame
def _on_theme_select(launcher, theme_name):
    try:
        if save_theme_preference(theme_name):
            try:
                launcher.selected_theme.set(theme_name)
                messagebox.showinfo(
                    launcher._t("THEME_CHANGED_TITLE"),
                    "Theme preference saved. Restart the launcher to apply the change."
                )
            except Exception as e:
                print(f"Error notifying theme change: {e}")
        else:
            messagebox.showerror(launcher._t("ERROR"), launcher._t("THEME_SAVE_ERROR"))
    except Exception as e:
        print(f"Error changing theme: {e}")
def _save_theme_preference(theme_name):
    try:
        config_path = os.path.expanduser("~/.minecraft_launcher_config.json")
        data = {}
        if os.path.exists(config_path):
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
            except Exception:
                data = {}
        data['theme'] = theme_name
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=2, ensure_ascii=False)
    except Exception as e:
        print(f"Error saving theme preference: {e}")

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
        
def _open_terminal_tailing_file(log_path: Path) -> bool:
    tail_args = ["tail", "-n", "200", "-f", str(log_path)]
    terminal_commands = [
        ("gnome-terminal", ["gnome-terminal", "--"] + tail_args),
        ("mate-terminal", ["mate-terminal", "--"] + tail_args),
        ("xfce4-terminal", ["xfce4-terminal", "--"] + tail_args),
        ("konsole", ["konsole", "-e"] + tail_args),
        ("xterm", ["xterm", "-e"] + tail_args),
        ("kitty", ["kitty", "-e"] + tail_args),
        ("alacritty", ["alacritty", "-e"] + tail_args),
        ("x-terminal-emulator", ["x-terminal-emulator", "-e"] + tail_args),
    ]

    for terminal_name, command in terminal_commands:
        if shutil.which(terminal_name):
            try:
                subprocess.Popen(command)
                return True
            except Exception as e:
                print(f"[DEBUG] Failed to launch {terminal_name}: {e}")

    return False

def _load_theme_preference():
    try:
        config_path = os.path.expanduser("~/.minecraft_launcher_config.json")
        if os.path.exists(config_path):
            with open(config_path, 'r', encoding='utf-8') as f:
                data = json.load(f)
            return data.get('theme', 'Arc')
    except Exception as e:
        print(f"Error loading theme preference: {e}")
    return 'Arc'

def _toggle_debug_mode(launcher):
    _save_settings(launcher)
    if launcher.debug_mode_enabled.get():
        print("[DEBUG] Debug mode enabled")
        log_dir = Path.home() / ".local" / "share" / "oranglauncher" / "logs"
        log_dir.mkdir(parents=True, exist_ok=True)
        log_file = log_dir / f"launcher_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
        if not hasattr(launcher, '_original_stdout'):
            launcher._original_stdout = sys.stdout
            launcher._original_stderr = sys.stderr
        sys.stdout = StreamCapture(str(log_file), launcher._original_stdout)
        sys.stderr = StreamCapture(str(log_file), launcher._original_stderr)
        launcher._current_log_file = str(log_file)
        print(f"[DEBUG] Logging to: {log_file}")
        
        if hasattr(launcher, '_debug_text_widget'):
            _update_debug_info(launcher)
    else:
        print("[DEBUG] Debug mode disabled")
        # Restore original streams
        if hasattr(launcher, '_original_stdout'):
            sys.stdout = launcher._original_stdout
            sys.stderr = launcher._original_stderr
        
    if hasattr(launcher, '_settings_current_content'):
        try:
            for btn in launcher._settings_nav_buttons:
                if "Advanced" in btn.cget("text"):
                    btn.invoke()
                    break
        except Exception as e:
            print(f"[DEBUG] Error refreshing debug UI: {e}")
def _update_debug_info(launcher):
    if not hasattr(launcher, '_debug_text_widget'):
        return
    if launcher._debug_text_widget is None:
        return
    try:
        if not launcher._debug_text_widget.winfo_exists():
            return
    except Exception:
        return
    try:
        debug_info = []
        debug_info.append("=== SYSTEM INFO ===")
        debug_info.append(f"Platform: {platform.platform()}")
        debug_info.append(f"Python: {sys.version}")
        debug_info.append(f"Current locale: {launcher.current_locale}")
        debug_info.append(f"Debug mode: {'ON' if launcher.debug_mode_enabled.get() else 'OFF'}")
        
        if launcher.debug_mode_enabled.get() and hasattr(launcher, '_current_log_file'):
            debug_info.append(f"Log file: {launcher._current_log_file}")
            debug_info.append("=== CAPTURED OUTPUT (last 30 lines) ===")
            if isinstance(sys.stdout, StreamCapture):
                output = sys.stdout.get_buffer_content()
                lines = output.split('\n')
                last_lines = lines[-30:] if len(lines) > 30 else lines
                debug_info.extend(last_lines)
        
        launcher._debug_text_widget.config(state='normal')
        launcher._debug_text_widget.delete('1.0', tk.END)
        launcher._debug_text_widget.insert('1.0', '\n'.join(debug_info))
        launcher._debug_text_widget.config(state='disabled')
        launcher._debug_text_widget.yview(tk.END)  # Scroll to bottom
    except Exception as e:
        print(f"Error updating debug info: {e}")

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
            pack_version = pack_data.get("version_id", "1.0")
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
                except Exception as inner_e:
                    return False, f"Failed to create instance: {str(inner_e)}", None
            shutil.rmtree(temp_dir)
            self.instance_mgr._notify_callbacks()
            return True, f"Successfully imported {pack_name} (Minecraft {game_version}, {mod_loader})", instance.name
        except Exception as e:
            traceback.print_exc()
            return False, f"Error importing modpack: {str(e)}", None
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
    def _download_mods_to_instance(self, pack_data, instance):
        files = pack_data.get("files", [])
        mods_dir = instance.mods_dir
        mods_dir.mkdir(parents=True, exist_ok=True)
        total_mods = len([f for f in files if f.get("path", "").startswith("mods/")])
        if self.launcher and hasattr(self.launcher, 'status_label'):
            self.launcher.after(0, lambda: self.launcher.status_label.config(text=f"Importing modpack: 0/{total_mods} mods"))  # type: ignore
        downloaded_count = 0
        for i, file_info in enumerate(files, 1):
            file_path = file_info.get("path", "")
            if not file_path.startswith("mods/"):
                continue
            download_url = file_info.get("downloads", [""])[0]
            if not download_url:
                print(f"Skipping {file_path} - no download URL")
                continue
            try:
                filename = Path(file_path).name
                mod_path = mods_dir / filename
                if mod_path.exists():
                    print(f"[MRPACK] {filename} already exists, skipping")
                    downloaded_count += 1
                    continue
                if self.launcher and hasattr(self.launcher, 'status_label'):
                    n = downloaded_count + 1
                    self.launcher.after(0, lambda n=n: self.launcher.status_label.config(text=f"Importing modpack: {n}/{total_mods} mods"))  # type: ignore
                print(f"[MRPACK] Downloading {filename}...")
                response = _http_session.get(download_url, stream=True, timeout=30)
                response.raise_for_status()
                with open(mod_path, 'wb') as f:
                    for chunk in response.iter_content(chunk_size=8192):
                        if chunk:
                            f.write(chunk)
                downloaded_count += 1
                print(f"[MRPACK] Downloaded {filename}")
            except Exception as e:
                print(f"[MRPACK] Error downloading {file_path}: {e}")
        print(f"[MRPACK] Successfully downloaded {downloaded_count} mods")
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
        mod_loader = self._detect_mod_loader(pack_data).lower()
        if mod_loader == "none" or mod_loader == "vanilla":
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
            latest_version = compatible_versions[0]
            version_number = latest_version.get("version_number", "unknown")
            print(f"[MRPACK] Found compatible version: {version_number}")
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
    def _import_overrides_to_instance(self, temp_dir, instance):
        overrides_dir = temp_dir / "overrides"
        if not overrides_dir.exists():
            print("No overrides directory found")
            return
        game_dir = instance.minecraft_dir
        game_dir.mkdir(parents=True, exist_ok=True)
        copied_files = 0
        for src_path in overrides_dir.glob("**/*"):
            if src_path.is_file():
                rel_path = src_path.relative_to(overrides_dir)
                dst_path = game_dir / rel_path
                dst_path.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(src_path, dst_path)
                copied_files += 1
        print(f"Copied {copied_files} override files")
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
                return False, "manifest.json not found — this does not appear to be a CurseForge modpack.", None

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
                loader_matches.sort(key=lambda x: x.get('date_published') or '', reverse=True)
                self._log(f"[Modrinth] Aggressive mode: selected {loader_matches[0].get('version_number')}")
                return loader_matches[0]

        for bucket in (exact_matches, newer_or_equal_patch, older_patch, fallback_matches):
            compat = [v for v in bucket if _is_version_compatible(v, game_version)]
            if compat:
                compat.sort(key=lambda x: x.get('date_published') or '', reverse=True)
                return compat[0]
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


        return _inner_update(local_jar, loader, game_version, aggressive=aggressive, force=force)

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

class ModdingTab:
    def __init__(self, parent, mod_loader_var, instance_manager=None):
        self.parent = parent
        self.mod_loader_var = mod_loader_var
        self.profile_manager = get_game_profile_manager()
        self.theme_manager = parent.theme_manager
        self.instance_manager = instance_manager
        self.mods_listbox = None
        self.current_profile = None
        self.current_instance = None
        self.mod_info_label = None
        self.download_thread = None
        self.search_var = tk.StringVar()
        self._all_mods = []
        register_mod_change_callback(self.on_mod_list_changed)
        if self.instance_manager:
            self.instance_manager.register_callback(self.on_instance_changed)
    def open_mod_download_sites(self):
        messagebox.showinfo(self.parent._t('MODS_DOWNLOAD_SITES_TITLE'), self.parent._t('MODS_DOWNLOAD_SITES_MSG'))
    def import_modpack(self):
        messagebox.showinfo(self.parent._t('MODS_IMPORT_FEATURE_TITLE'), self.parent._t('MODS_IMPORT_FEATURE_MSG'))
    def build_tab(self):
        mods_frame = ttk.Frame(self.parent.notebook)
        self.parent.notebook.add(mods_frame, text=self.parent._t('MODS_TAB_TITLE'))
        header_frame = ttk.Frame(mods_frame)
        header_frame.pack(fill="x", padx=20, pady=20)

        ttk.Label(header_frame, text=self.parent._t("MODS_MANAGEMENT_TITLE"),
                  style="Header.TLabel", font=("Segoe UI", 16, "bold")).pack(anchor="w")
        profile_info_frame = ttk.Frame(mods_frame)
        profile_info_frame.pack(fill="x", padx=20, pady=(0, 10))
        self.profile_info_label = ttk.Label(
            profile_info_frame,
            text=self.parent._t("MODS_CURRENT_PROFILE_LOADING"),
            style="Header.TLabel"
        )
        self.profile_info_label.pack(anchor="w")
        self.mod_loader_info_label = ttk.Label(
            profile_info_frame,
            text=self.parent._t("MODS_LOADER_NONE"),
            style="News.TLabel"
        )
        self.mod_loader_info_label.pack(anchor="w")
        self.loader_warning_frame = ttk.Frame(mods_frame)
        self.loader_warning_frame.pack(fill="x", padx=20, pady=(0, 10))
        self.loader_warning_label = ttk.Label(
            self.loader_warning_frame,
            text=self.parent._t("MODS_LOADER_WARNING"),
            style="News.TLabel",
            foreground=self.theme_manager.get_color('accent_primary')
        )
        self.loader_warning_label.pack(anchor="w")
        self.loader_warning_frame.pack_forget()
        mods_list_frame = ttk.LabelFrame(mods_frame, text=self.parent._t("MODS_INSTALLED_TITLE"), style="TLabelframe")
        mods_list_frame.pack(fill="both", expand=True, padx=20, pady=(0, 20))
        self.mods_count_label = ttk.Label(
            mods_list_frame,
            text=self.parent._t("MODS_COUNT_0"),
            style="News.TLabel"
        )
        self.mods_count_label.pack(anchor="w", padx=10, pady=(5, 0))
        listbox_frame = ttk.Frame(mods_list_frame)
        listbox_frame.pack(fill="both", expand=True, padx=10, pady=10)
        search_frame = ttk.Frame(listbox_frame)
        search_frame.pack(fill="x", pady=(0, 6))
        ttk.Label(search_frame, text="Search:").pack(side="left", padx=(0,6))
        search_entry = ttk.Entry(search_frame, textvariable=self.search_var, width=40)
        search_entry.pack(side="left", padx=(0,6))
        def _on_search_change(*args):
            try:
                self._apply_search_filter()
            except Exception:
                pass
        self.search_var.trace_add('write', _on_search_change)
        self.mods_listbox = tk.Listbox(
            listbox_frame,
            bg=self.theme_manager.get_color('bg_input'),
            fg=self.theme_manager.get_color('fg_primary'),
            selectbackground=self.theme_manager.get_color('bg_hover'),
            selectforeground=self.theme_manager.get_color('fg_primary'),
            selectmode=tk.EXTENDED
        )
        scrollbar_mods = ttk.Scrollbar(
            listbox_frame,
            orient="vertical",
            command=self.mods_listbox.yview,
            style="Modern.Vertical.TScrollbar"
        )
        self.mods_listbox.configure(yscrollcommand=scrollbar_mods.set)
        self.mods_listbox.pack(side="left", fill="both", expand=True)
        scrollbar_mods.pack(side="right", fill="y")
        mod_info_frame = ttk.Frame(mods_list_frame)
        mod_info_frame.pack(fill="x", padx=10, pady=(0, 10))
        self.mod_info_label = ttk.Label(
            mod_info_frame,
            text=self.parent._t("MODS_SELECT_INFO"),
            style="News.TLabel"
        )
        self.mod_info_label.pack(anchor="w")
        self.mods_listbox.bind("<<ListboxSelect>>", lambda e: self.on_mod_select())
        mods_btn_frame = ttk.Frame(mods_frame)
        mods_btn_frame.pack(fill="x", padx=20, pady=(0, 20))
        
        def create_mod_btn(text, icon_name, command):
            icon = self.parent._load_themed_icon(icon_name, size=(16, 16))
            btn = tk.Button(
                mods_btn_frame,
                text=f"  {text}",
                image=icon,
                compound="left",
                command=command,
                bg=self.theme_manager.get_color('bg_tertiary'),
                fg=self.theme_manager.get_color('fg_primary'),
                font=("Segoe UI", 9), bd=0, padx=12, pady=6, cursor="hand2", relief="flat"
            )
            btn._icon = icon
            return btn

        create_mod_btn(self.parent._t("MODS_ADD_BTN"), "plus", self.add_mods).pack(side="left", padx=(0, 10))
        create_mod_btn(self.parent._t("MODS_REMOVE_BTN"), "trash", self.remove_selected_mods).pack(side="left", padx=(0, 10))
        create_mod_btn(self.parent._t("MODS_UPDATE_BTN"), "update", self.update_mods).pack(side="left", padx=(0, 10))
        create_mod_btn(self.parent._t("MODS_RESTORE_BACKUPS"), "refresh", self.restore_backups).pack(side="left", padx=(0, 10))
        create_mod_btn(self.parent._t("MODS_OPEN_FOLDER_BTN"), "folder", self.open_mods_folder).pack(side="left", padx=(0, 10))
        self.refresh_ui()
    def refresh_ui(self):
        if self.instance_manager:
            self.current_instance = self.instance_manager.get_selected_instance()
            if self.current_instance:
                self.current_profile = None
                self.profile_info_label.config(
                    text=f"Current Instance: {self.current_instance.name}"
                )
                self.mod_loader_info_label.config(
                    text=f"Mod Loader: {self.current_instance.mod_loader} | Version: {self.current_instance.version}"
                )
                if self.current_instance.mod_loader.lower() == "vanilla":
                    self.loader_warning_frame.pack()
                else:
                    self.loader_warning_frame.pack_forget()
                self.mod_loader_var.set(self.current_instance.mod_loader)
                self.refresh_mods_list()
                return
        self.current_profile = self.profile_manager.get_selected_profile()
        if not self.current_profile:
            self.profile_info_label.config(text="Current Profile: None selected")
            self.mod_loader_info_label.config(text="Mod Loader: N/A")
            self.loader_warning_frame.pack()
            self.refresh_mods_list()
            return
        self.profile_info_label.config(
            text=f"Current Profile: {self.current_profile.name}"
        )
        self.mod_loader_info_label.config(
            text=f"Mod Loader: {self.current_profile.mod_loader} | Version: {self.current_profile.version}"
        )
        if self.current_profile.mod_loader == "None":
            self.loader_warning_frame.pack()
        else:
            self.loader_warning_frame.pack_forget()
        self.mod_loader_var.set(self.current_profile.mod_loader)
        self.refresh_mods_list()
    def on_mod_list_changed(self):
        self.refresh_ui()
    def on_instance_changed(self):
        self.refresh_ui()
    def on_mod_select(self):
        selection = self.mods_listbox.curselection()
        if not selection:
            self.mod_info_label.config(text=self.parent._t("MODS_SELECT_MOD_INFO"))
            return
        if len(selection) == 1:
            mod_name = self.mods_listbox.get(selection[0])
            if mod_name != "No mods installed" and mod_name != "No profile selected":
                if self.current_instance:
                    mod_path = self.current_instance.mods_dir / mod_name
                    if mod_path.exists():
                        size_mb = mod_path.stat().st_size / (1024 * 1024)
                        self.mod_info_label.config(
                            text=f"Selected: {mod_name} ({size_mb:.2f} MB)"
                        )
                    else:
                        self.mod_info_label.config(text=f"Selected: {mod_name}")
                elif self.current_profile:
                    mod_path = self.current_profile.get_mods_directory() / mod_name
                    if mod_path.exists():
                        size_mb = mod_path.stat().st_size / (1024 * 1024)
                        self.mod_info_label.config(
                            text=f"Selected: {mod_name} ({size_mb:.2f} MB)"
                        )
                    else:
                        self.mod_info_label.config(text=f"Selected: {mod_name}")
        else:
            self.mod_info_label.config(text=f"{len(selection)} mods selected")
    def add_mods(self):
        if not self.current_instance and not self.current_profile:
            messagebox.showwarning(self.parent._t("MODS_NO_PROFILE_WARNING_TITLE"), self.parent._t("MODS_NO_PROFILE_WARNING_MSG"))
            return
        is_vanilla = False
        if self.current_instance:
            is_vanilla = self.current_instance.mod_loader.lower() == "vanilla"
        elif self.current_profile:
            is_vanilla = self.current_profile.mod_loader == "None"
        if is_vanilla:
            result = messagebox.askyesno(
                self.parent._t("MODS_NO_LOADER_TITLE"),
                self.parent._t("MODS_NO_LOADER_MSG")
            )
            if not result:
                return
        filetypes = [("Mod files", "*.jar *.zip"), ("All files", "*.*")]
        mod_files = filedialog.askopenfilenames(
            title=self.parent._t("MODS_FILE_SELECT_TITLE"),
            filetypes=filetypes
        )
        if not mod_files:
            return
        added_count = 0
        failed_count = 0
        if self.current_instance:
            mods_dir = self.current_instance.mods_dir
            mods_dir.mkdir(parents=True, exist_ok=True)
            for mod_file in mod_files:
                try:
                    mod_filename = os.path.basename(mod_file)
                    dest_path = mods_dir / mod_filename
                    shutil.copy2(mod_file, dest_path)
                    added_count += 1
                except Exception as e:
                    print(f"Error adding mod {os.path.basename(mod_file)}: {e}")
                    failed_count += 1
        else:
            for mod_file in mod_files:
                try:
                    if add_mod_to_current_profile(mod_file):
                        added_count += 1
                    else:
                        failed_count += 1
                except Exception as e:
                    print(f"Error adding mod {os.path.basename(mod_file)}: {e}")
                    failed_count += 1
        if added_count > 0:
            if failed_count > 0:
                messagebox.showinfo(
                    self.parent._t("MODS_ADDED_TITLE"),
                    f"{self.parent._t('MODS_ADDED_SUCCESS').format(count=added_count)}\n"
                    f"{self.parent._t('MODS_ADDED_FAIL').format(count=failed_count)}"
                )
            else:
                messagebox.showinfo(
                    self.parent._t("SUCCESS"),
                    self.parent._t("MODS_ADDED_SUCCESS").format(count=added_count)
                )
            self.refresh_mods_list()
        elif failed_count > 0:
            messagebox.showerror(
                self.parent._t("ERROR"),
                self.parent._t("MODS_ADDED_FAIL").format(count=failed_count)
            )
    def remove_selected_mods(self):
        selection = self.mods_listbox.curselection()
        if not selection:
            messagebox.showinfo(self.parent._t("MODS_REMOVE_TITLE"), self.parent._t("MODS_REMOVE_NONE"))
            return
        if (not self.current_instance and not self.current_profile):
            return
        mod_names = [self.mods_listbox.get(i) for i in selection]
        mod_names = [name for name in mod_names if name not in ["No mods installed", "No profile selected"]]
        if not mod_names:
            return
        if len(mod_names) == 1:
            confirm_msg = self.parent._t("MODS_REMOVE_CONFIRM_SINGLE").format(name=mod_names[0])
        else:
            confirm_msg = self.parent._t("MODS_REMOVE_CONFIRM_MULTI").format(count=len(mod_names))
        confirm = messagebox.askyesno(self.parent._t("MODS_REMOVE_CONFIRM_TITLE"), confirm_msg)
        if not confirm:
            return
        removed_count = 0
        if self.current_instance:
            mods_dir = self.current_instance.mods_dir
            for mod_name in mod_names:
                try:
                    mod_path = mods_dir / mod_name
                    if mod_path.exists():
                        mod_path.unlink()
                        removed_count += 1
                except Exception as e:
                    print(f"Error removing mod {mod_name}: {e}")
        else:
            for mod_name in mod_names:
                try:
                    if remove_mod_from_current_profile(mod_name):
                        removed_count += 1
                except Exception as e:
                    print(f"Error removing mod {mod_name}: {e}")
        if removed_count > 0:
            self.refresh_mods_list()
            messagebox.showinfo(
                self.parent._t("SUCCESS"),
                self.parent._t("MODS_REMOVED_SUCCESS").format(count=removed_count)
            )
    def open_mods_folder(self):
        if not self.current_instance and not self.current_profile:
            messagebox.showwarning(self.parent._t("MODS_NO_PROFILE_WARNING_TITLE"), self.parent._t("MODS_NO_PROFILE_WARNING_MSG"))
            return
        mods_path = None
        if self.current_instance:
            mods_path = self.current_instance.mods_dir
            mods_path.mkdir(parents=True, exist_ok=True)
        else:
            mods_path = self.current_profile.ensure_mods_directory()
        try:
            subprocess.run(["xdg-open", str(mods_path)])
        except Exception as e:
            messagebox.showerror(self.parent._t("ERROR"), self.parent._t("MODS_OPEN_FOLDER_ERROR").format(e=str(e)))
    def import_mrpack(self):
        filetypes = [("Modrinth Modpack", "*.mrpack"), ("All files", "*.*")]
        mrpack_path = filedialog.askopenfilename(
            title=self.parent._t("MODS_IMPORT_TITLE"),
            filetypes=filetypes
        )
        if not mrpack_path:
            return
        old_status = ""
        if hasattr(self.parent, 'status_label'):
            old_status = self.parent.status_label.cget("text")
            self.parent.status_label.config(text="Importing modpack...")
            self.parent.update_idletasks()
        if hasattr(self.parent, 'status_bar_progress'):
            self.parent.status_bar_progress.config(mode='indeterminate')
            self.parent.status_bar_progress.start(15)
        def _restore_spinner():
            if hasattr(self.parent, 'status_bar_progress'):
                self.parent.status_bar_progress.stop()
                self.parent.status_bar_progress.config(mode='determinate')
                if hasattr(self.parent, 'progress'):
                    self.parent.progress.set(0)
            if hasattr(self.parent, 'status_label'):
                self.parent.status_label.config(text=old_status or "Ready")  # type: ignore
        def import_thread():
            try:
                launcher_obj = self.parent if hasattr(self.parent, 'status_label') else None
                success, message, profile_name = import_modpack(mrpack_path, launcher_obj)
                def update_ui():
                    _restore_spinner()
                    if success:
                        messagebox.showinfo(
                            self.parent._t("MODS_IMPORT_SUCCESS_TITLE"),
                            self.parent._t("MODS_IMPORT_SUCCESS_MSG").format(message=message, profile_name=profile_name)
                        )
                        if hasattr(self.parent, '_refresh_game_profiles'):
                            self.parent._refresh_game_profiles()
                        self.refresh_ui()
                    else:
                        messagebox.showerror(self.parent._t("MODS_IMPORT_FAIL_TITLE"), message)
                self.parent.after(0, update_ui)
            except Exception as e:
                error_msg = f"Error importing modpack: {str(e)}\n\n{traceback.format_exc()}"
                print(error_msg)
                def show_error():
                    _restore_spinner()
                    messagebox.showerror(self.parent._t("MODS_IMPORT_ERROR_TITLE"), str(e))
                self.parent.after(0, show_error)
        thread = threading.Thread(target=import_thread, daemon=True)
        thread.start()


    def restore_backups(self):
        if not self.current_instance and not self.current_profile:
            messagebox.showwarning(self.parent._t("MODS_NO_PROFILE_WARNING_TITLE"), self.parent._t("MODS_NO_PROFILE_WARNING_MSG"))
            return
        if self.current_instance:
            mods_dir = self.current_instance.mods_dir
        else:
            mods_dir = self.current_profile.ensure_mods_directory()
        bak_files = list(mods_dir.glob('*.jar.bak')) if mods_dir.exists() else []
        if not bak_files:
            messagebox.showinfo(self.parent._t("MODS_BACKUP_NONE") if hasattr(self.parent, '_t') else "No backups", self.parent._t("MODS_BACKUP_NONE_MSG") if hasattr(self.parent, '_t') else "No .bak backup files found.")
            return
        confirm = messagebox.askyesno(self.parent._t("MODS_BACKUP_RESTORE_TITLE") if hasattr(self.parent, '_t') else "Restore Backups", self.parent._t("MODS_BACKUP_RESTORE_CONFIRM").format(count=len(bak_files)) if hasattr(self.parent, '_t') else f"Restore {len(bak_files)} backup(s)?")
        if not confirm:
            return
        restored = 0
        errors = []
        for bak in bak_files:
            try:
                orig = bak.with_suffix('')
                if orig.exists():
                    try:
                        orig.unlink()
                    except Exception:
                        pass
                shutil.move(str(bak), str(orig))
                restored += 1
            except Exception as e:
                errors.append(f"{bak.name}: {e}")
        msg = f"Restored {restored} backup(s)."
        if errors:
            msg += "\nErrors:\n" + "\n".join(errors[:10])
        messagebox.showinfo(self.parent._t("MODS_BACKUP_RESTORE_DONE") if hasattr(self.parent, '_t') else "Restore complete", msg)
        self.refresh_mods_list()

    def _report_progress(self, current: int, total: int, message: str):
        try:
            percent = int((current / max(total, 1)) * 100)
        except Exception:
            percent = 0
        try:
            if hasattr(self.parent, '_submit_progress_update'):
                self.parent._submit_progress_update(percent, message)
            elif hasattr(self.parent, 'status_label'):
                self.parent.status_label.config(text=message)  # type: ignore
        except Exception:
            pass

    def update_mods(self):
        if not self.current_instance and not self.current_profile:
            messagebox.showwarning(self.parent._t("MODS_NO_PROFILE_WARNING_TITLE"), self.parent._t("MODS_NO_PROFILE_WARNING_MSG"))
            return
        if self.current_instance:
            mods_dir = self.current_instance.mods_dir
            loader = self.current_instance.mod_loader
            game_version = self.current_instance.version
        else:
            mods_dir = self.current_profile.ensure_mods_directory()
            loader = self.current_profile.mod_loader
            game_version = self.current_profile.version
        if not mods_dir.exists():
            messagebox.showinfo(self.parent._t("MODS_UPDATE_TITLE") if hasattr(self.parent, '_t') else "Update Mods", "No mods to update")
            return

        dlg = tk.Toplevel(self.parent)
        dlg.title(self.parent._t("MODS_UPDATE_TITLE") if hasattr(self.parent, '_t') else "Update Mods")
        dlg.transient(self.parent)
        dlg.grab_set()
        try:
            bg = self.parent._get_theme_color('bg_primary')
            fg = self.parent._get_theme_color('fg_primary')
            accent = self.parent._get_theme_color('accent_primary')
            hover = self.parent._get_theme_color('bg_hover')
        except Exception:
            bg = None
            fg = None
            accent = None
            hover = None
        if bg:
            try:
                dlg.configure(bg=bg)
            except Exception:
                pass
        lbl = tk.Label(dlg, text=self.parent._t("MODS_UPDATING") if hasattr(self.parent, '_t') else "Updating mods...",
                       bg=(bg if bg else None), fg=(fg if fg else None), font=("Segoe UI", 11, "bold"))
        lbl.pack(padx=12, pady=(12, 6))
        pb = ttk.Progressbar(dlg, orient='horizontal', length=360, mode='determinate')
        pb.pack(padx=12, pady=(0, 12))
        status_lbl = tk.Label(dlg, text=self.parent._t("MODS_UPDATING_START") if hasattr(self.parent, '_t') else "Starting...",
                              bg=(bg if bg else None), fg=(fg if fg else None))
        status_lbl.pack(padx=12, pady=(0, 12))
        cancel_event = threading.Event()
        def on_cancel():
            cancel_event.set()
            status_lbl.config(text="Cancelling...")
        cancel_btn = tk.Button(dlg, text=self.parent._t("CANCEL") if hasattr(self.parent, '_t') else "Cancel",
                       command=on_cancel,
                       bg=(hover if hover else None), fg=(fg if fg else None), bd=0, padx=12, pady=6,
                       cursor="hand2")
        cancel_btn.pack(padx=12, pady=(0, 12))

        def worker():
            updater = ModrinthUpdater(logger=lambda m: self.parent._safe_append_log(m) if hasattr(self.parent, '_safe_append_log') else None)
            def progress(curr, total, msg):
                try:
                    pct = int((curr / max(total, 1)) * 100)
                except Exception:
                    pct = 0
                def ui_update():
                    try:
                        pb['value'] = pct
                        status_lbl.config(text=msg)
                    except Exception:
                        pass
                self.parent.after(0, ui_update)
            try:
                jars = [p for p in mods_dir.iterdir() if p.suffix.lower() == '.jar' and p.is_file()]
                candidates = {}
                downgrade_candidates = {}  
                results = {} 
                incompatible_installed = [] 
                total = len(jars)
                for idx, jar in enumerate(sorted(jars), start=1):
                    if cancel_event.is_set():
                        break
                    if progress:
                        progress(idx - 1, total, f"Scanning {jar.name}...")
                    try:
                        cand = updater.check_mod(jar, loader, game_version, aggressive=False)
                        try:
                            info = updater._extract_info_from_jar(jar)
                            slug_candidates = []
                            if info.get('id'):
                                slug_candidates.append(info.get('id'))
                            if info.get('name'):
                                slug_candidates.append(info.get('name'))
                            slug_candidates.append(_normalize_query_from_filename(jar.name))
                            found_slug = None
                            for q in slug_candidates:
                                hits = updater.search_projects(q)
                                if hits:
                                    for h in hits:
                                        if h.get('slug'):
                                            found_slug = h.get('slug')
                                            break
                                if found_slug:
                                    break
                            if found_slug:
                                versions = updater.get_project_versions(found_slug)
                                if versions:
                                    compatible_versions = [v for v in versions if updater._version_is_compatible(v, game_version)]
                                    if not compatible_versions:
                                        incompatible_installed.append(jar.name)
                        except Exception:
                            pass
                        if cand:
                            try:
                                local_info = updater._extract_info_from_jar(jar)
                                local_ver_text = local_info.get('version')
                                local_nv = updater._parse_numeric_ver(local_ver_text) if local_ver_text else None
                                if local_nv is None:
                                    local_nv = updater._parse_numeric_ver(jar.name)
                                chosen_text = cand.get('version_number') or cand.get('chosen_filename') or ''
                                chosen_nv = updater._parse_numeric_ver(chosen_text)
                                if local_nv and chosen_nv and chosen_nv < local_nv:
                                    self.parent._safe_append_log(f"[Updater] Skipping candidate for {jar.name}: chosen {cand.get('version_number') or cand.get('chosen_filename')} older than local")
                                    downgrade_candidates[jar.name] = {'jar': jar, **cand}
                                    results[jar.name] = 'Remote version older than local (skipped)'
                                else:
                                    candidates[jar.name] = {'jar': jar, **cand}
                            except Exception:
                                candidates[jar.name] = {'jar': jar, **cand}
                        if progress:
                            progress(idx, total, f"Scanned {idx}/{total}")
                    except Exception as e:
                        self.parent._safe_append_log(f"[Updater] Error scanning {jar.name}: {e}")
                        results[jar.name] = f"Error scanning: {e}"
                if cancel_event.is_set():
                    results = {'__aborted__': 'Aborted'}
                else:
                    if incompatible_installed:
                        def show_incompat_warning():
                            msg = "The following installed mods do not appear to have any release compatible with this instance's Minecraft version (" + str(game_version) + "):\n\n"
                            msg += "\n".join(incompatible_installed[:50])
                            msg += "\n\nYou can either: upgrade the instance Minecraft version, remove the listed mods, or proceed and skip updating them.\n\nProceed and skip incompatible mods?"
                            return messagebox.askyesno(self.parent._t("MODS_INCOMPAT_WARNING_TITLE") if hasattr(self.parent, '_t') else "Incompatible mods detected", msg)
                        proceed = [False]
                        evt = threading.Event()
                        def ask_ui():
                            try:
                                proceed[0] = show_incompat_warning()
                            except Exception:
                                proceed[0] = False
                            finally:
                                evt.set()
                        self.parent.after(0, ask_ui)
                        while not evt.wait(timeout=0.1):
                            if cancel_event.is_set():
                                break
                        if cancel_event.is_set() or not proceed[0]:
                            self.parent._safe_append_log("[Updater] Aborted due to incompatible installed mods or user cancelled")
                            results = {'__aborted__': 'Aborted (incompatible mods)'}
                            for nm in incompatible_installed:
                                results[nm] = 'Incompatible with instance (no compatible release)'
                        else:
                            for nm in incompatible_installed:
                                if nm in candidates:
                                    del candidates[nm]
                    if not candidates:
                        self.parent._safe_append_log("[Updater] No updates found for installed mods")
                        for jar in jars:
                            if jar.name not in results:
                                results[jar.name] = 'No update available'
                    else:
                        selection_event = threading.Event()
                        selected = {'names': None, 'force': False}
                        def show_confirm():
                            try:
                                dlg2 = tk.Toplevel(self.parent)
                                dlg2.title(self.parent._t("MODS_UPDATE_CONFIRM_TITLE") if hasattr(self.parent, '_t') else "Confirm Updates")
                                dlg2.transient(self.parent)
                                dlg2.grab_set()
                                tm = self.parent.theme_manager
                                bg = tm.get_color('bg_primary')
                                fg = tm.get_color('fg_primary')
                                dlg2.configure(bg=bg)
                                tk.Label(dlg2, text=self.parent._t("MODS_UPDATE_CONFIRM_HEADER") if hasattr(self.parent, '_t') else "Select mods to update:", bg=bg, fg=fg, font=("Segoe UI", 11, "bold")).pack(padx=12, pady=(12, 6))
                                list_frame = tk.Frame(dlg2, bg=bg)
                                list_frame.pack(fill='both', expand=True, padx=8, pady=6)
                                canvas = tk.Canvas(list_frame, bg=tm.get_color('bg_secondary'), highlightthickness=0)
                                scrollbar = ttk.Scrollbar(list_frame, orient="vertical", command=canvas.yview, style="Modern.Vertical.TScrollbar")
                                inner = tk.Frame(canvas, bg=tm.get_color('bg_secondary'))
                                inner.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
                                canvas.create_window((0,0), window=inner, anchor='nw')
                                canvas.configure(yscrollcommand=scrollbar.set)
                                canvas.pack(side='left', fill='both', expand=True)
                                scrollbar.pack(side='right', fill='y')

                                checks = {}
                                downgrade_checks = {}
                                images = {}
                                for name, info in candidates.items():
                                    row = tk.Frame(inner, bg=tm.get_color('bg_secondary'))
                                    row.pack(fill='x', pady=4, padx=6)
                                    var = tk.BooleanVar(value=True)
                                    cb = tk.Checkbutton(row, variable=var, bg=tm.get_color('bg_secondary'))
                                    cb.pack(side='left')
                                    img_label = tk.Label(row, bg=tm.get_color('bg_secondary'))
                                    img_label.pack(side='left', padx=(6,8))
                                    icon_url = info.get('project_icon_url')
                                    if icon_url:
                                        try:
                                            icon_bytes = _cached_image_get(icon_url)
                                            img = Image.open(io.BytesIO(icon_bytes)).convert('RGBA')
                                            img = img.resize((32,32), Image.Resampling.LANCZOS)
                                            photo = ImageTk.PhotoImage(img)
                                            images[name] = photo
                                            img_label.config(image=photo)
                                        except Exception:
                                            pass
                                    lbl_text = f"{info.get('project_title')} → {info.get('version_number')}  (installed: {name})"
                                    tk.Label(row, text=lbl_text, bg=tm.get_color('bg_secondary'), fg=tm.get_color('fg_primary')).pack(side='left')
                                    checks[name] = var

                                if downgrade_candidates:
                                    sep = tk.Label(inner, text="", bg=tm.get_color('bg_secondary'))
                                    sep.pack(fill='x')
                                    warn_label = tk.Label(inner, text="Candidates skipped due to downgrade protection:", bg=tm.get_color('bg_secondary'), fg=tm.get_color('accent_primary'), font=("Segoe UI", 9, "italic"))
                                    warn_label.pack(anchor='w', padx=6, pady=(6, 0))
                                    for name, info in downgrade_candidates.items():
                                        row = tk.Frame(inner, bg=tm.get_color('bg_secondary'))
                                        row.pack(fill='x', pady=2, padx=6)
                                        var = tk.BooleanVar(value=False)
                                        cb = tk.Checkbutton(row, variable=var, bg=tm.get_color('bg_secondary'))
                                        cb.pack(side='left')
                                        cb.config(state='disabled')
                                        img_label = tk.Label(row, bg=tm.get_color('bg_secondary'))
                                        img_label.pack(side='left', padx=(6,8))
                                        icon_url = info.get('project_icon_url')
                                        if icon_url:
                                            try:
                                                icon_bytes = _cached_image_get(icon_url)
                                                img = Image.open(io.BytesIO(icon_bytes)).convert('RGBA')
                                                img = img.resize((32,32), Image.Resampling.LANCZOS)
                                                photo = ImageTk.PhotoImage(img)
                                                images[name] = photo
                                                img_label.config(image=photo)
                                            except Exception:
                                                pass
                                        lbl_text = f"{info.get('project_title')} → {info.get('version_number')}  (installed: {name})"
                                        tk.Label(row, text=lbl_text, bg=tm.get_color('bg_secondary'), fg=tm.get_color('fg_primary')).pack(side='left')
                                        note = tk.Label(row, text=' (skipped - older than local)', bg=tm.get_color('bg_secondary'), fg=tm.get_color('fg_disabled'))
                                        note.pack(side='left')
                                        downgrade_checks[name] = (var, cb)

                                btns = tk.Frame(dlg2, bg=bg)
                                btns.pack(pady=(8,12))
                                force_state = {'enabled': False}
                                def on_force_all():
                                    force_state['enabled'] = True
                                    for nm, (v, box) in downgrade_checks.items():
                                        try:
                                            box.config(state='normal')
                                            v.set(True)
                                        except Exception:
                                            pass
                                    try:
                                        force_btn.config(text=self.parent._t('MODS_FORCE_ENABLED') if hasattr(self.parent, '_t') else 'Force Enabled')
                                    except Exception:
                                        pass
                                    selected['force'] = True
                                force_btn = tk.Button(btns, text=self.parent._t('MODS_FORCE_BTN') if hasattr(self.parent, '_t') else "Force Update All", command=on_force_all, bg=tm.get_color('accent_primary'), fg=tm.get_color('fg_primary'))
                                force_btn.pack(side='left', padx=6)
                                def select_all():
                                    for v in checks.values():
                                        v.set(True)
                                def deselect_all():
                                    for v in checks.values():
                                        v.set(False)
                                tk.Button(btns, text=self.parent._t("SELECT_ALL") if hasattr(self.parent, '_t') else "Select All", command=select_all, bg=tm.get_color('accent_primary'), fg=tm.get_color('fg_primary')).pack(side='left', padx=6)
                                tk.Button(btns, text=self.parent._t("DESELECT_ALL") if hasattr(self.parent, '_t') else "Deselect All", command=deselect_all, bg=tm.get_color('bg_hover'), fg=tm.get_color('fg_primary')).pack(side='left', padx=6)
                                def on_confirm():
                                    names = [n for n, v in checks.items() if v.get()]
                                    names += [n for n, (v, _) in downgrade_checks.items() if v.get()]
                                    selected['names'] = names
                                    try:
                                        dlg2.destroy()
                                    except Exception:
                                        pass
                                    selection_event.set()
                                def on_cancel2():
                                    selected['names'] = []
                                    try:
                                        dlg2.destroy()
                                    except Exception:
                                        pass
                                    selection_event.set()
                                tk.Button(btns, text=self.parent._t("CONFIRM") if hasattr(self.parent, '_t') else "Confirm", command=on_confirm, bg=tm.get_color('accent_primary'), fg=tm.get_color('fg_primary')).pack(side='left', padx=6)
                                tk.Button(btns, text=self.parent._t("CANCEL") if hasattr(self.parent, '_t') else "Cancel", command=on_cancel2, bg=tm.get_color('bg_hover'), fg=tm.get_color('fg_primary')).pack(side='left', padx=6)
                                dlg2.wait_window()
                            except Exception as e:
                                self.parent._safe_append_log(f"[Updater] Error showing confirmation dialog: {e}")
                                selected['names'] = []
                                selection_event.set()

                        self.parent.after(0, show_confirm)
                        while not selection_event.wait(timeout=0.1):
                            if cancel_event.is_set():
                                break

                        sel = selected.get('names')
                        if not sel:
                            results = {jar.name: 'Skipped by user' for jar in jars}
                        else:
                            results = {}
                            sel_set = set(sel)
                            to_update = [(name, candidates[name]['jar']) for name in sel if name in candidates]
                            total2 = len(to_update)
                            for idx2, (name, jar) in enumerate(to_update, start=1):
                                if cancel_event.is_set():
                                    results['__aborted__'] = 'Aborted'
                                    break
                                if progress:
                                    progress(idx2 - 1, total2, f"Updating {name}...")
                                try:
                                    force_flag = selected.get('force', False)
                                    updated, msg = updater.update_mod(jar, loader, game_version, aggressive=False, force=force_flag)
                                    results[name] = msg if not updated else f"Updated: {msg}"
                                except Exception as e:
                                    results[name] = f"Error: {e}"
                                if progress:
                                    progress(idx2, total2, f"Processed {idx2}/{total2}")
                updated = [k for k, v in results.items() if v and v.startswith('Updated')]
                skipped = [k for k, v in results.items() if v in ('No project found', 'No versions found', 'No compatible version found', 'No downloadable files', 'Already up to date', 'Remote version older than local (skipped)', 'No update available', 'Skipped by user')]
                errors = [f"{k}: {v}" for k, v in results.items() if v and (v.startswith('Error') or v.startswith('Failed') or (not v.startswith('Updated') and k not in skipped))]
                summary = f"Updated: {len(updated)}, Skipped: {len(skipped)}, Errors: {len(errors)}"
                self.parent._safe_append_log(f"[Updater] {summary}")
                for k, v in results.items():
                    try:
                        self.parent._safe_append_log(f"[Updater] {k}: {v}")
                    except Exception:
                        pass
                def done_ui():
                    if errors:
                        messagebox.showwarning(self.parent._t("MODS_UPDATE_DONE_TITLE") if hasattr(self.parent, '_t') else "Update Mods", f"{summary}\nErrors:\n" + "\n".join(errors[:10]))
                    else:
                        messagebox.showinfo(self.parent._t("MODS_UPDATE_DONE_TITLE") if hasattr(self.parent, '_t') else "Update Mods", summary)
                    self.refresh_mods_list()
                    try:
                        dlg.destroy()
                    except Exception:
                        pass
                self.parent.after(0, done_ui)
            except Exception as e:
                self.parent._safe_append_log(f"[Updater] Error: {e}")
                def err_ui():
                    messagebox.showerror(self.parent._t("ERROR"), str(e))
                    try:
                        dlg.destroy()
                    except Exception:
                        pass
                self.parent.after(0, err_ui)

        thread = threading.Thread(target=worker, daemon=True)
        thread.start()
    def refresh_mods_list(self):
        self.mods_listbox.delete(0, tk.END)
        if self.current_instance:
            mods_dir = self.current_instance.mods_dir
            if not mods_dir.exists():
                self.mods_listbox.insert(tk.END, "No mods installed")
                self.mods_count_label.config(text="0 mods loaded")
                return
            mods = [f.name for f in mods_dir.iterdir() if f.suffix.lower() == '.jar' and f.is_file()]
            self._all_mods = sorted(mods)
            self._apply_search_filter()
            if not mods:
                self.mods_listbox.insert(tk.END, "No mods installed")
                self.mods_count_label.config(text="0 mods loaded")
            else:
                mod_text = "1 mod loaded" if len(mods) == 1 else f"{len(mods)} mods loaded"
                self.mods_count_label.config(text=mod_text)
            return
        if not self.current_profile:
            self.mods_listbox.insert(tk.END, "No profile selected")
            self.mods_count_label.config(text="0 mods loaded")
            return
        mods = get_current_profile_mods()
        self._all_mods = sorted(mods)
        self._apply_search_filter()
        if not mods:
            self.mods_listbox.insert(tk.END, "No mods installed")
            self.mods_count_label.config(text="0 mods loaded")
        else:
            mod_text = "1 mod loaded" if len(mods) == 1 else f"{len(mods)} mods loaded"
            self.mods_count_label.config(text=mod_text)

    def _apply_search_filter(self):
        q = (self.search_var.get() or '').strip().lower()
        self.mods_listbox.delete(0, tk.END)
        if not self._all_mods:
            return
        filtered = [m for m in self._all_mods if q in m.lower()] if q else list(self._all_mods)
        if not filtered:
            self.mods_listbox.insert(tk.END, "No mods match")
        else:
            for mod in filtered:
                self.mods_listbox.insert(tk.END, mod)
def build_mods_tab(parent, mod_loader_var, instance_manager=None):
    modding_tab = ModdingTab(parent, mod_loader_var, instance_manager)
    modding_tab.build_tab()
    parent.modding_tab = modding_tab
def build_modding_tab(launcher, notebook, selected_mod_loader):
    try:
        build_mods_tab(launcher, selected_mod_loader, get_instance_manager())
    except Exception as e:
        print(f"Error building modding tab: {e}")
        modding_frame = ttk.Frame(notebook)
        notebook.add(modding_frame, text=launcher._t("MODS_TAB_TITLE"))
        content_frame = ttk.Frame(modding_frame)
        content_frame.pack(fill="both", expand=True, padx=20, pady=20)
        ttk.Label(content_frame, text=launcher._t("MODS_NOT_AVAILABLE"), 
                 style="Header.TLabel").pack(pady=20)
        ttk.Label(content_frame, text=launcher._t("MODS_MODULE_MISSING"),
                 style="News.TLabel").pack(pady=10)
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
            self.create_default_instance()
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
            if not self.instances:
                self.create_default_instance()
        except Exception as e:
            print(f"Error loading instances: {e}")
            self.create_default_instance()
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
    machine = _platform.machine().lower()
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
    return ["1G", "2G", "3G", "4G", "6G", "8G", "12G", "16G"]

def _get_system_ram_mb() -> int:
    try:
        with open('/proc/meminfo') as f:
            for line in f:
                if line.startswith('MemTotal:'):
                    return int(line.split()[1]) // 1024
    except Exception:
        pass
    return 8192

def _make_ram_slider(parent, bg, ram_var, accent, fg_primary, fg_secondary, wizard_fmt=False):
    """
    Slider-based RAM selector.
    wizard_fmt=True  → sets ram_var as "4G" / "512M"  (for create_instance)
    wizard_fmt=False → sets ram_var as "4 GB" / "512 MB" (for GameProfilesTab save logic)
    """
    total_mb = _get_system_ram_mb()
    usable_mb = max(total_mb - 2048, 1024)

    frame = tk.Frame(parent, bg=bg)

    header = tk.Frame(frame, bg=bg)
    header.pack(fill="x")
    tk.Label(header, text="RAM allocation", bg=bg, fg=fg_primary,
             font=("Segoe UI", 10)).pack(side="left")
    tk.Label(header, text=f"system: {total_mb // 1024} GB total", bg=bg, fg=fg_secondary,
             font=("Segoe UI", 9)).pack(side="left", padx=(8, 0))
    val_label = tk.Label(header, text="", bg=bg, fg=accent, font=("Segoe UI", 10, "bold"))
    val_label.pack(side="right")

    def parse_mb(s):
        s = str(s).strip().upper().replace(' ', '')
        try:
            if s.endswith('GB') or (s.endswith('G') and not s.endswith('GB')):
                return int(float(s.rstrip('GB'))) * 1024
            if s.endswith('MB') or s.endswith('M'):
                return int(float(s.rstrip('MB')))
        except Exception:
            pass
        return 4096

    slider_var = tk.IntVar(value=parse_mb(ram_var.get()))

    def on_slide(v):
        mb = round(int(float(v)) / 256) * 256
        mb = max(512, min(mb, usable_mb))
        slider_var.set(mb)
        g = mb / 1024
        if mb % 1024 == 0:
            display = f"{int(g)} GB"
            store = f"{int(g)}G" if wizard_fmt else f"{int(g)} GB"
        else:
            display = f"{mb} MB"
            store = f"{mb}M" if wizard_fmt else f"{mb} MB"
        val_label.config(text=display)
        ram_var.set(store)

    tk.Scale(frame, from_=512, to=usable_mb, resolution=256,
             variable=slider_var, orient="horizontal",
             bg=bg, fg=fg_secondary, troughcolor=accent,
             highlightthickness=0, bd=0, showvalue=False,
             activebackground=accent,
             command=on_slide).pack(fill="x", pady=(4, 0))

    on_slide(slider_var.get())
    return frame

class GameProfilesTab:
    def __init__(self, parent, notebook):
        self.notebook = notebook
        self.parent = parent
        self.instance_manager = get_instance_manager()
        self.profile_manager = get_game_profile_manager()
        self.selected_instance_id = None
        self.editing_instance_id = None
        self.icons = {}
        self.profile_cards = {}
        self._card_icon_cache = {}  
        self.current_mode = "list"
        self.version_values = []
        self.custom_icon_path = None
        self.theme_manager = parent.theme_manager
    def _get_card_bg(self):
        return self.theme_manager.get_color('bg_secondary')
    def _get_card_bg_selected(self):
        return self.theme_manager.get_color('bg_hover')
    def build_tab(self):
        self._load_icons()
        self._load_versions()
        self.tab_frame = tk.Frame(self.notebook, bg=self._get_card_bg())
        self.notebook.add(self.tab_frame, text=self.parent._t('GAME_PROFILES_TITLE'))
        self.container = tk.Frame(self.tab_frame, bg=self._get_card_bg())

        self.container.pack(fill="both", expand=True, padx=16, pady=12)
        self.list_view = tk.Frame(self.container, bg=self._get_card_bg())
        self.list_view.pack(fill="both", expand=True)
        self._build_list_view()
        self._build_form_view()
        self._build_settings_view()
        self._show_list_view()
        self._refresh_profiles_list()
    def _load_icons(self):
        base = find_resource("oranglauncher/images")
        if not base or not base.exists():
            print("Warning: images directory not found")
            def _load_icon(filename):
                return None
            self.icons["vanilla"] = _load_icon('minecraft-green.png')
            self.icons["modded"] = _load_icon('minecraft-blue.png')
            return
        def _load_icon(filename):
            try:
                image_path = base / filename
                if not image_path.exists():
                    return None
                image = tk.PhotoImage(file=str(image_path))
                width = image.width() or 64
                scale = max(width // 64, 1)
                if scale > 1:
                    image = image.subsample(scale, scale)
                return image
            except Exception as err:
                print(f"[DEBUG] Failed to load icon {filename}: {err}")
                return None
        self.icons["vanilla"] = _load_icon('minecraft-green.png')
        self.icons["modded"] = _load_icon('minecraft-blue.png')
    def _load_versions(self):
        try:
            versions = get_available_versions()
            self.version_values = versions if versions else []
        except Exception as e:
            print(f"[DEBUG] Failed to load versions: {e}")
            self.version_values = []
    def _build_list_view(self):
        header = tk.Label(
            self.list_view,
            text=self.parent._t("GAME_PROFILES_TITLE"),
            bg=self._get_card_bg(),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 14, "bold")
        )
        header.pack(anchor="w", pady=(0, 12))
        canvas_wrapper = tk.Frame(self.list_view, bg=self._get_card_bg())
        canvas_wrapper.pack(fill="both", expand=True)
        self.cards_canvas = tk.Canvas(canvas_wrapper, highlightthickness=0, bg=self._get_card_bg())
        scrollbar = ttk.Scrollbar(canvas_wrapper, orient="vertical", command=self.cards_canvas.yview, style="Modern.Vertical.TScrollbar")
        self.cards_canvas.configure(yscrollcommand=scrollbar.set)
        self.cards_canvas.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")
        self.cards_inner = tk.Frame(self.cards_canvas, bg=self._get_card_bg())
        self.cards_window_id = self.cards_canvas.create_window((0, 0), window=self.cards_inner, anchor="n")
        self.cards_canvas.bind("<Configure>", lambda e: self.cards_canvas.itemconfig(self.cards_window_id, width=e.width))
        self.cards_inner.bind("<Configure>", lambda e: self.cards_canvas.configure(scrollregion=self.cards_canvas.bbox("all")))
        button_bar = tk.Frame(self.list_view, bg=self._get_card_bg())
        button_bar.pack(fill="x", pady=(12, 0))
        
        def get_btn_icon(name):
             return self.parent._load_themed_icon(name, size=(16, 16))

        new_icon = get_btn_icon("plus")
        new_btn = tk.Button(
            button_bar,
            text=f"  {self.parent._t('GAME_PROFILES_NEW')}",
            image=new_icon,
            compound="left",
            command=self._open_create_form,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        new_btn._icon = new_icon
        new_btn.pack(side="left", padx=(0, 6))
        
        dup_icon = get_btn_icon("dublicate")
        dup_btn = tk.Button(
            button_bar,
            text=f"  {self.parent._t('GAME_PROFILES_DUPLICATE')}",
            image=dup_icon,
            compound="left",
            command=self._duplicate_selected,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        dup_btn._icon = dup_icon
        dup_btn.pack(side="left", padx=(0, 6))
        
        del_icon = get_btn_icon("trash")
        del_btn = tk.Button(
            button_bar,
            text=f"  {self.parent._t('GAME_PROFILES_DELETE')}",
            image=del_icon,
            compound="left",
            command=self._delete_selected,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        del_btn._icon = del_icon
        del_btn.pack(side="left")

        imp_icon = get_btn_icon("mrpack")
        imp_btn = tk.Button(
            button_bar,
            text="  Import",
            image=imp_icon,
            compound="left",
            command=self._import_instance,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        imp_btn._icon = imp_icon
        imp_btn.pack(side="left", padx=(6, 0))

        mrpack_icon = get_btn_icon("mrpack")
        mrpack_btn = tk.Button(
            button_bar,
            text=f"  {self.parent._t('MODS_IMPORT_MRPACK_BTN')}",
            image=mrpack_icon,
            compound="left",
            command=self._import_mrpack,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        mrpack_btn._icon = mrpack_icon
        mrpack_btn.pack(side="left", padx=(6, 0))

    def _create_profile_card(self, instance):
        frame = tk.Frame(self.cards_inner, bg=self._get_card_bg(), padx=16, pady=12)
        frame.pack(fill='x', expand=False, padx=8, pady=6)
        frame.grid_columnconfigure(0, weight=0, minsize=64)
        frame.grid_columnconfigure(1, weight=1)
        frame.grid_columnconfigure(2, weight=0, minsize=60)
        frame.grid_rowconfigure(0, weight=0)
        def enter(_):
            pass
        def leave(_):
            self._highlight_selection()
        frame.bind('<Enter>', enter)
        frame.bind('<Leave>', leave)
        icon = None
        icon_file = instance.base_path / "icon.txt"
        if icon_file.exists():
            try:
                with open(icon_file, 'r', encoding='utf-8') as f:
                    icon_path = f.read().strip()
                if icon_path and Path(icon_path).exists():
                    try:
                        mtime = Path(icon_path).stat().st_mtime
                        cache_key = (instance.instance_id, mtime)
                        if cache_key not in self._card_icon_cache:
                            custom_icon = tk.PhotoImage(file=icon_path)
                            width = custom_icon.width() or 64
                            scale = max(width // 64, 1)
                            if scale > 1:
                                self._card_icon_cache[cache_key] = custom_icon.subsample(scale, scale)
                            else:
                                self._card_icon_cache[cache_key] = custom_icon
                        icon = self._card_icon_cache[cache_key]
                    except Exception as e:
                        print(f"Failed to load custom icon: {e}")
            except Exception as e:
                print(f"Failed to read icon file: {e}")
        if not icon:
            icon_key = 'vanilla' if instance.mod_loader.lower() == 'vanilla' else 'modded'
            icon = self.icons.get(icon_key)
        icon_label = tk.Label(frame, bg=self._get_card_bg())
        if icon:
            icon_label.configure(image=icon)
            icon_label.image = icon  # type: ignore
        icon_label.grid(row=0, column=0, sticky='nw', padx=(0, 12))
        info_frame = tk.Frame(frame, bg=self._get_card_bg())
        info_frame.grid(row=0, column=1, sticky='nsew')
        name_lbl = tk.Label(info_frame, text=instance.name, font=('Segoe UI', 12, 'bold'), fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg())
        name_lbl.pack(anchor='w')
        version_text = f"MC version {instance.version}"
        if instance.mod_loader.lower() != 'vanilla':
            version_text += f" | {instance.mod_loader.title()}"
        subtitle = tk.Label(info_frame, text=version_text, font=('Segoe UI', 9), fg=self.theme_manager.get_color('fg_tertiary'), bg=self._get_card_bg())
        subtitle.pack(anchor='w', pady=(2, 0))
        pt = getattr(instance, 'play_time', 0) or 0
        if pt >= 3600:
            pt_text = f"⏱ {pt // 3600}h {(pt % 3600) // 60}m"
        elif pt >= 60:
            pt_text = f"⏱ {pt // 60}m"
        elif pt > 0:
            pt_text = f"⏱ {pt}s"
        else:
            pt_text = "⏱ Never played"
        playtime_lbl = tk.Label(info_frame, text=pt_text, font=('Segoe UI', 8),
                                fg=self.theme_manager.get_color('fg_tertiary'), bg=self._get_card_bg())
        playtime_lbl.pack(anchor='w', pady=(1, 0))
        actions = tk.Frame(frame, bg=self._get_card_bg())
        actions.grid(row=0, column=2, sticky='n', padx=(12, 0))
        actions.grid_columnconfigure(0, weight=1)
        
        buttons_icon = self.parent._load_themed_icon("4buttons", size=(24, 24))
        
        menu_btn = tk.Button(
            actions,
            image=buttons_icon,
            command=lambda i=instance: self._show_instance_popup(menu_btn, i),
            bg=self._get_card_bg(),
            activebackground=self._get_card_bg(),
            bd=0,
            cursor="hand2",
            relief="flat"
        )
        menu_btn.image = buttons_icon  # type: ignore
        menu_btn.grid(row=0, column=0, sticky='ew', pady=2)

        for widget in (frame, icon_label, info_frame, name_lbl, subtitle, actions):
            widget.bind('<Button-1>', lambda e, inst_id=instance.instance_id: self._select_card(inst_id))
        self.profile_cards[instance.instance_id] = frame

    def _show_instance_popup(self, btn, instance):
        popup = tk.Toplevel(self.parent)
        popup.overrideredirect(True)
        popup.attributes('-topmost', True)
        
        popup.geometry("+10000+10000")

        bg_color = self.theme_manager.get_color('bg_secondary')
        fg_color = self.theme_manager.get_color('fg_primary')
        hover_color = self.theme_manager.get_color('bg_hover')
        border_color = self.theme_manager.get_color('border_primary')
        
        container = tk.Frame(popup, bg=bg_color, bd=1, relief="solid")
        container.pack(fill="both", expand=True)
        
        def close_popup(e=None):
            popup.destroy()
            
        popup.bind("<FocusOut>", lambda e: popup.destroy())
        popup.focus_set()

        def create_menu_item(text, icon_name, command, text_color=None):
            if text_color is None:
                text_color = fg_color
                
            item_frame = tk.Frame(container, bg=bg_color)
            item_frame.pack(fill="x")
            
            icon = self.parent._load_themed_icon(icon_name, size=(16, 16), force_color=text_color)
            
            btn = tk.Button(
                item_frame,
                text=f"  {text}",
                image=icon,
                compound="left",
                bg=bg_color,
                fg=text_color,
                activebackground=hover_color,
                activeforeground=text_color,
                bd=0,
                relief="flat",
                anchor="w",
                padx=12,
                pady=8,
                font=("Segoe UI", 9),
                cursor="hand2",
                command=lambda: [command(), close_popup()]
            )
            btn.image = icon  # type: ignore
            btn.pack(fill="x")
            return btn

        create_menu_item(self.parent._t("GAME_PROFILES_SELECT"), "instances", 
                         lambda: self._select_and_close(instance.instance_id))
                         
        create_menu_item(self.parent._t("GAME_PROFILES_EDIT"), "settings", 
                         lambda: self._open_settings_view(instance))
                         
        def open_folder():
             path = instance.base_path
             if platform.system() == "Windows":
                 os.startfile(path)
             elif platform.system() == "Darwin":
                 subprocess.Popen(["open", str(path)])
             else:
                 subprocess.Popen(["xdg-open", str(path)])

        create_menu_item(self.parent._t("RES_SH_OPEN_FOLDER"), "folder", open_folder)

        create_menu_item("Screenshots", "folder", lambda: self._show_screenshots(instance))

        create_menu_item("Export Instance", "update", lambda: self._export_instance(instance))

        create_menu_item(self.parent._t("GAME_PROFILES_DELETE_BTN"), "trash",
                         lambda: self._delete_instance(instance),
                         text_color="#FF5555")

        popup.update_idletasks()
        pw = popup.winfo_reqwidth()
        ph = popup.winfo_reqheight()
        sw = self.parent.winfo_screenwidth()
        sh = self.parent.winfo_screenheight()

        btn_x = btn.winfo_rootx()
        btn_y = btn.winfo_rooty()
        btn_w = btn.winfo_width()
        btn_h = btn.winfo_height()

        if btn_x + btn_w + pw + 5 <= sw:
            x = btn_x + btn_w + 5
        else:
            x = max(0, btn_x - pw - 5)

        y = btn_y
        if y + ph > sh:
            y = max(0, sh - ph - 5)

        popup.geometry(f"+{x}+{y}")

    def _instance_fingerprint(self, inst):
        icon_file = inst.base_path / "icon.txt"
        icon_mtime = 0
        if icon_file.exists():
            try:
                icon_mtime = icon_file.stat().st_mtime
            except OSError:
                pass
        return (inst.instance_id, inst.name, inst.version, inst.mod_loader,
                getattr(inst, 'play_time', 0) or 0, icon_mtime)

    def _refresh_profiles_list(self):
        try:
            instances = sorted(self.instance_manager.instances.values(), key=lambda inst: inst.name.lower())
            current_ids = {inst.instance_id for inst in instances}

            new_fingerprints = {inst.instance_id: self._instance_fingerprint(inst) for inst in instances}
            old_fingerprints = getattr(self, '_profile_fingerprints', {})

            if (new_fingerprints == old_fingerprints
                    and list(new_fingerprints.keys()) == list(old_fingerprints.keys())
                    and self.profile_cards):
                return
            self._profile_fingerprints = new_fingerprints

            for frame in self.cards_inner.winfo_children():
                frame.destroy()
            self.profile_cards.clear()
            stale = [k for k in self._card_icon_cache if k[0] not in current_ids]
            for k in stale:
                del self._card_icon_cache[k]
            if not instances:
                tk.Label(self.cards_inner, text=self.parent._t("GAME_PROFILES_NO_PROFILES"), fg=self.theme_manager.get_color('fg_tertiary'), bg=self._get_card_bg(), font=("Segoe UI", 10)).pack(anchor="center", pady=40)
                self.selected_instance_id = None
                return
            for inst in instances:
                self._create_profile_card(inst)
            if self.selected_instance_id not in self.profile_cards and instances:
                self.selected_instance_id = instances[0].instance_id
            self._highlight_selection()
        except Exception as e:
            print(f"Error refreshing profiles list: {e}")
            traceback.print_exc()
    def _select_and_close(self, instance_id):
        self._select_card(instance_id)
        try:
            self.instance_manager.set_selected_instance(instance_id)
            if hasattr(self.parent, '_refresh_game_profiles'):
                self.parent._refresh_game_profiles()
            if hasattr(self.parent, '_update_profile_display'):
                self.parent._update_profile_display()
        except Exception:
            pass
        self._highlight_selection()
    def _select_card(self, instance_id):
        self.selected_instance_id = instance_id
        self._highlight_selection()
        try:
            if hasattr(self.parent, '_update_profile_display'):
                self.parent._update_profile_display()
        except Exception as e:
            pass
    def _update_child_backgrounds(self, widget, color):
        for child in widget.winfo_children():
            try:
                child.configure(bg=color)
            except Exception:
                pass
            if isinstance(child, tk.Frame):
                self._update_child_backgrounds(child, color)
    def _highlight_selection(self):
        for inst_id, frame in self.profile_cards.items():
            bg = self._get_card_bg()
            frame.configure(bg=bg)
            self._update_child_backgrounds(frame, bg)
    def _build_form_view(self):
        self.form_view = tk.Frame(self.container, bg=self._get_card_bg())
        back_btn = tk.Label(self.form_view, text=self.parent._t("GAME_PROFILES_GO_BACK"), font=("Segoe UI", 14), fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg(), cursor="hand2")
        back_btn.pack(anchor="w")
        back_btn.bind("<Button-1>", lambda e: self._show_list_view())
        self.form_title_var = tk.StringVar(value=self.parent._t("GAME_PROFILES_CREATE_TITLE"))
        tk.Label(self.form_view, textvariable=self.form_title_var, font=("Segoe UI", 16, "bold"), 
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).pack(pady=(10, 20))
        self.form_icon_label = tk.Label(self.form_view, bg=self._get_card_bg())
        self.form_icon_label.pack()
        form_container = tk.Frame(self.form_view, bg=self._get_card_bg())
        form_container.pack(pady=20, fill="x")
        form_container.columnconfigure(0, weight=1)
        form_container.columnconfigure(1, weight=0)
        form_container.columnconfigure(2, weight=0)
        form_container.columnconfigure(3, weight=1)
        self.name_var = tk.StringVar()
        self.loader_var = tk.StringVar(value="vanilla")
        self.version_var = tk.StringVar()
        self.loader_version_var = tk.StringVar()
        tk.Label(form_container, text=self.parent._t("GAME_PROFILES_NAME"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=0, column=1, padx=(0, 12), pady=6, sticky="e")
        self.name_entry = ttk.Entry(form_container, textvariable=self.name_var, width=40)
        self.name_entry.grid(row=0, column=2, sticky="w", pady=6)
        tk.Label(form_container, text=self.parent._t("GAME_PROFILES_LOADER"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=1, column=1, padx=(0, 12), pady=6, sticky="e")
        self.loader_combo = ttk.Combobox(form_container, textvariable=self.loader_var, state="readonly", values=["vanilla", "forge", "neoforge", "fabric", "quilt"], width=37)
        self.loader_combo.bind("<<ComboboxSelected>>", lambda e: self._on_loader_change())
        self.loader_combo.grid(row=1, column=2, sticky="w", pady=6)
        tk.Label(form_container, text=self.parent._t("GAME_PROFILES_VERSION"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=2, column=1, padx=(0, 12), pady=6, sticky="e")
        self.version_combo = ttk.Combobox(form_container, textvariable=self.version_var, values=self.version_values, width=37)
        self.version_combo.grid(row=2, column=2, sticky="w", pady=6)
        self.version_combo.bind("<<ComboboxSelected>>", lambda e: self._on_loader_change())
        tk.Label(form_container, text=self.parent._t("GAME_PROFILES_LOADER_VERSION"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=3, column=1, padx=(0, 12), pady=6, sticky="e")
        self.loader_version_combo = ttk.Combobox(form_container, textvariable=self.loader_version_var, width=37)
        self.loader_version_combo.grid(row=3, column=2, sticky="w", pady=6)
        buttons = tk.Frame(self.form_view, bg=self._get_card_bg())
        buttons.pack(pady=20)
        
        create_icon = self.parent._load_themed_icon("plus", size=(16, 16))
        self.form_submit_btn = tk.Button(buttons, 
                                         text=f"  {self.parent._t('GAME_PROFILES_CREATE_BTN')}", 
                                         image=create_icon,
                                         compound="left",
                                         command=self._submit_form,
                                         bg=self.theme_manager.get_color('bg_tertiary'),
                                         fg=self.theme_manager.get_color('fg_primary'),
                                         font=("Segoe UI", 9), bd=0, pady=6, padx=12, cursor="hand2", relief="flat")
        self.form_submit_btn.image = create_icon  # type: ignore
        self.form_submit_btn.pack(side="left", padx=6)
        
        discard_icon = self.parent._load_themed_icon("trash", size=(16, 16))
        discard_btn = tk.Button(buttons, 
                  text=f"  {self.parent._t('GAME_PROFILES_DISCARD_BTN')}", 
                  image=discard_icon,
                  compound="left",
                  command=self._show_list_view,
                  bg=self.theme_manager.get_color('bg_tertiary'),
                  fg=self.theme_manager.get_color('fg_primary'),
                  font=("Segoe UI", 9), bd=0, pady=6, padx=12, cursor="hand2", relief="flat")
        discard_btn.image = discard_icon  # type: ignore
        discard_btn.pack(side="left", padx=6)
    def _build_settings_view(self):
        self.settings_view = tk.Frame(self.container, bg=self._get_card_bg())
        back_btn = tk.Label(self.settings_view, text=self.parent._t("GAME_PROFILES_GO_BACK"), font=("Segoe UI", 14), fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg(), cursor="hand2")
        back_btn.pack(anchor="w")
        back_btn.bind("<Button-1>", lambda e: self._show_list_view())
        tk.Label(self.settings_view, text=self.parent._t("GAME_PROFILES_EDIT_TITLE"), font=("Segoe UI", 16, "bold"),
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).pack(pady=(10, 20))
        self.settings_icon_label = tk.Label(self.settings_view, bg=self._get_card_bg())
        self.settings_icon_label.pack()
        settings_container = tk.Frame(self.settings_view, bg=self._get_card_bg())
        settings_container.pack(pady=20, fill="x")
        settings_container.columnconfigure(0, weight=1)
        settings_container.columnconfigure(1, weight=0)
        settings_container.columnconfigure(2, weight=0)
        settings_container.columnconfigure(3, weight=1)
        self.settings_name_var = tk.StringVar()
        self.settings_loader_var = tk.StringVar()
        self.settings_version_var = tk.StringVar()
        self.settings_loader_version_var = tk.StringVar()
        self.java_install_var = tk.StringVar(value="Auto")
        self.instance_path_var = tk.StringVar()
        self.icon_path_var = tk.StringVar()
        self.fullscreen_var = tk.BooleanVar(value=False)
        self.ram_var = tk.StringVar(value="16 GB")
        tk.Label(settings_container, text=self.parent._t("GAME_PROFILES_NAME"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=0, column=1, padx=(0, 12), pady=6, sticky="e")
        name_entry = ttk.Entry(settings_container, textvariable=self.settings_name_var, width=40)
        name_entry.grid(row=0, column=2, sticky="w", pady=6)
        tk.Label(settings_container, text=self.parent._t("GAME_PROFILES_LOADER"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=1, column=1, padx=(0, 12), pady=6, sticky="e")
        self.settings_loader_combo = ttk.Combobox(settings_container, textvariable=self.settings_loader_var, width=38, state="readonly",
                                                 values=["vanilla", "forge", "neoforge", "fabric", "quilt"], style="Modern.TCombobox")
        self.settings_loader_combo.grid(row=1, column=2, sticky="w", pady=6)
        self.settings_loader_combo.bind("<<ComboboxSelected>>", lambda e: self._update_settings_loader_versions())
        tk.Label(settings_container, text=self.parent._t("GAME_PROFILES_VERSION"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=2, column=1, padx=(0, 12), pady=6, sticky="e")
        self.settings_version_combo = ttk.Combobox(settings_container, textvariable=self.settings_version_var, width=38,
                                                   values=self.version_values, style="Modern.TCombobox")
        self.settings_version_combo.grid(row=2, column=2, sticky="w", pady=6)
        self.settings_version_combo.bind("<<ComboboxSelected>>", lambda e: self._update_settings_loader_versions())
        tk.Label(settings_container, text=self.parent._t("GAME_PROFILES_LOADER_VERSION"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=3, column=1, padx=(0, 12), pady=6, sticky="e")
        self.settings_loader_version_combo = ttk.Combobox(settings_container, textvariable=self.settings_loader_version_var, width=38, state="readonly",
                                                          style="Modern.TCombobox")
        self.settings_loader_version_combo.grid(row=3, column=2, sticky="w", pady=6)
        tk.Label(settings_container, text=self.parent._t("GAME_PROFILES_JAVA_DIR"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=4, column=1, padx=(0, 12), pady=6, sticky="e")
        java_frame = tk.Frame(settings_container, bg=self._get_card_bg())
        java_frame.grid(row=4, column=2, sticky="w", pady=6)
        _java_options = ["Auto"]
        for _jv in (8, 11, 17, 21, 25):
            _jp = find_java_executable(_jv)
            if _jp and _jp not in _java_options:
                _java_options.append(_jp)
        self.java_install_combo = ttk.Combobox(java_frame, textvariable=self.java_install_var, width=28,
                                              values=_java_options, style="Modern.TCombobox")
        self.java_install_combo.pack(side="left")
        
        folder_icon = self.parent._load_themed_icon("folder", size=(16, 16))
        
        browse_java_btn = tk.Button(java_frame, text=f"  Browse", image=folder_icon, compound="left",
                                   bg=self.theme_manager.get_color('bg_tertiary'),
                                   fg=self.theme_manager.get_color('fg_primary'),
                                   font=("Segoe UI", 9), bd=0, padx=8, pady=4, cursor="hand2", relief="flat",
                                   command=self._browse_java)
        browse_java_btn.image = folder_icon  # type: ignore
        browse_java_btn.pack(side="left", padx=(6, 0))

        tk.Label(settings_container, text=self.parent._t("GAME_PROFILES_PATH"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=5, column=1, padx=(0, 12), pady=6, sticky="e")
        path_frame = tk.Frame(settings_container, bg=self._get_card_bg())
        path_frame.grid(row=5, column=2, sticky="w", pady=6)
        self.path_entry = ttk.Entry(path_frame, textvariable=self.instance_path_var, width=30, state="readonly")
        self.path_entry.pack(side="left")
        
        open_btn = tk.Button(path_frame, text=f"  {self.parent._t('GAME_PROFILES_OPEN_BTN')}", image=folder_icon, compound="left",
                             bg=self.theme_manager.get_color('bg_tertiary'),
                             fg=self.theme_manager.get_color('fg_primary'),
                             font=("Segoe UI", 9), bd=0, padx=8, pady=4, cursor="hand2", relief="flat",
                             command=self._open_instance_folder)
        open_btn.image = folder_icon  # type: ignore
        open_btn.pack(side="left", padx=(6, 0))

        tk.Label(settings_container, text=self.parent._t("GAME_PROFILES_ICON"), width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=6, column=1, padx=(0, 12), pady=6, sticky="e")
        icon_frame = tk.Frame(settings_container, bg=self._get_card_bg())
        icon_frame.grid(row=6, column=2, sticky="w", pady=6)
        self.icon_entry = ttk.Entry(icon_frame, textvariable=self.icon_path_var, width=30)
        self.icon_entry.pack(side="left")
        
        browse_icon_btn = tk.Button(icon_frame, text="  Browse", image=folder_icon, compound="left",
                                    bg=self.theme_manager.get_color('bg_tertiary'),
                                    fg=self.theme_manager.get_color('fg_primary'),
                                    font=("Segoe UI", 9), bd=0, padx=8, pady=4, cursor="hand2", relief="flat",
                                    command=self._browse_icon)
        browse_icon_btn.image = folder_icon  # type: ignore
        browse_icon_btn.pack(side="left", padx=(6, 0))
        
        tk.Label(settings_container, text="Env Vars", width=18, anchor="e",
                 fg=self.theme_manager.get_color('fg_primary'), bg=self._get_card_bg()).grid(row=7, column=1, padx=(0, 12), pady=6, sticky="ne")
        env_frame = tk.Frame(settings_container, bg=self._get_card_bg())
        env_frame.grid(row=7, column=2, sticky="w", pady=6)
        self.settings_env_vars_text = tk.Text(env_frame, width=40, height=4,
                                               bg=self.theme_manager.get_color('bg_secondary'),
                                               fg=self.theme_manager.get_color('fg_primary'),
                                               insertbackground=self.theme_manager.get_color('fg_primary'),
                                               font=("Consolas", 9), bd=1, relief="solid",
                                               highlightthickness=0)
        self.settings_env_vars_text.pack()
        tk.Label(env_frame, text="One var per line: KEY=VALUE
                 fg=self.theme_manager.get_color('fg_secondary'), bg=self._get_card_bg(),
                 font=("Segoe UI", 8)).pack(anchor="w")

        ram_frame = tk.Frame(settings_container, bg=self._get_card_bg())
        ram_frame.grid(row=8, column=1, columnspan=2, pady=(8, 4), sticky="ew", padx=(12, 0))
        _make_ram_slider(ram_frame, self._get_card_bg(), self.ram_var,
                         self.theme_manager.get_color('accent_primary'),
                         self.theme_manager.get_color('fg_primary'),
                         self.theme_manager.get_color('fg_secondary'),
                         wizard_fmt=False).pack(fill="x")
        
        buttons = tk.Frame(self.settings_view, bg=self._get_card_bg())
        buttons.pack(pady=20)
        
        save_icon = self.parent._load_themed_icon("update", size=(16, 16))
        save_btn = tk.Button(buttons, text=f"  {self.parent._t('GAME_PROFILES_SAVE_BTN')}", 
                  image=save_icon, compound="left",
                  command=self._submit_settings,
                  bg=self.theme_manager.get_color('bg_tertiary'),
                  fg=self.theme_manager.get_color('fg_primary'),
                  font=("Segoe UI", 9), bd=0, pady=6, padx=12, cursor="hand2", relief="flat")
        save_btn.image = save_icon  # type: ignore
        save_btn.pack(side="left", padx=6)
        
        discard_icon = self.parent._load_themed_icon("trash", size=(16, 16))
        discard_btn = tk.Button(buttons, text=f"  {self.parent._t('GAME_PROFILES_DISCARD_BTN')}", 
                  image=discard_icon, compound="left",
                  command=self._show_list_view,
                  bg=self.theme_manager.get_color('bg_tertiary'),
                  fg=self.theme_manager.get_color('fg_primary'),
                  font=("Segoe UI", 9), bd=0, pady=6, padx=12, cursor="hand2", relief="flat")
        discard_btn.image = discard_icon  # type: ignore
        discard_btn.pack(side="left", padx=6)
    def _on_loader_change(self):
        loader = self.loader_var.get().lower()
        icon_key = "vanilla" if loader == "vanilla" else "modded"
        icon = self.icons.get(icon_key)
        self.form_icon_label.configure(image=icon)
        self.form_icon_label.image = icon  # type: ignore
        self.loader_version_combo.configure(state="disabled" if loader == "vanilla" else "readonly")
        if loader == "vanilla":
            self.loader_version_var.set("N/A")
        else:
            self.loader_version_var.set("Loading…")
            self.loader_version_combo.configure(values=["Loading…"])
            mc_ver = self.version_var.get()
            def _fetch_and_set(loader=loader, mc_ver=mc_ver):
                versions = self._fetch_loader_versions(loader, mc_ver)
                def _apply():
                    self.loader_version_combo.configure(values=versions)
                    if self.loader_version_var.get() not in versions:
                        self.loader_version_var.set(versions[0] if versions else "Latest")
                try:
                    self.parent.after(0, _apply)
                except Exception:
                    pass
            threading.Thread(target=_fetch_and_set, daemon=True).start()
    def _fetch_loader_versions(self, loader, mc_version):
        if not mc_version:
            return []
        try:
            if loader == "forge" and hasattr(minecraft_launcher_lib, "forge"):
                forge_versions = minecraft_launcher_lib.forge.list_forge_versions()
                return [v for v in reversed(forge_versions) if v.startswith(f"{mc_version}-")]
            if loader == "neoforge":
                from minecraft_launcher_lib.mod_loader import Neoforge
                nf = Neoforge()
                versions = nf.get_loader_versions(mc_version, True)
                if not versions:
                    versions = nf.get_loader_versions(mc_version, False)
                return versions
            if loader == "fabric" and hasattr(minecraft_launcher_lib, "fabric"):
                return [v["version"] for v in minecraft_launcher_lib.fabric.get_all_loader_versions()]
            if loader == "quilt" and hasattr(minecraft_launcher_lib, "quilt"):
                return [v["version"] for v in minecraft_launcher_lib.quilt.get_all_loader_versions()]
        except Exception as e:
            print(f"[DEBUG] Error fetching loader versions: {e}")
        return []
    def _open_create_form(self):
        self.current_mode = "create"
        self.editing_instance_id = None
        suggested_name = "New Profile"
        index = 1
        while self.instance_manager.get_instance_by_name(suggested_name):
            index += 1
            suggested_name = f"New Profile {index}"
        self.name_var.set(suggested_name)
        self.loader_var.set("vanilla")
        self.version_var.set(self.version_values[0] if self.version_values else "")
        self.loader_version_var.set("N/A")
        self.form_title_var.set("Create profile")
        self.form_submit_btn.config(text="Create")
        self._on_loader_change()
        self._show_form_view()
    def _open_edit_form(self, instance):
        self.current_mode = "edit"
        self.editing_instance_id = instance.instance_id
        self.form_title_var.set("Edit profile")
        self.form_submit_btn.config(text="Save")
        self.name_var.set(instance.name)
        self.loader_var.set(instance.mod_loader.lower())
        self.version_var.set(instance.version)
        self.loader_version_var.set(instance.installed_version_id or "Latest")
        self._on_loader_change()
        self._show_form_view()
    def _open_settings_view(self, instance):
        self.editing_instance_id = instance.instance_id
        self.settings_name_var.set(instance.name)
        self.settings_loader_var.set(instance.mod_loader.lower())
        self.settings_version_var.set(instance.version)
        self.settings_loader_version_var.set(instance.installed_version_id or "Latest")
        saved_java = getattr(instance, 'java_path', '') or ''
        self.java_install_var.set(saved_java if saved_java else "Auto")
        if saved_java and saved_java not in list(self.java_install_combo['values']):
            self.java_install_combo['values'] = list(self.java_install_combo['values']) + [saved_java]
        self.instance_path_var.set(str(instance.base_path))
        self.settings_env_vars_text.delete("1.0", "end")
        self.settings_env_vars_text.insert("1.0", getattr(instance, 'env_vars', '') or '')

        self._update_settings_loader_versions()
        
        icon_file = instance.base_path / "icon.txt"
        if icon_file.exists():
            try:
                with open(icon_file, 'r', encoding='utf-8') as f:
                    self.icon_path_var.set(f.read().strip())
            except:
                self.icon_path_var.set("")
        else:
            self.icon_path_var.set("")
        
        ram_value = "16 GB"
        if instance.java_args:
            match = re.search(r'-Xmx(\d+)([GM])', instance.java_args)
            if match:
                amount, unit = match.groups()
                ram_value = f"{amount} {unit}B"
        self.ram_var.set(ram_value)
        icon = None
        if self.icon_path_var.get().strip() and Path(self.icon_path_var.get().strip()).exists():
            try:
                custom_icon = tk.PhotoImage(file=self.icon_path_var.get().strip())
                width = custom_icon.width() or 64
                scale = max(width // 64, 1)
                if scale > 1:
                    icon = custom_icon.subsample(scale, scale)
                else:
                    icon = custom_icon
            except Exception as e:
                print(f"Failed to load custom icon: {e}")
        if not icon:
            icon_key = 'vanilla' if instance.mod_loader.lower() == 'vanilla' else 'modded'
            icon = self.icons.get(icon_key)
        if icon:
            self.settings_icon_label.configure(image=icon)
            self.settings_icon_label.image = icon  # type: ignore
        self.list_view.pack_forget()
        self.form_view.pack_forget()
        self.settings_view.pack(fill="both", expand=True)
    def _browse_java(self):
        dir_path = filedialog.askdirectory(
            title="Select Java Installation Directory",
            mustexist=True
        )
        if dir_path:
            self.java_install_var.set(dir_path)
            current_values = list(self.java_install_combo['values'])
            if dir_path not in current_values:
                current_values.append(dir_path)
                self.java_install_combo['values'] = current_values
    
    def _update_settings_loader_versions(self):
        loader = self.settings_loader_var.get().lower()
        version = self.settings_version_var.get()
        
        if loader == "vanilla":
            self.settings_loader_version_combo.configure(state="disabled", values=["N/A"])
            self.settings_loader_version_var.set("N/A")
        else:
            self.settings_loader_version_var.set("Loading…")
            self.settings_loader_version_combo.configure(values=["Loading…"])
            def _fetch_and_set(loader=loader, version=version):
                versions = self._fetch_loader_versions(loader, version)
                def _apply():
                    if versions:
                        self.settings_loader_version_combo.configure(state="readonly", values=versions)
                        if self.settings_loader_version_var.get() not in versions:
                            self.settings_loader_version_var.set(versions[0])
                    else:
                        self.settings_loader_version_combo.configure(state="readonly", values=["Latest"])
                        self.settings_loader_version_var.set("Latest")
                try:
                    self.parent.after(0, _apply)
                except Exception:
                    pass
            threading.Thread(target=_fetch_and_set, daemon=True).start()
    
    def _browse_icon(self):
        file_path = filedialog.askopenfilename(
            title="Select Icon",
            filetypes=[("PNG Images", "*.png"), ("All Files", "*.*")]
        )
        if file_path:
            self.icon_path_var.set(file_path)
    
    def _open_instance_folder(self):
        path = self.instance_path_var.get()
        if path and Path(path).exists():
            try:
                if os.name == 'nt':
                    os.startfile(path)
                elif os.name == 'posix':
                    subprocess.Popen(['xdg-open', path])
            except Exception as e:
                messagebox.showerror("Error", f"Failed to open folder: {e}")
    def _show_form_view(self):
        self.list_view.pack_forget()
        self.settings_view.pack_forget()
        self.form_view.pack(fill="both", expand=True)
    def _show_list_view(self):
        self.form_view.pack_forget()
        self.settings_view.pack_forget()
        self.list_view.pack(fill="both", expand=True)
        self.current_mode = "list"
        self._refresh_profiles_list()
    def _validate_version(self, version: str) -> bool:
        if version in self.version_values:
            return True
        suggestions = [v for v in self.version_values if version[:4] in v][:5]
        hint = f"\n\nDid you mean: {', '.join(suggestions)}?" if suggestions else ""
        messagebox.showerror("Unknown Version", f"'{version}' is not a valid Minecraft version.{hint}")
        return False

    def _submit_form(self):
        name = self.name_var.get().strip()
        loader = self.loader_var.get().strip().lower()
        version = self.version_var.get().strip()
        if not name or not version:
            messagebox.showerror("Invalid Profile", "Name and version are required.")
            return
        if loader not in {"vanilla", "forge", "neoforge", "fabric", "quilt"}:
            messagebox.showerror("Invalid Loader", "Please select a supported mod loader.")
            return
        if not self._validate_version(version):
            return
        if self.current_mode == "create":
            try:
                new_instance = self.instance_manager.create_instance(name, version, loader)
            except ValueError as e:
                messagebox.showerror(self.parent._t("GAME_PROFILES_CREATE_TITLE"), str(e))
                return
            except Exception as e:
                messagebox.showerror(self.parent._t("GAME_PROFILES_CREATE_TITLE"), self.parent._t("GAME_PROFILES_CREATE_ERROR"))
                return
            self.selected_instance_id = new_instance.instance_id if new_instance else None
            if new_instance and hasattr(self.parent, '_apply_sharing_for_instance'):
                try:
                    self.parent._apply_sharing_for_instance(new_instance)
                except Exception as e:
                    print(f"[Sharing] Error applying symlinks to new instance: {e}")
        else:
            instance = self.instance_manager.get_instance(self.editing_instance_id)
            if not instance:
                messagebox.showerror(self.parent._t("GAME_PROFILES_EDIT_TITLE"), self.parent._t("GAME_PROFILES_NOT_FOUND"))
                self._show_list_view()
                return
            version_changed = instance.version != version
            loader_changed = instance.mod_loader.lower() != loader
            instance.name = name
            instance.version = version
            instance.mod_loader = loader
            if version_changed or loader_changed:
                instance.installed_version_id = None
            try:
                self.instance_manager.save_instances()
            except Exception as e:
                messagebox.showerror(self.parent._t("GAME_PROFILES_EDIT_TITLE"), self.parent._t("GAME_PROFILES_SAVE_ERROR"))
                return
            self.selected_instance_id = instance.instance_id
        self._show_list_view()
    def _submit_settings(self):
        instance = self.instance_manager.get_instance(self.editing_instance_id)
        if not instance:
            messagebox.showerror(self.parent._t("GAME_PROFILES_EDIT_TITLE"), self.parent._t("GAME_PROFILES_NOT_FOUND"))
            self._show_list_view()
            return
        
        new_name = self.settings_name_var.get().strip()
        new_loader = self.settings_loader_var.get().strip().lower()
        new_version = self.settings_version_var.get().strip()
        new_loader_version = self.settings_loader_version_var.get().strip()
        
        if not new_name or not new_version:
            messagebox.showerror("Invalid Input", "Name and version are required.")
            return
        if not self._validate_version(new_version):
            return

        version_changed = instance.version != new_version
        loader_changed = instance.mod_loader.lower() != new_loader
        
        instance.name = new_name
        instance.version = new_version
        instance.mod_loader = new_loader
        old_installed = instance.installed_version_id
        
        if version_changed or loader_changed:
            instance.installed_version_id = None
        elif new_loader_version and new_loader_version not in ("N/A", "Latest", ""):
            instance.installed_version_id = new_loader_version
        
        instance.env_vars = self.settings_env_vars_text.get("1.0", "end").strip()

        java_install = self.java_install_var.get().strip()
        instance.java_path = "" if java_install == "Auto" else java_install

        _rt = self.ram_var.get().strip().upper().replace(' ', '')
        if _rt.endswith('GB'):
            ram_match, ram_unit = _rt[:-2], 'G'
        elif _rt.endswith('G'):
            ram_match, ram_unit = _rt[:-1], 'G'
        elif _rt.endswith('MB'):
            ram_match, ram_unit = _rt[:-2], 'M'
        elif _rt.endswith('M'):
            ram_match, ram_unit = _rt[:-1], 'M'
        else:
            ram_match, ram_unit = _rt, 'G'
        java_args = f"-Xmx{ram_match}{ram_unit}"
        instance.java_args = java_args
        instance.ram = f"{ram_match}{ram_unit}"
        icon_file = instance.base_path / "icon.txt"
        icon_path = self.icon_path_var.get().strip()
        if icon_path:
            try:
                with open(icon_file, 'w', encoding='utf-8') as f:
                    f.write(icon_path)
            except Exception as e:
                print(f"Failed to save icon path: {e}")
        elif icon_file.exists():
            try:
                icon_file.unlink()
            except:
                pass
        try:
            self.instance_manager.save_instances()
            messagebox.showinfo("Settings Saved", "Profile settings have been saved successfully.")
            if hasattr(self.parent, '_refresh_game_profiles'):
                self.parent._refresh_game_profiles()
            if hasattr(self.parent, 'version_label'):
                instance = self.instance_manager.get_instance(self.editing_instance_id)
                if instance:
                    self.parent.version_label.config(
                        text=f"Instance: {instance.name} | {instance.version} ({instance.mod_loader})"
                    )
        except Exception as e:
            messagebox.showerror("Edit Settings", f"Failed to save changes: {e}")
            return
        self._show_list_view()
    def _show_screenshots(self, instance):
        ss_dir = instance.minecraft_dir / "screenshots"
        if ss_dir.is_symlink():
            ss_dir = ss_dir.resolve()
        if not ss_dir.exists():
            messagebox.showinfo("Screenshots", "No screenshots folder found for this instance.")
            return
        pngs = sorted(ss_dir.glob("*.png"), key=lambda p: p.stat().st_mtime, reverse=True)
        if not pngs:
            messagebox.showinfo("Screenshots", "No screenshots found.")
            return
        dlg = tk.Toplevel(self.parent)
        dlg.title(f"Screenshots — {instance.name}")
        dlg.geometry("760x520")
        dlg.configure(bg=self.theme_manager.get_color('bg_primary'))
        toolbar = tk.Frame(dlg, bg=self.theme_manager.get_color('bg_secondary'))
        toolbar.pack(fill="x", padx=8, pady=(8, 0))
        tk.Label(toolbar, text=f"{len(pngs)} screenshot(s)", bg=self.theme_manager.get_color('bg_secondary'),
                 fg=self.theme_manager.get_color('fg_secondary'), font=("Segoe UI", 9)).pack(side="left", padx=8, pady=4)
        tk.Button(toolbar, text="Open folder", bg=self.theme_manager.get_color('bg_tertiary'),
                  fg=self.theme_manager.get_color('fg_primary'), bd=0, padx=10, pady=4, cursor="hand2", relief="flat",
                  command=lambda: subprocess.Popen(["xdg-open", str(ss_dir)])).pack(side="right", padx=8, pady=4)
        canvas = tk.Canvas(dlg, bg=self.theme_manager.get_color('bg_primary'), highlightthickness=0)
        vsb = ttk.Scrollbar(dlg, orient="vertical", command=canvas.yview)
        canvas.configure(yscrollcommand=vsb.set)
        vsb.pack(side="right", fill="y")
        canvas.pack(fill="both", expand=True, padx=8, pady=8)
        grid_frame = tk.Frame(canvas, bg=self.theme_manager.get_color('bg_primary'))
        canvas.create_window((0, 0), window=grid_frame, anchor="nw")
        grid_frame.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
        thumb_refs = []
        COLS = 4
        THUMB = 160
        def _load_thumbs():
            for i, png in enumerate(pngs[:60]):
                try:
                    img = Image.open(png)
                    img.thumbnail((THUMB, THUMB))
                    photo = ImageTk.PhotoImage(img)
                    thumb_refs.append(photo)
                    row, col = divmod(i, COLS)
                    def _make_cell(photo=photo, png=png, r=row, c=col):
                        cell = tk.Frame(grid_frame, bg=self.theme_manager.get_color('bg_secondary'), bd=1, relief="solid")
                        cell.grid(row=r, column=c, padx=6, pady=6)
                        lbl = tk.Label(cell, image=photo, bg=self.theme_manager.get_color('bg_secondary'), cursor="hand2")
                        lbl.pack(padx=4, pady=4)
                        lbl.bind("<Button-1>", lambda e, p=png: subprocess.Popen(["xdg-open", str(p)]))
                        tk.Label(cell, text=png.name[:20], bg=self.theme_manager.get_color('bg_secondary'),
                                 fg=self.theme_manager.get_color('fg_tertiary'), font=("Segoe UI", 7)).pack()
                    dlg.after(0, _make_cell)
                except Exception:
                    pass
        threading.Thread(target=_load_thumbs, daemon=True).start()

    def _export_instance(self, instance):
        path = filedialog.asksaveasfilename(
            title=f"Export {instance.name}",
            defaultextension=".zip",
            filetypes=[("Zip archive", "*.zip")],
            initialfile=f"{instance.name.replace(' ', '_')}.zip"
        )
        if not path:
            return
        if hasattr(self.parent, 'status_bar_progress'):
            self.parent.status_bar_progress.config(mode='indeterminate')
            self.parent.status_bar_progress.start(15)
        def _do_export():
            try:
                with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED) as zf:
                    for f in instance.base_path.rglob("*"):
                        if f.is_symlink() or not f.is_file():
                            continue
                        zf.write(f, f.relative_to(instance.base_path))
                def done():
                    if hasattr(self.parent, 'status_bar_progress'):
                        self.parent.status_bar_progress.stop()
                        self.parent.status_bar_progress.config(mode='determinate')
                        if hasattr(self.parent, 'progress'):
                            self.parent.progress.set(0)
                    messagebox.showinfo("Export", f"Exported to:\n{path}")
                self.parent.after(0, done)
            except Exception as e:
                def err():
                    if hasattr(self.parent, 'status_bar_progress'):
                        self.parent.status_bar_progress.stop()
                        self.parent.status_bar_progress.config(mode='determinate')
                    messagebox.showerror("Export Failed", str(e))
                self.parent.after(0, err)
        threading.Thread(target=_do_export, daemon=True).start()

    def _import_instance(self):
        path = filedialog.askopenfilename(
            title="Import Instance",
            filetypes=[("Zip archive", "*.zip"), ("All files", "*.*")]
        )
        if not path:
            return
        if hasattr(self.parent, 'status_bar_progress'):
            self.parent.status_bar_progress.config(mode='indeterminate')
            self.parent.status_bar_progress.start(15)
        def _do_import():
            try:
                new_id = str(_uuid.uuid4())
                dest = InstanceManager.get_instances_dir() / new_id
                dest.mkdir(parents=True, exist_ok=True)
                with zipfile.ZipFile(path, 'r') as zf:
                    zf.extractall(dest)
                inst_json = dest / "instance.json"
                if not inst_json.exists():
                    raise FileNotFoundError("instance.json not found in zip — not a valid OrangLauncher export.")
                with open(inst_json, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                data['instance_id'] = new_id
                data['base_path'] = str(dest)
                data['minecraft_dir'] = str(dest / ".minecraft")
                with open(inst_json, 'w', encoding='utf-8') as f:
                    json.dump(data, f, indent=2)
                instance = MinecraftInstance.from_dict(data)
                self.instance_manager.instances[new_id] = instance
                self.instance_manager.save_instances()
                if hasattr(self.parent, '_apply_sharing_for_instance'):
                    self.parent._apply_sharing_for_instance(instance)
                def done():
                    if hasattr(self.parent, 'status_bar_progress'):
                        self.parent.status_bar_progress.stop()
                        self.parent.status_bar_progress.config(mode='determinate')
                        if hasattr(self.parent, 'progress'):
                            self.parent.progress.set(0)
                    self._refresh_profiles_list()
                    if hasattr(self.parent, '_refresh_game_profiles'):
                        self.parent._refresh_game_profiles()
                    messagebox.showinfo("Import", f"Imported: {data.get('name', new_id)}")
                self.parent.after(0, done)
            except Exception as e:
                def err():
                    if hasattr(self.parent, 'status_bar_progress'):
                        self.parent.status_bar_progress.stop()
                        self.parent.status_bar_progress.config(mode='determinate')
                    messagebox.showerror("Import Failed", str(e))
                self.parent.after(0, err)
        threading.Thread(target=_do_import, daemon=True).start()

    def _import_mrpack(self):
        mrpack_path = filedialog.askopenfilename(
            title=self.parent._t("MODS_IMPORT_TITLE"),
            filetypes=[("Modrinth Modpack", "*.mrpack"), ("All files", "*.*")]
        )
        if not mrpack_path:
            return
        old_status = self.parent.status_label.cget("text") if hasattr(self.parent, 'status_label') else ""
        if hasattr(self.parent, 'status_label'):
            self.parent.status_label.config(text="Importing modpack...")
        if hasattr(self.parent, 'status_bar_progress'):
            self.parent.status_bar_progress.config(mode='indeterminate')
            self.parent.status_bar_progress.start(15)
        def _restore():
            if hasattr(self.parent, 'status_bar_progress'):
                self.parent.status_bar_progress.stop()
                self.parent.status_bar_progress.config(mode='determinate')
                if hasattr(self.parent, 'progress'):
                    self.parent.progress.set(0)
            if hasattr(self.parent, 'status_label'):
                self.parent.status_label.config(text=old_status or "Ready")
        def _do():
            try:
                success, message, profile_name = import_modpack(mrpack_path, self.parent)
                def done():
                    _restore()
                    if success:
                        messagebox.showinfo(
                            self.parent._t("MODS_IMPORT_SUCCESS_TITLE"),
                            self.parent._t("MODS_IMPORT_SUCCESS_MSG").format(message=message, profile_name=profile_name)
                        )
                        self._refresh_profiles_list()
                        if hasattr(self.parent, '_refresh_game_profiles'):
                            self.parent._refresh_game_profiles()
                    else:
                        messagebox.showerror(self.parent._t("MODS_IMPORT_FAIL_TITLE"), message)
                self.parent.after(0, done)
            except Exception as e:
                self.parent.after(0, lambda: (_restore(), messagebox.showerror(self.parent._t("MODS_IMPORT_ERROR_TITLE"), str(e))))
        threading.Thread(target=_do, daemon=True).start()

    def _delete_instance(self, instance):
        if not messagebox.askyesno("Delete Profile", f"Delete profile '{instance.name}'?"):
            return
        try:
            self.instance_manager.remove_instance(instance.instance_id)
            if hasattr(self.parent, '_refresh_game_profiles'):
                self.parent._refresh_game_profiles()
        except Exception as e:
            messagebox.showerror("Delete Profile", f"Failed to delete profile: {e}")
        self._refresh_profiles_list()
    def _duplicate_selected(self):
        if not self.selected_instance_id:
            messagebox.showinfo("Duplicate Profile", "Select a profile first.")
            return
        source = self.instance_manager.get_instance(self.selected_instance_id)
        if not source:
            messagebox.showerror("Duplicate Profile", "Selected profile not found.")
            return
        base_name = f"{source.name} Copy"
        candidate = base_name
        index = 2
        while self.instance_manager.get_instance_by_name(candidate):
            candidate = f"{base_name} {index}"
            index += 1
        try:
            dup = self.instance_manager.create_instance(candidate, source.version, source.mod_loader, ram=source.ram, java_args=source.java_args)
        except Exception as e:
            messagebox.showerror("Duplicate Profile", f"Failed to duplicate profile: {e}")
            return
        self.selected_instance_id = dup.instance_id if dup else None
        self._refresh_profiles_list()
    def _delete_selected(self):
        if not self.selected_instance_id:
            messagebox.showinfo("Delete Profile", "Select a profile first.")
            return
        instance = self.instance_manager.get_instance(self.selected_instance_id)
        if not instance:
            self._refresh_profiles_list()
            return
        self._delete_instance(instance)
def build_game_profiles_tab(parent, notebook):
    tab = GameProfilesTab(parent, notebook)
    tab.build_tab()
    parent.game_profiles_tab = tab
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
    def apply_to_style(self, style: ttk.Style):
        if not self.theme_data:
            return
        colors = self.theme_data.get('colors', {})
        style.configure("TNotebook", 
                       background=colors.get('bg_primary'),
                       borderwidth=0, 
                       tabmargins=0)
        style.configure("TNotebook.Tab", 
                       padding=(8, 4), 
                       borderwidth=0)
        style.map("TNotebook.Tab", 
                 background=[("selected", colors.get('tab_selected')), 
                           ("!selected", colors.get('tab_unselected'))], 
                 foreground=[("selected", colors.get('fg_primary')), 
                           ("!selected", colors.get('fg_tertiary'))])
        style.configure("TFrame", background=colors.get('bg_primary'))
        style.configure("TLabelframe", 
                       background=colors.get('bg_primary'),
                       borderwidth=0, 
                       relief="flat")
        style.configure("TLabelframe.Label",
                       background=colors.get('bg_primary'),
                       foreground=colors.get('accent_primary'))
        style.configure("TLabel", 
                       background=colors.get('bg_primary'),
                       foreground=colors.get('fg_secondary'))
        style.configure("Header.TLabel", 
                       font=(self.get_font(), 10, "bold"),
                       background=colors.get('bg_primary'),
                       foreground=colors.get('fg_primary'))
        style.configure("News.TLabel", 
                       font=(self.get_font(), 9),
                       background=colors.get('bg_primary'),
                       foreground=colors.get('fg_secondary'))
        style.configure("TButton", 
                       background=colors.get('button_bg'),
                       foreground=colors.get('button_fg'),
                       borderwidth=1)
        style.map("TButton", 
                 background=[("active", colors.get('bg_hover')), 
                           ("pressed", colors.get('bg_pressed'))],
                 foreground=[("active", colors.get('fg_primary')),
                           ("pressed", colors.get('fg_primary'))])
        style.configure("Play.TButton", 
                       background=colors.get('play_button_bg', colors.get('accent_primary')),
                       foreground=colors.get('play_button_fg', colors.get('fg_primary')),
                       borderwidth=0, 
                       font=(self.get_font(), 11, "bold"))
        style.map("Play.TButton", 
                 background=[("active", colors.get('play_button_hover', colors.get('accent_hover'))), 
                           ("pressed", colors.get('play_button_pressed', colors.get('accent_pressed')))])
        style.configure("Settings.TButton",
                       background=colors.get('button_bg'),
                       foreground=colors.get('button_fg'),
                       borderwidth=1,
                       focuscolor="none",
                       font=(self.get_font(), 9))
        style.map("Settings.TButton",
                 background=[("active", colors.get('bg_hover')), 
                           ("pressed", colors.get('bg_pressed'))],
                 foreground=[("active", colors.get('fg_primary')),
                           ("pressed", colors.get('fg_primary'))])
        style.configure("TCombobox", 
                       fieldbackground=colors.get('bg_input'),
                       background=colors.get('bg_input'),
                       foreground=colors.get('fg_primary'),
                       borderwidth=0, 
                       selectbackground=colors.get('bg_section'),
                       selectforeground=colors.get('fg_primary'))
        style.map("TCombobox", 
                 fieldbackground=[("readonly", colors.get('bg_input'))],
                 selectbackground=[("readonly", colors.get('bg_section'))])
        style.configure("TEntry", 
                       fieldbackground=colors.get('bg_input'),
                       background=colors.get('bg_input'),
                       foreground=colors.get('fg_primary'),
                       borderwidth=0, 
                       highlightthickness=0)
        style.configure("Settings.TEntry",
                       fieldbackground=colors.get('bg_input'),
                       background=colors.get('bg_input'),
                       foreground=colors.get('fg_primary'),
                       borderwidth=1,
                       insertcolor=colors.get('fg_primary'))
        style.configure("TProgressbar", 
                       background=colors.get('progress_bar'),
                       troughcolor=colors.get('progress_track'),
                       borderwidth=0, 
                       lightcolor=colors.get('progress_bar'),
                       darkcolor=colors.get('progress_bar'))
        style.configure("TScrollbar", 
                       background=colors.get('scrollbar_thumb'),
                       troughcolor=colors.get('scrollbar_track'),
                       arrowcolor=colors.get('fg_primary'),
                       borderwidth=0)
        style.configure("Settings.TCheckbutton",
                       background=colors.get('bg_tertiary'),
                       foreground=colors.get('fg_secondary'),
                       focuscolor="none",
                       font=(self.get_font(), 9))
        style.map("Settings.TCheckbutton",
                 background=[("active", colors.get('bg_tertiary'))],
                 foreground=[("active", colors.get('fg_primary'))])
        
        try:
            _configure_enhanced_styles(style)
        except Exception:
            pass

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
class PluginSecurityError(Exception):
    pass


class PluginSandbox:
    BLOCKED_MODULES = frozenset([
        'requests', 'urllib', 'urllib2', 'urllib3', 'httplib', 'httplib2',
        'http.client', 'socket', 'socketserver', 'ssl', 'ftplib', 'smtplib',
        'poplib', 'imaplib', 'nntplib', 'telnetlib', 'xmlrpc', 'aiohttp',
        'httpx', 'pycurl', 'websocket', 'websockets', 'asyncio',
        'subprocess', 'popen2', 'commands',
        'code', 'codeop', 'compile', 'exec', 'eval',
        'ctypes', 'cffi', 'multiprocessing', 'pty', 'tty', 'termios',
        'pickle', 'shelve', 'marshal', 'dill',
        'webview', 'pywebview',
        'webbrowser', 'selenium', 'playwright',
    ])
    BLOCKED_OS_FUNCTIONS = frozenset([
        'system', 'popen', 'popen2', 'popen3', 'popen4',
        'spawnl', 'spawnle', 'spawnlp', 'spawnlpe',
        'spawnv', 'spawnve', 'spawnvp', 'spawnvpe',
        'execl', 'execle', 'execlp', 'execlpe',
        'execv', 'execve', 'execvp', 'execvpe',
        'startfile',
        'fork', 'forkpty', 'kill', 'killpg',
    ])
    BLOCKED_BUILTINS = frozenset([
        'exec', 'eval', 'compile', '__import__', 'open',
    ])
    
    _original_import = None
    _original_os_funcs = {}
    _original_tk_toplevel = None
    _original_tk_tk = None
    _sandbox_active = False
    _plugin_context = None
    
    @classmethod
    def _create_safe_open(cls, plugin_dir):
        original_open = builtins.open
        
        def safe_open(file, mode='r', *args, **kwargs):
            if any(m in mode for m in ['w', 'a', 'x', '+']):
                try:
                    file_path = Path(file).resolve()
                    plugin_path = Path(plugin_dir).resolve()
                    if not str(file_path).startswith(str(plugin_path)):
                        raise PluginSecurityError(
                            f"Plugin cannot write to files outside plugin directory: {file}"
                        )
                except Exception as e:
                    if isinstance(e, PluginSecurityError):
                        raise
                    raise PluginSecurityError(f"Invalid file path: {file}")
            return original_open(file, mode, *args, **kwargs)
        return safe_open
    
    @classmethod
    def _block_new_windows(cls):
        cls._original_tk_toplevel = tk.Toplevel.__init__
        def blocked_toplevel(self, *args, **kwargs):
            raise PluginSecurityError(
                "Plugins cannot create new windows (Toplevel). "
                "Use the provided notebook/frames instead."
            )
        
        tk.Toplevel.__init__ = blocked_toplevel
        cls._original_tk_tk = tk.Tk.__init__
        def blocked_tk(self, *args, **kwargs):
            raise PluginSecurityError(
                "Plugins cannot create new Tk root windows. "
                "Use the provided launcher interface instead."
            )
        
        tk.Tk.__init__ = blocked_tk
    
    @classmethod
    def _restore_windows(cls):
        if cls._original_tk_toplevel:
            tk.Toplevel.__init__ = cls._original_tk_toplevel
            cls._original_tk_toplevel = None
        if cls._original_tk_tk:
            tk.Tk.__init__ = cls._original_tk_tk
            cls._original_tk_tk = None
    
    @classmethod
    def _create_restricted_import(cls):
        original_import = builtins.__import__
        
        def restricted_import(name, globals=None, locals=None, fromlist=(), level=0):
            top_module = name.split('.')[0]
            if top_module in cls.BLOCKED_MODULES or name in cls.BLOCKED_MODULES:
                raise PluginSecurityError(
                    f"Plugin attempted to import blocked module: {name}"
                )
            if name.startswith('http.') or name.startswith('urllib.'):
                raise PluginSecurityError(
                    f"Plugin attempted to import blocked module: {name}"
                )
            
            return original_import(name, globals, locals, fromlist, level)
        
        return restricted_import, original_import
    
    @classmethod
    def _block_os_functions(cls):
        for func_name in cls.BLOCKED_OS_FUNCTIONS:
            if hasattr(os, func_name):
                cls._original_os_funcs[func_name] = getattr(os, func_name)
                
                def blocked_func(*args, func_name=func_name, **kwargs):
                    raise PluginSecurityError(
                        f"Plugin attempted to call blocked function: os.{func_name}"
                    )
                
                setattr(os, func_name, blocked_func)
    
    @classmethod
    def _restore_os_functions(cls):
        for func_name, original_func in cls._original_os_funcs.items():
            setattr(os, func_name, original_func)
        cls._original_os_funcs.clear()
    
    @classmethod
    def activate(cls, plugin_dir: str):
        if cls._sandbox_active:
            return
        
        cls._plugin_context = plugin_dir
        restricted_import, cls._original_import = cls._create_restricted_import()
        builtins.__import__ = restricted_import
        cls._block_os_functions()
        cls._original_open = builtins.open
        builtins.open = cls._create_safe_open(plugin_dir)
        cls._block_new_windows()
        cls._sandbox_active = True
        print("[Plugin Security] Sandbox activated")
    
    @classmethod
    def deactivate(cls):
        if not cls._sandbox_active:
            return
        if cls._original_import:
            builtins.__import__ = cls._original_import
            cls._original_import = None
        cls._restore_os_functions()
        if hasattr(cls, '_original_open'):
            builtins.open = cls._original_open
        cls._restore_windows()
        cls._sandbox_active = False
        cls._plugin_context = None
        print("[Plugin Security] Sandbox deactivated")
    
    @classmethod
    def validate_plugin_code(cls, plugin_path: str) -> tuple:
        warnings = []
        
        try:
            with open(plugin_path, 'r', encoding='utf-8') as f:
                source = f.read()
        except Exception as e:
            return False, [f"Cannot read plugin file: {e}"]
        dangerous_patterns = [
            (r'\bimport\s+requests\b', "imports 'requests' module (network)"),
            (r'\bimport\s+urllib\b', "imports 'urllib' module (network)"),
            (r'\bimport\s+socket\b', "imports 'socket' module (network)"),
            (r'\bimport\s+http\b', "imports 'http' module (network)"),
            (r'\bimport\s+asyncio\b', "imports 'asyncio' module (async/network)"),
            (r'\bfrom\s+urllib', "imports from 'urllib' (network)"),
            (r'\bfrom\s+http', "imports from 'http' (network)"),
            (r'\bimport\s+aiohttp\b', "imports 'aiohttp' module (network)"),
            (r'\bimport\s+websocket', "imports 'websocket' module (network)"),
            (r'\bimport\s+subprocess\b', "imports 'subprocess' module (process execution)"),
            (r'\bfrom\s+subprocess\b', "imports from 'subprocess' (process execution)"),
            (r'\bos\.system\s*\(', "calls os.system() (command execution)"),
            (r'\bos\.popen\s*\(', "calls os.popen() (command execution)"),
            (r'\bos\.exec', "calls os.exec*() (process execution)"),
            (r'\bos\.spawn', "calls os.spawn*() (process execution)"),
            (r'\bos\.startfile\s*\(', "calls os.startfile() (file execution)"),
            (r'["\']powershell', "contains powershell command"),
            (r'["\']pwsh', "contains pwsh command"),
            (r'["\']bash\s+-c', "contains bash -c command"),
            (r'["\']sh\s+-c', "contains sh -c command"),
            (r'\bexec\s*\(', "calls exec() (code execution)"),
            (r'\beval\s*\(', "calls eval() (code execution)"),
            (r'\bcompile\s*\(', "calls compile() (code compilation)"),
            (r'__import__\s*\(', "calls __import__() (dynamic import)"),
            (r'\bimport\s+ctypes\b', "imports 'ctypes' module (low-level access)"),
            (r'\bimport\s+pickle\b', "imports 'pickle' module (arbitrary code execution)"),
            (r'\bToplevel\s*\(', "creates Toplevel window (potential phishing)"),
            (r'\btk\.Tk\s*\(', "creates new Tk root window (potential phishing)"),
            (r'\btkinter\.Tk\s*\(', "creates new Tk root window (potential phishing)"),
            (r'\bimport\s+webview\b', "imports 'webview' module (phishing risk)"),
            (r'\bfrom\s+webview', "imports from 'webview' (phishing risk)"),
            (r'\bimport\s+webbrowser\b', "imports 'webbrowser' module (can open URLs)"),
            (r'password', "contains 'password' keyword (potential credential theft)"),
            (r'login\.live\.com', "references Microsoft login URL (phishing risk)"),
            (r'login\.microsoftonline', "references Microsoft login URL (phishing risk)"),
            (r'microsoft.*auth', "references Microsoft auth (phishing risk)"),
            (r'oauth.*microsoft', "references Microsoft OAuth (phishing risk)"),
            (r'enter.*(password|credential|token)', "asks for password/credentials (phishing)"),
            (r'(password|token|secret).*entry', "has password entry field (phishing)"),
            (r'\.bind\s*\(', "calls .bind() (opens network port)"),
            (r'\.listen\s*\(', "calls .listen() (opens server socket)"),
            (r'\.accept\s*\(', "calls .accept() (accepts connections)"),
            (r'socketserver', "uses socketserver (network server)"),
            (r'BaseHTTPServer|HTTPServer|SimpleHTTPServer', "creates HTTP server"),
        ]
        for pattern, description in dangerous_patterns:
            if re.search(pattern, source, re.IGNORECASE):
                warnings.append(f"Plugin {description}")
        
        is_safe = len(warnings) == 0
        return is_safe, warnings


class PluginBase:
    def __init__(self):
        self.name = "Unnamed Plugin"
        self.version = "1.0.0"
        self.author = "Unknown"
        self.description = "No description provided"
        self.launcher = None
    def on_load(self):
        pass
    def on_enable(self):
        pass
    def on_disable(self):
        pass
    def on_launcher_start(self, launcher):
        self.launcher = launcher
    def on_game_launch(self, profile_name: str, minecraft_version: str):
        pass
    def on_game_close(self, profile_name: str):
        pass
    def add_custom_tab(self, notebook):
        return None
    def add_menu_item(self, menu):
        pass
    def get_settings(self) -> Dict[str, Any]:
        return {}
    def set_settings(self, settings: Dict[str, Any]):
        pass
class PluginManager:
    def __init__(self, plugin_dir: str):
        self.plugin_dir = Path(plugin_dir)
        self.plugins: List[PluginBase] = []
        self.enabled_plugins: List[PluginBase] = []
        self.plugin_errors: Dict[str, str] = {}
        self.plugin_dir.mkdir(parents=True, exist_ok=True)
    def discover_plugins(self) -> List[str]:
        plugin_files = []
        if not self.plugin_dir.exists():
            return plugin_files
        for file_path in self.plugin_dir.glob("*.py"):
            if file_path.name.startswith("_"):
                continue
            if file_path.name == "plugin_manager.py":
                continue
            plugin_files.append(str(file_path))
        return plugin_files
    def load_plugin(self, plugin_path: str) -> PluginBase:
        try:
            plugin_path = Path(plugin_path)
            is_safe, warnings = PluginSandbox.validate_plugin_code(str(plugin_path))
            if not is_safe:
                warning_text = "\n  - ".join(warnings)
                error_msg = f"[Plugin Security] Blocked potentially dangerous plugin: {plugin_path.name}\n  - {warning_text}"
                print(error_msg)
                self.plugin_errors[str(plugin_path)] = error_msg
                return None
            
            PluginSandbox.activate(str(self.plugin_dir))
            
            try:
                module_name = f"oranglauncher_plugin_{plugin_path.stem}"
                spec = importlib.util.spec_from_file_location(module_name, plugin_path)
                if spec is None or spec.loader is None:
                    raise ImportError(f"Could not load spec from {plugin_path}")
                module = importlib.util.module_from_spec(spec)
                sys.modules[module_name] = module
                spec.loader.exec_module(module)
                plugin_class = None
                for item_name in dir(module):
                    item = getattr(module, item_name)
                    if (isinstance(item, type) and 
                        issubclass(item, PluginBase) and 
                        item is not PluginBase):
                        plugin_class = item
                        break
                if plugin_class is None:
                    raise ValueError(f"No plugin class found in {plugin_path}")
                plugin = plugin_class()
                plugin.on_load()
                print(f"[Plugin] Loaded: {plugin.name} v{plugin.version} by {plugin.author}")
                return plugin
            finally:
                PluginSandbox.deactivate()
                
        except PluginSecurityError as e:
            error_msg = f"[Plugin Security] Blocked: {plugin_path.name} - {str(e)}"
            print(error_msg)
            self.plugin_errors[str(plugin_path)] = error_msg
            PluginSandbox.deactivate()
            return None
        except Exception as e:
            error_msg = f"Failed to load plugin {plugin_path}: {str(e)}\n{tb.format_exc()}"
            print(error_msg)
            self.plugin_errors[str(plugin_path)] = error_msg
            PluginSandbox.deactivate()
            return None
    def load_all_plugins(self):
        plugin_files = self.discover_plugins()
        print(f"[Plugin] Discovering plugins in: {self.plugin_dir}")
        print(f"[Plugin] Found {len(plugin_files)} plugin file(s)")
        for plugin_file in plugin_files:
            plugin = self.load_plugin(plugin_file)
            if plugin:
                self.plugins.append(plugin)
                self.enabled_plugins.append(plugin)
                plugin.on_enable()
        print(f"[Plugin] Successfully loaded {len(self.plugins)} plugin(s)")
    def call_on_launcher_start(self, launcher):
        for plugin in self.enabled_plugins:
            try:
                plugin.on_launcher_start(launcher)
            except Exception as e:
                print(f"[Plugin] Error in {plugin.name}.on_launcher_start: {e}")
    def call_on_game_launch(self, profile_name: str, minecraft_version: str):
        for plugin in self.enabled_plugins:
            try:
                plugin.on_game_launch(profile_name, minecraft_version)
            except Exception as e:
                print(f"[Plugin] Error in {plugin.name}.on_game_launch: {e}")
    def call_on_game_close(self, profile_name: str):
        for plugin in self.enabled_plugins:
            try:
                plugin.on_game_close(profile_name)
            except Exception as e:
                print(f"[Plugin] Error in {plugin.name}.on_game_close: {e}")
    def add_custom_tabs(self, notebook):
        for plugin in self.enabled_plugins:
            try:
                plugin.add_custom_tab(notebook)
            except Exception as e:
                print(f"[Plugin] Error in {plugin.name}.add_custom_tab: {e}")
    def add_menu_items(self, menu):
        for plugin in self.enabled_plugins:
            try:
                plugin.add_menu_item(menu)
            except Exception as e:
                print(f"[Plugin] Error in {plugin.name}.add_menu_item: {e}")
    def disable_plugin(self, plugin: PluginBase):
        if plugin in self.enabled_plugins:
            try:
                plugin.on_disable()
                self.enabled_plugins.remove(plugin)
            except Exception as e:
                print(f"[Plugin] Error disabling {plugin.name}: {e}")
    def enable_plugin(self, plugin: PluginBase):
        if plugin not in self.enabled_plugins:
            try:
                plugin.on_enable()
                self.enabled_plugins.append(plugin)
            except Exception as e:
                print(f"[Plugin] Error enabling {plugin.name}: {e}")
    def get_all_plugins(self) -> List[PluginBase]:
        return self.plugins.copy()
    def get_enabled_plugins(self) -> List[PluginBase]:
        return self.enabled_plugins.copy()
    def cleanup(self):
        for plugin in self.enabled_plugins.copy():
            self.disable_plugin(plugin)
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
class ResourceShaderTab:
    def __init__(self, parent, instance_manager=None):
        self.parent = parent
        self.instance_manager = instance_manager
        self.manager = get_resource_shader_manager(instance_manager)
        self.theme_manager = parent.theme_manager
        self.resourcepacks_listbox = None
        self.shaderpacks_listbox = None
        self.profile_info_label = None
        self.rp_count_label = None
        self.sp_count_label = None
        self.rp_info_label = None
        self.sp_info_label = None
        self.manager.register_change_callback(self.refresh_ui)
    def build_tab(self):
        rs_frame = ttk.Frame(self.parent.notebook)
        self.parent.notebook.add(rs_frame, text=self.parent._t('RES_SH_TAB_TITLE'))
        header_frame = ttk.Frame(rs_frame)
        header_frame.pack(fill="x", padx=20, pady=20)

        ttk.Label(header_frame, text=self.parent._t("RES_SH_HEADER"),
                  style="Header.TLabel", font=("Segoe UI", 16, "bold")).pack(anchor="w")
        profile_info_frame = ttk.Frame(rs_frame)
        profile_info_frame.pack(fill="x", padx=20, pady=(0, 10))
        self.profile_info_label = ttk.Label(
            profile_info_frame,
            text=self.parent._t("RES_SH_PROFILE_LOADING"),
            style="Header.TLabel"
        )
        self.profile_info_label.pack(anchor="w")
        container = ttk.Frame(rs_frame)
        container.pack(fill="both", expand=True, padx=20, pady=(0, 20))
        self._build_resourcepacks_section(container)
        self._build_shaderpacks_section(container)
        self.refresh_ui()
    def _build_resourcepacks_section(self, parent):
        left_frame = ttk.Frame(parent)
        left_frame.pack(side="left", fill="both", expand=True, padx=(0, 10))
        rp_list_frame = ttk.LabelFrame(left_frame, text=self.parent._t("RES_SH_RP_TITLE"), style="TLabelframe")
        rp_list_frame.pack(fill="both", expand=True, pady=(0, 10))
        self.rp_count_label = ttk.Label(
            rp_list_frame,
            text=self.parent._t("RES_SH_RP_COUNT_0"),
            style="News.TLabel"
        )
        self.rp_count_label.pack(anchor="w", padx=10, pady=(5, 0))
        rp_listbox_frame = ttk.Frame(rp_list_frame)
        rp_listbox_frame.pack(fill="both", expand=True, padx=10, pady=10)
        self.resourcepacks_listbox = tk.Listbox(
            rp_listbox_frame,
            bg=self.theme_manager.get_color('bg_input'),
            fg=self.theme_manager.get_color('fg_primary'),
            selectbackground=self.theme_manager.get_color('bg_hover'),
            selectforeground=self.theme_manager.get_color('fg_primary'),
            selectmode=tk.EXTENDED
        )
        rp_scrollbar = ttk.Scrollbar(
            rp_listbox_frame,
            orient="vertical",
            command=self.resourcepacks_listbox.yview,
            style="Modern.Vertical.TScrollbar"
        )
        self.resourcepacks_listbox.configure(yscrollcommand=rp_scrollbar.set)
        self.resourcepacks_listbox.pack(side="left", fill="both", expand=True)
        rp_scrollbar.pack(side="right", fill="y")
        self.resourcepacks_listbox.bind("<<ListboxSelect>>", self.on_resourcepack_selected)
        rp_info_frame = ttk.Frame(rp_list_frame)
        rp_info_frame.pack(fill="x", padx=10, pady=(0, 10))
        self.rp_info_label = ttk.Label(
            rp_info_frame,
            text=self.parent._t("RES_SH_RP_SELECT_INFO"),
            style="News.TLabel"
        )
        self.rp_info_label.pack(anchor="w")
        rp_btn_frame = ttk.Frame(left_frame)
        rp_btn_frame.pack(fill="x", pady=(0, 10))
        
        def get_icon(name): return self.parent._load_themed_icon(name, size=(16, 16))

        add_icon = get_icon("plus")
        add_btn = tk.Button(
            rp_btn_frame,
            text=f"  {self.parent._t('RES_SH_RP_ADD')}",
            image=add_icon,
            compound="left",
            command=self.add_resourcepacks,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9), bd=0, padx=12, pady=6, cursor="hand2", relief="flat"
        )
        add_btn._icon = add_icon
        add_btn.pack(side="left", padx=(0, 5))

        rem_icon = get_icon("trash")
        rem_btn = tk.Button(
            rp_btn_frame,
            text=f"  {self.parent._t('RES_SH_REMOVE_SELECTED')}",
            image=rem_icon,
            compound="left",
            command=self.remove_selected_resourcepacks,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9), bd=0, padx=12, pady=6, cursor="hand2", relief="flat"
        )
        rem_btn._icon = rem_icon
        rem_btn.pack(side="left", padx=(0, 5))

        folder_icon = get_icon("folder")
        folder_btn = tk.Button(
            rp_btn_frame,
            text=f"  {self.parent._t('RES_SH_OPEN_FOLDER')}",
            image=folder_icon,
            compound="left",
            command=self.open_resourcepacks_folder,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9), bd=0, padx=12, pady=6, cursor="hand2", relief="flat"
        )
        folder_btn._icon = folder_icon
        folder_btn.pack(side="left")
    def _build_shaderpacks_section(self, parent):
        right_frame = ttk.Frame(parent)
        right_frame.pack(side="left", fill="both", expand=True, padx=(10, 0))
        
        sp_list_frame = ttk.LabelFrame(right_frame, text=self.parent._t("RES_SH_SP_TITLE"), style="TLabelframe")
        sp_list_frame.pack(fill="both", expand=True, pady=(0, 10))
        
        self.sp_count_label = ttk.Label(
            sp_list_frame,
            text=self.parent._t("RES_SH_SP_COUNT_0"),
            style="News.TLabel"
        )
        self.sp_count_label.pack(anchor="w", padx=10, pady=(5, 0))

        list_container = ttk.Frame(sp_list_frame)
        list_container.pack(fill="both", expand=True, padx=10, pady=5)

        self.shaderpacks_listbox = tk.Listbox(
            list_container,
            selectmode=tk.SINGLE,
            activestyle="none",
            highlightthickness=0,
            bd=0,
            bg=self.theme_manager.get_color('bg_input'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 10)
        )
        
        sp_scrollbar = ttk.Scrollbar(list_container, orient="vertical", command=self.shaderpacks_listbox.yview, style="Modern.Vertical.TScrollbar")
        self.shaderpacks_listbox.configure(yscrollcommand=sp_scrollbar.set)
        
        self.shaderpacks_listbox.pack(side="left", fill="both", expand=True)
        sp_scrollbar.pack(side="right", fill="y")
        self.shaderpacks_listbox.bind("<<ListboxSelect>>", self.on_shaderpack_selected)

        sp_info_frame = ttk.Frame(sp_list_frame)
        sp_info_frame.pack(fill="x", padx=10, pady=(0, 10))
        self.sp_info_label = ttk.Label(
            sp_info_frame,
            text=self.parent._t("RES_SH_SP_SELECT_INFO"),
            style="News.TLabel"
        )
        self.sp_info_label.pack(anchor="w")

        sp_btn_frame = ttk.Frame(right_frame)
        sp_btn_frame.pack(fill="x", pady=(0, 10))

        def get_icon(name): return self.parent._load_themed_icon(name, size=(16, 16))

        add_icon = get_icon("plus")
        add_btn = tk.Button(
            sp_btn_frame,
            text=f"  {self.parent._t('RES_SH_SP_ADD')}",
            image=add_icon,
            compound="left",
            command=self.add_shaderpacks,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9), bd=0, padx=12, pady=6, cursor="hand2", relief="flat"
        )
        add_btn._icon = add_icon
        add_btn.pack(side="left", padx=(0, 5))

        rem_icon = get_icon("trash")
        rem_btn = tk.Button(
            sp_btn_frame,
            text=f"  {self.parent._t('RES_SH_REMOVE_SELECTED')}",
            image=rem_icon,
            compound="left",
            command=self.remove_selected_shaderpacks,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9), bd=0, padx=12, pady=6, cursor="hand2", relief="flat"
        )
        rem_btn._icon = rem_icon
        rem_btn.pack(side="left", padx=(0, 5))

        folder_icon = get_icon("folder")
        folder_btn = tk.Button(
            sp_btn_frame,
            text=f"  {self.parent._t('RES_SH_OPEN_FOLDER')}",
            image=folder_icon,
            compound="left",
            command=self.open_shaderpacks_folder,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9), bd=0, padx=12, pady=6, cursor="hand2", relief="flat"
        )
        folder_btn._icon = folder_icon
        folder_btn.pack(side="left")

    def refresh_ui(self):
        context_name = self.manager.get_current_context_name()
        self.profile_info_label.config(text=self.parent._t("RES_SH_PROFILE_CURRENT", context_name=context_name))
        self.refresh_resourcepacks_list()
        self.refresh_shaderpacks_list()
    def refresh_resourcepacks_list(self):
        self.resourcepacks_listbox.delete(0, tk.END)
        packs = self.manager.get_resourcepacks()
        if not packs:
            self.resourcepacks_listbox.insert(tk.END, self.parent._t("RES_SH_RP_NONE"))
            self.rp_count_label.config(text=self.parent._t("RES_SH_RP_COUNT_0"))
        else:
            for pack in packs:
                self.resourcepacks_listbox.insert(tk.END, pack)
            if len(packs) == 1:
                pack_text = self.parent._t("RES_SH_RP_COUNT_1")
            else:
                pack_text = self.parent._t("RES_SH_RP_COUNT", count=len(packs))
            self.rp_count_label.config(text=pack_text)
    def refresh_shaderpacks_list(self):
        self.shaderpacks_listbox.delete(0, tk.END)
        packs = self.manager.get_shaderpacks()
        if not packs:
            self.shaderpacks_listbox.insert(tk.END, self.parent._t("RES_SH_SP_NONE"))
            self.sp_count_label.config(text=self.parent._t("RES_SH_SP_COUNT_0"))
        else:
            for pack in packs:
                self.shaderpacks_listbox.insert(tk.END, pack)
            if len(packs) == 1:
                pack_text = self.parent._t("RES_SH_SP_COUNT_1")
            else:
                pack_text = self.parent._t("RES_SH_SP_COUNT", count=len(packs))
            self.sp_count_label.config(text=pack_text)
    def on_resourcepack_selected(self, event):
        selection = self.resourcepacks_listbox.curselection()
        if not selection:
            self.rp_info_label.config(text=self.parent._t("RES_SH_RP_SELECT_INFO"))
            return
        if len(selection) == 1:
            pack_name = self.resourcepacks_listbox.get(selection[0])
            if pack_name != self.parent._t("RES_SH_RP_NONE"):
                pack_info = self.manager.get_pack_info(pack_name, 'resource')
                if pack_info:
                    info_text = self.parent._t("RES_SH_SELECTED_INFO", pack_name=pack_name, size=f"{pack_info['size_mb']:.2f}", type=pack_info['type'])
                    self.rp_info_label.config(text=info_text)
                else:
                    self.rp_info_label.config(text=self.parent._t("RES_SH_SELECTED_INFO_SIMPLE", pack_name=pack_name))
        else:
            self.rp_info_label.config(text=self.parent._t("RES_SH_RP_SELECTED_COUNT", count=len(selection)))
    def on_shaderpack_selected(self, event):
        selection = self.shaderpacks_listbox.curselection()
        if not selection:
            self.sp_info_label.config(text=self.parent._t("RES_SH_SP_SELECT_INFO"))
            return
        if len(selection) == 1:
            pack_name = self.shaderpacks_listbox.get(selection[0])
            if pack_name != self.parent._t("RES_SH_SP_NONE"):
                pack_info = self.manager.get_pack_info(pack_name, 'shader')
                if pack_info:
                    info_text = self.parent._t("RES_SH_SELECTED_INFO", pack_name=pack_name, size=f"{pack_info['size_mb']:.2f}", type=pack_info['type'])
                    self.sp_info_label.config(text=info_text)
                else:
                    self.sp_info_label.config(text=self.parent._t("RES_SH_SELECTED_INFO_SIMPLE", pack_name=pack_name))
        else:
            self.sp_info_label.config(text=self.parent._t("RES_SH_SP_SELECTED_COUNT", count=len(selection)))
    def add_resourcepacks(self):
        if self.manager.get_current_context_name() == "None":
            messagebox.showwarning(self.parent._t("RES_SH_NO_PROFILE_TITLE"), self.parent._t("RES_SH_NO_PROFILE_MSG"))
            return
        filetypes = [
            ("Resource Pack files", "*.zip"),
            ("All files", "*.*")
        ]
        file_paths = filedialog.askopenfilenames(
            title=self.parent._t("RES_SH_RP_SELECT_DIALOG"),
            filetypes=filetypes
        )
        if not file_paths:
            return
        added_count, failed_count = self.manager.add_resourcepacks(file_paths)
        if added_count > 0:
            if failed_count > 0:
                    def done_ui():
                        messagebox.showinfo(self.parent._t("MODS_UPDATE_DONE_TITLE") if hasattr(self.parent, '_t') else "Update Mods")
                        self.refresh_mods_list()
            messagebox.showinfo(
                    self.parent._t("SUCCESS"),
                    self.parent._t("RES_SH_RP_ADDED_SUCCESS", count=added_count)
                )
            self.refresh_resourcepacks_list()
        elif failed_count > 0:
            messagebox.showerror(
                self.parent._t("ERROR"),
                self.parent._t("RES_SH_RP_ADDED_FAIL", count=failed_count)
            )
    def add_shaderpacks(self):
        if self.manager.get_current_context_name() == "None":
            messagebox.showwarning(self.parent._t("RES_SH_NO_PROFILE_TITLE"), self.parent._t("RES_SH_NO_PROFILE_MSG"))
            return
        filetypes = [
            ("Shader Pack files", "*.zip"),
            ("All files", "*.*")
        ]
        file_paths = filedialog.askopenfilenames(
            title=self.parent._t("RES_SH_SP_SELECT_DIALOG"),
            filetypes=filetypes
        )
        if not file_paths:
            return
        added_count, failed_count = self.manager.add_shaderpacks(file_paths)
        if added_count > 0:
            if failed_count > 0:
                messagebox.showinfo(
                    self.parent._t("RES_SH_SP_ADDED_TITLE"),
                    self.parent._t("RES_SH_SP_ADDED_SUCCESS", count=added_count) + "\n" +
                    self.parent._t("RES_SH_SP_ADDED_FAIL", count=failed_count)
                )
            else:
                messagebox.showinfo(
                    self.parent._t("SUCCESS"),
                    self.parent._t("RES_SH_SP_ADDED_SUCCESS", count=added_count)
                )
            self.refresh_shaderpacks_list()
        elif failed_count > 0:
            messagebox.showerror(
                self.parent._t("ERROR"),
                self.parent._t("RES_SH_SP_ADDED_FAIL", count=failed_count)
            )
    def remove_selected_resourcepacks(self):
        selection = self.resourcepacks_listbox.curselection()
        if not selection:
            messagebox.showinfo(self.parent._t("RES_SH_RP_REMOVE_TITLE"), self.parent._t("RES_SH_RP_REMOVE_NONE"))
            return
        pack_names = [self.resourcepacks_listbox.get(i) for i in selection]
        pack_names = [name for name in pack_names if name != self.parent._t("RES_SH_RP_NONE")]
        if not pack_names:
            return
        if len(pack_names) == 1:
            confirm_msg = self.parent._t("RES_SH_DELETE_CONFIRM_SINGLE", name=pack_names[0])
        else:
            confirm_msg = self.parent._t("RES_SH_RP_DELETE_CONFIRM_MULTI", count=len(pack_names))
        confirm = messagebox.askyesno(self.parent._t("CONFIRM_REMOVE"), confirm_msg)
        if not confirm:
            return
        removed_count = self.manager.remove_resourcepacks(pack_names)
        if removed_count > 0:
            self.refresh_resourcepacks_list()
            messagebox.showinfo(
                self.parent._t("SUCCESS"),
                self.parent._t("RES_SH_RP_REMOVED_SUCCESS", count=removed_count)
            )
    def remove_selected_shaderpacks(self):
        selection = self.shaderpacks_listbox.curselection()
        if not selection:
            messagebox.showinfo(self.parent._t("RES_SH_SP_REMOVE_TITLE"), self.parent._t("RES_SH_SP_REMOVE_NONE"))
            return
        pack_names = [self.shaderpacks_listbox.get(i) for i in selection]
        pack_names = [name for name in pack_names if name != self.parent._t("RES_SH_SP_NONE")]
        if not pack_names:
            return
        if len(pack_names) == 1:
            confirm_msg = self.parent._t("RES_SH_DELETE_CONFIRM_SINGLE", name=pack_names[0])
        else:
            confirm_msg = self.parent._t("RES_SH_SP_DELETE_CONFIRM_MULTI", count=len(pack_names))
        confirm = messagebox.askyesno(self.parent._t("CONFIRM_REMOVE"), confirm_msg)
        if not confirm:
            return
        removed_count = self.manager.remove_shaderpacks(pack_names)
        if removed_count > 0:
            self.refresh_shaderpacks_list()
            messagebox.showinfo(
                self.parent._t("SUCCESS"),
                self.parent._t("RES_SH_SP_REMOVED_SUCCESS", count=removed_count)
            )
    def browse_resourcepacks_location(self):
        folder_path = filedialog.askdirectory(
            title=self.parent._t("RES_SH_RP_BROWSE_TITLE")
        )
        if folder_path:
            messagebox.showinfo(
                self.parent._t("RES_SH_RP_LOCATION_TITLE"),
                self.parent._t("RES_SH_RP_BROWSE_MSG", path=folder_path)
            )
    def browse_shaderpacks_location(self):
        folder_path = filedialog.askdirectory(
            title=self.parent._t("RES_SH_SP_BROWSE_TITLE")
        )
        if folder_path:
            messagebox.showinfo(
                self.parent._t("RES_SH_SP_LOCATION_TITLE"),
                self.parent._t("RES_SH_SP_BROWSE_MSG", path=folder_path)
            )
    def open_resourcepacks_folder(self):
        if self.manager.get_current_context_name() == "None":
            messagebox.showwarning(self.parent._t("RES_SH_NO_PROFILE_TITLE"), self.parent._t("RES_SH_NO_PROFILE_MSG"))
            return
        if not self.manager.open_resourcepacks_folder():
            messagebox.showerror(self.parent._t("ERROR"), self.parent._t("RES_SH_RP_OPEN_FAIL"))
    def open_shaderpacks_folder(self):
        if self.manager.get_current_context_name() == "None":
            messagebox.showwarning(self.parent._t("RES_SH_NO_PROFILE_TITLE"), self.parent._t("RES_SH_NO_PROFILE_MSG"))
            return
        if not self.manager.open_shaderpacks_folder():
            messagebox.showerror(self.parent._t("ERROR"), self.parent._t("RES_SH_SP_OPEN_FAIL"))
def build_res_sh_tab(launcher, notebook, instance_manager=None):
    rs_tab = ResourceShaderTab(launcher, instance_manager)
    rs_tab.build_tab()
    launcher.res_sh_tab = rs_tab
_WEBKIT_MODS = None 

def _load_webkit():
    global _WEBKIT_MODS
    if _WEBKIT_MODS is not None:
        return _WEBKIT_MODS or None
    os.environ.setdefault("GDK_BACKEND", "x11")
    try:
        
        gi.require_version("Gtk", "3.0")
        gi.require_version("GdkX11", "3.0")
        try:
            gi.require_version("WebKit2", "4.1")
        except ValueError:
            gi.require_version("WebKit2", "4.0")
        from gi.repository import Gtk, WebKit2, GdkX11
        _WEBKIT_MODS = (Gtk, WebKit2, GdkX11)
    except Exception as e:
        print(f"[news] WebKitGTK unavailable, falling back: {e}")
        _WEBKIT_MODS = False
    return _WEBKIT_MODS or None


class ChromiumWidget(tk.Frame):
    NEWS_URL = "https://oranges.lt/launcher.html"

    def __init__(self, parent):
        tm = get_theme_manager()
        super().__init__(parent, bg=tm.get_color('bg_primary'), bd=0, highlightthickness=0)
        self.parent = parent
        self.browser = None          # WebKit2.WebView
        self._gtk_win = None         # decorationless Gtk.Window reparented into us
        self._pump_id = None
        self._embedded = False
        self._destroyed = False
        self.main_window = parent.winfo_toplevel()
        self.placeholder = tk.Label(
            self,
            text="Loading news...",
            bg=tm.get_color('bg_primary'),
            fg=tm.get_color('text_primary'),
            font=('Segoe UI', 12)
        )
        self.placeholder.pack(fill="both", expand=True)
        self.bind("<Configure>", self._on_configure)
        self.after(300, self._create_browser)
    def _pump(self):
        if self._destroyed:
            return
        mods = _WEBKIT_MODS
        if mods:
            Gtk = mods[0]
            try:
                while Gtk.events_pending():
                    Gtk.main_iteration_do(False)
            except Exception:
                pass
        self._pump_id = self.after(30, self._pump)

    def _on_configure(self, event):
        if self._gtk_win is not None and event.width > 1 and event.height > 1:
            try:
                self._gtk_win.resize(event.width, event.height)
            except Exception:
                pass

    def _create_browser(self):
        if self.browser or self._destroyed:
            return
        mods = _load_webkit()
        if mods:
            try:
                self._create_webkit(mods)
                return
            except Exception as e:
                print(f"[news] WebKit embed failed, using fallback: {e}")
                tb.print_exc()
                self._gtk_win = None
        self._create_fallback()

    def _create_webkit(self, mods):
        Gtk, WebKit2, GdkX11 = mods
        self.update_idletasks()
        xid = self.winfo_id()
        w = max(self.winfo_width(), 1)
        h = max(self.winfo_height(), 1)
        gtk_win = Gtk.Window()
        gtk_win.set_decorated(False)
        web = WebKit2.WebView()
        gtk_win.add(web)
        gtk_win.realize()
        web.realize()
        display = GdkX11.X11Display.get_default()
        parent = GdkX11.X11Window.foreign_new_for_display(display, xid)
        gtk_win.get_window().reparent(parent, 0, 0)
        gtk_win.move(0, 0)
        gtk_win.resize(w, h)
        gtk_win.show_all()
        web.load_uri(self.NEWS_URL)
        self.browser = web
        self._gtk_win = gtk_win
        self._embedded = True
        self.placeholder.pack_forget()
        if self._pump_id is None:
            self._pump_id = self.after(30, self._pump)

    def _create_fallback(self):
        tm = get_theme_manager()
        try:
            self.browser = tkinterweb.HtmlFrame(self, messages_enabled=False)
            self.browser.load_website(self.NEWS_URL)
            self.browser.pack(fill="both", expand=True)
            self.placeholder.pack_forget()
        except Exception as e:
            print(f"[news] tkinterweb fallback failed: {e}")
            self.browser = tk.Text(self, wrap=tk.WORD, bg=tm.get_color('bg_primary'),
                                   fg=tm.get_color('text_primary'), bd=0)
            self.browser.pack(fill="both", expand=True)
            self.browser.insert("1.0", "Minecraft News\n\nVisit: https://oranges.lt/launcher.html\n\nFor the latest Minecraft news and updates.")
            self.browser.config(state=tk.DISABLED)
            self.placeholder.pack_forget()

    def enable_embed(self):
        if not self.browser:
            self.placeholder.pack(fill="both", expand=True)
            self._create_browser()
            return
        if self._embedded and self._gtk_win is not None:
            try:
                self._gtk_win.show()
            except Exception:
                pass
            if self._pump_id is None:
                self._pump_id = self.after(30, self._pump)

    def disable_embed(self):
        if self._embedded and self._gtk_win is not None:
            try:
                self._gtk_win.hide()
            except Exception:
                pass
            return
        if self.browser and not self._embedded:
            try:
                self.browser.destroy()
            except Exception:
                pass
            self.browser = None

    def _start_following_window(self):
        pass
    def _stop_following_window(self):
        pass
    def _follow_main_window(self):
        pass
    def _position_qt_window(self, should_be_visible=True):
        pass

    def load_url(self, url):
        if self._embedded and self.browser is not None:
            try:
                self.browser.load_uri(url)
            except Exception:
                pass
        elif self.browser and hasattr(self.browser, 'load_website'):
            self.browser.load_website(url)

    def reload_content(self):
        if self._embedded and self.browser is not None:
            try:
                self.browser.reload()
            except Exception:
                pass
        elif self.browser and hasattr(self.browser, 'load_website'):
            try:
                self.browser.load_website(self.NEWS_URL)
            except Exception as e:
                print(f"[news] reload failed: {e}")

    def destroy(self):
        self._destroyed = True
        if self._pump_id is not None:
            try:
                self.after_cancel(self._pump_id)
            except Exception:
                pass
            self._pump_id = None
        if self._gtk_win is not None:
            try:
                self._gtk_win.destroy()
            except Exception:
                pass
            self._gtk_win = None
        if hasattr(self, "browser") and self.browser and not self._embedded:
            try:
                self.browser.destroy()
            except Exception:
                pass
        self.browser = None
        super().destroy()


class OrangLibTab:
    def __init__(self, parent):
        self.parent = parent
        self.theme_manager = parent.theme_manager
        self.modpacks = []
        self.versions = []
        self.selected_modpack = None
        self.search_var = tk.StringVar()
    def build_tab(self, notebook):
        tab_frame = ttk.Frame(notebook)
        notebook.add(tab_frame, text=self.parent._t('ORANGLIB'))
        container = tk.Frame(tab_frame, bg=self.theme_manager.get_color('bg_primary'))
        container.pack(fill="both", expand=True, padx=14, pady=14)
        header = tk.Frame(container, bg=self.theme_manager.get_color('bg_primary'))
        header.pack(fill="x", pady=(0, 10))
        title = tk.Label(
            header,
            text=self.parent._t("ORANGLIB_MODPACKS_TITLE"),
            bg=self.theme_manager.get_color('bg_primary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 14, "bold")
        )
        title.pack(side="left")
        refresh_btn = tk.Button(
            header,
            text=self.parent._t("ORANGLIB_REFRESH"),
            command=self.refresh_modpacks,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        refresh_btn.pack(side="right")
        body = tk.Frame(container, bg=self.theme_manager.get_color('bg_primary'))
        body.pack(fill="both", expand=True)
        left_panel = tk.Frame(body, bg=self.theme_manager.get_color('bg_primary'))
        left_panel.pack(side="left", fill="both", expand=True, padx=(0, 8))
        left_header = tk.Frame(left_panel, bg=self.theme_manager.get_color('bg_primary'))
        left_header.pack(fill="x", padx=10, pady=(10, 6))
        tk.Label(
            left_header,
            text=self.parent._t("ORANGLIB_AVAILABLE_MODPACKS"),
            bg=self.theme_manager.get_color('bg_primary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 10, "bold")
        ).pack(side="left")

        search_entry = tk.Entry(
            left_panel,
            textvariable=self.search_var,
            bg=self.theme_manager.get_color('bg_input'),
            fg=self.theme_manager.get_color('fg_primary'),
            insertbackground=self.theme_manager.get_color('fg_primary'),
            relief="flat",
            font=("Segoe UI", 10)
        )
        search_entry.pack(fill="x", padx=10, pady=(0, 8), ipady=6)
        search_entry.insert(0, self.parent._t("ORANGLIB_SEARCH_PLACEHOLDER"))
        search_entry.config(fg=self.theme_manager.get_color('fg_secondary'))
        def _on_search_focus_in(e):
            if search_entry.get() == self.parent._t("ORANGLIB_SEARCH_PLACEHOLDER"):
                search_entry.delete(0, tk.END)
                search_entry.config(fg=self.theme_manager.get_color('fg_primary'))
        def _on_search_focus_out(e):
            if not search_entry.get():
                search_entry.insert(0, self.parent._t("ORANGLIB_SEARCH_PLACEHOLDER"))
                search_entry.config(fg=self.theme_manager.get_color('fg_secondary'))
        search_entry.bind("<FocusIn>", _on_search_focus_in)
        search_entry.bind("<FocusOut>", _on_search_focus_out)
        self.search_var.trace_add('write', lambda *_: self._render_modpacks())
        modpacks_list_wrap = tk.Frame(left_panel, bg=self.theme_manager.get_color('bg_primary'))
        modpacks_list_wrap.pack(fill="both", expand=True, padx=10, pady=(0, 10))
        self.modpacks_listbox = tk.Listbox(
            modpacks_list_wrap,
            bg=self.theme_manager.get_color('bg_input'),
            fg=self.theme_manager.get_color('fg_primary'),
            selectbackground=self.theme_manager.get_color('bg_hover'),
            selectforeground=self.theme_manager.get_color('fg_primary'),
            relief="flat",
            borderwidth=0,
            highlightthickness=0,
            font=("Segoe UI", 10)
        )
        modpacks_scroll = ttk.Scrollbar(modpacks_list_wrap, orient="vertical", command=self.modpacks_listbox.yview)
        self.modpacks_listbox.configure(yscrollcommand=modpacks_scroll.set)
        self.modpacks_listbox.pack(side="left", fill="both", expand=True)
        modpacks_scroll.pack(side="right", fill="y")
        self.modpacks_listbox.bind("<<ListboxSelect>>", lambda _e: self._on_select_modpack())
        right_panel = tk.Frame(body, bg=self.theme_manager.get_color('bg_primary'))
        right_panel.pack(side="right", fill="both", expand=True, padx=(8, 0))

        tk.Label(
            right_panel,
            text=self.parent._t("ORANGLIB_VERSIONS"),
            bg=self.theme_manager.get_color('bg_primary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 10, "bold")
        ).pack(anchor="w", padx=10, pady=(10, 6))

        versions_wrap = tk.Frame(right_panel, bg=self.theme_manager.get_color('bg_primary'))
        versions_wrap.pack(fill="both", expand=True, padx=10, pady=(0, 10))
        self.versions_listbox = tk.Listbox(
            versions_wrap,
            bg=self.theme_manager.get_color('bg_input'),
            fg=self.theme_manager.get_color('fg_primary'),
            selectbackground=self.theme_manager.get_color('bg_hover'),
            selectforeground=self.theme_manager.get_color('fg_primary'),
            relief="flat",
            borderwidth=0,
            highlightthickness=0,
            font=("Segoe UI", 10)
        )
        versions_scroll = ttk.Scrollbar(versions_wrap, orient="vertical", command=self.versions_listbox.yview)
        self.versions_listbox.configure(yscrollcommand=versions_scroll.set)
        self.versions_listbox.pack(side="left", fill="both", expand=True)
        versions_scroll.pack(side="right", fill="y")
        action_bar = tk.Frame(right_panel, bg=self.theme_manager.get_color('bg_primary'))
        action_bar.pack(fill="x", padx=10, pady=(0, 10))
        self.download_btn = tk.Button(
            action_bar,
            text=self.parent._t("ORANGLIB_DOWNLOAD_INSTALL"),
            command=self.download_selected,
            bg=self.theme_manager.get_color('accent_primary'),
            fg="#ffffff",
            font=("Segoe UI", 9, "bold"),
            bd=0,
            padx=14,
            pady=8,
            cursor="hand2",
            relief="flat",
            state="disabled"
        )
        self.download_btn.pack(side="left", padx=(0, 8))
        open_desktop_btn = tk.Button(
            action_bar,
            text=self.parent._t("ORANGLIB_OPEN_DESKTOP"),
            command=self.open_desktop,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=8,
            cursor="hand2",
            relief="flat"
        )
        open_desktop_btn.pack(side="left")
        self.info_label = tk.Label(
            right_panel,
            text=self.parent._t("ORANGLIB_READY_MODPACK"),
            bg=self.theme_manager.get_color('bg_primary'),
            fg=self.theme_manager.get_color('fg_secondary'),
            font=("Segoe UI", 9),
            anchor="w",
            justify="left"
        )
        self.info_label.pack(fill="x", padx=10, pady=(0, 10))
        self.status_label = tk.Label(
            container,
            text=self.parent._t("ORANGLIB_READY_MODPACK"),
            bg=self.theme_manager.get_color('bg_primary'),
            fg=self.theme_manager.get_color('fg_secondary'),
            font=("Segoe UI", 9),
            anchor="w"
        )
        self.status_label.pack(fill="x")

        self.versions_listbox.bind("<<ListboxSelect>>", lambda _e: self._on_select_version())
        self.refresh_modpacks()

    def _set_status(self, text):
        self.status_label.config(text=text)

    def _api_get(self, path, params=None):
        return _http_session.get(f"{ORANGLIB_API_URL}{path}", params=params, timeout=25)

    def refresh_modpacks(self):
        self._set_status("Loading OrangLib modpacks...")

        def worker():
            try:
                response = self._api_get("/modpacks", params={"page": 1, "page_size": 100, "sort_by": "updated"})
                response.raise_for_status()
                payload = response.json()
                items = payload.get("items", []) if isinstance(payload, dict) else []
                try:
                    self.parent.after(0, lambda: self._apply_modpacks(items))
                except RuntimeError:
                    pass
            except Exception as exc:
                try:
                    self.parent.after(0, lambda: self._set_status(f"Failed to load modpacks: {exc}"))
                except RuntimeError:
                    pass

        threading.Thread(target=worker, daemon=True).start()

    def _apply_modpacks(self, items):
        self.modpacks = items or []
        self.selected_modpack = None
        self.versions = []
        self._render_modpacks()
        self._render_versions()
        self.download_btn.config(state="disabled")
        self.info_label.config(text=self.parent._t("ORANGLIB_LOADED_COUNT").format(count=len(self.modpacks)))
        self._set_status(self.parent._t("ORANGLIB_READY_MODPACK"))

    def _render_modpacks(self):
        query = self.search_var.get().strip().lower()
        self.modpacks_listbox.delete(0, tk.END)
        self._filtered_modpacks = [
            m for m in self.modpacks
            if not query or query in (m.get("name", "").lower()) or query in (m.get("owner_username", "").lower())
        ]
        for item in self._filtered_modpacks:
            name = item.get("name", "Unnamed")
            owner = item.get("owner_username", "Unknown")
            game_version = item.get("game_version", "?")
            self.modpacks_listbox.insert(tk.END, f"{name}  •  {owner}  •  MC {game_version}")

    def _on_select_modpack(self):
        idxs = self.modpacks_listbox.curselection()
        if not idxs:
            return
        idx = idxs[0]
        if idx >= len(getattr(self, '_filtered_modpacks', [])):
            return
        self.selected_modpack = self._filtered_modpacks[idx]
        modpack_id = self.selected_modpack.get("id")
        self.versions = []
        self._render_versions()
        self.download_btn.config(state="disabled")
        self._set_status("Loading versions...")

        def worker():
            try:
                response = self._api_get(f"/modpacks/{modpack_id}")
                response.raise_for_status()
                payload = response.json()
                versions = payload.get("versions", []) if isinstance(payload, dict) else []
                self.parent.after(0, lambda: self._apply_versions(versions))
            except Exception as exc:
                self.parent.after(0, lambda: self._set_status(f"Failed to load versions: {exc}"))

        threading.Thread(target=worker, daemon=True).start()

    def _apply_versions(self, versions):
        self.versions = versions or []
        self._render_versions()
        self._set_status(f"Loaded {len(self.versions)} versions")

    def _render_versions(self):
        self.versions_listbox.delete(0, tk.END)
        for version in self.versions:
            version_no = version.get("version_number", "?")
            file_name = version.get("file_name", "")
            file_size = version.get("file_size", 0)
            size_mb = file_size / (1024 * 1024) if file_size else 0
            verdict = version.get("scan_verdict")
            verdict_txt = f"[{verdict}]" if verdict else ""
            self.versions_listbox.insert(tk.END, f"v{version_no}  •  {file_name}  •  {size_mb:.2f} MB {verdict_txt}".strip())

    def _on_select_version(self):
        version = self.get_selected_version()
        if not version:
            self.download_btn.config(state="disabled")
            return
        self.download_btn.config(state="normal")
        file_name = (version.get("file_name") or "").lower()
        if file_name.endswith('.mrpack'):
            action = "Will download to temp and install into launcher profiles"
        elif file_name.endswith('.zip'):
            action = "Will download to Desktop"
        elif '.tar.' in file_name:
            action = "Will download to Desktop"
        else:
            action = "Unknown file extension"
        self.info_label.config(text=action)

    def get_selected_version(self):
        idxs = self.versions_listbox.curselection()
        if not idxs:
            return None
        idx = idxs[0]
        if idx >= len(self.versions):
            return None
        return self.versions[idx]

    def download_selected(self):
        version = self.get_selected_version()
        modpack = self.selected_modpack
        if not version or not modpack:
            return

        modpack_id = modpack.get("id")
        version_id = version.get("id")
        file_name = version.get("file_name") or f"modpack_{modpack_id}_{version_id}"
        download_url = f"{ORANGLIB_API_URL}/modpacks/{modpack_id}/versions/{version_id}/download"
        lower_file = file_name.lower()

        if lower_file.endswith('.mrpack'):
            target_dir = ORANGLIB_TEMP_DIR
        else:
            target_dir = ORANGLIB_DESKTOP_DIR

        target_dir.mkdir(parents=True, exist_ok=True)
        destination = target_dir / file_name

        def worker():
            try:
                self.parent.after(0, lambda: self._set_status(f"Downloading {file_name}..."))
                with _http_session.get(download_url, stream=True, timeout=60) as response:
                    response.raise_for_status()
                    total = int(response.headers.get('content-length', 0) or 0)
                    downloaded = 0
                    with open(destination, 'wb') as out:
                        for chunk in response.iter_content(chunk_size=1024 * 128):
                            if not chunk:
                                continue
                            out.write(chunk)
                            downloaded += len(chunk)
                            if total > 0:
                                pct = int((downloaded / total) * 100)
                                self.parent.after(0, lambda p=pct: self._set_status(f"Downloading {file_name}... {p}%"))

                if lower_file.endswith('.mrpack'):
                    self.parent.after(0, lambda: self._set_status("Installing .mrpack in launcher..."))
                    success, message, profile_name = import_modpack(str(destination), self.parent)
                    if success:
                        self.parent.after(0, lambda: messagebox.showinfo("OrangLib", f"Installed MRPACK successfully.\n\nProfile: {profile_name}\n{message}"))
                        if hasattr(self.parent, '_refresh_game_profiles'):
                            self.parent.after(0, self.parent._refresh_game_profiles)
                        self.parent.after(0, lambda: self._set_status("MRPACK installed successfully"))
                    else:
                        self.parent.after(0, lambda: messagebox.showerror("OrangLib", f"MRPACK install failed:\n{message}"))
                        self.parent.after(0, lambda: self._set_status("MRPACK install failed"))
                else:
                    self.parent.after(0, lambda: self._set_status(f"Downloaded to {destination}"))
                    self.parent.after(0, lambda: messagebox.showinfo("OrangLib", f"Downloaded:\n{destination}"))
            except Exception as exc:
                self.parent.after(0, lambda: self._set_status(f"Download failed: {exc}"))
                self.parent.after(0, lambda: messagebox.showerror("OrangLib", f"Download failed:\n{exc}"))

        threading.Thread(target=worker, daemon=True).start()

    def open_desktop(self):
        try:
            ORANGLIB_DESKTOP_DIR.mkdir(parents=True, exist_ok=True)
            subprocess.run(["xdg-open", str(ORANGLIB_DESKTOP_DIR)])
        except Exception as exc:
            messagebox.showerror("OrangLib", f"Could not open Desktop:\n{exc}")


def build_oranglib_tab(launcher, notebook):
    oranglib_tab = OrangLibTab(launcher)
    oranglib_tab.build_tab(notebook)
    launcher.oranglib_tab = oranglib_tab


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


class ServersTab:
    def __init__(self, parent):
        self.parent = parent
        self.theme_manager = get_theme_manager()
        self.servers = []
        self.servers_dat_path = None
        self._ping_cache: dict = {}
        self._server_status_labels: list = []
        self._server_latency_labels: list = []
        self._server_latency_rows: list = []
        self._server_icon_labels: list = []
        self._server_motd_labels: list = []
        self._server_rows: list = []
        self._server_favicon_refs: dict = {}  # idx -> PhotoImage replaces on update           

    def build_tab(self, notebook):
        tab_frame = ttk.Frame(notebook)
        notebook.add(tab_frame, text=self.parent._t('SERVERS'))

        container = tk.Frame(tab_frame, bg=self.theme_manager.get_color('bg_primary'))
        container.pack(fill="both", expand=True, padx=14, pady=14)

        header = tk.Frame(container, bg=self.theme_manager.get_color('bg_primary'))
        header.pack(fill="x", pady=(0, 10))

        title = tk.Label(
            header,
            text=self.parent._t('SERVERS_TITLE'),
            bg=self.theme_manager.get_color('bg_primary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 14, "bold")
        )
        title.pack(side="left")

        btn_frame = tk.Frame(header, bg=self.theme_manager.get_color('bg_primary'))
        btn_frame.pack(side="right")

        refresh_btn = tk.Button(
            btn_frame,
            text=self.parent._t('SERVERS_REFRESH'),
            command=self.refresh_servers,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        refresh_btn.pack(side="left", padx=(0, 8))

        ping_all_btn = tk.Button(
            btn_frame,
            text=self.parent._t('SERVERS_PING_ALL'),
            command=self._ping_all_servers,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        ping_all_btn.pack(side="left", padx=(0, 8))

        add_btn = tk.Button(
            btn_frame,
            text=self.parent._t('SERVERS_ADD'),
            command=self.add_server,
            bg=self.theme_manager.get_color('accent_primary'),
            fg="#ffffff",
            font=("Segoe UI", 9, "bold"),
            bd=0,
            padx=12,
            pady=6,
            cursor="hand2",
            relief="flat"
        )
        add_btn.pack(side="left")

        body = tk.Frame(container, bg=self.theme_manager.get_color('bg_primary'))
        body.pack(fill="both", expand=True)

        list_wrap = tk.Frame(body, bg=self.theme_manager.get_color('bg_primary'))
        list_wrap.pack(fill="both", expand=True, padx=10, pady=10)

        self.servers_canvas = tk.Canvas(
            list_wrap,
            bg=self.theme_manager.get_color('bg_input'),
            highlightthickness=0,
            borderwidth=0
        )
        self.servers_scroll = ttk.Scrollbar(list_wrap, orient="vertical", command=self.servers_canvas.yview)
        self.servers_canvas.configure(yscrollcommand=self.servers_scroll.set)
        self.servers_canvas.pack(side="left", fill="both", expand=True)
        self.servers_scroll.pack(side="right", fill="y")
        self.servers_inner = tk.Frame(self.servers_canvas, bg=self.theme_manager.get_color('bg_input'))
        self.servers_canvas.create_window((0, 0), window=self.servers_inner, anchor="nw")
        self.servers_inner.bind("<Configure>", lambda e: self.servers_canvas.configure(scrollregion=self.servers_canvas.bbox("all")))
        self.server_icon_images = [] 
        action_bar = tk.Frame(body, bg=self.theme_manager.get_color('bg_primary'))
        action_bar.pack(fill="x", padx=10, pady=(0, 10))

        self.edit_btn = tk.Button(
            action_bar,
            text=self.parent._t('SERVERS_EDIT'),
            command=self.edit_server,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg=self.theme_manager.get_color('fg_primary'),
            font=("Segoe UI", 9),
            bd=0,
            padx=14,
            pady=8,
            cursor="hand2",
            relief="flat",
            state="disabled"
        )
        self.edit_btn.pack(side="left", padx=(0, 8))

        self.delete_btn = tk.Button(
            action_bar,
            text=self.parent._t('SERVERS_DELETE'),
            command=self.delete_server,
            bg=self.theme_manager.get_color('bg_tertiary'),
            fg="#ff6b6b",
            font=("Segoe UI", 9),
            bd=0,
            padx=14,
            pady=8,
            cursor="hand2",
            relief="flat",
            state="disabled"
        )
        self.delete_btn.pack(side="left")

        self.info_label = tk.Label(
            action_bar,
            text=self.parent._t('SERVERS_SELECT_INSTANCE'),
            bg=self.theme_manager.get_color('bg_primary'),
            fg=self.theme_manager.get_color('fg_secondary'),
            font=("Segoe UI", 9)
        )
        self.info_label.pack(side="right")

        self.refresh_servers()

    def _get_servers_dat_path(self) -> Path:
        instance_mgr = get_instance_manager()
        if instance_mgr.selected_instance_id:
            instance = instance_mgr.instances.get(instance_mgr.selected_instance_id)
            if instance:
                return instance.minecraft_dir / "servers.dat"
        return None

    def refresh_servers(self):
        self.servers_dat_path = self._get_servers_dat_path()
        if not self.servers_dat_path:
            self.servers = []
            self.info_label.config(text=self.parent._t('SERVERS_NO_INSTANCE'))
        else:
            self.servers = ServersNBT.read_servers_dat(self.servers_dat_path)
            self.info_label.config(text=self.parent._t('SERVERS_LOADED', count=len(self.servers), name=self.servers_dat_path.parent.name))
        self._render_servers()
        if self.servers:
            self._ping_all_servers()

    def _render_servers(self):
        
        for widget in self.servers_inner.winfo_children():
            widget.destroy()
        self.server_icon_images.clear()
        self._server_favicon_refs.clear()
        self._server_status_labels.clear()
        self._server_latency_labels.clear()
        self._server_latency_rows.clear()
        self._server_icon_labels.clear()
        self._server_motd_labels.clear()
        self._server_rows.clear()

        card_bg = self.theme_manager.get_color('bg_secondary')
        fg1 = self.theme_manager.get_color('fg_primary')
        fg3 = self.theme_manager.get_color('fg_tertiary')
        fg_dim = self.theme_manager.get_color('fg_disabled')

        for idx, server in enumerate(self.servers):
            name = server.get('name', 'Unknown')
            ip = server.get('ip', '?')
            hidden = server.get('hidden', 0)
            if hidden:
                name += "  [hidden]"

            icon_img = None
            icon_data = server.get('icon')
            if icon_data:
                try:
                    icon_bytes = base64.b64decode(icon_data.split(',')[-1])
                    image = Image.open(io.BytesIO(icon_bytes)).resize((64, 64), Image.Resampling.LANCZOS)
                    icon_img = ImageTk.PhotoImage(image)
                    self.server_icon_images.append(icon_img)
                except Exception:
                    icon_img = None

            
            row = tk.Frame(self.servers_inner, bg=card_bg)
            row.pack(fill="x", padx=4, pady=3)
            row.columnconfigure(1, weight=1)
            self._server_rows.append(row)

            if icon_img:
                icon_lbl = tk.Label(row, image=icon_img, bg=card_bg)
            else:
                placeholder = self.parent._load_themed_icon("server", size=(64, 64))
                self.server_icon_images.append(placeholder)
                icon_lbl = tk.Label(row, image=placeholder, bg=card_bg)
            icon_lbl.grid(row=0, column=0, rowspan=3, padx=(12, 10), pady=10, sticky="w")
            self._server_icon_labels.append(icon_lbl)

            name_lbl = tk.Label(row, text=name, anchor="w", bg=card_bg, fg=fg1,
                                font=("Segoe UI", 12, "bold"))
            name_lbl.grid(row=0, column=1, sticky="sw", padx=(0, 8), pady=(10, 0))

            host_part, _, port_str = ip.partition(':')
            cache_key = f"{host_part}:{int(port_str) if port_str.isdigit() else 25565}"
            cached = self._ping_cache.get(cache_key)
            motd_text = ""
            if isinstance(cached, dict) and cached.get('motd'):
                motd_text = _strip_mc_formatting(cached['motd'])
            motd_lbl = tk.Label(row, text=motd_text, anchor="w", bg=card_bg, fg=fg3,
                                font=("Segoe UI", 9))
            motd_lbl.grid(row=1, column=1, sticky="w", padx=(0, 8))
            self._server_motd_labels.append(motd_lbl)

            addr_lbl = tk.Label(row, text=ip, anchor="w", bg=card_bg, fg=fg_dim,
                                font=("Segoe UI", 8))
            addr_lbl.grid(row=2, column=1, sticky="nw", padx=(0, 8), pady=(0, 10))

            status_frame = tk.Frame(row, bg=card_bg)
            status_frame.grid(row=0, column=2, rowspan=3, sticky="e", padx=(0, 14), pady=10)

            if isinstance(cached, dict):
                players_text = f"{cached['online']}/{cached['max']}"
                players_fg = fg1
                lat_text, lat_fg, bar_count = self._format_latency(cached['latency'])
            elif cached is None and cache_key in self._ping_cache:
                players_text = "Offline"
                players_fg = "#ff6b6b"
                lat_text, lat_fg, bar_count = "", fg_dim, 0
            else:
                players_text = "..."
                players_fg = fg_dim
                lat_text, lat_fg, bar_count = "Pinging", fg_dim, 0

            players_lbl = tk.Label(status_frame, text=players_text, anchor="e",
                                   bg=card_bg, fg=players_fg, font=("Segoe UI", 10))
            players_lbl.pack(anchor="e")
            self._server_status_labels.append(players_lbl)

            latency_row = tk.Frame(status_frame, bg=card_bg)
            latency_row.pack(anchor="e")
            self._server_latency_rows.append(latency_row)

            latency_lbl = tk.Label(latency_row, text=lat_text, anchor="e",
                                   bg=card_bg, fg=lat_fg, font=("Segoe UI", 9))
            latency_lbl.pack(side="left", padx=(0, 4))
            self._server_latency_labels.append(latency_lbl)

            if bar_count > 0:
                bars_canvas = self._create_signal_bars(latency_row, bar_count, lat_fg, card_bg)
                bars_canvas.pack(side="left")

            # quick play only enabled for MC >= 1.20.1
            qp_supported = self._quickplay_supported()
            try:
                accent = self.theme_manager.get_color('accent')
            except Exception:
                accent = "#e8772e"
            qp_btn = tk.Button(
                status_frame,
                text=self.parent._t('SERVERS_QUICKPLAY'),
                command=(lambda i=idx: self._quick_play(i)),
                bg=accent if qp_supported else self.theme_manager.get_color('bg_tertiary'),
                fg="#ffffff" if qp_supported else fg_dim,
                font=("Segoe UI", 9), bd=0, padx=10, pady=4, relief="flat",
                cursor="hand2" if qp_supported else "arrow",
                state="normal" if qp_supported else "disabled"
            )
            qp_btn.pack(anchor="e", pady=(4, 0))

            def on_row_click(event, i=idx):
                self._select_server(i)

            click_widgets = [row, icon_lbl, name_lbl, motd_lbl, addr_lbl,
                             status_frame, players_lbl, latency_lbl, latency_row]
            for child in latency_row.winfo_children():
                click_widgets.append(child)
            for w in click_widgets:
                w.bind("<Button-1>", on_row_click)

        self.selected_server_idx = None
        self.edit_btn.config(state="disabled")
        self.delete_btn.config(state="disabled")

    @staticmethod
    def _format_latency(ms):
        if ms < 100:
            n, color = 5, "#249737"
        elif ms < 200:
            n, color = 4, "#94d82d"
        elif ms < 400:
            n, color = 3, "#fcc419"
        elif ms < 800:
            n, color = 2, "#ff922b"
        else:
            n, color = 1, "#d51414"
        return f"{ms}ms", color, n

    @staticmethod
    def _create_signal_bars(parent, bar_count, color, bg_color):
        total_bars = 5
        bar_width = 4
        bar_gap = 2
        max_height = 18
        min_height = 4
        canvas_width = total_bars * bar_width + (total_bars - 1) * bar_gap
        canvas = tk.Canvas(parent, width=canvas_width, height=max_height,
                           bg=bg_color, highlightthickness=0, borderwidth=0)
        for i in range(total_bars):
            h = min_height + int((max_height - min_height) * (i / (total_bars - 1)))
            x = i * (bar_width + bar_gap)
            y = max_height - h
            fill = color if i < bar_count else "#555555"
            canvas.create_rectangle(x, y, x + bar_width, max_height, fill=fill, outline="")
        return canvas

    def _current_launch_version(self):
        try:
            inst = self.parent.instance_manager.get_selected_instance()
            if inst and getattr(inst, 'version', ''):
                return inst.version
        except Exception:
            pass
        try:
            prof = self.parent.game_profile_manager.get_selected_profile()
            if prof and getattr(prof, 'version', ''):
                return prof.version
        except Exception:
            pass
        return ""

    def _quickplay_supported(self):
        ver = self._current_launch_version()
        if not ver:
            return False
        return _mc_version_tuple(ver) >= (1, 20, 1)

    def _quick_play(self, idx):
        if not self._quickplay_supported():
            messagebox.showinfo(
                self.parent._t('SERVERS_QUICKPLAY'),
                "Quick Play requires Minecraft 1.20.1 or newer."
            )
            return
        if idx is None or idx < 0 or idx >= len(self.servers):
            return
        ip = self.servers[idx].get('ip', '').strip()
        if not ip:
            return
        self._select_server(idx)
        self.parent._pending_quickplay = ip
        self.parent._launch_game()

    def _set_row_bg(self, widget, color):
        try:
            widget.config(bg=color)
        except tk.TclError:
            pass
        for child in widget.winfo_children():
            self._set_row_bg(child, color)

    def _select_server(self, idx):
        card_bg = self.theme_manager.get_color('bg_secondary')
        sel_bg = self.theme_manager.get_color('bg_hover')
        self.selected_server_idx = idx
        for i, row in enumerate(self._server_rows):
            if not row.winfo_exists():
                continue
            self._set_row_bg(row, sel_bg if i == idx else card_bg)
        self.edit_btn.config(state="normal")
        self.delete_btn.config(state="normal")

    def _get_selected_server_idx(self):
        return getattr(self, 'selected_server_idx', None)

    def _on_select(self):
        idx = self._get_selected_server_idx()
        if idx is not None:
            self.edit_btn.config(state="normal")
            self.delete_btn.config(state="normal")
        else:
            self.edit_btn.config(state="disabled")
            self.delete_btn.config(state="disabled")

    def add_server(self):
        self._show_server_dialog()

    def edit_server(self):
        idx = self._get_selected_server_idx()
        if idx is None or idx >= len(self.servers):
            return
        self._show_server_dialog(idx)

    def delete_server(self):
        idx = self._get_selected_server_idx()
        if idx is None or idx >= len(self.servers):
            return
        server = self.servers[idx]
        if messagebox.askyesno(self.parent._t('SERVERS_DELETE_TITLE'), self.parent._t('SERVERS_DELETE_CONFIRM', name=server.get('name', 'Unknown'))):
            self.servers.pop(idx)
            self._save_and_refresh()

    def _show_server_dialog(self, edit_idx=None):
        dialog = tk.Toplevel(self.parent)
        dialog.title(self.parent._t('SERVERS_EDIT_TITLE') if edit_idx is not None else self.parent._t('SERVERS_ADD_TITLE'))
        dialog.geometry("400x320")
        dialog.configure(bg=self.theme_manager.get_color('bg_secondary'))
        dialog.transient(self.parent)
        dialog.grab_set()

        is_edit = edit_idx is not None
        server = self.servers[edit_idx] if is_edit else {}

        tk.Label(dialog, text=self.parent._t('SERVERS_NAME_LABEL'), bg=self.theme_manager.get_color('bg_secondary'),
                 fg=self.theme_manager.get_color('fg_primary'), font=("Segoe UI", 10)).pack(anchor="w", padx=20, pady=(20, 5))
        name_entry = tk.Entry(dialog, bg=self.theme_manager.get_color('bg_input'),
                              fg=self.theme_manager.get_color('fg_primary'),
                              insertbackground=self.theme_manager.get_color('fg_primary'),
                              relief="flat", font=("Segoe UI", 10))
        name_entry.pack(fill="x", padx=20, ipady=6)
        name_entry.insert(0, server.get('name', ''))

        tk.Label(dialog, text=self.parent._t('SERVERS_ADDRESS_LABEL'), bg=self.theme_manager.get_color('bg_secondary'),
                 fg=self.theme_manager.get_color('fg_primary'), font=("Segoe UI", 10)).pack(anchor="w", padx=20, pady=(15, 5))
        ip_entry = tk.Entry(dialog, bg=self.theme_manager.get_color('bg_input'),
                            fg=self.theme_manager.get_color('fg_primary'),
                            insertbackground=self.theme_manager.get_color('fg_primary'),
                            relief="flat", font=("Segoe UI", 10))
        ip_entry.pack(fill="x", padx=20, ipady=6)
        ip_entry.insert(0, server.get('ip', ''))

        def save():
            name = name_entry.get().strip()
            ip = ip_entry.get().strip()
            if not name or not ip:
                messagebox.showwarning(self.parent._t('SERVERS_INVALID_TITLE'), self.parent._t('SERVERS_INVALID_MSG'))
                return
            new_server = {'name': name, 'ip': ip}
            if is_edit:
                if 'icon' in self.servers[edit_idx]:
                    new_server['icon'] = self.servers[edit_idx]['icon']
                if 'hidden' in self.servers[edit_idx]:
                    new_server['hidden'] = self.servers[edit_idx]['hidden']
                self.servers[edit_idx] = new_server
            else:
                self.servers.append(new_server)
            dialog.destroy()
            self._save_and_refresh()

        btn_frame = tk.Frame(dialog, bg=self.theme_manager.get_color('bg_secondary'))
        btn_frame.pack(fill="x", padx=20, pady=20)

        tk.Button(btn_frame, text=self.parent._t('SERVERS_CANCEL'), command=dialog.destroy,
                  bg=self.theme_manager.get_color('bg_tertiary'),
                  fg=self.theme_manager.get_color('fg_primary'),
                  font=("Segoe UI", 9), bd=0, padx=14, pady=6, cursor="hand2", relief="flat").pack(side="right", padx=(8, 0))
        tk.Button(btn_frame, text=self.parent._t('SERVERS_SAVE'), command=save,
                  bg=self.theme_manager.get_color('accent_primary'),
                  fg="#ffffff", font=("Segoe UI", 9, "bold"), bd=0, padx=14, pady=6, cursor="hand2", relief="flat").pack(side="right")

    def _ping_all_servers(self):
        for i, srv in enumerate(self.servers):
            ip = srv.get('ip', '')
            host, _, port_str = ip.partition(':')
            port = int(port_str) if port_str.isdigit() else 25565
            cache_key = f"{host}:{port}"
            self._ping_cache[cache_key] = True
            if i < len(self._server_status_labels):
                lbl = self._server_status_labels[i]
                if lbl.winfo_exists():
                    lbl.config(
                        text="⏳ …",
                        fg=self.theme_manager.get_color('fg_secondary')
                    )
            threading.Thread(
                target=self._ping_worker,
                args=(i, host, port, cache_key),
                daemon=True
            ).start()

    def _ping_worker(self, idx: int, host: str, port: int, cache_key: str):
        result = _slp_ping(host, port)
        self._ping_cache[cache_key] = result  # dict or None
        try:
            self.parent.after(0, lambda i=idx, r=result: self._update_status_label(i, r))
        except RuntimeError:
            pass

    def _update_status_label(self, idx: int, result):
        if idx >= len(self._server_status_labels):
            return
        players_lbl = self._server_status_labels[idx]
        if not players_lbl.winfo_exists():
            return

        fg1 = self.theme_manager.get_color('fg_primary')
        fg_dim = self.theme_manager.get_color('fg_disabled')

        if result:
            players_lbl.config(text=f"{result['online']}/{result['max']}", fg=fg1)

            if idx < len(self._server_latency_labels) and idx < len(self._server_latency_rows):
                lat_lbl = self._server_latency_labels[idx]
                lat_row = self._server_latency_rows[idx]
                if lat_lbl.winfo_exists() and lat_row.winfo_exists():
                    lat_text, lat_fg, bar_count = self._format_latency(result['latency'])
                    lat_lbl.config(text=lat_text, fg=lat_fg)
                    for child in lat_row.winfo_children():
                        if isinstance(child, tk.Canvas):
                            child.destroy()
                    card_bg = self.theme_manager.get_color('bg_secondary')
                    bars_canvas = self._create_signal_bars(lat_row, bar_count, lat_fg, card_bg)
                    bars_canvas.pack(side="left")

            if idx < len(self._server_motd_labels):
                motd_lbl = self._server_motd_labels[idx]
                if motd_lbl.winfo_exists() and result.get('motd'):
                    motd_lbl.config(text=_strip_mc_formatting(result['motd']))

            favicon = result.get('favicon', '')
            if favicon and idx < len(self._server_icon_labels):
                icon_lbl = self._server_icon_labels[idx]
                if icon_lbl.winfo_exists():
                    try:
                        icon_bytes = base64.b64decode(favicon.split(',')[-1])
                        img = Image.open(io.BytesIO(icon_bytes)).resize((64, 64), Image.Resampling.LANCZOS)
                        photo = ImageTk.PhotoImage(img)
                        self._server_favicon_refs[idx] = photo
                        icon_lbl.config(image=photo)
                    except Exception:
                        pass
        else:
            players_lbl.config(text="Offline", fg="#ff6b6b")
            if idx < len(self._server_latency_labels):
                lat_lbl = self._server_latency_labels[idx]
                if lat_lbl.winfo_exists():
                    lat_lbl.config(text="", fg=fg_dim)
            if idx < len(self._server_latency_rows):
                lat_row = self._server_latency_rows[idx]
                if lat_row.winfo_exists():
                    for child in lat_row.winfo_children():
                        if isinstance(child, tk.Canvas):
                            child.destroy()

    def _save_and_refresh(self):
        if self.servers_dat_path:
            ServersNBT.write_servers_dat(self.servers_dat_path, self.servers)
        self.refresh_servers()


def build_servers_tab(launcher, notebook):
    servers_tab = ServersTab(launcher)
    servers_tab.build_tab(notebook)
    launcher.servers_tab = servers_tab


def build_news_tab(launcher, notebook: ttk.Notebook):
    tm = get_theme_manager()
    bg_color = tm.get_color('bg_primary')
    style = ttk.Style()
    style.configure("TNotebook", borderwidth=0, padding=0, background=bg_color)
    style.layout("TNotebook", [("Notebook.client", {"sticky": "nswe"})])
    style.configure("TFrame", borderwidth=0, background=bg_color)
    style.configure("TNotebook.Tab", background=bg_color)
    news_frame = tk.Frame(
        notebook, bg=bg_color,
        bd=0, highlightthickness=0, relief="flat"
    )
    news_frame.pack_propagate(False)
    notebook.add(news_frame, text=launcher._t('UPDATE_NOTES'))
    launcher.news_viewer = ChromiumWidget(news_frame)
    launcher.news_viewer.pack(fill="both", expand=True)

    news_frame.update_idletasks()
    news_frame.update()
def build_launcher_log_tab(launcher, notebook):
    log_frame = ttk.Frame(notebook)
    notebook.add(log_frame, text=launcher._t('LAUNCHER_LOG'))

    bg       = launcher._get_theme_color('bg_primary')
    bg_input = launcher._get_theme_color('bg_input')
    fg       = launcher._get_theme_color('fg_primary')
    fg_sec   = launcher._get_theme_color('fg_secondary')
    toolbar = tk.Frame(log_frame, bg=bg, pady=6)
    toolbar.pack(fill="x", padx=10, pady=(8, 0))

    search_frame = tk.Frame(toolbar, bg=bg_input, highlightbackground=launcher._get_theme_color('bg_hover'),
                            highlightthickness=1)
    search_frame.pack(side="left", fill="x", expand=True, padx=(0, 10), ipady=3)
    tk.Label(search_frame, text="⌕", bg=bg_input, fg=fg_sec, font=("Segoe UI", 11)).pack(side="left", padx=(6, 2))
    launcher._log_search_var = tk.StringVar()
    search_entry = tk.Entry(search_frame, textvariable=launcher._log_search_var,
                            bg=bg_input, fg=fg, insertbackground=fg,
                            relief="flat", font=("Consolas", 9), bd=0,
                            highlightthickness=0)
    search_entry.pack(side="left", fill="x", expand=True, padx=(0, 6))
    search_entry.insert(0, launcher._t("LOGS_SEARCH_PLACEHOLDER"))
    search_entry.config(fg=fg_sec)
    def _on_search_focus_in(e):
        if search_entry.get() == launcher._t("LOGS_SEARCH_PLACEHOLDER"):
            search_entry.delete(0, tk.END)
            search_entry.config(fg=fg)
    def _on_search_focus_out(e):
        if not search_entry.get():
            search_entry.insert(0, launcher._t("LOGS_SEARCH_PLACEHOLDER"))
            search_entry.config(fg=fg_sec)
    search_entry.bind("<FocusIn>",  _on_search_focus_in)
    search_entry.bind("<FocusOut>", _on_search_focus_out)
    _log_search_debounce_id = [None]
    def _debounced_filter(*_):
        if _log_search_debounce_id[0]:
            try:
                launcher.after_cancel(_log_search_debounce_id[0])
            except Exception:
                pass
        _log_search_debounce_id[0] = launcher.after(250, lambda: launcher._apply_log_filters() if hasattr(launcher, '_apply_log_filters') else None)
    launcher._log_search_var.trace_add("write", _debounced_filter)

    FILTERS = [
        (launcher._t("LOGS_FILTER_ERROR"),   "error",   "#f20000"),
        (launcher._t("LOGS_FILTER_WARN"),    "warning", "#ffc107"),
        (launcher._t("LOGS_FILTER_INFO"),    "info",    "#74c0fc"),
        (launcher._t("LOGS_FILTER_SUCCESS"), "success", "#3bc652"),
        (launcher._t("LOGS_FILTER_OTHER"),   None,      fg_sec),
    ]
    launcher._log_filter_vars = {}

    def _export_log():
        if not getattr(launcher, '_log_buffer', None):
            messagebox.showinfo(launcher._t("LOGS_EXPORT_TITLE"), launcher._t("LOGS_NO_ENTRIES"))
            return
        path = filedialog.asksaveasfilename(
            defaultextension=".txt",
            filetypes=[("Text files", "*.txt"), ("All files", "*.*")],
            initialfile="launcher_log.txt",
            title=launcher._t("LOGS_EXPORT_TITLE")
        )
        if not path:
            return
        try:
            with open(path, "w", encoding="utf-8") as f:
                for msg, _ in launcher._log_buffer:
                    f.write(msg + "\n")
            messagebox.showinfo(launcher._t("LOGS_EXPORT_TITLE"), launcher._t("LOGS_EXPORT_SUCCESS") + f"\n{path}")
        except Exception as e:
            messagebox.showerror(launcher._t("LOGS_EXPORT_TITLE"), launcher._t("LOGS_EXPORT_FAIL") + f"\n{e}")

    save_btn = tk.Button(toolbar, text=launcher._t("LOGS_SAVE_BTN"), command=_export_log,
                         bg=bg_input, fg=fg, font=("Segoe UI", 9),
                         relief="flat", bd=0, padx=10, pady=4,
                         cursor="hand2", activebackground=launcher._get_theme_color('bg_hover'),
                         activeforeground=fg)
    save_btn.pack(side="right", padx=(0, 8))

    filter_bar = tk.Frame(toolbar, bg=bg)
    filter_bar.pack(side="right")

    for label, tag, color in FILTERS:
        var = tk.BooleanVar(value=True)
        launcher._log_filter_vars[tag] = var

        btn_frame = tk.Frame(filter_bar, bg=bg)
        btn_frame.pack(side="left", padx=3)

        dot = tk.Label(btn_frame, text="●", fg=color, bg=bg, font=("Segoe UI", 8))
        dot.pack(side="left")

        cb = tk.Checkbutton(btn_frame, text=label, variable=var,
                            bg=bg, fg=fg, activebackground=bg, activeforeground=fg,
                            selectcolor=bg, font=("Segoe UI", 9),
                            command=launcher._apply_log_filters if hasattr(launcher, '_apply_log_filters') else lambda: None,
                            relief="flat", bd=0, cursor="hand2",
                            highlightthickness=0)
        cb.pack(side="left")
        cb._filter_tag = tag
    container = tk.Frame(log_frame, bg=bg_input)
    container.pack(fill="both", expand=True, padx=10, pady=(6, 10))

    scrollbar = ttk.Scrollbar(container, orient="vertical", style="Modern.Vertical.TScrollbar")

    launcher.log_text = tk.Text(
        container,
        bg=bg_input, fg=fg,
        insertbackground=fg,
        selectbackground=launcher._get_theme_color('bg_hover'),
        selectforeground=fg,
        font=("Consolas", 9),
        wrap=tk.WORD,
        yscrollcommand=scrollbar.set,
        relief="flat", bd=0, highlightthickness=0
    )

    scrollbar.config(command=launcher.log_text.yview)
    launcher.log_text.pack(side="left", fill="both", expand=True)
    scrollbar.pack(side="right", fill="y")

    launcher.log_text.tag_configure("error",   foreground="#ff0000")
    launcher.log_text.tag_configure("warning", foreground="#ffc107")
    launcher.log_text.tag_configure("success", foreground="#51cf66")
    launcher.log_text.tag_configure("info",    foreground="#74c0fc")

    launcher._log_buffer = deque(maxlen=2000)
    launcher._ansi_tags_configured = set()

    _ANSI_COLORS = {
        '0': None, '': None,
        '30': '#4d4d4d', '31': '#ff6b6b', '32': '#51cf66', '33': '#ffc107',
        '34': '#74c0fc', '35': '#cc5de8', '36': '#22d3ee', '37': '#d4d4d4',
        '90': '#808080', '91': '#ff8787', '92': '#69db7c', '93': '#ffd43b',
        '94': '#91a7ff', '95': '#f783ac', '96': '#66d9e8', '97': '#ffffff',
    }
    

    def _ensure_ansi_tag(color_hex):
        tag_name = f"ansi_{color_hex[1:]}"
        if tag_name not in launcher._ansi_tags_configured:
            launcher.log_text.tag_configure(tag_name, foreground=color_hex)
            launcher._ansi_tags_configured.add(tag_name)
        return tag_name

    def _insert_ansi_line(msg, filter_tag):
        segments = []
        current_color = None
        last_end = 0
        for m in _re_ansi.finditer(r'(?:\x1b|\033)\[([0-9;]*)m|\[([0-9;]+)m', msg):
            if m.start() > last_end:
                segments.append((msg[last_end:m.start()], current_color))
            code = m.group(1) if m.group(1) is not None else (m.group(2) or '')
            current_color = _ANSI_COLORS.get(code, current_color)
            last_end = m.end()
        if last_end < len(msg):
            segments.append((msg[last_end:], current_color))

        has_ansi = any(color is not None for _, color in segments)
        if not has_ansi:
            launcher.log_text.insert(tk.END, f"{msg}\n", filter_tag or "")
            return

        for text, color in segments:
            if not text:
                continue
            tags = tuple(filter(None, [filter_tag,
                                       _ensure_ansi_tag(color) if color else None]))
            launcher.log_text.insert(tk.END, text, tags)
        launcher.log_text.insert(tk.END, "\n", filter_tag or "")

    launcher._insert_ansi_line = _insert_ansi_line

    launcher.log_text.insert("1.0", launcher._t("NO_LOGS"))
    launcher.log_text.config(state="disabled")

    def _apply_log_filters():
        search = launcher._log_search_var.get()
        if search == "Type to filter logs...":
            search = ""
        search = search.lower()
        active_tags = {tag for tag, var in launcher._log_filter_vars.items() if var.get()}

        launcher.log_text.config(state="normal")
        launcher.log_text.delete("1.0", tk.END)
        for msg, tag in launcher._log_buffer:
            if tag not in active_tags:
                continue
            if search and search not in msg.lower():
                continue
            _insert_ansi_line(msg, tag)
        launcher.log_text.see(tk.END)
        launcher.log_text.config(state="disabled")

    launcher._apply_log_filters = _apply_log_filters

    for widget in filter_bar.winfo_children():
        for child in widget.winfo_children():
            if isinstance(child, tk.Checkbutton):
                child.config(command=_apply_log_filters)
# discord presence 
class DiscordRPCManager:
    def __init__(self, app_id: str, on_connected=None):
        self.app_id = app_id
        self.on_connected = on_connected
        self.loop = None
        self.thread = None
        self.client = None
        self.stop_event = threading.Event()
    def start(self):
        if self.thread and self.thread.is_alive():
            return
        def _thread_main():
            try:
                loop = asyncio.new_event_loop()
                self.loop = loop
                asyncio.set_event_loop(loop)
                loop.run_until_complete(self._runner())
            finally:
                try:
                    pending = asyncio.all_tasks(self.loop)
                    for t in pending:
                        t.cancel()
                    self.loop.run_until_complete(asyncio.gather(*pending, return_exceptions=True))
                    self.loop.run_until_complete(self.loop.shutdown_asyncgens())
                except Exception:
                    pass
                try:
                    self.loop.close()
                except Exception:
                    pass
        self.stop_event.clear()
        self.thread = threading.Thread(target=_thread_main, name="DiscordRPC", daemon=True)
        self.thread.start()
    async def _runner(self):
        try:
            from pypresence.presence import AioPresence
        except Exception:
            return
        try:
            self.client = AioPresence(self.app_id)
            try:
                await self.client.connect()
            except Exception:
                self.client = None
                return
            if self.on_connected:
                try:
                    self.on_connected()
                except Exception:
                    pass
            while not self.stop_event.is_set() and self.client:
                try:
                    await asyncio.sleep(0.2)
                except asyncio.CancelledError:
                    break
        except Exception:
            pass
        finally:
            if self.client:
                try:
                    await self.client.clear()
                except Exception:
                    pass
                try:
                    await self.client.close()
                except Exception:
                    pass
    def update(self, **presence_data):
        if not self.loop or not self.client:
            return
        async def _do_update():
            try:
                await self.client.update(**presence_data)
            except Exception:
                pass
        try:
            if self.loop and not self.loop.is_closed():
                asyncio.run_coroutine_threadsafe(_do_update(), self.loop)
        except Exception:
            pass
    def stop(self):
        try:
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
    try:
        mixer = getattr(_pg, "mixer", None)
        if mixer and mixer.get_init():
            try:
                mixer.music.stop()
            except Exception:
                pass
            try:
                mixer.quit()
            except Exception:
                pass
        _pg.quit()
    except Exception:
        pass
atexit.register(_atexit_cleanup)
def build_tab(launcher, notebook):
    log_frame = ttk.Frame(notebook)
    notebook.add(log_frame, text=launcher._t("LAUNCHER_LOG"))
    launcher.log_text = scrolledtext.ScrolledText(
        log_frame, 
        bg=launcher._get_theme_color('bg_input'),
        fg=launcher._get_theme_color('fg_primary'),
        insertbackground=launcher._get_theme_color('fg_primary'),
        selectbackground=launcher._get_theme_color('bg_hover'),
        selectforeground=launcher._get_theme_color('fg_primary'),
        font=("Consolas", 9),
        wrap=tk.WORD
    )
    launcher.log_text.pack(fill="both", expand=True, padx=10, pady=10)
    launcher.log_text.tag_configure("error",   foreground="#ff6b6b")
    launcher.log_text.tag_configure("warning", foreground="#ffc107")
    launcher.log_text.tag_configure("success", foreground="#51cf66")
    launcher.log_text.tag_configure("info",    foreground="#74c0fc")
    if not _has_logs():
        launcher.log_text.insert("1.0", launcher._t("NO_LOGS"))
    launcher.log_text.config(state="disabled")
def _has_logs():
    log_dir = Path.home() / ".local" / "share" / "oranglauncher" / "logs"
    if log_dir.exists() and any(log_dir.glob("launcher_*.log")):
        return True
    spawn_log = Path(tempfile.gettempdir()) / "OrangLauncher_spawn.log"
    return spawn_log.exists()
try:
    from cryptography.fernet import Fernet
    from cryptography.hazmat.primitives import hashes
    from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
    ENCRYPTION_AVAILABLE = True
except ImportError:
    ENCRYPTION_AVAILABLE = False
PROFILE_FILENAME = "profiles.json"
CLIENT_ID = "00000000402B5328"
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
def get_profile_names():
    result = []
    for p in load_profiles():
        if p["type"] == "offline":
            result.append(f"{p['username']} (Offline)")
        elif p["type"] == "microsoft":
            result.append(f"{p['username']} (Microsoft)")
        else:
            result.append(p['username'])
    return result
def select_profile(index):
    profiles_data = load_profiles()
    if 0 <= index < len(profiles_data):
        profile = profiles_data.pop(index)
        profiles_data.insert(0, profile)
        save_profiles(profiles_data)
        return True
    return False
def _ensure_tk():
    root = tk._default_root
    if root is None:
        root = tk.Tk()
        root.withdraw()
    return root
def ms_token_flow_interactive():
    try:
        return _ms_token_flow_webview()
    except Exception as e:
        raise Exception(f"Embedded browser login failed: {e}")
def _ms_token_flow_webview():
    auth_code_result = {'code': None, 'cancelled': False}
    oauth_url = (
        "https://login.live.com/oauth20_authorize.srf"
        f"?client_id={CLIENT_ID}"
        f"&redirect_uri={REDIRECT_URI}"
        "&response_type=code"
        f"&scope={SCOPE}"
    )
    print(f"[DEBUG] Loading OAuth URL in webview...")
    def on_loaded():
        try:
            current_url = window.get_current_url()
            if current_url:
                print(f"[DEBUG] Current URL: {current_url}")
                if 'login.live.com/oauth20_desktop.srf' in current_url and 'code=' in current_url:
                    print("[DEBUG] Found auth code in URL!")
                    parsed = urllib.parse.urlparse(current_url)
                    params = urllib.parse.parse_qs(parsed.query)
                    if 'code' in params:
                        auth_code = params['code'][0]
                        print(f"[DEBUG] Extracted auth code: {auth_code[:20]}...")
                        auth_code_result['code'] = auth_code
                        window.destroy()
        except Exception as e:
            print(f"[DEBUG] Error checking URL: {e}")
    def on_closing():
        if not auth_code_result['code']:
            auth_code_result['cancelled'] = True
    window = webview.create_window(
        'Microsoft Account Login',
        oauth_url,
        width=600,
        height=700,
        resizable=True
    )
    window.events.loaded += on_loaded
    window.events.closing += on_closing
    webview.start()
    if auth_code_result['cancelled'] and not auth_code_result['code']:
        raise Exception("Login cancelled or failed")
    if not auth_code_result['code']:
        raise Exception("Failed to capture auth code")
    return _complete_oauth_flow(auth_code_result['code'])
def _complete_oauth_flow(auth_code):
    print(f"[DEBUG] Completing OAuth flow with code: {auth_code[:20]}...")
    token_data = {
        "client_id": CLIENT_ID,
        "scope": SCOPE,
        "code": auth_code,
        "redirect_uri": REDIRECT_URI,
        "grant_type": "authorization_code"
    }
    r = requests.post("https://login.live.com/oauth20_token.srf", data=token_data)
    if not r.ok:
        raise Exception("Failed to get Microsoft token: " + r.text)
    tokens = r.json()
    microsoft_token = tokens["access_token"]
    microsoft_refresh_token = tokens["refresh_token"]
    r = requests.post("https://user.auth.xboxlive.com/user/authenticate", json={
        "Properties": {
            "AuthMethod": "RPS",
            "SiteName": "user.auth.xboxlive.com",
            "RpsTicket": microsoft_token
        },
        "RelyingParty": "http://auth.xboxlive.com",
        "TokenType": "JWT"
    })
    if not r.ok:
        raise Exception("Failed to get Xbox Live token: " + r.text)
    xbl_token = r.json()["Token"]
    r = requests.post("https://xsts.auth.xboxlive.com/xsts/authorize", json={
        "Properties": {
            "SandboxId": "RETAIL",
            "UserTokens": [xbl_token]
        },
        "RelyingParty": "rp://api.minecraftservices.com/",
        "TokenType": "JWT"
    })
    if not r.ok:
        raise Exception("Failed to get XSTS token: " + r.text)
    xsts_userhash = r.json()["DisplayClaims"]["xui"][0]["uhs"]
    xsts_token = r.json()["Token"]
    r = requests.post("https://api.minecraftservices.com/authentication/login_with_xbox", json={
        "identityToken": f"XBL3.0 x={xsts_userhash};{xsts_token}"
    })
    if not r.ok:
        raise Exception("Failed to get Minecraft token: " + r.text)
    minecraft_token = r.json()["access_token"]
    r = _http_session.get("https://api.minecraftservices.com/minecraft/profile", headers={
        "Authorization": f"Bearer {minecraft_token}"
    })
    if not r.ok:
        raise Exception("Failed to get Minecraft profile: " + r.text)
    profile_data = r.json()
    username = profile_data["name"]
    uuid = profile_data["id"]
    print(f"[DEBUG] Successfully authenticated as: {username}")
    return {
        "microsoft_refresh_token": microsoft_refresh_token,
        "minecraft_token": minecraft_token,
        "username": username,
        "uuid": uuid,
        "last_refresh": time.time()
    }
def refresh_mc_token(microsoft_refresh_token):
    r = requests.post("https://login.live.com/oauth20_token.srf", data={
        "scope": SCOPE,
        "client_id": CLIENT_ID,
        "grant_type": "refresh_token",
        "refresh_token": microsoft_refresh_token
    })
    if not r.ok:
        raise Exception("Failed to refresh Microsoft token: " + r.text)
    microsoft_token = r.json()["access_token"]
    r = requests.post("https://user.auth.xboxlive.com/user/authenticate", json={
        "Properties": {
            "AuthMethod": "RPS",
            "SiteName": "user.auth.xboxlive.com",
            "RpsTicket": microsoft_token
        },
        "RelyingParty": "http://auth.xboxlive.com",
        "TokenType": "JWT"
    })
    if not r.ok:
        raise Exception("Failed to get Xbox Live token: " + r.text)
    xbl_token = r.json()["Token"]
    r = requests.post("https://xsts.auth.xboxlive.com/xsts/authorize", json={
        "Properties": {
            "SandboxId": "RETAIL",
            "UserTokens": [xbl_token]
        },
        "RelyingParty": "rp://api.minecraftservices.com/",
        "TokenType": "JWT"
    })
    if not r.ok:
        raise Exception("Failed to get XSTS token: " + r.text)
    xsts_userhash = r.json()["DisplayClaims"]["xui"][0]["uhs"]
    xsts_token = r.json()["Token"]
    r = requests.post("https://api.minecraftservices.com/authentication/login_with_xbox", json={
        "identityToken": f"XBL3.0 x={xsts_userhash};{xsts_token}"
    })
    if not r.ok:
        raise Exception("Failed to get Minecraft token: " + r.text)
    minecraft_token = r.json()["access_token"]
    r = _http_session.get("https://api.minecraftservices.com/minecraft/profile", headers={
        "Authorization": f"Bearer {minecraft_token}"
    })
    if not r.ok:
        raise Exception("Failed to get Minecraft profile: " + r.text)
    username = r.json()["name"]
    uuid = r.json()["id"]
    return {
        "microsoft_refresh_token": microsoft_refresh_token,
        "minecraft_token": minecraft_token,
        "username": username,
        "uuid": uuid,
        "last_refresh": time.time()
    }
def check_mc_token(minecraft_token):
    r = _http_session.get("https://api.minecraftservices.com/minecraft/profile", headers={
        "Authorization": f"Bearer {minecraft_token}"
    })
    return r.ok
def ensure_mc_profile_valid(profile):
    if profile.get("type") != "microsoft":
        return profile
    token = profile.get("minecraft_token")
    refresh_token = profile.get("microsoft_refresh_token")
    if token and check_mc_token(token):
        return profile
    try:
        newdata = refresh_mc_token(refresh_token)
        profile.update(newdata)
        profiles_data = load_profiles()
        for idx, p in enumerate(profiles_data):
            if p.get("uuid") == profile.get("uuid") or (p.get("type") == "microsoft" and p.get("username") == profile.get("username")):
                profiles_data[idx] = profile
                break
        save_profiles(profiles_data)
        return profile
    except Exception as e:
        print(f"Refresh failed: {e}. Need full login.")
        try:
            newdata = ms_token_flow_interactive()
            profile.update(newdata)
            profiles_data = load_profiles()
            for idx, p in enumerate(profiles_data):
                if p.get("uuid") == profile.get("uuid") or (p.get("type") == "microsoft" and p.get("username") == profile.get("username")):
                    profiles_data[idx] = profile
                    break
            save_profiles(profiles_data)
            return profile
        except Exception as ee:
            raise Exception("Could not refresh or re-authenticate: " + str(ee))
def ask_profile_type(parent=None):
    result = {'value': None}
    win = tk.Toplevel(parent) if parent is not None else tk.Toplevel()
    win.title("Select Profile Type")
    win.transient(parent) if parent is not None else None
    win.grab_set()
    win.resizable(False, False)
    tm = get_theme_manager()
    bg = tm.get_color('bg_primary')
    fg = tm.get_color('fg_primary')
    btn_bg = tm.get_color('accent_primary')
    btn_fg = tm.get_color('fg_primary')
    win.configure(bg=bg)
    tk.Label(win, text="Select profile type:", bg=bg, fg=fg, font=("Segoe UI", 11)).pack(padx=24, pady=12)
    btn_frame = tk.Frame(win, bg=bg)
    btn_frame.pack(pady=10)

    def choose(val):
        result['value'] = val
        win.destroy()

    offline_btn = tk.Button(btn_frame, text="Offline", width=12,
                           bg=btn_bg, fg=btn_fg, font=("Segoe UI", 10, "bold"),
                           bd=0, padx=20, pady=10, cursor="hand2", relief="flat",
                           command=lambda: choose("offline"))
    offline_btn.pack(side="left", padx=8)

    ms_btn = tk.Button(btn_frame, text="Microsoft", width=12,
                      bg=btn_bg, fg=btn_fg, font=("Segoe UI", 10, "bold"),
                      bd=0, padx=20, pady=10, cursor="hand2", relief="flat",
                      command=lambda: choose("microsoft"))
    ms_btn.pack(side="left", padx=8)

    win.wait_window()
    return result['value']

def themed_askstring(title, prompt, parent=None, launcher=None, initialvalue=None):
    if launcher is not None and hasattr(launcher, '_get_theme_color'):
        def get_color(k):
            try:
                return launcher._get_theme_color(k)
            except Exception:
                return get_theme_manager().get_color(k)
    else:
        tm = get_theme_manager()
        def get_color(k):
            return tm.get_color(k)

    created_root = False
    if parent is None:
        parent = tk.Tk()
        parent.withdraw()
        created_root = True

    dialog = tk.Toplevel(parent)
    dialog.title(title)
    dialog.geometry("400x160")
    dialog.configure(bg=get_color('bg_primary'))
    dialog.transient(parent)
    dialog.grab_set()

    tk.Label(dialog, text=prompt,
             bg=get_color('bg_primary'),
             fg=get_color('fg_primary'),
             font=("Segoe UI", 11)).pack(pady=(20, 8), padx=20)

    username_var = tk.StringVar(value=initialvalue or "")
    entry = ttk.Entry(dialog, textvariable=username_var, width=34, font=("Segoe UI", 10))
    entry.pack(pady=6)
    entry.focus()

    result = {'value': None}

    def on_ok():
        val = username_var.get().strip()
        result['value'] = val if val != "" else None
        dialog.destroy()

    def on_cancel():
        dialog.destroy()

    btn_frame = tk.Frame(dialog, bg=get_color('bg_primary'))
    btn_frame.pack(pady=14)

    ok_btn = tk.Button(btn_frame, text="OK", width=10,
                       bg=get_color('accent_primary'),
                       fg=get_color('fg_primary'),
                       font=("Segoe UI", 10, "bold"),
                       bd=0, padx=20, pady=8, cursor="hand2", relief="flat",
                       command=on_ok)
    ok_btn.pack(side="left", padx=6)

    cancel_btn = tk.Button(btn_frame, text="Cancel", width=10,
                          bg=get_color('bg_hover'),
                          fg=get_color('fg_primary'),
                          font=("Segoe UI", 10),
                          bd=0, padx=20, pady=8, cursor="hand2", relief="flat",
                          command=on_cancel)
    cancel_btn.pack(side="left", padx=6)

    entry.bind('<Return>', lambda e: on_ok())
    entry.bind('<Escape>', lambda e: on_cancel())

    try:
        parent.update_idletasks()
        dialog.update_idletasks()
        x = parent.winfo_rootx() + (parent.winfo_width() // 2) - (dialog.winfo_width() // 2)
        y = parent.winfo_rooty() + (parent.winfo_height() // 2) - (dialog.winfo_height() // 2)
        dialog.geometry(f"+{x}+{y}")
    except Exception:
        pass

    dialog.wait_window()
    if created_root:
        try:
            parent.destroy()
        except Exception:
            pass
    return result['value']

def classic_askstring(title, prompt, parent=None):
    tm = get_theme_manager()
    bg = tm.get_color('bg_primary')
    fg = tm.get_color('fg_primary')
    accent = tm.get_color('accent_primary')
    hover = tm.get_color('bg_hover')

    created_root = False
    if parent is None:
        parent = tk.Tk()
        parent.withdraw()
        created_root = True

    dialog = tk.Toplevel(parent)
    dialog.title(title)
    dialog.geometry("400x200")
    dialog.configure(bg=bg)
    dialog.transient(parent)
    dialog.grab_set()

    tk.Label(dialog, text=prompt,
             bg=bg, fg=fg, font=("Segoe UI", 11)).pack(pady=(20, 10))

    username_var = tk.StringVar()
    entry = ttk.Entry(dialog, textvariable=username_var, width=30, font=("Segoe UI", 10))
    entry.pack(pady=10)
    entry.focus()

    result = {'username': None}

    def on_ok():
        val = username_var.get().strip()
        result['username'] = val if val != "" else None
        dialog.destroy()

    def on_cancel():
        dialog.destroy()

    btn_frame = tk.Frame(dialog, bg=bg)
    btn_frame.pack(pady=20)

    ok_btn = tk.Button(btn_frame, text="OK", width=10,
                      bg=accent, fg=fg, font=("Segoe UI", 10, "bold"),
                      bd=0, padx=20, pady=8, cursor="hand2", relief="flat",
                      command=on_ok)
    ok_btn.pack(side="left", padx=5)

    cancel_btn = tk.Button(btn_frame, text="Cancel", width=10,
                          bg=hover, fg=fg, font=("Segoe UI", 10),
                          bd=0, padx=20, pady=8, cursor="hand2", relief="flat",
                          command=on_cancel)
    cancel_btn.pack(side="left", padx=5)

    entry.bind('<Return>', lambda e: on_ok())
    entry.bind('<Escape>', lambda e: on_cancel())

    dialog.wait_window()
    if created_root:
        try:
            parent.destroy()
        except Exception:
            pass
    return result['username']
def add_profile(parent=None):
    created_root = False
    if parent is None:
        root = tk.Tk()
        root.withdraw()
        created_root = True
        use_parent = root
    else:
        use_parent = parent

    mode = ask_profile_type(parent=use_parent)
    print("DEBUG: Selected profile type:", mode)
    if mode not in ("offline", "microsoft"):
        if created_root:
            try:
                root.destroy()
            except Exception:
                pass
        return None
    profile = {"type": mode}
    if mode == "offline":
        username = classic_askstring("Username", "Enter offline username", parent=use_parent)
        if not username:
            messagebox.showerror("Error", "Username required.")
            if created_root:
                try:
                    root.destroy()
                except Exception:
                    pass
            return None
        profile["username"] = username
    elif mode == "microsoft":
        try:
            tokens = ms_token_flow_interactive()
            profile.update(tokens)
        except Exception as e:
            messagebox.showerror("Auth Error", f"Microsoft authentication failed:\n{e}")
            if created_root:
                try:
                    root.destroy()
                except Exception:
                    pass
            return None
    profiles_data = load_profiles()
    profiles_data.append(profile)
    if created_root:
        try:
            root.destroy()
        except Exception:
            pass
    if created_root:
        try:
            root.destroy()
        except Exception:
            pass
    save_profiles(profiles_data)
    messagebox.showinfo("Success", f"Profile '{profile.get('username', '')}' added.")
def make_style(root: tk.Tk) -> ttk.Style:
    style = ttk.Style(root)
    try:
        style.theme_use("clam")
    except:
        pass
    saved_theme = load_saved_theme()
    tm = get_theme_manager()
    tm.load_theme(saved_theme)
    tm.apply_to_style(style)
    root.option_add('*TCheckbutton*indicatorColor', tm.get_color('bg_input'))
    root.option_add('*TCheckbutton*selectColor', tm.get_color('accent_primary'))
    root.option_add('*TCheckbutton*indicatorBackground', tm.get_color('bg_input'))
    root.option_add('*TCheckbutton*indicatorForeground', tm.get_color('accent_primary'))
    return style
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
            
            nf = Neoforge()
            return nf.get_loader_versions(mc_version, True) or nf.get_loader_versions(mc_version, False)
        if loader == "fabric" and hasattr(minecraft_launcher_lib, "fabric"):
            return [v["version"] for v in minecraft_launcher_lib.fabric.get_all_loader_versions()]
        if loader == "quilt" and hasattr(minecraft_launcher_lib, "quilt"):
            return [v["version"] for v in minecraft_launcher_lib.quilt.get_all_loader_versions()]
    except Exception as e:
        print(f"[setup] loader versions fetch failed: {e}")
    return []

class WelcomeWizard(tk.Frame):
    PAGE_COUNT = 5
    def __init__(self, launcher):
        self.launcher = launcher
        self.tm = launcher.theme_manager
        super().__init__(launcher, bg=self._c('bg_primary'))
        self.page = 0
        self.completed = [True, False, False, False, True]  # greet
        self.java_var = tk.StringVar()
        self.java_choices = {}  # label
        self.recommended_java_label = None
        self.p_name = tk.StringVar()
        self.p_loader = tk.StringVar(value="vanilla")
        self.p_version = tk.StringVar()
        self.p_loader_version = tk.StringVar(value="N/A")
        self.p_ram = tk.StringVar(value="4G")
        self.profile_created = False
        self.rec_vars = {}
        self._build_chrome()
        self._render_page()
        self.place(x=0, y=0, relwidth=1, relheight=1)
        self.lift()
        self.focus_force()

    def _c(self, key):
        return self.tm.get_color(key)

    def _on_close(self):
        self.place_forget()
        self.destroy()

    def _build_chrome(self):
        header = tk.Frame(self, bg=self._c('bg_primary'))
        header.pack(fill="x", padx=28, pady=(24, 8))
        tk.Label(header, text="OrangLauncher", font=("Segoe UI", 20, "bold"),
                 bg=self._c('bg_primary'), fg=self._c('accent_primary')).pack(anchor="w")
        self.subtitle = tk.Label(header, text="", font=("Segoe UI", 10),
                                 bg=self._c('bg_primary'), fg=self._c('fg_secondary'))
        self.subtitle.pack(anchor="w", pady=(2, 0))
        self.dots_frame = tk.Frame(self, bg=self._c('bg_primary'))
        self.dots_frame.pack(fill="x", padx=28, pady=(6, 4))
        self.dot_labels = []
        for i in range(self.PAGE_COUNT):
            d = tk.Label(self.dots_frame, text="●", font=("Segoe UI", 13),
                         bg=self._c('bg_primary'), fg=self._c('fg_disabled'))
            d.pack(side="left", padx=(0, 6))
            self.dot_labels.append(d)
        self.body = tk.Frame(self, bg=self._c('bg_secondary'))
        self.body.pack(fill="both", expand=True, padx=28, pady=12)
        footer = tk.Frame(self, bg=self._c('bg_primary'))
        footer.pack(fill="x", padx=28, pady=(0, 22))
        self.back_btn = tk.Button(footer, text="Back", command=self._go_back,
                                  bg=self._c('bg_tertiary'), fg=self._c('fg_primary'),
                                  font=("Segoe UI", 10), bd=0, relief="flat",
                                  padx=18, pady=8, cursor="hand2",
                                  activebackground=self._c('bg_hover'),
                                  activeforeground=self._c('fg_primary'))
        self.back_btn.pack(side="left")
        self.next_btn = tk.Button(footer, text="Continue", command=self._go_next,
                                  bg=self._c('accent_primary'), fg="#ffffff",
                                  font=("Segoe UI", 10, "bold"), bd=0, relief="flat",
                                  padx=22, pady=8, cursor="hand2",
                                  activebackground=self._c('accent_hover'),
                                  activeforeground="#ffffff")
        self.next_btn.pack(side="right")

    def _clear_body(self):
        for w in self.body.winfo_children():
            w.destroy()

    def _set_complete(self, idx, value=True):
        self.completed[idx] = value
        self._refresh_nav()

    def _refresh_nav(self):
        for i, d in enumerate(self.dot_labels):
            if i == self.page:
                d.config(fg=self._c('accent_primary'))
            elif self.completed[i]:
                d.config(fg=self._c('fg_secondary'))
            else:
                d.config(fg=self._c('fg_disabled'))
        self.back_btn.config(state="normal" if self.page > 0 else "disabled")
        last = self.page == self.PAGE_COUNT - 1
        self.next_btn.config(text="Finish" if last else "Continue")
        if self.completed[self.page]:
            self.next_btn.config(state="normal", bg=self._c('accent_primary'),
                                 cursor="hand2")
        else:
            self.next_btn.config(state="disabled", bg=self._c('bg_tertiary'),
                                 cursor="arrow")

    def _go_back(self):
        if self.page > 0:
            self.page -= 1
            self._render_page()

    def _go_next(self):
        if not self.completed[self.page]:
            return
        if self.page == 3 and not self.profile_created:
            if not self._create_profile():
                return
        if self.page == self.PAGE_COUNT - 1:
            self._finish()
            return
        self.page += 1
        self._render_page()

    def _finish(self):
        self._apply_recommended_settings()
        mark_setup_done(True)
        try:
            self.launcher._refresh_profiles()
            self.launcher._refresh_game_profiles()
        except Exception:
            pass
        self._on_close()
    def _render_page(self):
        self._clear_body()
        self.next_btn.pack(side="right")
        [self._page_greet, self._page_account, self._page_java,
         self._page_profile, self._page_settings][self.page]()
        self._refresh_nav()

    def _heading(self, text, sub=None):
        tk.Label(self.body, text=text, font=("Segoe UI", 16, "bold"),
                 bg=self._c('bg_secondary'), fg=self._c('fg_primary')).pack(anchor="w", padx=24, pady=(22, 4))
        if sub:
            tk.Label(self.body, text=sub, font=("Segoe UI", 10), justify="left",
                     wraplength=560, bg=self._c('bg_secondary'),
                     fg=self._c('fg_secondary')).pack(anchor="w", padx=24, pady=(0, 8))

    def _page_greet(self):
        self.subtitle.config(text=self.launcher._t('WIZARD_STEP_GREET'))
        bg = self._c('bg_secondary')
        acc = self._c('accent_primary')

        mid = tk.Frame(self.body, bg=bg)
        mid.pack(fill="both", expand=True, padx=28, pady=(24, 0))

        tk.Label(mid, text=self.launcher._t('WIZARD_GREET_TAGLINE'),
                 font=("Segoe UI", 13), bg=bg, fg=self._c('fg_secondary')).pack(anchor="w", pady=(0, 20))

        for i, (t_key, d_key) in enumerate([
            ('WIZARD_GREET_STEP_ACCOUNT', 'WIZARD_GREET_STEP_ACCOUNT_DESC'),
            ('WIZARD_GREET_STEP_JAVA',    'WIZARD_GREET_STEP_JAVA_DESC'),
            ('WIZARD_GREET_STEP_PROFILE', 'WIZARD_GREET_STEP_PROFILE_DESC'),
            ('WIZARD_GREET_STEP_SETTINGS','WIZARD_GREET_STEP_SETTINGS_DESC'),
        ]):
            row = tk.Frame(mid, bg=bg)
            row.pack(fill="x", pady=7)
            num_bg = tk.Frame(row, bg=acc, width=26, height=26)
            num_bg.pack(side="left", padx=(0, 14))
            num_bg.pack_propagate(False)
            tk.Label(num_bg, text=str(i + 1), font=("Segoe UI", 10, "bold"),
                     bg=acc, fg="#ffffff").pack(expand=True)
            info = tk.Frame(row, bg=bg)
            info.pack(side="left", fill="x", expand=True)
            tk.Label(info, text=self.launcher._t(t_key), font=("Segoe UI", 10, "bold"),
                     bg=bg, fg=self._c('fg_primary'), anchor="w").pack(anchor="w")
            tk.Label(info, text=self.launcher._t(d_key), font=("Segoe UI", 9),
                     bg=bg, fg=self._c('fg_secondary'), anchor="w").pack(anchor="w")

        spacer = tk.Frame(mid, bg=bg)
        spacer.pack(fill="both", expand=True)

        foot = tk.Frame(self.body, bg=bg)
        foot.pack(fill="x", padx=28, pady=(0, 18))
        tk.Button(foot, text=self.launcher._t('WIZARD_SKIP_ALL'), command=self._skip_all,
                  bg=self._c('bg_tertiary'), fg=self._c('fg_tertiary'),
                  font=("Segoe UI", 9), bd=0, relief="flat", padx=14, pady=6,
                  cursor="hand2", activebackground=self._c('bg_hover'),
                  activeforeground=self._c('fg_primary')).pack(side="left")

    def _skip_all(self):
        mark_setup_done(True)
        self._on_close()

    def _page_account(self):
        self.subtitle.config(text=self.launcher._t('WIZARD_STEP_ACCOUNT'))
        self.next_btn.pack_forget()
        self._heading(self.launcher._t('WIZARD_ACCOUNT_TITLE'),
                      self.launcher._t('WIZARD_ACCOUNT_DESC'))
        self.acc_status = tk.Label(self.body, text="", font=("Segoe UI", 10),
                                   bg=self._c('bg_secondary'), fg=self._c('fg_secondary'))
        self.acc_status.pack(anchor="w", padx=24, pady=(0, 12))
        self._update_account_status()
        row = tk.Frame(self.body, bg=self._c('bg_secondary'))
        row.pack(anchor="w", padx=24, pady=(0, 8))
        tk.Button(row, text=self.launcher._t('WIZARD_ACCOUNT_LOGIN_MS'),
                  command=self._do_ms_login,
                  bg=self._c('accent_primary'), fg="#ffffff",
                  font=("Segoe UI", 10, "bold"), bd=0, relief="flat",
                  padx=18, pady=8, cursor="hand2",
                  activebackground=self._c('accent_hover'),
                  activeforeground="#ffffff").pack(side="left", padx=(0, 10))
        tk.Button(row, text=self.launcher._t('WIZARD_ACCOUNT_SKIP'),
                  command=self._account_skip,
                  bg=self._c('bg_tertiary'), fg=self._c('fg_secondary'),
                  font=("Segoe UI", 10), bd=0, relief="flat",
                  padx=18, pady=8, cursor="hand2",
                  activebackground=self._c('bg_hover'),
                  activeforeground=self._c('fg_primary')).pack(side="left")

    def _update_account_status(self):
        try:
            accounts = load_profiles()
        except Exception:
            accounts = []
        if accounts:
            names = ", ".join(a.get('username', '?') for a in accounts)
            try:
                self.acc_status.config(
                    text=self.launcher._t('WIZARD_ACCOUNT_SIGNED_IN').format(names=names),
                    fg=self._c('accent_primary'))
            except Exception:
                pass
            self._set_complete(1)
            return True
        else:
            try:
                self.acc_status.config(text=self.launcher._t('WIZARD_ACCOUNT_NO_ACCOUNTS'),
                                       fg=self._c('fg_secondary'))
            except Exception:
                pass
            return False

    def _account_skip(self):
        self._set_complete(1)
        self.page += 1
        self._render_page()

    def _do_ms_login(self):
        try:
            add_profile(parent=self)
        except Exception as e:
            messagebox.showerror("Error", f"Microsoft sign-in failed:\n{e}", parent=self)
        if self._update_account_status():
            self.page += 1
            self._render_page()

    def _page_java(self):
        self.subtitle.config(text=self.launcher._t('WIZARD_STEP_JAVA'))
        self._heading(self.launcher._t('WIZARD_JAVA_TITLE'),
                      self.launcher._t('WIZARD_JAVA_DESC'))
        installed = _wizard_detect_javas()
        opts = tk.Frame(self.body, bg=self._c('bg_secondary'))
        opts.pack(anchor="w", padx=24, pady=(0, 8), fill="x")
        self.java_choices = {}
        radio_items = []
        if installed:
            newest_major = installed[0][0]
            for major, path in installed:
                label = f"Java {major}"
                if major == newest_major:
                    label += f"  —  {self.launcher._t('WIZARD_JAVA_RECOMMENDED')}"
                    self.recommended_java_label = label
                self.java_choices[label] = path
                radio_items.append(label)
        auto_label = self.launcher._t('WIZARD_JAVA_AUTO')
        self.java_choices[auto_label] = "Auto"
        radio_items.append(auto_label)
        if not installed:
            self.recommended_java_label = auto_label
        if not self.java_var.get():
            self.java_var.set(self.recommended_java_label or auto_label)
        for label in radio_items:
            rb = tk.Radiobutton(opts, text=label, value=label, variable=self.java_var,
                                bg=self._c('bg_secondary'), fg=self._c('fg_primary'),
                                selectcolor=self._c('bg_input'), anchor="w",
                                activebackground=self._c('bg_secondary'),
                                activeforeground=self._c('fg_primary'),
                                font=("Segoe UI", 10), bd=0, highlightthickness=0,
                                command=lambda: self._set_complete(2))
            rb.pack(anchor="w", pady=3, fill="x")
        self._set_complete(2)

    def _page_profile(self):
        self.subtitle.config(text=self.launcher._t('WIZARD_STEP_PROFILE'))
        self._heading(self.launcher._t('WIZARD_PROFILE_TITLE'),
                      self.launcher._t('WIZARD_PROFILE_DESC'))
        if not self.p_version.get():
            vers = self.launcher_versions()
            self.p_version.set(vers[0] if vers else "")
        grid = tk.Frame(self.body, bg=self._c('bg_secondary'))
        grid.pack(anchor="w", padx=24, pady=(0, 6), fill="x")
        grid.columnconfigure(1, weight=1)
        def label(r, text):
            tk.Label(grid, text=text, width=14, anchor="w",
                     bg=self._c('bg_secondary'), fg=self._c('fg_primary'),
                     font=("Segoe UI", 10)).grid(row=r, column=0, sticky="w", pady=6, padx=(0, 10))
        label(0, "Name")
        ttk.Entry(grid, textvariable=self.p_name, width=34, style="Modern.TEntry").grid(row=0, column=1, sticky="ew", pady=6)
        label(1, "Loader")
        loader_combo = ttk.Combobox(grid, textvariable=self.p_loader, state="readonly",
                                    values=["vanilla", "forge", "neoforge", "fabric", "quilt"],
                                    style="Modern.TCombobox")
        loader_combo.grid(row=1, column=1, sticky="ew", pady=6)
        label(2, "Version")
        version_combo = ttk.Combobox(grid, textvariable=self.p_version,
                                    values=self.launcher_versions(), style="Modern.TCombobox")
        version_combo.grid(row=2, column=1, sticky="ew", pady=6)
        label(3, "Loader version")
        self.p_loader_combo = ttk.Combobox(grid, textvariable=self.p_loader_version,
                                          state="disabled", style="Modern.TCombobox")
        self.p_loader_combo.grid(row=3, column=1, sticky="ew", pady=6)
        ram_cell = tk.Frame(grid, bg=self._c('bg_secondary'))
        ram_cell.grid(row=4, column=1, sticky="ew", pady=6)
        label(4, "RAM")
        _make_ram_slider(ram_cell, self._c('bg_secondary'), self.p_ram,
                         self._c('accent_primary'), self._c('fg_primary'),
                         self._c('fg_secondary'), wizard_fmt=True).pack(fill="x")

        if not self.p_name.get():
            base = "My Profile"
            name = base
            i = 1
            while self.launcher.instance_manager.get_instance_by_name(name):
                i += 1
                name = f"{base} {i}"
            self.p_name.set(name)

        def on_loader_change(*_):
            self._refresh_loader_versions()
        loader_combo.bind("<<ComboboxSelected>>", on_loader_change)
        version_combo.bind("<<ComboboxSelected>>", on_loader_change)

        self.profile_status = tk.Label(self.body, text="", font=("Segoe UI", 9),
                                       bg=self._c('bg_secondary'), fg=self._c('fg_secondary'))
        self.profile_status.pack(anchor="w", padx=24, pady=(4, 0))
        self._set_complete(3, bool(self.p_name.get().strip()))
        self.p_name.trace_add("write", lambda *_: self._set_complete(3, bool(self.p_name.get().strip())))

    def launcher_versions(self):
        try:
            return get_available_versions()
        except Exception:
            return []

    def _refresh_loader_versions(self):
        loader = self.p_loader.get().lower()
        if loader == "vanilla":
            self.p_loader_combo.configure(state="disabled", values=[])
            self.p_loader_version.set("N/A")
            return
        self.p_loader_combo.configure(state="readonly", values=["Loading…"])
        self.p_loader_version.set("Loading…")
        mc = self.p_version.get()
        def work():
            versions = _wizard_loader_versions(loader, mc)
            def apply():
                if not self.p_loader_combo.winfo_exists():
                    return
                self.p_loader_combo.configure(values=versions or ["Latest"])
                self.p_loader_version.set(versions[0] if versions else "Latest")
            try:
                self.after(0, apply)
            except Exception:
                pass
        threading.Thread(target=work, daemon=True).start()

    def _create_profile(self):
        name = self.p_name.get().strip()
        version = self.p_version.get().strip()
        loader = self.p_loader.get().strip().lower()
        if not name or not version:
            messagebox.showerror("Missing info", "Please enter a name and pick a version.", parent=self)
            return False
        if self.launcher.instance_manager.get_instance_by_name(name):
            messagebox.showerror("Name taken", f"A profile called '{name}' already exists.", parent=self)
            return False
        lv = self.p_loader_version.get().strip()
        if loader == "vanilla" or lv in ("", "N/A", "Latest", "Loading…"):
            lv = ""
        try:
            inst = self.launcher.instance_manager.create_instance(
                name=name, version=version, mod_loader=loader,
                ram=self.p_ram.get(), loader_version=lv or None)
            if inst is None:
                raise RuntimeError("instance creation returned None")
            java_val = self.java_choices.get(self.java_var.get(), "Auto")
            if java_val and java_val != "Auto":
                inst.java_path = java_val
                self.launcher.instance_manager.save_instances()
        except Exception as e:
            messagebox.showerror("Error", f"Could not create profile:\n{e}", parent=self)
            return False
        self.profile_created = True
        return True

    def _page_settings(self):
        self.subtitle.config(text=self.launcher._t('WIZARD_STEP_SETTINGS'))
        self._heading(self.launcher._t('WIZARD_SETTINGS_TITLE'),
                      self.launcher._t('WIZARD_SETTINGS_DESC'))
        recs = [
            ("show_progress_bar",           self.launcher._t('WIZARD_SETTINGS_SHOW_PROGRESS'), True),
            ("discord_rpc_enabled",         self.launcher._t('WIZARD_SETTINGS_DISCORD'), bool(Presence)),
            ("delete_telemetry_on_startup", self.launcher._t('WIZARD_SETTINGS_TELEMETRY'), True),
            ("show_status_bar",             self.launcher._t('WIZARD_SETTINGS_STATUS_BAR'), True),
        ]
        box = tk.Frame(self.body, bg=self._c('bg_secondary'))
        box.pack(anchor="w", padx=24, pady=(4, 8), fill="x")
        self.rec_vars = {}
        for key, text, default in recs:
            row = tk.Frame(box, bg=self._c('bg_secondary'))
            row.pack(fill="x", pady=5)
            var = tk.BooleanVar(value=default)
            self.rec_vars[key] = var
            ToggleSwitch(row, var, bg=self._c('bg_secondary')).pack(side="left", padx=(0, 12))
            tk.Label(row, text=text, bg=self._c('bg_secondary'), fg=self._c('fg_primary'),
                     font=("Segoe UI", 10)).pack(side="left")
        # theme selector
        themes = self.launcher.theme_manager.get_available_themes()
        if themes:
            sep = tk.Frame(box, bg=self._c('border'), height=1)
            sep.pack(fill="x", pady=(8, 8))
            theme_row = tk.Frame(box, bg=self._c('bg_secondary'))
            theme_row.pack(fill="x", pady=4)
            tk.Label(theme_row, text=self.launcher._t('WIZARD_SETTINGS_THEME'),
                     bg=self._c('bg_secondary'), fg=self._c('fg_primary'),
                     font=("Segoe UI", 10)).pack(side="left", padx=(0, 12))
            self._theme_var = tk.StringVar(value=self.launcher.theme_manager.current_theme or (themes[0] if themes else ""))
            theme_combo = ttk.Combobox(theme_row, textvariable=self._theme_var,
                                       values=themes, state="readonly", width=20,
                                       style="Modern.TCombobox")
            theme_combo.pack(side="left")
            tk.Label(theme_row, text=self.launcher._t('WIZARD_SETTINGS_THEME_HINT'),
                     bg=self._c('bg_secondary'), fg=self._c('fg_secondary'),
                     font=("Segoe UI", 9)).pack(side="left", padx=(8, 0))
        tk.Label(self.body, text=self.launcher._t('WIZARD_SETTINGS_FINISH_HINT'),
                 font=("Segoe UI", 9, "italic"),
                 bg=self._c('bg_secondary'), fg=self._c('fg_secondary')).pack(anchor="w", padx=24, pady=(10, 0))
        self._set_complete(4)

    def _apply_recommended_settings(self):
        try:
            for key, var in self.rec_vars.items():
                target = getattr(self.launcher, key, None)
                if isinstance(target, tk.BooleanVar):
                    target.set(var.get())
                else:
                    setattr(self.launcher, key, tk.BooleanVar(value=var.get()))
            _save_settings(self.launcher)
            # apply visual effects that depend on the toggles
            try:
                self.launcher._toggle_status_bar()
            except Exception:
                pass
            try:
                _toggle_discord_rpc(self.launcher)
            except Exception:
                pass
            try:
                pb = getattr(self.launcher, 'show_progress_bar', None)
                sbf = getattr(self.launcher, 'status_bar_frame', None)
                if pb and sbf:
                    if pb.get():
                        sbf.pack(fill="x", side="bottom", pady=(0, 4))
                    else:
                        sbf.pack_forget()
            except Exception:
                pass
        except Exception as e:
            print(f"[setup] applying recommended settings failed: {e}")


# main app
class MinecraftLauncher(tk.Tk):
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
    def _set_locale(self, code):
        if code in self.locales:
            old_locale = self.current_locale
            self.current_locale = code
            self.translations = self.locales[code]
            print(f"[DEBUG] Changed language from {old_locale} to {code}")
            self._update_ui_language()
            try:
                _save_settings(self)
            except Exception as e:
                print(f"[DEBUG] Error saving language setting: {e}")
        else:
            print(f"[DEBUG] Locale {code} not found in available locales")
    def _t(self, key, **kwargs):
        text = self.translations.get(key, key)
        if kwargs:
            try:
                return text.format(**kwargs)
            except Exception:
                return text
        return text
    def _get_theme_color(self, color_key):
        return self.theme_manager.get_color(color_key)
    def _update_ui_language(self):
        print(f"[DEBUG] Updating UI language to: {self.current_locale}")
        if hasattr(self, 'play_btn'):
            self.play_btn.config(text=self._t('PLAY'))
        self._refresh_tabs_styling()

    def _load_themed_icon(self, icon_name, size=(24, 24), force_color=None):
        current_theme = self.theme_manager.current_theme
        color = force_color if force_color else self.theme_manager.get_color('fg_primary')
        
        cache_key = (icon_name, size, color, current_theme)
        if cache_key in self.icon_cache:
            return self.icon_cache[cache_key]
            
        icon_path = find_resource(f"oranglauncher/images/icons/{icon_name}.png")
        if not icon_path:
            icon_path = find_resource(f"oranglauncher/images/{icon_name}.png")
            
        if not icon_path:
            print(f"[WARN] Icon {icon_name} not found at {icon_path}")
            return None
            
        try:
            image = Image.open(icon_path).convert("RGBA")
            image = image.resize(size, Image.Resampling.LANCZOS)
            
            r, g, b = self.winfo_rgb(color) # returns 16-bit values
            color_tuple = (r//256, g//256, b//256, 255)
            
            colored_image = Image.new("RGBA", size, color_tuple)
            mask = image.split()[3]
            colored_image.putalpha(mask)
            
            photo = ImageTk.PhotoImage(colored_image)
            self.icon_cache[cache_key] = photo
            return photo
        except Exception as e:
            print(f"[ERROR] Failed to load icon {icon_name}: {e}")
            return None

    def _refresh_tabs_styling(self):
        if hasattr(self, 'notebook'):
            tab_icons = {
                0: 'news', 
                1: 'logs', 
                2: 'instances', 
                3: 'modding', 
                4: 'file',
                5: 'plugin',
                6: 'rs_sh', 
                7: 'settings'
            }
            
            for i in range(self.notebook.index('end')):
                if i in tab_icons:
                    icon_name = tab_icons[i]
                    icon = self._load_themed_icon(icon_name, size=(20, 20))
                    if icon:
                        try:
                            self.notebook.tab(i, image=icon, compound="left")
                            if not hasattr(self.notebook, '_tab_icons'): self.notebook._tab_icons = {}
                            self.notebook._tab_icons[i] = icon
                        except Exception as e:
                            print(f"Error setting tab icon: {e}")

        if hasattr(self, '_settings_nav_buttons_data'):
            for btn, icon_name, text_key in self._settings_nav_buttons_data:
                try:
                    icon = self._load_themed_icon(icon_name, size=(20, 20))
                    if icon:
                        btn.config(image=icon, compound="left", text=f"  {self._t(text_key)}")
                        btn._icon_ref = icon
                except Exception:
                    pass

        if hasattr(self, 'music_btn') and self.music_btn is not None:
            try:
                self.music_btn.config(text=self._t('PLAY_MUSIC'))
            except Exception:
                pass
        if hasattr(self, 'profile_cb') and self.profile_cb is not None:
            pass
        if hasattr(self, 'status_label') and self.status_label is not None:
            try:
                self.status_label.config(text=self._t('WELCOME'))
            except Exception:
                pass
        if hasattr(self, 'profile_label') and self.profile_label is not None:
            try:
                self.profile_label.config(text=self._t('PROFILE'))
            except Exception:
                pass
        if hasattr(self, 'new_profile_btn') and self.new_profile_btn is not None:
            try:
                self.new_profile_btn.config(text=self._t('NEW_PROFILE'))
            except Exception:
                pass
        if hasattr(self, 'version_title_label') and self.version_title_label is not None:
            try:
                self.version_title_label.config(text=self._t('GAME_PROFILES'))
            except Exception:
                pass
        if hasattr(self, 'notebook'):
            for i in range(self.notebook.index('end')):
                tab_id = self.notebook.tabs()[i]
                if i == 0:
                    self.notebook.tab(tab_id, text=self._t('UPDATE_NOTES'))
                elif i == 1:
                    self.notebook.tab(tab_id, text=self._t('LAUNCHER_LOG'))
                elif i == 2:
                    self.notebook.tab(tab_id, text=self._t('GAME_PROFILES'))
                elif i == 3:
                    self.notebook.tab(tab_id, text=self._t('MODS'))
                elif i == 4:
                    self.notebook.tab(tab_id, text=self._t('ORANGLIB'))
                elif i == 5:
                    self.notebook.tab(tab_id, text=self._t('SERVERS'))
                elif i == 6:
                    self.notebook.tab(tab_id, text=self._t('RES_SH_TAB_TITLE'))
                elif i == 7:
                    self.notebook.tab(tab_id, text=self._t('SETTINGS'))
        if hasattr(self, 'game_profile_cb'):
            self._refresh_game_profiles()
        try:
            update_language_ui(self)
        except Exception as e:
            print(f"[DEBUG] Error updating settings language UI: {e}")
        print(f"[DEBUG] UI language update completed")
    def _toggle_status_bar(self, *args):
        if hasattr(self, 'status_bar_frame'):
            if self.show_status_bar.get():
                self.status_bar_frame.pack(fill="x", side="bottom", pady=(0, 4))
            else:
                self.status_bar_frame.pack_forget()
        self._save_settings()
    def _cancel_launch(self):
        if hasattr(self, 'mc_process') and self.mc_process is not None:
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
            self.status_label.config(text=self._t("STOPPED"))
            self._restore_ui()
        else:
            self.cancel_requested = True
            self.play_btn.config(state="disabled", text=self._t("CANCELLING"))
            self.status_label.config(text=self._t("CANCELLING_LAUNCH"))
            try:
                if hasattr(self, 'launcher') and hasattr(self.launcher, 'stop_minecraft'):
                    self.launcher.stop_minecraft()
            except Exception as e:
                print(f"Error cancelling launch: {e}")
    def _set_window_icon(self):
        try:
            icon_path = find_resource("oranglauncher/images/minecraft.png")
            if icon_path:
                img = Image.open(str(icon_path))
                img = img.resize((64, 64), Image.Resampling.LANCZOS)
                photo = ImageTk.PhotoImage(img)
                self.iconphoto(False, photo)
                self._icon_photo = photo   
        except Exception as e:
            print(f"Could not set window icon: {e}")
    
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
    def _start_discord_rpc(self):
        if not Presence:
            print("[DEBUG] pypresence not installed, Discord RPC disabled")
            return
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
    def __init__(self):
        super().__init__()
        try:
            _orig_call = self.tk.call
            def _call_with_geometry_debug(*args):
                is_external_geometry = (
                    len(args) >= 4
                    and args[0] == "wm"
                    and args[1] == "geometry"
                    and args[2] == self._w
                    and not getattr(self, "_geometry_lock", False)
                )
                if getattr(self, "_geometry_debug", False) and is_external_geometry:
                    try:
                        stack = ''.join(tb.format_stack(limit=6))
                        print(f"[DEBUG] tk.call geometry: {args[3]}\n{stack}")
                    except Exception:
                        pass
                if is_external_geometry:
                    self._record_external_geometry(args[3])
                return _orig_call(*args)
            self._orig_tk_call = _orig_call
            self.tk.call = _call_with_geometry_debug
        except Exception:
            pass
        self.use_default_args = tk.BooleanVar(value=True)
        self.custom_args = tk.StringVar()
        self.show_status_bar = tk.BooleanVar(value=False)
        self.discord_rpc_enabled = tk.BooleanVar(value=bool(Presence))
        self.discord_rpc_mgr = None
        self.discord_start_time = None
        self._main_thread_id = threading.get_ident()
        self._progress_queue = queue.Queue(maxsize=32)
        try:
            global _APP_REF
            _APP_REF = weakref.ref(self)
        except Exception:
            pass
        self.game_profile_manager = get_game_profile_manager()
        self.instance_manager = get_instance_manager()
        self.icon_cache = {}
        self.icons = {}
        self.selected_game_profile = tk.StringVar()
        register_mod_change_callback(self._on_game_profile_changed)
        self.theme_manager = get_theme_manager()
        self.style = make_style(self)
        self._load_locales()
        try:
            saved_language = load_saved_language()
            if saved_language in self.locales:
                self.current_locale = saved_language
                self.translations = self.locales.get(saved_language, {})
            else:
                print(f"[DEBUG] Saved language {saved_language} not available, using default")
        except Exception as e:
            print(f"[DEBUG] Error loading saved language: {e}")
        self.title("OrangLauncher")
        icon_path = find_resource("oranglauncher/images/orange.ico")
        if icon_path:
            try:
                icon_image = Image.open(icon_path)
                icon_photo = ImageTk.PhotoImage(icon_image)
                self.iconphoto(True, icon_photo)
            except Exception as e:
                print(f"Failed to load icon: {e}")
        self.os_type = platform.system()
        self._geometry_lock = False
        self._initial_geometry = None
        self._user_geometry = None
        self._capture_initial_geometry = True
        self._forced_move_threshold = 48
        self._last_geometry = None
        self._ignore_forced_until = 0.0
        self._forced_return_window = 24
        self._forced_size_window = 16
        self._last_forced_source = None
        self._initial_restore_done = False
        self._forced_teleport_delta = 128
        self._user_geometry_history = deque(maxlen=25)
        self._history_suppress_seconds = 12.0
        self._last_forced_time = 0.0
        self._last_external_geometry = None
        self._last_external_geometry_time = 0.0
        self._recent_external_window = 2.0
        self._last_user_move_time = 0.0
        self._recent_user_move_window = 6.0
        try:
            self.bind("<Configure>", self._on_root_configure, add="+")
        except Exception:
            pass
        initial_width, initial_height = 1200, 900
        self._apply_geometry(initial_width, initial_height)
        self._capture_initial_geometry = True
        self.minsize(960, 750)
        self.configure(bg=self.theme_manager.get_color('bg_primary'))
        try:
            self.protocol("WM_DELETE_WINDOW", self.destroy)
        except Exception:
            pass
        self.selected_mod_loader = tk.StringVar(value="None")
        self._set_window_icon()
        self.music_thread = None
        self.music_stop_event = threading.Event()
        self.music_playing = False
        self.music_playlist = []   
        self.current_music_index = 0   
        self._music_monitor_active = False   
        self.selected_profile = tk.StringVar()
        self.progress = tk.DoubleVar(value=0)
        self.launch_thread = None
        self.cancel_requested = False
        self.profiles_list = []
        self.play_btn = None
        self.music_btn = None
        self.progress_bar = None
        self.status_label = None
        self.loaded_plugins = []
        self._profiles_cache = None   
        self._build_interface()
        try:
            self._refresh_profiles()
            self._refresh_game_profiles()
        except Exception as e:
            print(f"[DEBUG] Exception in _refresh_profiles: {e}")
        try:
            initialize_settings_on_startup(self)
            update_language_ui(self)
            self._update_ui_language() # Force refresh styling
            if self.discord_rpc_enabled.get():
                self._start_discord_rpc()
            if hasattr(self, 'status_bar_frame') and self.status_bar_frame is not None:
                if self.show_progress_bar.get():
                    self.status_bar_frame.pack(fill="x", side="bottom", pady=(0, 4))
                else:
                    self.status_bar_frame.pack_forget()
        except Exception as e:
            
            print(f"Settings initialization failed: {e}")
            traceback.print_exc()
        self.after(1000, self._on_tab_changed)
        self._progress_polling_active = False
        self._offline = False
        self._offline_label = None
        self.after(100, self._initialize_plugins)
        self.after(500, self._periodic_debug_update)
        self.after(2000, self._check_connectivity)
        self._pending_quickplay = None
        self._pending_open_file = None
        self.after(1500, self._startup_sync_sharing)
        self.after(400, self._maybe_show_welcome)
    def _maybe_show_welcome(self):
        try:
            done = is_setup_done()
            print(f"[setup] setup_done={done}")
            if not done:
                print("[setup] showing welcome wizard")
                WelcomeWizard(self)
                print("[setup] wizard created ok")
        except Exception as e:
            print(f"[setup] welcome wizard failed: {e}")
            traceback.print_exc()
        if self._pending_open_file:
            path = self._pending_open_file
            self._pending_open_file = None
            self.after(800, lambda: self._open_file_from_cli(path))

    def _open_file_from_cli(self, path: str):
        p = path.lower()
        if p.endswith('.mrpack'):
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
                    else:
                        messagebox.showerror("Import Failed", message)
                self.after(0, done)
            except Exception as e:
                self.after(0, lambda: (_restore(), messagebox.showerror("Import Error", str(e))))
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
                self.after(0, lambda: (_restore(), messagebox.showerror("Import Error", str(e))))
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
    def _on_game_profile_changed(self):
        if hasattr(self, 'modding_tab'):
            self.modding_tab.refresh_ui()
        current_profile = self.game_profile_manager.get_selected_profile()
        if current_profile:
            self.selected_mod_loader.set(current_profile.mod_loader)
    def _toggle_music(self):
        global pygame_mixer_initialized
        if not pygame or not pygame_available:
            messagebox.showerror(self._t("MUSIC_ERROR"), self._t("PYGAME_NOT_INSTALLED"))
            return
        if not pygame_mixer_initialized:
            try:
                if not pygame.display.get_init():
                    pygame.init()
                pygame.mixer.init()
                pygame_mixer_initialized = True
                print("[Music] Pygame mixer initialized on first music play")
            except Exception as e:
                print(f"Warning: pygame mixer module not available, audio disabled")
                print(f"Audio init error: {e}")
                messagebox.showerror(self._t("MUSIC_ERROR"), self._t("PYGAME_NOT_INSTALLED"))
                return
        
        if self.music_playing:
            try:
                pygame.mixer.music.stop()
            except Exception as e:
                print(f"Music stop error: {e}")
            self.music_playing = False
            self._music_monitor_active = False
            self.music_stop_event.set()
            self.music_playlist = []
            self.current_music_index = 0
            if self.music_btn:
                try:
                    self.music_btn.config(text=self._t("PLAY_MUSIC"))
                except tk.TclError as e:
                    print(f"Music button config error (stop): {e}")
        else:
            music_dir = find_resource("oranglauncher/music")
            if not music_dir or not music_dir.exists():
                base_dir = get_resource_path()
                music_dir = base_dir / "oranglauncher" / "music"
            if not music_dir or not music_dir.exists():
                messagebox.showwarning(
                    self._t("MUSIC_ERROR"), 
                    "Music folder not found at oranglauncher/music/"
                )
                return
            music_files = []
            for ext in ['*.mp3', '*.ogg', '*.wav']:
                music_files.extend(list(music_dir.glob(ext)))
            if not music_files:
                messagebox.showwarning(
                    self._t("MUSIC_ERROR"), 
                    "No music files found in oranglauncher/music/\nSupported formats: .mp3, .ogg, .wav, .flac"
                )
                return
            random.shuffle(music_files)
            self.music_playlist = music_files
            self.current_music_index = 0
            self._play_next_song()
            if not self._music_monitor_active:
                self._music_monitor_active = True
                self.music_stop_event.clear()
                threading.Thread(target=self._monitor_music_end, daemon=True).start()
    def _play_next_song(self):
        if not self.music_playlist or self.current_music_index >= len(self.music_playlist):
            self.current_music_index = 0
        if not self.music_playlist:
            return
        music_path = self.music_playlist[self.current_music_index]
        try:
            pygame.mixer.music.load(str(music_path))
            pygame.mixer.music.play(0)   
            self.music_playing = True
            if self.music_btn:
                try:
                    self.music_btn.config(text=self._t("STOP_MUSIC"))
                except tk.TclError as e:
                    print(f"Music button config error (play): {e}")
            print(f"[Music] Now playing ({self.current_music_index + 1}/{len(self.music_playlist)}): {music_path.name}")
            self.current_music_index += 1
        except Exception as e:
            print(f"Music play error: {e}")
            messagebox.showerror(
                self._t("MUSIC_ERROR"), 
                self._t("MUSIC_PLAY_FAILED") + f"\n{e}"
            )
    def _monitor_music_end(self):
        while self._music_monitor_active:
            try:
                if self.music_playing and pygame and pygame_available:
                    if not pygame.mixer.music.get_busy():
                        self._play_next_song()
                if self.music_stop_event.wait(timeout=1.0):
                    break
            except Exception as e:
                print(f"Music monitor error: {e}")
                if self.music_stop_event.wait(timeout=2.0):
                    break
    def _build_bottom_section(self, parent):
        bottom_frame = ttk.Frame(parent)
        bottom_frame.pack(side="bottom", fill="x", pady=(0, 8))
        bottom_frame.columnconfigure(0, weight=0)
        bottom_frame.columnconfigure(1, weight=1)
        bottom_frame.columnconfigure(2, weight=0)
        left_frame = ttk.Frame(bottom_frame)
        left_frame.grid(row=0, column=0, sticky="nw")
        self.profile_label = ttk.Label(left_frame, text=self._t("PROFILE"), style="Header.TLabel")
        self.profile_label.grid(row=0, column=0, sticky="w", padx=(0, 5))
        self.profile_cb = ttk.Combobox(
            left_frame,
            textvariable=self.selected_profile,
            state="readonly",
            width=25,
            font=("Segoe UI", 9)
        )
        self.profile_cb.grid(row=1, column=0, pady=(5, 0))
        self.profile_cb.bind("<<ComboboxSelected>>", self._on_profile_selected)
        btn_frame = ttk.Frame(left_frame)
        btn_frame.grid(row=2, column=0, pady=(5, 0), sticky="w")
        plus_icon_main = self._load_themed_icon("plus", size=(16, 16))
        self.new_profile_btn = tk.Button(btn_frame, 
                                        text=f"  {self._t('NEW_PROFILE')}",
                                        image=plus_icon_main,
                                        compound="left",
                                        bg=self.theme_manager.get_color('bg_hover'),
                                        fg=self.theme_manager.get_color('fg_primary'),
                                        activebackground=self.theme_manager.get_color('bg_pressed'),
                                        activeforeground=self.theme_manager.get_color('fg_primary'),
                                        font=("Segoe UI", 9),
                                        relief="flat",
                                        bd=0,
                                        padx=10,
                                        pady=6,
                                        cursor="hand2",
                                        command=self._new_profile)
        self.new_profile_btn.image = plus_icon_main  # type: ignore
        self.new_profile_btn.pack(side="left", padx=(0, 5))
        center_frame = ttk.Frame(bottom_frame)
        center_frame.grid(row=0, column=1, sticky="n", padx=20)
        content_frame = ttk.Frame(center_frame)
        content_frame.grid(row=0, column=0, sticky="n")
        content_frame.columnconfigure(0, weight=1)
        content_frame.configure(width=420)
        self.version_title_label = ttk.Label(
            content_frame,
            text=self._t("GAME_PROFILES"),
            style="Header.TLabel",
            anchor="center",
            justify="center"
        )
        self.version_title_label.grid(row=0, column=0, pady=(0, 5), sticky="n")
        selected_instance = self.instance_manager.get_selected_instance()
        selected_profile = self.game_profile_manager.get_selected_profile()
        if selected_instance:
            display_text = f"Instance: {selected_instance.name} | {selected_instance.version} ({selected_instance.mod_loader})"
        elif selected_profile:
            display_text = f"Profile: {selected_profile.name} | {selected_profile.version} ({selected_profile.mod_loader})"
        else:
            display_text = self._t("NO_PROFILE_SELECTED")
        self.version_label = ttk.Label(
            content_frame,
            text=display_text,
            style="Header.TLabel",
            anchor="center",
            justify="center",
            width=58,
            wraplength=380
        )
        self.version_label.grid(row=1, column=0, pady=(5, 0), sticky="ew")
        def _update_version_wrap(event=None):
            try:
                width = content_frame.winfo_width()
                wrap = max(width - 40, 260)
                self.version_label.config(wraplength=wrap)
            except Exception:
                pass
        content_frame.bind("<Configure>", _update_version_wrap)
        self.after_idle(_update_version_wrap)
        self.play_btn = ttk.Button(
            content_frame,
            text=self._t("PLAY"),
            style="Play.TButton",
            command=self._launch_game,
            width=12
        )
        self.play_btn.grid(row=2, column=0, pady=(12, 0))
        right_frame = ttk.Frame(bottom_frame)
        right_frame.grid(row=0, column=2, sticky="ne", padx=(0, 20))
        right_frame.grid_columnconfigure(0, weight=1)
        self.status_label = ttk.Label(
            right_frame,
            text=self._t("WELCOME"),
            style="News.TLabel",
            justify="right",
            anchor="e"
        )
        self.status_label.pack(anchor="e")
        style = ttk.Style()
        style_name = "Gray.TCombobox"
        combo_bg = self.theme_manager.get_color('bg_section')
        self.option_add('*TCombobox*Listbox.background', self.theme_manager.get_color('bg_secondary'))
        self.option_add('*TCombobox*Listbox.foreground', self.theme_manager.get_color('fg_primary'))
        self.option_add('*TCombobox*Listbox.selectBackground', self.theme_manager.get_color('accent_primary'))
        self.option_add('*TCombobox*Listbox.selectForeground', self.theme_manager.get_color('fg_primary'))
        style.configure(
            style_name, 
            fieldbackground=combo_bg,
            background=self.theme_manager.get_color('bg_secondary'),
            foreground=self.theme_manager.get_color('fg_primary'),
            arrowcolor=self.theme_manager.get_color('fg_primary'),
            bordercolor=self.theme_manager.get_color('bg_secondary'),
            darkcolor=self.theme_manager.get_color('bg_secondary'),
            lightcolor=self.theme_manager.get_color('bg_secondary'),
        )
        style.map(
            style_name,
            fieldbackground=[('readonly', combo_bg)],
            selectbackground=[('readonly', combo_bg)],
            selectforeground=[('readonly', self.theme_manager.get_color('fg_primary'))],
            background=[('readonly', self.theme_manager.get_color('bg_secondary'))]
        )
        self.game_profile_cb = ttk.Combobox(
            right_frame,
            textvariable=self.selected_game_profile,
            state="readonly",
            width=30,
            font=("Segoe UI", 9),
            style=style_name,
            justify="left"
        )
        self.game_profile_cb.pack(anchor="e", pady=(5, 0))
        self.game_profile_cb.bind("<<ComboboxSelected>>", self._on_game_profile_selected)
        self.status_bar_frame = ttk.Frame(self)
        self.status_bar_progress = ttk.Progressbar(
            self.status_bar_frame,
            variable=self.progress,
            maximum=100,
            length=300,
            mode="determinate"
        )
        self.status_bar_progress.pack(anchor="w", padx=10, pady=(0, 4), fill="x")
        if hasattr(self, 'show_progress_bar') and self.show_progress_bar.get():
            self.status_bar_frame.pack(fill="x", side="bottom", pady=(0, 4))
        else:
            self.status_bar_frame.pack_forget()
    def _refresh_game_profiles(self):
        try:
            instance_names = self.instance_manager.get_instance_names()
            old_profile_names = get_game_profile_names()
            all_profiles = []
            for name in instance_names:
                if name != "Latest Release":
                    all_profiles.append(name)
            for name in old_profile_names:
                if name not in all_profiles and name != "Latest Release":
                    all_profiles.append(name)
            if hasattr(self, 'game_profile_cb'):
                self.game_profile_cb['values'] = all_profiles
                selected_instance = self.instance_manager.get_selected_instance()
                if selected_instance:
                    current_selection = self.selected_game_profile.get()
                    if current_selection not in all_profiles:
                        self.selected_game_profile.set(selected_instance.name)
                    if hasattr(self, 'version_label'):
                        self.version_label.config(text=f"{selected_instance.version} ({selected_instance.mod_loader})")
                else:
                    selected_profile = self.game_profile_manager.get_selected_profile()
                    if selected_profile:
                        current_selection = self.selected_game_profile.get()
                        if current_selection not in all_profiles:
                            self.selected_game_profile.set(selected_profile.name)
                        if hasattr(self, 'version_label'):
                            self.version_label.config(text=f"{selected_profile.version} ({selected_profile.mod_loader})")
                    elif all_profiles:
                        self.selected_game_profile.set(all_profiles[0])
                        if instance_names:
                            first_instance = self.instance_manager.get_instance_by_name(instance_names[0])
                            if first_instance:
                                self.instance_manager.set_selected_instance(first_instance.instance_id)
        except Exception as e:
            print(f"Error refreshing game profiles: {e}")
    def _refresh_profiles(self):
        self._profiles_cache = None
        try:
            profile_data = load_profiles()
            self.profiles_list = [f"{p['username']} ({p['type']})" for p in profile_data]
            if hasattr(self, 'profile_cb') and self.profile_cb:
                self.profile_cb['values'] = self.profiles_list + [self._t("LOADING_PROFILES")]
                if self.profiles_list:
                    self.profile_cb.current(0)
                    self.selected_profile.set(self.profiles_list[0])
                    username = profile_data[0]['username']
                    self.status_label.config(text=self._t("WELCOME_USER", username=username))
                else:
                    self.profile_cb.current(len(self.profiles_list))
                    self.selected_profile.set(self._t("LOADING_PROFILES"))    
        except Exception as e:
            print(f"Error refreshing profiles: {e}")
            if hasattr(self, 'profile_cb') and self.profile_cb:
                self.profile_cb['values'] = [self._t("LOADING_PROFILES")]
                self.profile_cb.current(0)
    def _on_profile_selected(self, event=None):
        selection = self.selected_profile.get()
        if selection == self._t("LOADING_PROFILES"):
            self._new_profile()
        else:
            try:
                username = selection.split(' (')[0]
                self.status_label.config(text=self._t("WELCOME_USER", username=username))
                self._update_profile_display()
            except Exception as e:
                print(f"Error updating welcome message: {e}")
    def _update_profile_display(self):
        try:
            selected_instance = self.instance_manager.get_selected_instance()
            if selected_instance:
                self.version_label.config(text=f"Instance: {selected_instance.name} | {selected_instance.version} ({selected_instance.mod_loader})")
                return
            current_game_profile = self.game_profile_manager.get_selected_profile()
            if current_game_profile:
                self.version_label.config(text=f"Profile: {current_game_profile.name} | {current_game_profile.version} ({current_game_profile.mod_loader})")
            else:
                self.version_label.config(text=self._t("NO_PROFILE_SELECTED"))
        except Exception as e:
            print(f"Error updating profile display: {e}")
            self.version_label.config(text=self._t("NO_PROFILE_SELECTED"))
    def _on_tab_changed(self, event=None):
        try:
            selected_tab_index = self.notebook.index(self.notebook.select())
            if selected_tab_index == 0:
                self._show_news_embed()
            else:
                self._hide_news_embed()
        except Exception as e:
            print(f"[DEBUG] Error handling tab change: {e}")
    def _show_news_embed(self):
        try:
            if hasattr(self, 'news_viewer') and self.news_viewer:
                if hasattr(self.news_viewer, 'enable_embed'):
                    self.news_viewer.enable_embed()
        except Exception as e:
            print(f"[DEBUG] Error showing news embed: {e}")
    def _hide_news_embed(self):
        try:
            if hasattr(self, 'news_viewer') and self.news_viewer:
                if hasattr(self.news_viewer, 'disable_embed'):
                    self.news_viewer.disable_embed()
        except Exception as e:
            print(f"[DEBUG] Error hiding news embed: {e}")
    def _new_profile(self):
        try:
            result = add_profile(parent=self)
            self._refresh_profiles()
            if result:
                new_label = f"{result['username']} ({result['type']})"
                self.selected_profile.set(new_label)
                for i, profile in enumerate(self.profile_cb['values']):
                    if profile == new_label:
                        self.profile_cb.current(i)
                        break
        except Exception as e:
            messagebox.showerror(self._t("PROFILE_ERROR"), f"{self._t('FAILED_CREATE_PROFILE')}\n{e}")
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
    def _apply_progress_update(self, percent, status_text):
        try:
            percent = min(max(percent, 0), 100)
            if hasattr(self, "progress"):
                self.progress.set(percent)
            status_label = getattr(self, "status_label", None)
            if status_label is not None:
                status_label.config(text=status_text)
        except Exception:
            pass
    def _launch_game(self):
        if getattr(self, 'launch_thread', None) and self.launch_thread and self.launch_thread.is_alive():
            messagebox.showinfo(self._t("LAUNCHER_BUSY"), self._t("LAUNCHER_BUSY_MSG"))
            return
        selected_name = self.selected_profile.get()
        if not selected_name or selected_name == self._t("LOADING_PROFILES"):
            messagebox.showerror(self._t("PROFILE_ERROR"), self._t("SELECT_VALID_PROFILE"))
            return
        selected_profile = None
        try:
            profile_data = load_profiles()
            for prof in profile_data:
                label = f"{prof['username']} ({prof['type']})"
                if label == selected_name:
                    selected_profile = prof
                    break
        except Exception as e:
            messagebox.showerror(self._t("PROFILE_ERROR"), f"{self._t('ERROR_LOADING_PROFILES')}\n{e}")
            return
        if not selected_profile:
            messagebox.showerror(self._t("PROFILE_ERROR"), self._t("SELECTED_PROFILE_NOT_FOUND"))
            return
        current_game_profile = self.game_profile_manager.get_selected_profile()
        current_instance = self.instance_manager.get_selected_instance()
        if current_instance:
            version = current_instance.version
            mod_loader = current_instance.mod_loader
            ram = current_instance.ram
            launch_name = current_instance.name
        elif current_game_profile:
            version = current_game_profile.version
            mod_loader = current_game_profile.mod_loader.lower()
            ram = current_game_profile.ram
            launch_name = current_game_profile.name
        else:
            try:
                default_instance = self.instance_manager.create_instance(
                    "Default Instance", "26.1.2", "vanilla", "4G"
                )
                if default_instance:
                    self.instance_manager.set_selected_instance(default_instance.instance_id)
                    self._refresh_game_profiles()
                    version = default_instance.version
                    mod_loader = default_instance.mod_loader
                    ram = default_instance.ram
                    launch_name = default_instance.name
                else:
                    messagebox.showerror("Instance Error", "Failed to create default instance. Please create an instance manually.")
                    return
            except Exception as e:
                messagebox.showerror("Instance Error", f"Failed to create default instance: {e}")
                return
        username = selected_profile.get("username", "")
        uuid = selected_profile.get("uuid", "")
        if not uuid or uuid == "0-0-0-0":
            uuid = str(uuid_module.uuid4())
        if not version or version.strip() == "":
            messagebox.showerror("Version Error", f"Selected instance has no Minecraft version set. Please edit the instance to set a valid version.")
            return
        print(f"Playing Minecraft {version} ({mod_loader}) as {username} with {ram} RAM...")
        print(f"Using instance/profile: {launch_name}")
        if getattr(self, 'discord_rpc_mgr', None) and self.discord_rpc_enabled.get():
            self._update_discord_rpc(
                "Playing Minecraft", 
                f"{version} ({mod_loader})"
            )
        if current_game_profile:
            mark_profile_used(current_game_profile.name)
        self.progress.set(0)
        self.cancel_requested = False
        self.play_btn.config(text=self._t("STOP"), state="normal", command=self._cancel_launch)
        self.profile_cb.config(state="disabled")
        self.status_label.config(text=f"Launching {version} ({mod_loader})... 0%")
        if hasattr(self, 'log_text'):
            self.log_text.config(state="normal")
            self.log_text.delete("1.0", tk.END)
            self.log_text.insert(tk.END, f"[Launcher] Launching Minecraft {version} ({mod_loader}) as {username}...\n")
            self.log_text.config(state="disabled")
        self.mc_process = None
        if not ram.endswith('G') and not ram.endswith('M'):
            ram = f"{ram}G"
            print(f"{ram}")
        quick_play_server = getattr(self, '_pending_quickplay', None)
        self._pending_quickplay = None
        self.launch_thread = threading.Thread(
            target=self._run_launcher_thread,
            args=(current_instance, launch_name, version, mod_loader, ram, selected_profile, username, uuid, quick_play_server),
            daemon=True
        )
        self.launch_thread.start()
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
            options = {
                'username': username,
                'uuid': uuid,
                'token': access_token,
                'executablePath': java_exe,
                'jvmArguments': [f"-Xmx{ram}", f"-Xms{ram}"]
            }
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
                    if version_exists:
                        version = current_instance.installed_version_id
                        self._safe_append_log(f"[Launcher] Using installed version: {version}")
                    else:
                        needs_loader_install = True
                        self._safe_append_log(f"[Launcher] Version {current_instance.installed_version_id} not found locally, will install...")
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
                            nf = Neoforge()
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
            self._safe_append_log(f"[Launcher] Starting Minecraft...")
            command = minecraft_launcher_lib.command.get_minecraft_command(version, minecraft_directory, options)
            command = [arg for arg in command if arg != "--sun-misc-unsafe-memory-access=allow"]
            self.after(0, self._on_mc_started)
            launch_env = os.environ.copy()
            if getattr(self, 'use_dri_prime', None) and self.use_dri_prime.get():
                launch_env['DRI_PRIME'] = '1'
            if current_instance and getattr(current_instance, 'env_vars', None):
                for line in current_instance.env_vars.splitlines():
                    if '=' in line and not line.strip().startswith('#'):
                        k, _, v = line.partition('=')
                        launch_env[k.strip()] = v.strip()
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
            for line in self.mc_process.stdout:
                if self.cancel_requested:
                    self.mc_process.terminate()
                    break
                self._safe_append_log(line.rstrip())
            self.mc_process.wait()
            exit_code = self.mc_process.returncode
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
            self.after(0, lambda: messagebox.showerror(
                "Version Not Found",
                f"Minecraft version '{bad_ver}' does not exist.{hint}\n\nPlease check the version name and try again."
            ))
        except Exception as e:
            self._safe_append_log(f"[ERROR] Launch failed: {e}")
            self._safe_append_log(traceback.format_exc())
        finally:
            self.after(0, self._restore_ui)
    def _safe_append_log(self, line):
        try:
            self.after(0, self._append_log, line)
        except Exception:
            pass
    _SHARE_TARGETS = [
        ('share_options',       'options.txt',    'options.txt',         True),
        ('share_resourcepacks', 'resourcepacks',  'resourcepacks',       False),
        ('share_shaderpacks',   'shaderpacks',    'shaderpacks',         False),
        ('share_servers',       'servers.dat',    'servers.dat',         True),
        ('share_screenshots',   'screenshots',    'screenshots',         False),
    ]
    def _check_connectivity(self):
        def _ping():
            try:
                _http_session.get("https://launchermeta.mojang.com/mc/game/version_manifest.json",
                             timeout=5, stream=True).close()
                online = True
            except Exception:
                online = False
            self.after(0, lambda: self._set_connectivity(online))
        threading.Thread(target=_ping, daemon=True).start()
        try:
            if self.winfo_exists():
                self.after(60000, self._check_connectivity)
        except Exception:
            pass
    def _set_connectivity(self, online):
        self._offline = not online
        if not hasattr(self, 'status_label') or self.status_label is None:
            return
        try:
            parent = self.status_label.master
        except Exception:
            return
        if online:
            if self._offline_label and self._offline_label.winfo_exists():
                self._offline_label.pack_forget()
        else:
            if not self._offline_label or not self._offline_label.winfo_exists():
                self._offline_label = tk.Label(
                    parent, text=" ⚠ Offline ",
                    bg="#cc3333", fg="#ffffff",
                    font=("Segoe UI", 8, "bold"),
                    relief="flat", padx=4, pady=2
                )
                self._offline_label.pack(side="right", padx=(0, 8))
    def _periodic_debug_update(self):
        try:
            if hasattr(self, 'debug_mode_enabled') and self.debug_mode_enabled.get():
                if hasattr(self, '_debug_text_widget') and self._debug_text_widget is not None:
                    try:
                        if self._debug_text_widget.winfo_exists():
                            _update_debug_info(self)
                    except tk.TclError:
                        pass
        except Exception:
            pass
        try:
            if self.winfo_exists():
                self.after(500, self._periodic_debug_update)
        except Exception:
            pass

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
                    if not shared_target.exists() and not shared_target.is_symlink():
                        if inst_path.exists() and not inst_path.is_symlink():
                            if is_file:
                                shutil.copy2(inst_path, shared_target)
                            else:
                                shutil.copytree(str(inst_path), str(shared_target))
                        else:
                            if is_file:
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
                            # merge this instance's packs into the shared folder so nothing is lost
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
    def _check_crash_report(self, minecraft_directory, exit_code):
        try:
            crash_dir = Path(minecraft_directory) / "crash-reports"
            if not crash_dir.exists():
                return
            reports = sorted(crash_dir.glob("crash-*.txt"), key=lambda p: p.stat().st_mtime, reverse=True)
            if not reports:
                return
            latest = reports[0]
            content = latest.read_text(encoding="utf-8", errors="replace")
            lines = content.splitlines()
            summary_lines = [l for l in lines[:60] if l.strip()][:12]
            summary = "\n".join(summary_lines)
            win = tk.Toplevel(self)
            win.title("Minecraft Crashed")
            win.configure(bg=self._get_theme_color('bg_primary'))
            win.resizable(True, True)
            win.geometry("640x400")
            win.transient(self)
            tk.Label(win, text=f"Minecraft exited with code {exit_code}",
                     bg=self._get_theme_color('bg_primary'), fg="#ff6b6b",
                     font=("Segoe UI", 12, "bold")).pack(anchor="w", padx=16, pady=(16, 4))
            tk.Label(win, text=f"Latest crash report: {latest.name}",
                     bg=self._get_theme_color('bg_primary'), fg=self._get_theme_color('fg_secondary'),
                     font=("Segoe UI", 9)).pack(anchor="w", padx=16, pady=(0, 8))
            txt = scrolledtext.ScrolledText(win, font=("Consolas", 8),
                                            bg=self._get_theme_color('bg_input'),
                                            fg=self._get_theme_color('fg_primary'),
                                            relief="flat", bd=0)
            txt.pack(fill="both", expand=True, padx=16, pady=(0, 8))
            txt.insert("1.0", summary)
            txt.config(state="disabled")
            btn_row = tk.Frame(win, bg=self._get_theme_color('bg_primary'))
            btn_row.pack(fill="x", padx=16, pady=(0, 12))
            def _open_full():
                
                _sp.Popen(["xdg-open", str(latest)])
            tk.Button(btn_row, text="Open full report", command=_open_full,
                      bg=self._get_theme_color('bg_tertiary'), fg=self._get_theme_color('fg_primary'),
                      relief="flat", bd=0, padx=12, pady=6, cursor="hand2").pack(side="left", padx=(0, 8))
            tk.Button(btn_row, text="Close", command=win.destroy,
                      bg=self._get_theme_color('accent_primary'), fg="#ffffff",
                      relief="flat", bd=0, padx=12, pady=6, cursor="hand2").pack(side="left")
        except Exception as e:
            print(f"[DEBUG] Error reading crash report: {e}")
    def _set_status_text(self, text):
        self.status_label.config(text=text)
    def _on_mc_started(self):
        current_game_profile = self.game_profile_manager.get_selected_profile()
        selected_name = self.selected_profile.get()
        username = selected_name.split(' (')[0] if selected_name else ''
        selected_instance = self.instance_manager.get_selected_instance()
        if selected_instance:
            self.status_label.config(text=f"Minecraft {selected_instance.version} ({selected_instance.mod_loader}) running as {username}")
            self.version_label.config(text=f"Instance: {selected_instance.name} | {selected_instance.version} ({selected_instance.mod_loader})")
            if getattr(self, 'discord_rpc_mgr', None) and self.discord_rpc_enabled.get():
                self._update_discord_rpc(
                    "Playing Minecraft",
                    f"{selected_instance.version} ({selected_instance.mod_loader})"
                )
        elif current_game_profile:
            self.status_label.config(text=f"Minecraft {current_game_profile.version} ({current_game_profile.mod_loader}) running as {username}")
            self.version_label.config(text=f"Profile: {current_game_profile.name} | {current_game_profile.version} ({current_game_profile.mod_loader})")
            if getattr(self, 'discord_rpc_mgr', None) and self.discord_rpc_enabled.get():
                self._update_discord_rpc(
                    "Playing Minecraft",
                    f"{current_game_profile.version} ({current_game_profile.mod_loader})"
                )
        else:
            self.status_label.config(text=f"Minecraft running as {username}")
        self.play_btn.config(text=self._t("STOP"), state="normal", command=self._cancel_launch)
    def _restore_ui(self):
        self.play_btn.config(text=self._t("PLAY"), state="normal", command=self._launch_game)
        self.profile_cb.config(state="readonly")
        if hasattr(self, '_progress_queue') and self._progress_queue is not None:
            try:
                while True:
                    self._progress_queue.get_nowait()
            except Exception:
                pass
        self.progress.set(0)
        self.launch_thread = None
        self.mc_process = None
        selected_name = self.selected_profile.get()
        if selected_name and selected_name != self._t("LOADING_PROFILES"):
            username = selected_name.split(' (')[0]
            self.status_label.config(text=self._t("WELCOME_USER", username=username))
        if getattr(self, 'discord_rpc_mgr', None) and self.discord_rpc_enabled.get():
            self._update_discord_rpc("Idling in Launcher")
    def _append_log(self, message):
        if hasattr(self, 'log_text'):

            message = message.rstrip()
            if not message:
                return
            plain = _re.sub(r'(?:\x1b|\033)\[([0-9;]*)m|\[([0-9;]+)m', '', message)
            msg_lower = plain.lower()
            _mc_level = _re.search(r'\[(?:[^\]]+)/([A-Z]+)\]', plain)
            if _mc_level:
                level = _mc_level.group(1)
                if level in ("ERROR", "FATAL"):
                    tag = "error"
                elif level == "WARN":
                    tag = "warning"
                elif level == "INFO":
                    tag = "info"
                else:
                    tag = None
            elif any(k in msg_lower for k in ("[error]", "error:", "exception", "traceback", "failed")):
                tag = "error"
            elif any(k in msg_lower for k in ("[warn]", "warning")):
                tag = "warning"
            elif any(k in msg_lower for k in ("success", "done", "finished", "installed", "complete")):
                tag = "success"
            elif any(k in msg_lower for k in ("[launcher]", "[forge]", "[fabric]", "[quilt]", "[install]", "[updater]")):
                tag = "info"
            else:
                tag = None
            if hasattr(self, '_log_buffer'):
                self._log_buffer.append((message, tag))
            show = True
            if hasattr(self, '_log_filter_vars'):
                show = self._log_filter_vars.get(tag, tk.BooleanVar(value=True)).get()
            if show and hasattr(self, '_log_search_var'):
                search = self._log_search_var.get()
                if search and search != "Type to filter logs..." and search.lower() not in msg_lower:
                    show = False
            if show:
                self.log_text.config(state="normal")
                if hasattr(self, '_insert_ansi_line'):
                    self._insert_ansi_line(message, tag)
                else:
                    self.log_text.insert(tk.END, f"{plain}\n", tag)
                self.log_text.see(tk.END)
                self.log_text.config(state="disabled")
    def log_message(self, message):
        if hasattr(self, 'log_text'):
            self.log_text.config(state="normal")
            self.log_text.insert(tk.END, f"{message}\n")
            self.log_text.see(tk.END)
            self.log_text.config(state="disabled")
    def _save_settings(self):
        config_dir = Path.home() / ".config" / "oranglauncher"
        config_dir.mkdir(parents=True, exist_ok=True)
        config_path = config_dir / "launcher_config.json"
        try:
            with open(config_path, "r") as f:
                data = json.load(f)
        except Exception:
            data = {}
        data["custom_args"] = self.custom_args.get()
        data["use_default_args"] = self.use_default_args.get()
        if hasattr(self, 'show_status_bar'):
            data["show_status_bar"] = self.show_status_bar.get()
        if hasattr(self, 'discord_rpc_enabled'):
            data["discord_rpc_enabled"] = self.discord_rpc_enabled.get()
        data["language"] = self.current_locale
        try:
            with open(config_path, "w") as f:
                json.dump(data, f, indent=2)
        except Exception:
            pass
    def _build_interface(self):
        main_frame = ttk.Frame(self)
        main_frame.pack(fill="both", expand=True, padx=8, pady=8)
        self._build_bottom_section(main_frame)
        self.notebook = ttk.Notebook(main_frame)
        self.notebook.pack(fill="both", expand=True, pady=(0, 8))
        self.notebook.bind("<<NotebookTabChanged>>", self._on_tab_changed)
        
        def _on_mousewheel(event):
            try:
                delta = 0
                if event.num == 5:
                    delta = -1
                elif event.num == 4: 
                    delta = 1
                elif event.delta:
                    if abs(event.delta) >= 120:
                        delta = int(event.delta / 120)
                    else: 
                        delta = 1 if event.delta > 0 else -1
                
                if delta == 0:
                    return
                x, y = self.winfo_pointerxy()
                widget_under_mouse = self.winfo_containing(x, y)
                current_scrollable = None
                if widget_under_mouse:
                    if hasattr(widget_under_mouse, 'yview_scroll'):
                        current_scrollable = widget_under_mouse
                    else:
                        parent = widget_under_mouse
                        count = 0 
                        while parent and count < 10:
                            if hasattr(parent, 'yview_scroll'):
                                current_scrollable = parent
                                break
                            if isinstance(parent, ttk.Notebook):
                                break
                            if not hasattr(parent, 'master'):
                                break
                            parent = parent.master
                            count += 1
                if not current_scrollable:
                    current_tab = self.notebook.select()
                    if current_tab:
                        try:
                            tab_frame = self.notebook.nametowidget(current_tab)
                            def find_primary_scrollable(widget):
                                for child in widget.winfo_children():
                                    if hasattr(child, 'yview_scroll') and widget.winfo_ismapped():
                                        return child
                                    found = find_primary_scrollable(child)
                                    if found: return found
                                return None
                            current_scrollable = find_primary_scrollable(tab_frame)
                        except: pass
                if current_scrollable:
                     current_scrollable.yview_scroll(int(-1 * delta), "units")
            except Exception:
                pass

        self.bind_all("<MouseWheel>", _on_mousewheel)
        self.bind_all("<Button-4>", _on_mousewheel)
        self.bind_all("<Button-5>", _on_mousewheel)
        build_news_tab(self, self.notebook)
        build_launcher_log_tab(self, self.notebook)
        build_game_profiles_tab(self, self.notebook)
        try:
            build_modding_tab(self, self.notebook, self.selected_mod_loader)
        except Exception as e:
            print(f"Error building modding tab: {e}")
        try:
            build_oranglib_tab(self, self.notebook)
        except Exception as e:
            print(f"Error building OrangLib tab: {e}")
        try:
            build_servers_tab(self, self.notebook)
        except Exception as e:
            print(f"Error building Servers tab: {e}")
        try:
            build_res_sh_tab(self, self.notebook, get_instance_manager())
        except Exception as e:
            print(f"Error building resource & shader packs tab: {e}")
        build_settings_tab(self, self.notebook)
    def _load_plugins(self):
        if hasattr(self, 'loaded_plugins'):
            for plugin_info in self.loaded_plugins:
                try:
                    mod = plugin_info.get('module') if isinstance(plugin_info, dict) else plugin_info
                    if mod and hasattr(mod, 'deinit_plugin'):
                        mod.deinit_plugin(self)
                except Exception as e:
                    print(f"[Plugins] Error during deinit_plugin: {e}")

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
                    nf = Neoforge()
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
                self.after(0, lambda: messagebox.showerror(self._t("ERROR"), f"Failed to install {loader.title()} {loader_version or ''}: {e}"))
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
    def destroy(self):
        try:
            self._stop_discord_rpc()
        except Exception:
            pass
        try:
            if pygame and getattr(pygame, "mixer", None) and pygame.mixer.get_init():
                try:
                    pygame.mixer.music.stop()
                except Exception:
                    pass
                try:
                    pygame.mixer.quit()
                except Exception:
                    pass
            if pygame:
                pygame.quit()
        except Exception:
            pass
        super().destroy()
        # gd betrayed me :(
        # then you better update ts
    def geometry(self, new_geometry=None):
        if new_geometry is None:
            return super().geometry()
        external_call = not getattr(self, "_geometry_lock", False)
        if getattr(self, "_geometry_debug", False) and external_call:
            try:
                stack = ''.join(tb.format_stack(limit=6))
                print(f"[DEBUG] External geometry request: {new_geometry}\n{stack}")
            except Exception:
                pass
        if external_call:
            self._record_external_geometry(new_geometry)
        return super().geometry(new_geometry)
    def wm_geometry(self, new_geometry=None):
        if new_geometry is None:
            return super().wm_geometry()
        external_call = not getattr(self, "_geometry_lock", False)
        if getattr(self, "_geometry_debug", False) and external_call:
            try:
                stack = ''.join(tb.format_stack(limit=6))
                print(f"[DEBUG] External wm_geometry request: {new_geometry}\n{stack}")
            except Exception:
                pass
        if external_call:
            self._record_external_geometry(new_geometry)
        return super().wm_geometry(new_geometry)
    @staticmethod
    def _geometry_near(a, b, pos_window, size_window):
        if not a or not b:
            return False
        return (
            abs(a[0] - b[0]) <= pos_window
            and abs(a[1] - b[1]) <= pos_window
            and abs(a[2] - b[2]) <= size_window
            and abs(a[3] - b[3]) <= size_window
        )
    def _record_external_geometry(self, geometry_str):
        if not geometry_str or not isinstance(geometry_str, str):
            return
        try:
            width, height, x, y = self._parsegeometry(geometry_str)
            geom = (int(x), int(y), int(width), int(height))
        except Exception:
            return
        self._last_external_geometry = geom
        self._last_external_geometry_time = time.monotonic()
    def _remember_user_geometry(self, geometry, *, skip_if_recent_forced=False):
        if not geometry:
            return
        history = getattr(self, "_user_geometry_history", None)
        if history is None:
            return
        ts = time.monotonic()
        if skip_if_recent_forced and self._initial_restore_done:
            size_window = getattr(self, "_forced_size_window", 8)
            if (
                self._initial_geometry
                and self._geometry_near(geometry, self._initial_geometry, self._forced_return_window, size_window)
                and (ts - self._last_forced_time) < self._history_suppress_seconds
            ):
                return
            if (
                self._last_forced_source
                and self._geometry_near(geometry, self._last_forced_source, self._forced_return_window, size_window)
                and (ts - self._last_forced_time) < self._history_suppress_seconds
            ):
                return
        if history and history[-1][0] == geometry:
            history[-1] = (geometry, ts)
        else:
            history.append((geometry, ts))
        if (
            self._initial_geometry
            and self._geometry_near(geometry, self._initial_geometry, self._forced_return_window, getattr(self, "_forced_size_window", 8))
        ):
            self._last_user_move_time = 0.0
    def _apply_geometry(self, width, height, x=None, y=None):
        try:
            geo = f"{int(width)}x{int(height)}"
            if x is not None and y is not None:
                geo = f"{geo}+{int(x)}+{int(y)}"
            self._geometry_lock = True
            self.geometry(geo)
        except Exception:
            self._geometry_lock = False
    def _on_root_configure(self, event):
        if getattr(event, "widget", None) is not self:
            return
        try:
            current = (
                self.winfo_x(),
                self.winfo_y(),
                self.winfo_width(),
                self.winfo_height(),
            )
        except Exception:
            return
        previous_user_geometry = self._user_geometry
        now = time.monotonic()
        prev_geometry = self._last_geometry
        self._last_geometry = current
        if self._ignore_forced_until and time.monotonic() < self._ignore_forced_until:
            if getattr(self, "_geometry_debug", False):
                print(
                    "[DEBUG] Ignoring configure during forced recovery: current=%s user=%s" % (
                        current,
                        self._user_geometry,
                    )
                )
            if (
                self._user_geometry
                and not self._geometry_lock
            ):
                user_x, user_y, user_w, user_h = self._user_geometry
                size_window = getattr(self, "_forced_size_window", 8)
                if (
                    (
                        abs(current[0] - user_x) > 1
                        or abs(current[1] - user_y) > 1
                    )
                    and abs(current[2] - user_w) <= size_window
                    and abs(current[3] - user_h) <= size_window
                ):
                    self._geometry_lock = True
                    self.geometry(f"{user_w}x{user_h}+{user_x}+{user_y}")
            return
        else:
            self._ignore_forced_until = 0.0
        if self._geometry_lock:
            self._geometry_lock = False
            if self._capture_initial_geometry:
                self._initial_geometry = current
                self._user_geometry = current
                self._capture_initial_geometry = False
                self._remember_user_geometry(current)
                return
            if self._user_geometry is None:
                self._user_geometry = current
                self._remember_user_geometry(current)
            return
        if self._capture_initial_geometry:
            self._initial_geometry = current
            self._user_geometry = current
            self._capture_initial_geometry = False
            if getattr(self, "_geometry_debug", False):
                print(f"[DEBUG] Initial geometry captured: {current}")
            self._remember_user_geometry(current)
            return
        if self._user_geometry is None:
            self._user_geometry = current
            if getattr(self, "_geometry_debug", False):
                print(f"[DEBUG] User geometry initialized: {current}")
            self._remember_user_geometry(current)
            return
        if current == self._user_geometry:
            return
        forced_jump = False
        size_window = getattr(self, "_forced_size_window", 8)
        teleport_jump = False
        position_jump = False
        size_jump = False
        if prev_geometry:
            dx = abs(current[0] - prev_geometry[0])
            dy = abs(current[1] - prev_geometry[1])
            dw = abs(current[2] - prev_geometry[2])
            dh = abs(current[3] - prev_geometry[3])
            position_jump = (
                dx >= self._forced_move_threshold
                or dy >= self._forced_move_threshold
            )
            size_jump = (
                dw >= size_window
                or dh >= size_window
            )
            teleport_jump = (
                dx >= self._forced_teleport_delta
                or dy >= self._forced_teleport_delta
                or dw >= self._forced_teleport_delta
                or dh >= self._forced_teleport_delta
            )
        jump_event = teleport_jump or position_jump or size_jump
        recent_external = (
            getattr(self, "_last_external_geometry", None)
            and self._geometry_near(
                current,
                self._last_external_geometry,
                self._forced_return_window,
                size_window,
            )
            and (now - self._last_external_geometry_time) < getattr(self, "_recent_external_window", 1.0)
        )
        recent_user_move = (
            getattr(self, "_last_user_move_time", 0.0) > 0.0
            and (now - self._last_user_move_time) < getattr(self, "_recent_user_move_window", 1.5)
        )
        if (
            self._user_geometry is not None
            and not self._geometry_lock
        ):
            initial_near = self._geometry_near(current, self._initial_geometry, self._forced_return_window, size_window)
            user_far = (
                abs(current[0] - self._user_geometry[0]) > self._forced_move_threshold
                or abs(current[1] - self._user_geometry[1]) > self._forced_move_threshold
            )
            if (
                self._initial_geometry
                and self._user_geometry != self._initial_geometry
                and initial_near
                and user_far
            ):
                prev_also_near_initial = self._geometry_near(prev_geometry, self._initial_geometry, self._forced_return_window, size_window)
                if prev_also_near_initial or jump_event or recent_external or recent_user_move:
                    forced_jump = True
            elif (
                self._last_forced_source
                and self._geometry_near(current, self._last_forced_source, self._forced_return_window, size_window)
                and user_far
            ):
                prev_also_near_forced = self._geometry_near(prev_geometry, self._last_forced_source, self._forced_return_window, size_window)
                if prev_also_near_forced or jump_event or recent_external or recent_user_move:
                    forced_jump = True
        if forced_jump:
            restore_geom = self._user_geometry
            history = getattr(self, "_user_geometry_history", None)
            if history:
                for recorded_geom, recorded_ts in reversed(history):
                    if not recorded_geom:
                        continue
                    if (
                        self._initial_geometry
                        and self._initial_restore_done
                        and self._geometry_near(recorded_geom, self._initial_geometry, self._forced_return_window, size_window)
                        and (now - recorded_ts) < self._history_suppress_seconds
                    ):
                        continue
                    if (
                        self._geometry_near(recorded_geom, current, self._forced_return_window, size_window)
                        and (now - recorded_ts) < self._history_suppress_seconds
                    ):
                        continue
                    restore_geom = recorded_geom
                    break
            if not restore_geom:
                restore_geom = self._user_geometry or self._initial_geometry or current
            user_x, user_y, user_w, user_h = restore_geom
            target_w = user_w
            target_h = user_h
            if abs(current[2] - user_w) > size_window:
                target_w = current[2]
            if abs(current[3] - user_h) > size_window:
                target_h = current[3]
            x, y, w, h = user_x, user_y, target_w, target_h
            if getattr(self, "_geometry_debug", False):
                print(
                    "[DEBUG] Forced geometry jump detected: current=%s user=%s (prev=%s)" % (
                        current,
                        (user_x, user_y, user_w, user_h),
                        prev_geometry,
                    )
                )
            self._geometry_lock = True
            self.geometry(f"{w}x{h}+{x}+{y}")
            self._ignore_forced_until = now + 0.25
            self._last_forced_source = current
            self._last_forced_time = now
            self._initial_restore_done = True
            self._user_geometry = (x, y, w, h)
            self._remember_user_geometry((x, y, w, h))
            self._last_external_geometry = None
            self._last_external_geometry_time = 0.0
            self._last_user_move_time = now
            return
        if getattr(self, "_geometry_debug", False):
            if (not hasattr(self, '_last_logged_geo') or
                abs(current[0] - self._last_logged_geo[0]) > 10 or
                abs(current[1] - self._last_logged_geo[1]) > 10 or
                abs(current[2] - self._last_logged_geo[2]) > 20 or
                abs(current[3] - self._last_logged_geo[3]) > 20):
                print(
                    "[DEBUG] Configure change detected: current=%s user=%s initial=%s" % (
                        current,
                        self._user_geometry,
                        self._initial_geometry,
                    )
                )
                self._last_logged_geo = current
        if (
            self._initial_geometry
            and not self._initial_restore_done
            and current == self._initial_geometry
            and self._user_geometry != self._initial_geometry
        ):
            x, y, w, h = self._user_geometry
            self._geometry_lock = True
            self.geometry(f"{w}x{h}+{x}+{y}")
            self._initial_restore_done = True
            if getattr(self, "_geometry_debug", False):
                print(
                    "[DEBUG] Restoring user geometry: current=%s -> user=%s" % (
                        current,
                        self._user_geometry,
                    )
                )
            return
        self._user_geometry = current
        self._remember_user_geometry(current, skip_if_recent_forced=True)
        if (
            self._initial_geometry
            and not self._geometry_near(current, self._initial_geometry, self._forced_return_window, size_window)
            and not self._geometry_lock
        ):
            self._last_user_move_time = now
        if previous_user_geometry and previous_user_geometry != current:
            self._initial_restore_done = True
        if self._last_forced_source and (
            abs(current[0] - self._last_forced_source[0]) > self._forced_return_window
            or abs(current[1] - self._last_forced_source[1]) > self._forced_return_window
            or abs(current[2] - self._last_forced_source[2]) > size_window
            or abs(current[3] - self._last_forced_source[3]) > size_window
        ):
            self._last_forced_source = None
        if getattr(self, "_geometry_debug", False):
            print(f"[DEBUG] User geometry updated: {self._user_geometry}")
    def _on_game_profile_selected(self, event=None):
        selection = self.selected_game_profile.get()
        if not selection:
            return
        instance = self.instance_manager.get_instance_by_name(selection)
        if instance:
            self.instance_manager.set_selected_instance(instance.instance_id)
        else:
            pass
        self._update_profile_display()

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
        options = {
            'username': username,
            'uuid': uuid,
            'token': access_token,
            'executablePath': java_exe,
            'jvmArguments': [f"-Xmx{ram}", f"-Xms{ram}"]
        }
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
        command = minecraft_launcher_lib.command.get_minecraft_command(version, minecraft_directory, options)
        command = [arg for arg in command if arg != "--sun-misc-unsafe-memory-access=allow"]
        launch_env = os.environ.copy()
        if getattr(instance, 'env_vars', None):
            for line in instance.env_vars.splitlines():
                if '=' in line and not line.strip().startswith('#'):
                    k, _, v = line.partition('=')
                    launch_env[k.strip()] = v.strip()
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


def main():
    try:
        if "--terminal" in sys.argv:
            terminal_main()
            return
        if "--testing" in sys.argv:
            mark_setup_done(False)
            print("[testing] reset setup.mark → setup_done=false")
        open_file_arg = None
        for arg in sys.argv[1:]:
            if not arg.startswith('-') and (arg.endswith('.mrpack') or arg.endswith('.zip')):
                open_file_arg = arg
                break
        if platform.system() == "Linux":
            if 'GDK_BACKEND' not in os.environ:
                os.environ['GDK_BACKEND'] = 'x11'
            if 'SDL_VIDEODRIVER' not in os.environ:
                os.environ['SDL_VIDEODRIVER'] = 'x11'
            try:
                subprocess.run(["fc-cache", "-f"], check=False, capture_output=True)
            except:
                pass
        app = MinecraftLauncher()
        if open_file_arg:
            app._pending_open_file = open_file_arg
        app.mainloop()
    except KeyboardInterrupt:
        print("\\cya :>")
        sys.exit(0)
if __name__ == "__main__":
    main()
