"""
NATIVE PLUGIN EXAMPLE FOR ORANGLAUNCHER
========================================
This plugin demonstrates the native plugin system for OrangLauncher.

PLUGIN METADATA:
- Type: Native
- Version: 1.0.0
- Purpose: Demonstrate native plugin capabilities

ALLOWED CAPABILITIES:
 Add custom tabs/widgets to launcher UI
 Hook into launcher events
 Access launcher data (read-only)
 Modify launcher behavior
 Create notifications

NOTE: Plugins run directly in the launcher process.
"""

import tkinter as tk
from tkinter import ttk, messagebox, messagebox
import traceback


# ============================================================================
# PLUGIN METADATA - Required for plugin system discovery
# ============================================================================
__plugin_name__ = "Example Plugin"
__plugin_version__ = "1.0.0"
__plugin_description__ = "Example plugin showcasing native plugin capabilities"
__plugin_author__ = "adasjusk"
_launcher = None
_plugin_context = {}

def init_plugin(launcher):
    global _launcher, _plugin_context
    _launcher = launcher
    print(f"[{__plugin_name__}] Initializing...")
    print(f"[{__plugin_name__}] Plugin Type: native (built-in)")
    print(f"[{__plugin_name__}] Version: {__plugin_version__}")
    _plugin_context['initialized'] = True
    _plugin_context['launch_count'] = 0
    try:
        add_custom_tab(launcher)
        add_custom_button(launcher)
        hook_game_events(launcher)
        hook_tab_change(launcher)
        print(f"[{__plugin_name__}] Initialized successfully!")
        print(f"[{__plugin_name__}] Launcher Version: {launcher.title()}")
        print(f"[{__plugin_name__}] Current Theme: {launcher.theme_manager.current_theme}")
    except Exception as e:
        print(f"[{__plugin_name__}] Error during initialization: {e}")
        traceback.print_exc()


def deinit_plugin(launcher):
    global _plugin_context
    print(f"[{__plugin_name__}] Cleaning up...")
    print(f"[{__plugin_name__}] Total game launches this session: {_plugin_context.get('launch_count', 0)}")
    _plugin_context.clear()

def add_custom_tab(launcher):
    
    try:
        custom_tab = ttk.Frame(launcher.notebook)
        launcher.notebook.add(custom_tab, text="Plugin Example tab")
        # create main
        container = tk.Frame(custom_tab, bg=launcher._get_theme_color('bg_primary'))
        container.pack(fill="both", expand=True, padx=20, pady=20)
        title = tk.Label(
            container,
            text="Native Plugin Demo",
            font=("Segoe UI", 18, "bold"),
            bg=launcher._get_theme_color('bg_primary'),
            fg=launcher._get_theme_color('fg_primary')
        )
        title.pack(pady=(0, 5))
        # subtitle
        subtitle = tk.Label(
            container,
            text="This tab was created by a native plugin",
            font=("Segoe UI", 11),
            bg=launcher._get_theme_color('bg_primary'),
            fg=launcher._get_theme_color('fg_secondary')
        )
        subtitle.pack(pady=(0, 20))
        # info card
        card = tk.Frame(
            container,
            bg=launcher._get_theme_color('bg_tertiary'),
            relief="flat",
            bd=1,
            highlightthickness=1,
            highlightcolor=launcher._get_theme_color('border_primary'),
            highlightbackground=launcher._get_theme_color('border_primary')
        )
        card.pack(fill="both", expand=True, pady=10)
        
        card_content = tk.Frame(card, bg=launcher._get_theme_color('bg_tertiary'))
        card_content.pack(fill="both", expand=True, padx=20, pady=15)
        info_label = tk.Label(
            card_content,
            text="Plugin Information:",
            font=("Segoe UI", 12, "bold"),
            bg=launcher._get_theme_color('bg_tertiary'),
            fg=launcher._get_theme_color('fg_primary')
        )
        info_label.pack(anchor="w", pady=(0, 10))
        
        info_text = f"Name: {__plugin_name__}\nVersion: {__plugin_version__}\nType: Native\nAuthor: {__plugin_author__}"
        info_display = tk.Label(
            card_content,
            text=info_text,
            font=("Segoe UI", 10),
            bg=launcher._get_theme_color('bg_tertiary'),
            fg=launcher._get_theme_color('fg_secondary'),
            justify="left"
        )
        info_display.pack(anchor="w", pady=(0, 15))
        # game profiles section
        profiles_label = tk.Label(
            card_content,
            text="Available Game Instances:",
            font=("Segoe UI", 11, "bold"),
            bg=launcher._get_theme_color('bg_tertiary'),
            fg=launcher._get_theme_color('fg_primary')
        )
        profiles_label.pack(anchor="w", pady=(0, 5))
        try:
            instances = launcher.instance_manager.get_instance_names()
            for instance_name in instances[:5]:
                instance_label = tk.Label(
                    card_content,
                    text=f"  • {instance_name}",
                    font=("Segoe UI", 9),
                    bg=launcher._get_theme_color('bg_tertiary'),
                    fg=launcher._get_theme_color('fg_secondary')
                )
                instance_label.pack(anchor="w")
            if len(instances) > 5:
                more_label = tk.Label(
                    card_content,
                    text=f"  ... and {len(instances) - 5} more",
                    font=("Segoe UI", 9, "italic"),
                    bg=launcher._get_theme_color('bg_tertiary'),
                    fg=launcher._get_theme_color('fg_secondary')
                )
                more_label.pack(anchor="w")
        except Exception as e:
            error_label = tk.Label(
                card_content,
                text=f"  Error: {e}",
                font=("Segoe UI", 9),
                bg=launcher._get_theme_color('bg_tertiary'),
                fg="red"
            )
            error_label.pack(anchor="w")
        # action button
        def on_plugin_action():
            selected = launcher.selected_profile.get()
            game_profile = launcher.selected_game_profile.get()
            messagebox.showinfo(
                "Plugin Action",
                f"Profile: {selected}\nGame Profile: {game_profile}\n\nNative plugin is working!"
            )
        action_btn = tk.Button(
            card_content,
            text="Test Plugin Action",
            command=on_plugin_action,
            bg=launcher._get_theme_color('accent_primary'),
            fg=launcher._get_theme_color('fg_primary'),
            font=("Segoe UI", 10, "bold"),
            bd=0,
            padx=20,
            pady=10,
            cursor="hand2",
            relief="flat"
        )
        action_btn.pack(pady=(15, 0))
        print(f"[{__plugin_name__}] Added custom tab to launcher UI")
    except Exception as e:
        print(f"[{__plugin_name__}] Error adding custom tab: {e}")
        traceback.print_exc()
