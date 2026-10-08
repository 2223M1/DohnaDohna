using Godot;
using DohnaDohna.Cards;
using DohnaDohna.Content;
using DohnaDohna.Code.Squad;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace DohnaDohna.Code.UI;

/// <summary>Two levels of one native screen/lobby. This node owns only the secondary selection draft.</summary>
public sealed partial class SquadSelectPanel : Control
{
    private readonly List<string> _selected = [];
    private readonly List<SquadRoleButton> _buttons = [];
    private readonly List<SquadRoleButton> _slots = [];
    private readonly HBoxContainer _roles = new() { Name = "DohnaRoleBar", Alignment = BoxContainer.AlignmentMode.Center };
    private readonly HBoxContainer _formation = new() { Name = "DohnaFormation", Alignment = BoxContainer.AlignmentMode.Center };
    private readonly Label _caption = new() { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
    private NCharacterSelectScreen _screen = null!;
    private Control _primaryBar = null!, _primaryInfo = null!, _memberInfo = null!;
    private NConfirmButton _confirm = null!;
    private NCard? _preview;
    private bool _locked;
    public bool IsSecondary => Visible;
    internal Control DefaultFocusedControl => _buttons[0];

    public static SquadSelectPanel? Get(NCharacterSelectScreen screen) => screen.GetNodeOrNull<SquadSelectPanel>(nameof(SquadSelectPanel));
    public static void Open(NCharacterSelectScreen screen)
    {
        var panel = Get(screen);
        if (panel == null)
        {
            panel = new SquadSelectPanel { Name = nameof(SquadSelectPanel), MouseFilter = MouseFilterEnum.Ignore, _screen = screen };
            screen.AddChild(panel);
        }
        panel.ShowSecondary();
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _primaryBar = _screen.GetNode<Control>("CharSelectButtons");
        _primaryInfo = _screen.GetNode<Control>("InfoPanel");
        _confirm = _screen.GetNode<NConfirmButton>("ConfirmButton");
        _memberInfo = (Control)_primaryInfo.Duplicate();
        _memberInfo.Name = "MemberInfo";
        AddChild(_memberInfo);
        _memberInfo.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _memberInfo.Position = new Vector2(245, 170);
        _memberInfo.Size = new Vector2(600, 365);
        var descriptionLayout = _memberInfo.GetNode<VBoxContainer>("VBoxContainer");
        descriptionLayout.Alignment = BoxContainer.AlignmentMode.Begin;
        descriptionLayout.Size = new Vector2(550, 360);
        _memberInfo.GetNode<Control>("VBoxContainer/HpGoldSpacer/HpGold/Gold").Hide();
        _memberInfo.GetNode<Control>("VBoxContainer/Relic/Icon").Hide();
        AddChild(_roles);
        AddChild(_formation);
        AddChild(_caption);
        _caption.AddThemeFontSizeOverride("font_size", 22);
        _caption.AddThemeConstantOverride("outline_size", 6);
        _caption.AddThemeColorOverride("font_outline_color", Colors.Black);
        _roles.AddThemeConstantOverride("separation", 16);
        _formation.AddThemeConstantOverride("separation", 16);
        foreach (var role in RoleDefinition.All)
        {
            var button = SquadRoleButton.Create(role);
            button.Activated = b => Toggle(b.Role!.Id);
            button.Previewed = Preview;
            _roles.AddChild(button);
            _buttons.Add(button);
        }
        for (int i = 0; i < 4; i++)
        {
            var slot = SquadRoleButton.Create(null);
            slot.Name = "Slot" + i;
            slot.Slot = i;
            slot.Previewed = Preview;
            slot.Activated = b => { if (b.Role != null) Toggle(b.Role.Id); };
            slot.Reordered = Reorder;
            _formation.AddChild(slot);
            _slots.Add(slot);
        }
        _screen.Resized += Layout;
        Layout();
    }

    private void ShowSecondary()
    {
        if (Visible && _locked) return;
        ResetDraft();
        _locked = false;
        Show();
        _primaryBar.Hide();
        _primaryInfo.Hide();
        _screen.GetChildrenRecursive<SelectBackground>().Single().SetSecondary(true);
        _confirm.OverrideHotkeys([]); // Selection is handled by the focused native button, not an embark shortcut.
        foreach (var button in _buttons.Concat(_slots)) button.Enable();
        UpdateSelection();
        Preview(RoleDefinition.Get(_selected.FirstOrDefault() ?? RoleDefinition.All[0].Id));
        _buttons[0].TryGrabFocus();
    }

    private void Layout()
    {
        float width = _roles.GetCombinedMinimumSize().X;
        _roles.Position = new Vector2((_screen.Size.X - width) * .5f, _screen.Size.Y - 220);
        _roles.Size = new Vector2(width, 148);
        _formation.Size = new Vector2(448, 148);
        _formation.Position = new Vector2((_screen.Size.X - _formation.Size.X) * .5f, _roles.Position.Y - 170);
        _caption.Size = new Vector2(width, 32);
        _caption.Position = new Vector2(_roles.Position.X, _formation.Position.Y - 42);
        if (_preview != null) _preview.Position = new Vector2(_screen.Size.X - 390, 380);
    }

    private void Toggle(string role)
    {
        if (_locked) return;
        if (!_selected.Remove(role) && _selected.Count < 4) _selected.Add(role);
        UpdateSelection();
    }
    private void Reorder(string role, int destination)
    {
        if (_locked || !_selected.Contains(role)) return;
        _selected.Remove(role);
        _selected.Insert(Math.Min(destination, _selected.Count), role);
        UpdateSelection();
    }
    private void UpdateSelection()
    {
        for (int i = 0; i < 4; i++)
        {
            _slots[i].SetRole(i < _selected.Count ? RoleDefinition.Get(_selected[i]) : null);
        _slots[i].SetSelected(i < _selected.Count);
            _slots[i].FocusNeighborLeft = _slots[(i + 3) % 4].GetPath();
            _slots[i].FocusNeighborRight = _slots[(i + 1) % 4].GetPath();
            _slots[i].FocusNeighborBottom = _buttons[Math.Min(i * 3, 9)].GetPath();
        }
        foreach (var button in _buttons) button.SetSelected(_selected.Contains(button.Role!.Id));
        var text = new LocString("characters", "DOHNA_DOHNA_SQUAD_UI.formation");
        text.Add("Count", _selected.Count);
        _caption.Text = text.GetFormattedText();
        _confirm.SetEnabled(_selected.Count == 4 && !_locked);
        for (int i = 0; i < _buttons.Count; i++)
        {
            _buttons[i].FocusNeighborLeft = _buttons[(i + 9) % 10].GetPath();
            _buttons[i].FocusNeighborRight = _buttons[(i + 1) % 10].GetPath();
            _buttons[i].FocusNeighborTop = _slots[Math.Min(i / 3, 3)].GetPath();
            _buttons[i].FocusNeighborBottom = _confirm.GetPath();
        }
    }

    private void Preview(RoleDefinition role)
    {
        _memberInfo.GetNode<MegaLabel>("VBoxContainer/Name").SetTextAutoSize(role.GetDisplayName(LocManager.Instance.Language));
        _memberInfo.GetNode<MegaLabel>("VBoxContainer/Name").AddThemeColorOverride("font_color", new Color(role.Color));
        _memberInfo.GetNode<MegaLabel>("VBoxContainer/HpGoldSpacer/HpGold/Hp/Label").SetTextAutoSize($"{role.StartingHp}/{role.StartingHp}");
        _memberInfo.GetNode<MegaRichTextLabel>("VBoxContainer/DescriptionLabel").Text = new LocString("characters", "DOHNA_DOHNA_SQUAD_UI.passive_" + role.Id).GetFormattedText();
        _memberInfo.GetNode<Control>("VBoxContainer/Relic").Hide();
        var model = ModelDb.AllCards.OfType<SquadAttackCard>().Single(c => c.FixedRole == role.Id);
        if (_preview == null)
        {
            _preview = NCard.Create(model) ?? throw new InvalidOperationException("Native card preview could not be created.");
            AddChild(_preview);
            _preview.Scale = Vector2.One * .8f;
            _preview.Position = new Vector2(_screen.Size.X - 390, 380);
            _preview.MouseFilter = MouseFilterEnum.Ignore;
        }
        else _preview.Model = model;
        _preview.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
    }

    public bool Commit()
    {
        if (_locked || _selected.Count != 4) return false;
        SquadStore.State.Lobby.Set(_screen.Lobby, _screen.Lobby.NetService.NetId, SquadRunState.Create(_selected));
        return true;
    }
    public void LockSelection()
    {
        _locked = true;
        foreach (var button in _buttons.Concat(_slots)) button.Disable();
    }
    public void ReturnToPrimary(bool restoreInfo = true)
    {
        ResetDraft();
        UpdateSelection();
        Hide();
        _primaryBar.Show();
        _primaryInfo.Show();
        _screen.GetChildrenRecursive<SelectBackground>().Single().SetSecondary(false);
        _confirm.OverrideHotkeys([MegaInput.select]);
        if (restoreInfo)
        {
            _confirm.Enable();
            _primaryBar.GetChildrenRecursive<NCharacterSelectButton>().Single(b => b.Character is DohnaSquad).TryGrabFocus();
        }
    }
    private void ResetDraft()
    {
        _selected.Clear();
        _locked = false;
        if (_screen.Lobby != null)
            SquadStore.State.Lobby.Remove(_screen.Lobby, _screen.Lobby.NetService.NetId);
    }
    public void ClearDraft()
    {
        // Successful embark transfers ownership to the run. Closing the old
        // screen must not remove that committed state before its capture hook.
        if (_locked) { _selected.Clear(); Hide(); return; }
        ReturnToPrimary(false);
    }
    public override void _ExitTree()
    {
        _screen.Resized -= Layout;
    }
}
