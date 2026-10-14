using System.Security.Cryptography;
using System.Text.Json;
using DohnaDohna.Cards;
using DohnaDohna.Content;
using DohnaDohna.Relics;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    // Only in the isolated smoke driver. Every icon is a real native NRelic;
    // every detail page is the game's own inspection screen.
    private async Task VerifyRelicVisuals(Player owner, RunState run)
    {
        var touch = ModelDb.Relic<TouchOfOrobas>();
        var models = RoleDefinition.All.SelectMany(role => new RelicModel[]
        {
            SquadRoleRelic.Basic(role.Id).ToMutable(),
            touch.GetUpgradedStarterRelic(SquadRoleRelic.Basic(role.Id)).ToMutable()
        }).ToArray();
        var evidence = new List<object>();
        var layer = new CanvasLayer { Layer = 100 };
        var size = GetViewport().GetVisibleRect().Size;
        var background = new ColorRect { Size = size, Color = new Color("#22282e"), MouseFilter = Control.MouseFilterEnum.Ignore };
        var labels = new List<Label>();
        GetTree().Root.AddChild(layer);
        layer.AddChild(background);
        void LabelAt(string text, Vector2 position, int fontSize = 17)
        {
            var label = new Label { Text = text, Position = position };
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", Colors.White);
            labels.Add(label);
            layer.AddChild(label);
        }
        try
        {
            LabelAt("DOHNADOHNA - NATIVE RELIC RENDERING / NORMAL + GOLD", new Vector2(22, 12), 23);
            float width = (size.X - 40) / 5;
            float height = (size.Y - 135) / 2;
            for (int i = 0; i < models.Length; i++)
            {
                var model = (SquadRoleRelic)models[i];
                int roleIndex = i / 2, variant = i % 2;
                var origin = new Vector2(20 + roleIndex % 5 * width, 60 + roleIndex / 5 * height);
                if (variant == 0) LabelAt(model.Role.ToUpperInvariant() + " / NORMAL - GOLD", origin, 16);
                var big = NRelic.Create(model, NRelic.IconSize.Large) ?? throw new Exception("Native large relic unavailable");
                layer.AddChild(big);
                big.Position = origin + new Vector2(variant * width / 2, 30);
                big.Size = new Vector2(width / 2 - 6, width / 2 - 6);
                var holder = NRelicInventoryHolder.Create(model) ?? throw new Exception("Native inventory holder unavailable");
                layer.AddChild(holder);
                holder.Position = origin + new Vector2(variant * width / 2 + 22, width / 2 + 45);
                holder.MouseFilter = Control.MouseFilterEnum.Ignore;
                var small = holder.Relic;
                CatalogCheck(big.Icon.Texture.GetSize() == new Vector2(256, 256) && !big.Outline.Visible,
                    model.Id + " 256px big art / no duplicate native outline");
                CatalogCheck(small.Icon.Texture.GetSize() == new Vector2(85, 85)
                    && small.Outline.Texture.GetSize() == new Vector2(85, 85) && small.Outline.Visible,
                    model.Id + " native 85px icon and independent outline");
                CatalogCheck(small.Outline.SelfModulate.R == 0 && Math.Abs(small.Outline.SelfModulate.A - 128f/255) < .001,
                    model.Id + " native black 50 percent outline modulation");
                var profile = model.AssetProfile;
                CatalogCheck(small.Icon.Texture.ResourcePath == profile.IconPath
                    && small.Outline.Texture.ResourcePath == profile.IconOutlinePath && big.Icon.Texture.ResourcePath == profile.BigIconPath,
                    model.Id + " all three public asset bindings");
                using var image = big.Icon.Texture.GetImage();
                var data = image.GetData();
                evidence.Add(new { model = model.Id.ToString(), model.Role, gold = variant == 1,
                    icon = profile.IconPath, outline = profile.IconOutlinePath, big = profile.BigIconPath,
                    renderedTextureSha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant() });
            }
            LabelAt("Bottom row: native 60px display size. Reference: Anchor / Burning Blood / Oddly Smooth Stone", new Vector2(22, size.Y-76), 15);
            RelicModel[] references = [ModelDb.Relic<Anchor>(), ModelDb.Relic<BurningBlood>(), ModelDb.Relic<OddlySmoothStone>()];
            for (int i = 0; i < references.Length; i++)
            {
                var native = NRelic.Create(references[i].ToMutable(), NRelic.IconSize.Small)!;
                layer.AddChild(native);
                native.Position = new Vector2(size.X-240+i*75, size.Y-72);
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Screenshot("relic-gallery-dark.png");
            background.Color = new Color("#dedbd5");
            foreach (var label in labels) label.AddThemeColorOverride("font_color", new Color("#282525"));
            await Screenshot("relic-gallery-light.png");
        }
        finally { layer.QueueFree(); }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var screen = NGame.Instance!.GetInspectRelicScreen();
        foreach (var model in models) SaveManager.Instance.MarkRelicAsSeen(model);
        try
        {
            foreach (var model in models.Cast<SquadRoleRelic>())
            {
                screen.Open(models, model);
                await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
                CatalogCheck(screen.GetNode<TextureRect>("%RelicImage").Texture.ResourcePath == model.AssetProfile.BigIconPath,
                    model.Id + " native inspect screen shows selected normal/gold art");
                CatalogCheck(!model.Flavor.GetFormattedText().Contains("DOHNA_"), model.Id + " no missing flavor key in inspect screen");
                await Screenshot($"relic-inspect-{model.Role}-{(model.Multiplier == 2 ? "gold" : "normal")}.png");
                screen.Close();
            }
        }
        finally { screen.Close(); }
        await ToSignal(GetTree().CreateTimer(.4), SceneTreeTimer.SignalName.Timeout);
        var previews = new List<object>();
        var cardLayer = new CanvasLayer { Layer = 100 };
        GetTree().Root.AddChild(cardLayer);
        cardLayer.AddChild(new ColorRect { Size = size, Color = new Color("#22282e"), MouseFilter = Control.MouseFilterEnum.Ignore });
        try
        {
            for (int i = 0; i < models.Length; i += 2)
            {
                var basic = (SquadRoleRelic)models[i];
                var option = run.CreateCard<SquadRelicUpgradeChoice>(owner);
                option.SetRelic(basic, models[i + 1]);
                var card = NCard.Create(option) ?? throw new Exception("Native refinement card unavailable");
                cardLayer.AddChild(card);
                card.Position = new Vector2((i / 2 % 5 + .5f) * size.X / 5, 245 + i / 10 * 410);
                card.Scale = new Vector2(.85f, .85f);
                card.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
                var portrait = card.GetNode<TextureRect>("%Portrait").Texture;
                CatalogCheck(portrait.ResourcePath == RoleDefinition.Get(basic.Role).AssetRoot + "/relic_refinement.png"
                    && portrait.GetSize() == new Vector2(500, 380), basic.Role + " native gold choice card loads 500x380 portrait");
                using var portraitImage = portrait.GetImage();
                previews.Add(new { basic.Role, path = portrait.ResourcePath,
                    renderedTextureSha256 = Convert.ToHexString(SHA256.HashData(portraitImage.GetData())).ToLowerInvariant() });
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Screenshot("relic-refinement-all-ten.png");
        }
        finally { cardLayer.QueueFree(); }
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        System.IO.File.WriteAllText(System.IO.Path.Combine(System.Environment.GetEnvironmentVariable("LOCALAPPDATA")!, "relic-visuals.json"),
            JsonSerializer.Serialize(new { host = "0.111.0", nativeSmallDisplayPixels = 60, evidence, previews }, new JsonSerializerOptions { WriteIndented = true }));
        CatalogCheck(evidence.Count == 20, "twenty native relic visual variants verified");
        CatalogCheck(previews.Count == 10, "ten native refinement card portraits verified");
    }
}
