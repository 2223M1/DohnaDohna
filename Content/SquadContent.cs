using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Characters;
using STS2RitsuLib.Scaffolding.Content;
using STS2RitsuLib.Utils;

namespace DohnaDohna.Content;

public sealed class SquadCardPool : TypeListCardPoolModel
{
    public override string Title => "DohnaDohna";
    public override string EnergyColorName => "colorless";
    public override Color DeckEntryCardColor => new("#657F9A");
    public override bool IsColorless => false;
    public override Material? PoolFrameMaterial => MaterialUtils.CreateReplaceHueShaderMaterial(0.21f, 0.26f, 0.35f);
}

public sealed class SquadRelicPool : TypeListRelicPoolModel
{
    public override string EnergyColorName => "colorless";
}

public sealed class SquadPotionPool : TypeListPotionPoolModel
{
    public override string EnergyColorName => "colorless";
}

[RegisterCharacter]
public sealed class DohnaSquad : ModCharacterTemplate<SquadCardPool, SquadRelicPool, SquadPotionPool>
{
    public override Color NameColor => new("#E256A7");
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 25;
    public override int StartingGold => 99;
    public override bool RequiresEpochAndTimeline => false;
    public override bool AllowInVanillaRandomCharacterSelect => false;
    public override float AttackAnimDelay => 0;
    public override float CastAnimDelay => 0;
    public override List<string> GetArchitectAttackVfx() => ["vfx/vfx_attack_blunt"];
    public override CharacterAssetProfile AssetProfile => CharacterAssetProfiles.Merge(
        CharacterAssetProfiles.Ironclad(), new CharacterAssetProfile(
            Scenes: new(VisualsPath: "res://DohnaDohna/scenes/controller.tscn"),
            Ui: new(IconTexturePath: "res://DohnaDohna/images/icons/squad.png", IconOutlineTexturePath: "res://DohnaDohna/images/icons/squad.png",
                IconPath: "res://DohnaDohna/images/icons/squad.png", CharacterSelectIconPath: "res://DohnaDohna/images/icons/squad.png",
                CharacterSelectLockedIconPath: "res://DohnaDohna/images/icons/squad.png", MapMarkerPath: "res://DohnaDohna/images/icons/squad.png",
                CharacterSelectBgPath: "res://DohnaDohna/scenes/select_background.tscn")));
}
