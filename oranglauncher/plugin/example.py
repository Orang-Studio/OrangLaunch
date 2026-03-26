"""
PLUGIN EXAMPLE FOR ORANGLAUNCHER
==============================================
This plugin demonstrates capabilities available to OrangLauncher plugins.
Plugins are heavily sandboxed for security.

ALLOWED CAPABILITIES:
✓ Add widgets to existing launcher frames/tabs
✓ React to launcher events (start, game launch, game close)
✓ Access game profiles and instances (read-only, no tokens)
✓ Read launcher settings and themes
✓ Create notifications via messagebox (uses launcher's window)
✓ Read files from anywhere
✓ Write files within plugin directory only

NOTE: Plugins cannot create popup windows to prevent phishing attacks.
      Use the provided notebook to add custom tabs instead.
"""
def init_plugin(launcher):
    print("[Plugin] Initializing...")
    global _launcher
    _launcher = launcher
    print(f"[Plugin] Launcher title: {launcher.title()}")
    print(f"[Plugin] Current theme: {launcher.theme_manager.current_theme}")
    print(f"[Plugin] Current locale: {launcher.current_locale}")
    add_custom_tab(launcher)
    enhance_existing_tabs(launcher)
    add_custom_button(launcher)
    hook_game_events(launcher)
    print("[Plugin] initialized successfully!")
def deinit_plugin(launcher):
    print("[Plugin] cleaning up...")
def add_custom_tab(launcher):
    import tkinter as tk
    from tkinter import ttk
    custom_tab = ttk.Frame(launcher.notebook)
    launcher.notebook.add(custom_tab, text="My Plugin")
    container = tk.Frame(custom_tab, bg=launcher._get_theme_color('bg_primary'))
    container.pack(fill="both", expand=True, padx=20, pady=20)
    # Title
    title = tk.Label(
        container,
        text="Plugin tab",
        font=("Segoe UI", 18, "bold"),
        bg=launcher._get_theme_color('bg_primary'),
        fg=launcher._get_theme_color('fg_primary')
    )
    title.pack(pady=(0, 10))
    # Description
    desc = tk.Label(
        container,
        text="this tab was created by a plugin! Plugins can add any custom functionality.",
        font=("Segoe UI", 10),
        bg=launcher._get_theme_color('bg_primary'),
        fg=launcher._get_theme_color('fg_secondary'),
        wraplength=500
    )
    desc.pack(pady=(0, 20))
    # Card with information
    card = tk.Frame(
        container,
        bg=launcher._get_theme_color('bg_tertiary'),
        relief="flat",
        bd=1,
        highlightthickness=1,
        highlightcolor=launcher._get_theme_color('border_primary'),
        highlightbackground=launcher._get_theme_color('border_primary')
    )
    card.pack(fill="x", pady=10)
    card_content = tk.Frame(card, bg=launcher._get_theme_color('bg_tertiary'))
    card_content.pack(fill="x", padx=20, pady=15)
    # Show game profiles
    profiles_label = tk.Label(
        card_content,
        text="Available Game Profiles:",
        font=("Segoe UI", 11, "bold"),
        bg=launcher._get_theme_color('bg_tertiary'),
        fg=launcher._get_theme_color('fg_primary')
    )
    profiles_label.pack(anchor="w", pady=(0, 5))
    try:
        instance_names = launcher.instance_manager.get_instance_names()
        for name in instance_names[:5]:
            profile_label = tk.Label(
                card_content,
                text=f"  • {name}",
                font=("Segoe UI", 9),
                bg=launcher._get_theme_color('bg_tertiary'),
                fg=launcher._get_theme_color('fg_secondary')
            )
            profile_label.pack(anchor="w")
    except Exception as e:
        print(f"[Plugin] Error listing profiles: {e}")
    def on_button_click():
        from tkinter import messagebox
        selected = launcher.selected_profile.get()
        messagebox.showinfo(
            "Plugin Action",
            f"Current profile: {selected}\n\nThis is a custom plugin action!"
        )
    button = tk.Button(
        card_content,
        text="Test Plugin Action",
        command=on_button_click,
        bg=launcher._get_theme_color('accent_primary'),
        fg=launcher._get_theme_color('fg_primary'),
        font=("Segoe UI", 10, "bold"),
        bd=0,
        padx=20,
        pady=10,
        cursor="hand2",
        relief="flat"
    )
    button.pack(pady=(15, 0))
def enhance_existing_tabs(launcher):
    import tkinter as tk
    try:
        if hasattr(launcher, 'log_text'):
            log_parent = launcher.log_text.master
            custom_label = tk.Label(
                log_parent,
                text="📝 Enhanced by Custom Plugin",
                font=("Segoe UI", 8, "italic"),
                bg=launcher._get_theme_color('bg_secondary'),
                fg=launcher._get_theme_color('accent_primary')
            )
            custom_label.pack(side="bottom", pady=2)
            print("[Plugin] Enhanced launcher log tab")
    except Exception as e:
        print(f"[Plugin] Could not enhance log tab: {e}")