def add_custom_button(launcher):
    def plugin_action():
        current_profile = launcher.selected_profile.get()
        current_game_profile = launcher.selected_game_profile.get()
        info = f"Account: {current_profile}\n"
        info += f"Game Profile: {current_game_profile}\n"
        info += f"Theme: {launcher.theme_manager.current_theme}\n"
        info += f"Language: {launcher.current_locale}"
        messagebox.showinfo("Plugin Info", info)
    try:
        if hasattr(launcher, 'play_btn') and launcher.play_btn:
            parent = launcher.play_btn.master
            plugin_btn = ttk.Button(
                parent,
                text="Plugin",
                command=plugin_action,
                width=14
            )
            plugin_btn.grid(row=3, column=0, pady=(8, 0))
            print(f"[{__plugin_name__}] Added custom button to main UI")
    except Exception as e:
        print(f"[{__plugin_name__}] Error adding custom button: {e}")
def hook_game_events(launcher):
    global _plugin_context
    original_launch = launcher._launch_game
    original_restore = launcher._restore_ui
    
    def hooked_launch():
        _plugin_context['launch_count'] += 1
        selected = launcher.selected_profile.get()
        game_profile = launcher.selected_game_profile.get()
        print(f"[{__plugin_name__}] Game launching!")
        print(f"[{__plugin_name__}]   Account: {selected}")
        print(f"[{__plugin_name__}]   Game Profile: {game_profile}")
        print(f"[{__plugin_name__}]   Total launches: {_plugin_context['launch_count']}")
        # call original method
        original_launch()
    
    def hooked_restore():
        print(f"[{__plugin_name__}] Game closed!")
        print(f"[{__plugin_name__}]   Total launches this session: {_plugin_context['launch_count']}")
        original_restore()
    # replace methods
    launcher._launch_game = hooked_launch
    launcher._restore_ui = hooked_restore
    print(f"[{__plugin_name__}] Hooked into game launch/close events")


def hook_tab_change(launcher):
    original_tab_change = launcher._on_tab_changed
    def custom_tab_change(event=None):
        try:
            tab_index = launcher.notebook.index(launcher.notebook.select())
            tab_text = launcher.notebook.tab(tab_index, "text")
            print(f"[{__plugin_name__}] User switched to tab: {tab_text}")
        except Exception:
            pass
        original_tab_change(event)
    launcher._on_tab_changed = custom_tab_change
    print(f"[{__plugin_name__}] Hooked into tab change events")
def get_launcher_info(launcher):
    info = {}
    try:
        info['current_profile'] = launcher.selected_profile.get()
        info['game_profile'] = launcher.selected_game_profile.get()
        info['theme'] = launcher.theme_manager.current_theme
        info['locale'] = launcher.current_locale
        
        instance = launcher.instance_manager.get_selected_instance()
        if instance:
            info['version'] = instance.version
            info['mod_loader'] = instance.mod_loader
            info['ram'] = instance.ram
    except Exception as e:
        print(f"[{__plugin_name__}] Error getting launcher info: {e}")
    return info