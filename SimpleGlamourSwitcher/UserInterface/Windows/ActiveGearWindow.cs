using System.Diagnostics;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using SimpleGlamourSwitcher.Configuration.Files;
using SimpleGlamourSwitcher.Configuration.Interface;
using SimpleGlamourSwitcher.UserInterface.Components;
using SimpleGlamourSwitcher.UserInterface.Page;

namespace SimpleGlamourSwitcher.UserInterface.Windows;

public class ActiveGearWindow() : Window("SGS###SimpleGlamourSwitcherEquipped", ImGuiWindowFlags.AlwaysAutoResize) {

    private OutfitConfigFile? OutfitCache { get; set; } = null;
    private OutfitConfigFile? updatedCache;
    private bool updatingOutfit = false;
    private Stopwatch updateOutfitTimer = Stopwatch.StartNew();
    private bool dirty;

    private bool compact;
    private bool locked;
    private bool collapsed;


    private void UpdateButtons() {
        TitleBarButtons = [];

        if (locked) {
            TitleBarButtons.Add(lockButton);
            return;
        }
        
        TitleBarButtons.Add(compactButton);
        if (compact) {
            TitleBarButtons.Add(lockButton);
        }
    }
    
    private readonly TitleBarButton compactButton = new() {
        Icon = FontAwesomeIcon.ArrowRight,
        ShowTooltip = () => { },
    };
    
    private readonly TitleBarButton lockButton = new() {
        Icon = FontAwesomeIcon.Lock,
        ShowTooltip = () => { }
    };
    
    public override void OnClose() {
        if (Plugin.IsDisposing) return;
        PluginConfig.EquippedWindowConfig.WindowOpen = false;
        PluginConfig.Save(true);
    }

    public override bool DrawConditions() => ActiveCharacter != null && PlayerStateService.IsLoaded && Objects.LocalPlayer != null;

    public override void OnOpen() {
        PluginConfig.EquippedWindowConfig.WindowOpen = true;
        
        compact = PluginConfig.EquippedWindowConfig.UseCompactWindow;
        if (compact) {
            locked = PluginConfig.EquippedWindowConfig.LockWindow;
        } else {
            PluginConfig.EquippedWindowConfig.LockWindow = false;
        }
        PluginConfig.Save(true);
        
        compactButton.Click = ToggleCompactMode;
        lockButton.Click = ToggleLock;
        UpdateButtons();
        
        dirty = false;
        updatingOutfit = false;
        updateOutfitTimer.Restart();
        
        AllowClickthrough = false;
        AllowPinning = false;
        RespectCloseHotkey = false;
        UpdateOutfit();
    }

    private void ToggleLock(ImGuiMouseButton obj) {
        PluginConfig.EquippedWindowConfig.LockWindow = locked = !locked;
        PluginConfig.Save(true);
        UpdateButtons();
    }

    private void ToggleCompactMode(ImGuiMouseButton obj) {
        PluginConfig.EquippedWindowConfig.UseCompactWindow = compact = !compact;
        PluginConfig.Save(true);
        UpdateButtons();
    }


    public override void PreDraw() {
        if (compact && locked) {
            ImGui.PushStyleColor(ImGuiCol.TitleBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, Vector4.Zero);
        } else {
            ImGui.PushStyleColor(ImGuiCol.TitleBg, ImGui.GetColorU32(ImGuiCol.TitleBg));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, ImGui.GetColorU32(ImGuiCol.TitleBgActive));
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, ImGui.GetColorU32(ImGuiCol.TitleBgCollapsed));
        }
    }

    public override void Update() {
        compactButton.Icon = compact ? FontAwesomeIcon.ArrowRight : FontAwesomeIcon.ArrowLeft;
        lockButton.Icon = locked ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen;
        ShowCloseButton = !compact;
        WindowName = compact ? "SGS###SimpleGlamourSwitcherEquipped" : "Simple Glamour Switcher | Equipped###SimpleGlamourSwitcherEquipped";
        collapsed = true;

        if (locked) {
            Flags |= ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove;
        } else {
            Flags &= ~(ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoMove);
        }
    }

    public override void PostDraw() {
        ImGui.PopStyleColor(3);
    }

    private void UpdateOutfit() {
        if (updatingOutfit || dirty) return;
        updateOutfitTimer.Restart();
        updatingOutfit = true;
        Task.Run(() => {
            var outfit = ActiveCharacter == null ? null : OutfitConfigFile.CreateFromLocalPlayer(ActiveCharacter, Guid.Empty, DefaultOptions.Equipment);
            updatedCache = outfit;
            updatingOutfit = false;
            updateOutfitTimer.Restart();
        });
    }
    

    
    public override void Draw() {
        collapsed = false;
        var outfit = OutfitCache;
        if (updateOutfitTimer.ElapsedMilliseconds > 1000 && !updatingOutfit) {
            if (dirty && outfit != null) {
                outfit.Apply().ConfigureAwait(false);
                dirty = false;
                updatedCache = null;
                updateOutfitTimer.Restart();
            } else if (updatedCache != null) {
                OutfitCache = updatedCache;
                updatedCache = null;
                outfit = OutfitCache;
            } else {
                UpdateOutfit();
            }
        }
        
        if (outfit == null) {
            outfit = OutfitConfigFile.Create(ActiveCharacter);
        }
        
        dirty |= EquipmentDisplay.DrawEquipment(outfit.Equipment, EquipmentDisplayFlags.Simple | EquipmentDisplayFlags.EnableCustomItemPicker | EquipmentDisplayFlags.ContextShowSaveSlot | (compact ? EquipmentDisplayFlags.Compact : EquipmentDisplayFlags.None));

        if (PluginConfig.EquippedWindowConfig.ShowSaveButton && ActiveCharacter != null) {
            if (compact) {
                var s = new Vector2(ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2);
                ImGui.SetCursorScreenPos(ImGui.GetItemRectMax() - s with { X = -1 });
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Save, s)) {
                    Plugin.MainWindow.IsOpen = true;
                    Plugin.MainWindow.OpenPage(new EditOutfitPage(ActiveCharacter, Guid.Empty, outfit));
                }
            } else {
                ImGui.SetCursorScreenPos(ImGui.GetItemRectMax() - new Vector2(-ImGui.GetStyle().ItemSpacing.X, ImGui.GetTextLineHeightWithSpacing()));
                if (ImGui.Button("Save Outfit", ImGui.GetContentRegionAvail())) {
                    Plugin.MainWindow.IsOpen = true;
                    Plugin.MainWindow.OpenPage(new EditOutfitPage(ActiveCharacter, Guid.Empty, outfit));
                }
            }
        }
    }
}