def add_custom_button(launcher):
    import tkinter as tk
    from tkinter import ttk, messagebox
    def custom_action():
        current_profile = launcher.selected_profile.get()
        current_game_profile = launcher.selected_game_profile.get()
        info = f"Current Account: {current_profile}\n"
        info += f"Current Game Profile: {current_game_profile}\n"
        info += f"Theme: {launcher.theme_manager.current_theme}\n"
        info += f"Language: {launcher.current_locale}"
        messagebox.showinfo("Plugin Info", info)
    try:
        if hasattr(launcher, 'play_btn') and launcher.play_btn:
            parent = launcher.play_btn.master
            plugin_btn = ttk.Button(
                parent,
                text="Plugin Action",
                command=custom_action,
                style="Play.TButton",
                width=14
            )
            plugin_btn.grid(row=3, column=0, pady=(8, 0))
            print("[Plugin] Added custom button to main UI")
    except Exception as e:
        print(f"[Plugin] Could not add custom button: {e}")
def hook_game_events(launcher):
    original_launch = launcher._launch_game
    original_restore = launcher._restore_ui
    launch_count = [0]  # Using list to maintain reference
    def hooked_launch():
        launch_count[0] += 1
        print(f"[Plugin] Game launching! Total launches this session: {launch_count[0]}")
        selected = launcher.selected_profile.get()
        game_profile = launcher.selected_game_profile.get()
        print(f"[Plugin] Launching with profile: {selected}")
        print(f"[Plugin] Game profile: {game_profile}")
        # Call original method
        original_launch()
    def hooked_restore():
        print(f"[Plugin] Game closed!")
        print(f"[Plugin] Total launches this session: {launch_count[0]}")
        # Call original method
        original_restore()
    # Replace methods with hooked versions
    launcher._launch_game = hooked_launch
    launcher._restore_ui = hooked_restore
    print("[Plugin] Hooked into game launch events")
# ============================================================================
# EXAMPLE 5: ACCESSING LAUNCHER DATA (READ-ONLY, SAFE)
# ============================================================================
def get_safe_profile_info(launcher):
    safe_data = {}
    try:
        current_profile = launcher.selected_profile.get()
        safe_data['current_profile'] = current_profile
        current_game_profile = launcher.selected_game_profile.get()
        safe_data['game_profile'] = current_game_profile
        instance = launcher.instance_manager.get_selected_instance()
        if instance:
            safe_data['version'] = instance.version
            safe_data['mod_loader'] = instance.mod_loader
            safe_data['ram'] = instance.ram
        safe_data['theme'] = launcher.theme_manager.current_theme
        safe_data['locale'] = launcher.current_locale
    except Exception as e:
        print(f"[Plugin] Error getting safe profile info: {e}")
    return safe_data
# ============================================================================
# EXAMPLE: CREATING CUSTOM DIALOGS
# ============================================================================
def create_custom_dialog(launcher):
    import tkinter as tk
    from tkinter import ttk
    dialog = tk.Toplevel(launcher)
    dialog.title("Plugin Dialog")
    dialog.geometry("400x300")
    dialog.configure(bg=launcher._get_theme_color('bg_primary'))
    dialog.transient(launcher)
    dialog.grab_set()
    label = tk.Label(
        dialog,
        text="This is a custom dialog created by a plugin!",
        font=("Segoe UI", 12),
        bg=launcher._get_theme_color('bg_primary'),
        fg=launcher._get_theme_color('fg_primary')
    )
    label.pack(pady=20)
    close_btn = tk.Button(
        dialog,
        text="Close",
        command=dialog.destroy,
        bg=launcher._get_theme_color('accent_primary'),
        fg=launcher._get_theme_color('fg_primary'),
        font=("Segoe UI", 10, "bold"),
        bd=0,
        padx=20,
        pady=10,
        cursor="hand2"
    )
    close_btn.pack(pady=10)
    return dialog
# ============================================================================
# EXAMPLE: MODIFYING LAUNCHER BEHAVIOR
# ============================================================================
def add_custom_behavior(launcher):
    original_tab_change = launcher._on_tab_changed
    def custom_tab_change(event=None):
        try:
            tab_index = launcher.notebook.index(launcher.notebook.select())
            tab_text = launcher.notebook.tab(tab_index, "text")
            print(f"[Plugin] User switched to tab: {tab_text}")
        except:
            pass
        original_tab_change(event)
    launcher._on_tab_changed = custom_tab_change
    print("[Plugin] Added custom tab change behavior")
# ============================================================================
# PLUGIN METADATA
# ============================================================================
__plugin_name__ = "Example Plugin"
__plugin_version__ = "0.0.1"
__plugin_author__ = "InterJava's Studio"
__plugin_description__ = """
Demonstrates all plugin capabilities:
- Creating custom tabs
- Modifying existing UI
- Hooking into events
- Accessing launcher data safely
- Creating custom dialogs
- Modifying launcher behavior
"""
print("[Plugin] Module loaded. Ready for init_plugin() call.")
