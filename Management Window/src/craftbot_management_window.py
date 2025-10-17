#!/usr/bin/env python3
"""
Craftbot Management Window
A comprehensive GUI for managing Craftbot recipes, commands, ranks, and logs
"""

import tkinter as tk
from tkinter import ttk, messagebox, scrolledtext, simpledialog
import json
from pathlib import Path

class CraftbotManagementWindow:
    def __init__(self, root):
        self.root = root
        self.root.title("Craftbot Management Window")
        self.root.geometry("1200x700")

        # Set up paths - find the Control Panel directory
        script_file = Path(__file__)

        # Check if we're in bin/Debug/Control Panel (copied location)
        if script_file.parent.name == "Control Panel":
            control_panel = script_file.parent
        else:
            # We're in Management Window/src (original location)
            script_dir = script_file.parent.parent.parent  # Go up to Craftbot root
            control_panel = script_dir / "bin" / "Debug" / "Control Panel"

        self.config_path = control_panel / "config"
        self.logs_path = control_panel / "logs"
        self.ranks_path = self.config_path / "ranks"
        self.help_templates_path = self.config_path / "help-templates"

        # Ensure directories exist
        self.ranks_path.mkdir(parents=True, exist_ok=True)
        self.help_templates_path.mkdir(parents=True, exist_ok=True)

        # Create default ranks if they don't exist
        self.create_default_ranks()

        # Create notebook (tabs)
        self.notebook = ttk.Notebook(root)
        self.notebook.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)

        # Create tabs
        self.recipes_tab = ttk.Frame(self.notebook)
        self.help_menu_tab = ttk.Frame(self.notebook)
        self.commands_tab = ttk.Frame(self.notebook)
        self.ranks_tab = ttk.Frame(self.notebook)
        self.logs_tab = ttk.Frame(self.notebook)

        self.notebook.add(self.recipes_tab, text="Recipes")
        self.notebook.add(self.help_menu_tab, text="Help Menu")
        self.notebook.add(self.commands_tab, text="Commands")
        self.notebook.add(self.ranks_tab, text="Ranks")
        self.notebook.add(self.logs_tab, text="Logs")

        # Initialize tabs
        self.setup_recipes_tab()
        self.setup_help_menu_tab()
        self.setup_commands_tab()
        self.setup_ranks_tab()
        self.setup_logs_tab()
    
    def create_default_ranks(self):
        """Create default rank files if they don't exist"""
        default_ranks = ["Admin", "Moderator", "VIP", "User"]
        for rank in default_ranks:
            rank_file = self.ranks_path / f"{rank}.json"
            if not rank_file.exists():
                rank_file.write_text(json.dumps({"rank": rank, "players": []}, indent=2))
    
    def setup_recipes_tab(self):
        """Setup the Recipes tab with dual columns"""
        # Left column - Recipe list
        left_frame = ttk.Frame(self.recipes_tab)
        left_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=False, padx=5, pady=5)
        
        ttk.Label(left_frame, text="Recipes", font=("Arial", 12, "bold")).pack()
        
        self.recipes_listbox = tk.Listbox(left_frame, width=25, height=30)
        self.recipes_listbox.pack(fill=tk.BOTH, expand=True)
        self.recipes_listbox.bind('<<ListboxSelect>>', self.on_recipe_select)
        
        # Load recipes
        self.load_recipes_list()
        
        # Right column - Recipe details
        right_frame = ttk.Frame(self.recipes_tab)
        right_frame.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        ttk.Label(right_frame, text="Recipe Details", font=("Arial", 12, "bold")).pack()
        
        self.recipe_text = scrolledtext.ScrolledText(right_frame, wrap=tk.WORD, height=30)
        self.recipe_text.pack(fill=tk.BOTH, expand=True)
        
        # Save button
        ttk.Button(right_frame, text="Save Recipe", command=self.save_recipe).pack(pady=5)
    
    def load_recipes_list(self):
        """Load recipes from config/recipes directory"""
        self.recipes_listbox.delete(0, tk.END)
        recipes_dir = self.config_path / "recipes"
        if recipes_dir.exists():
            for recipe_file in sorted(recipes_dir.glob("*.json")):
                if recipe_file.name != "_template.json":
                    self.recipes_listbox.insert(tk.END, recipe_file.stem)
    
    def on_recipe_select(self, event):
        """Load selected recipe for editing"""
        selection = self.recipes_listbox.curselection()
        if selection:
            recipe_name = self.recipes_listbox.get(selection[0])
            recipe_file = self.config_path / "recipes" / f"{recipe_name}.json"
            if recipe_file.exists():
                content = recipe_file.read_text()
                self.recipe_text.delete(1.0, tk.END)
                self.recipe_text.insert(1.0, content)
    
    def save_recipe(self):
        """Save edited recipe"""
        selection = self.recipes_listbox.curselection()
        if not selection:
            messagebox.showwarning("Warning", "Please select a recipe first")
            return
        
        recipe_name = self.recipes_listbox.get(selection[0])
        recipe_file = self.config_path / "recipes" / f"{recipe_name}.json"
        
        try:
            content = self.recipe_text.get(1.0, tk.END)
            json.loads(content)  # Validate JSON
            recipe_file.write_text(content)
            messagebox.showinfo("Success", f"Recipe '{recipe_name}' saved successfully!")
        except json.JSONDecodeError as e:
            messagebox.showerror("Error", f"Invalid JSON: {e}")

    def setup_help_menu_tab(self):
        """Setup the Help Menu tab with dual columns"""
        # Left column - Help template list
        left_frame = ttk.Frame(self.help_menu_tab)
        left_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=False, padx=5, pady=5)

        ttk.Label(left_frame, text="Help Templates", font=("Arial", 12, "bold")).pack()

        self.help_templates_listbox = tk.Listbox(left_frame, width=25, height=30)
        self.help_templates_listbox.pack(fill=tk.BOTH, expand=True)
        self.help_templates_listbox.bind('<<ListboxSelect>>', self.on_help_template_select)

        # Load help templates
        self.load_help_templates_list()

        # Right column - Help template details
        right_frame = ttk.Frame(self.help_menu_tab)
        right_frame.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=5, pady=5)

        ttk.Label(right_frame, text="Template Content", font=("Arial", 12, "bold")).pack()

        self.help_template_text = scrolledtext.ScrolledText(right_frame, wrap=tk.WORD, height=30)
        self.help_template_text.pack(fill=tk.BOTH, expand=True)

        # Save button
        ttk.Button(right_frame, text="Save Template", command=self.save_help_template).pack(pady=5)

    def load_help_templates_list(self):
        """Load help templates from config/help-templates directory"""
        self.help_templates_listbox.delete(0, tk.END)
        if self.help_templates_path.exists():
            for template_file in sorted(self.help_templates_path.glob("*")):
                if template_file.is_file():
                    self.help_templates_listbox.insert(tk.END, template_file.name)

    def on_help_template_select(self, event):
        """Load selected help template for editing"""
        selection = self.help_templates_listbox.curselection()
        if selection:
            template_name = self.help_templates_listbox.get(selection[0])
            template_file = self.help_templates_path / template_name
            if template_file.exists():
                content = template_file.read_text()
                self.help_template_text.delete(1.0, tk.END)
                self.help_template_text.insert(1.0, content)

    def save_help_template(self):
        """Save edited help template"""
        selection = self.help_templates_listbox.curselection()
        if not selection:
            messagebox.showwarning("Warning", "Please select a template first")
            return

        template_name = self.help_templates_listbox.get(selection[0])
        template_file = self.help_templates_path / template_name

        try:
            content = self.help_template_text.get(1.0, tk.END)
            template_file.write_text(content)
            messagebox.showinfo("Success", f"Template '{template_name}' saved successfully!")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to save template: {e}")

    def setup_commands_tab(self):
        """Setup the Commands tab with dual columns"""
        # Left column - Command list
        left_frame = ttk.Frame(self.commands_tab)
        left_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=False, padx=5, pady=5)
        
        ttk.Label(left_frame, text="Commands", font=("Arial", 12, "bold")).pack()
        
        self.commands_listbox = tk.Listbox(left_frame, width=25, height=30)
        self.commands_listbox.pack(fill=tk.BOTH, expand=True)
        self.commands_listbox.bind('<<ListboxSelect>>', self.on_command_select)
        
        # Load commands
        self.load_commands_list()
        
        # Right column - Command details
        right_frame = ttk.Frame(self.commands_tab)
        right_frame.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        ttk.Label(right_frame, text="Command Details", font=("Arial", 12, "bold")).pack()
        
        self.command_text = scrolledtext.ScrolledText(right_frame, wrap=tk.WORD, height=30)
        self.command_text.pack(fill=tk.BOTH, expand=True)
        
        # Save button
        ttk.Button(right_frame, text="Save Command", command=self.save_command).pack(pady=5)
    
    def load_commands_list(self):
        """Load commands from config/commands.json"""
        self.commands_listbox.delete(0, tk.END)
        commands_file = self.config_path / "commands.json"
        if commands_file.exists():
            try:
                data = json.loads(commands_file.read_text())
                if "Commands" in data:
                    # Sort commands alphabetically by name
                    sorted_commands = sorted(data["Commands"], key=lambda x: x.get("Name", "").lower())
                    for cmd in sorted_commands:
                        self.commands_listbox.insert(tk.END, cmd.get("Name", "Unknown"))
            except:
                pass
    
    def on_command_select(self, event):
        """Load selected command for viewing"""
        selection = self.commands_listbox.curselection()
        if selection:
            cmd_name = self.commands_listbox.get(selection[0])
            commands_file = self.config_path / "commands.json"
            if commands_file.exists():
                try:
                    data = json.loads(commands_file.read_text())
                    for cmd in data.get("Commands", []):
                        if cmd.get("Name") == cmd_name:
                            self.command_text.delete(1.0, tk.END)
                            self.command_text.insert(1.0, json.dumps(cmd, indent=2))
                            break
                except:
                    pass
    
    def save_command(self):
        """Save edited command"""
        selection = self.commands_listbox.curselection()
        if not selection:
            messagebox.showwarning("Warning", "Please select a command first")
            return
        
        try:
            content = self.command_text.get(1.0, tk.END)
            updated_cmd = json.loads(content)
            
            commands_file = self.config_path / "commands.json"
            data = json.loads(commands_file.read_text())
            
            for i, cmd in enumerate(data.get("Commands", [])):
                if cmd.get("Name") == updated_cmd.get("Name"):
                    data["Commands"][i] = updated_cmd
                    break
            
            commands_file.write_text(json.dumps(data, indent=2))
            messagebox.showinfo("Success", "Command saved successfully!")
        except json.JSONDecodeError as e:
            messagebox.showerror("Error", f"Invalid JSON: {e}")
    
    def setup_ranks_tab(self):
        """Setup the Ranks tab with nested tabs"""
        self.ranks_notebook = ttk.Notebook(self.ranks_tab)
        self.ranks_notebook.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Load rank tabs
        self.load_rank_tabs()
        
        # Add/Remove rank buttons
        button_frame = ttk.Frame(self.ranks_tab)
        button_frame.pack(fill=tk.X, padx=5, pady=5)
        ttk.Button(button_frame, text="Add Rank", command=self.add_rank).pack(side=tk.LEFT, padx=2)
        ttk.Button(button_frame, text="Remove Rank", command=self.remove_rank).pack(side=tk.LEFT, padx=2)
    
    def load_rank_tabs(self):
        """Load rank tabs from rank files"""
        for tab in self.ranks_notebook.tabs():
            self.ranks_notebook.forget(tab)
        
        if self.ranks_path.exists():
            for rank_file in sorted(self.ranks_path.glob("*.json")):
                rank_name = rank_file.stem
                frame = self.create_rank_frame(rank_name)
                self.ranks_notebook.add(frame, text=rank_name)
    
    def create_rank_frame(self, rank_name):
        """Create a frame for a specific rank"""
        frame = ttk.Frame(self.ranks_notebook)
        
        # Players listbox
        ttk.Label(frame, text=f"Players in {rank_name}", font=("Arial", 10, "bold")).pack()
        
        players_frame = ttk.Frame(frame)
        players_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        players_listbox = tk.Listbox(players_frame, height=20)
        players_listbox.pack(fill=tk.BOTH, expand=True, side=tk.LEFT)
        
        scrollbar = ttk.Scrollbar(players_frame, orient=tk.VERTICAL, command=players_listbox.yview)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        players_listbox.config(yscrollcommand=scrollbar.set)
        
        # Load players
        rank_file = self.ranks_path / f"{rank_name}.json"
        if rank_file.exists():
            data = json.loads(rank_file.read_text())
            for player in data.get("players", []):
                players_listbox.insert(tk.END, player)
        
        # Buttons
        button_frame = ttk.Frame(frame)
        button_frame.pack(fill=tk.X, padx=5, pady=5)
        
        ttk.Button(button_frame, text="Add Player", 
                  command=lambda: self.add_player_to_rank(rank_name, players_listbox)).pack(side=tk.LEFT, padx=2)
        ttk.Button(button_frame, text="Remove Player", 
                  command=lambda: self.remove_player_from_rank(rank_name, players_listbox)).pack(side=tk.LEFT, padx=2)
        
        return frame
    
    def add_player_to_rank(self, rank_name, listbox):
        """Add a player to a rank"""
        player_name = simpledialog.askstring("Add Player", f"Enter player name for {rank_name}:")
        if player_name:
            rank_file = self.ranks_path / f"{rank_name}.json"
            data = json.loads(rank_file.read_text())
            if player_name not in data["players"]:
                data["players"].append(player_name)
                rank_file.write_text(json.dumps(data, indent=2))
                listbox.insert(tk.END, player_name)
                messagebox.showinfo("Success", f"Added {player_name} to {rank_name}")
            else:
                messagebox.showwarning("Warning", f"{player_name} is already in {rank_name}")
    
    def remove_player_from_rank(self, rank_name, listbox):
        """Remove a player from a rank"""
        selection = listbox.curselection()
        if selection:
            player_name = listbox.get(selection[0])
            rank_file = self.ranks_path / f"{rank_name}.json"
            data = json.loads(rank_file.read_text())
            if player_name in data["players"]:
                data["players"].remove(player_name)
                rank_file.write_text(json.dumps(data, indent=2))
                listbox.delete(selection[0])
                messagebox.showinfo("Success", f"Removed {player_name} from {rank_name}")
    
    def add_rank(self):
        """Add a new rank"""
        rank_name = simpledialog.askstring("Add Rank", "Enter new rank name:")
        if rank_name:
            rank_file = self.ranks_path / f"{rank_name}.json"
            if not rank_file.exists():
                rank_file.write_text(json.dumps({"rank": rank_name, "players": []}, indent=2))
                self.load_rank_tabs()
                messagebox.showinfo("Success", f"Rank '{rank_name}' created")
            else:
                messagebox.showwarning("Warning", f"Rank '{rank_name}' already exists")
    
    def remove_rank(self):
        """Remove a rank"""
        current_tab = self.ranks_notebook.index(self.ranks_notebook.select())
        rank_name = self.ranks_notebook.tab(current_tab, "text")
        if messagebox.askyesno("Confirm", f"Remove rank '{rank_name}'?"):
            rank_file = self.ranks_path / f"{rank_name}.json"
            rank_file.unlink()
            self.load_rank_tabs()
            messagebox.showinfo("Success", f"Rank '{rank_name}' removed")
    
    def setup_logs_tab(self):
        """Setup the Logs tab with file viewing and editing"""
        # Left column - Log file list
        left_frame = ttk.Frame(self.logs_tab)
        left_frame.pack(side=tk.LEFT, fill=tk.BOTH, expand=False, padx=5, pady=5)
        
        ttk.Label(left_frame, text="Log Files", font=("Arial", 12, "bold")).pack()
        
        self.logs_listbox = tk.Listbox(left_frame, width=30, height=30)
        self.logs_listbox.pack(fill=tk.BOTH, expand=True)
        self.logs_listbox.bind('<<ListboxSelect>>', self.on_log_select)
        
        # Load logs
        self.load_logs_list()
        
        # Right column - Log content
        right_frame = ttk.Frame(self.logs_tab)
        right_frame.pack(side=tk.RIGHT, fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        ttk.Label(right_frame, text="Log Content", font=("Arial", 12, "bold")).pack()
        
        self.log_text = scrolledtext.ScrolledText(right_frame, wrap=tk.WORD, height=30)
        self.log_text.pack(fill=tk.BOTH, expand=True)
        
        # Save button
        ttk.Button(right_frame, text="Save Log", command=self.save_log).pack(pady=5)
    
    def load_logs_list(self):
        """Load log files from logs directory"""
        self.logs_listbox.delete(0, tk.END)
        if self.logs_path.exists():
            # Look for specific log files
            log_files = []
            for pattern in ["alien_armor.log", "craftbot_debug.log", "trade_logs.txt"]:
                log_file = self.logs_path / pattern
                if log_file.exists():
                    log_files.append(log_file)

            # Also add any other .log and .txt files
            for log_file in sorted(self.logs_path.glob("*.*")):
                if log_file.is_file() and log_file not in log_files:
                    if log_file.suffix in ['.log', '.txt']:
                        log_files.append(log_file)

            # Sort by modification time (newest first)
            log_files.sort(key=lambda x: x.stat().st_mtime, reverse=True)

            for log_file in log_files:
                self.logs_listbox.insert(tk.END, log_file.name)
        else:
            self.logs_listbox.insert(tk.END, f"Logs path not found: {self.logs_path}")
    
    def on_log_select(self, event):
        """Load selected log file for viewing"""
        selection = self.logs_listbox.curselection()
        if selection:
            log_name = self.logs_listbox.get(selection[0])
            log_file = self.logs_path / log_name
            if log_file.exists():
                content = log_file.read_text()
                self.log_text.delete(1.0, tk.END)
                self.log_text.insert(1.0, content)
    
    def save_log(self):
        """Save edited log file"""
        selection = self.logs_listbox.curselection()
        if not selection:
            messagebox.showwarning("Warning", "Please select a log file first")
            return
        
        log_name = self.logs_listbox.get(selection[0])
        log_file = self.logs_path / log_name
        
        try:
            content = self.log_text.get(1.0, tk.END)
            log_file.write_text(content)
            messagebox.showinfo("Success", f"Log file '{log_name}' saved successfully!")
        except Exception as e:
            messagebox.showerror("Error", f"Failed to save log: {e}")

if __name__ == "__main__":
    root = tk.Tk()
    app = CraftbotManagementWindow(root)
    root.mainloop()

