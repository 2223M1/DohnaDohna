using DohnaDohna.Cards;
using DohnaDohna.Cards.Catalog;
using DohnaDohna.Code.Squad;
using DohnaDohna.Content;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Runs;
using STS2RitsuLib.Keywords;

namespace DohnaDohna.SmokeDriver;

public partial class LoadProbe
{
    private async Task VerifyKeywords(Player owner, RunState run, SquadCatalogCard[] cards)
    {
        CatalogCheck(KeywordId(SquadKeywords.Advance) == "DOHNA_DOHNA_KEYWORD_ADVANCE", "stable Advance keyword identity");
        foreach (var canonical in cards)
        {
            void Check(CardModel card, string stage)
            {
                CatalogCheck(card.Keywords.Contains(SquadKeywords.Advance) == canonical.IsMelee,
                    canonical.Prototype + " " + stage + " melee/keyword agreement");
            }
            Check(canonical, "canonical");
            var card = run.CreateCard(canonical, owner);
            Check(card, "mutable");
            Check(run.CloneCard(card), "clone");
            Check(CardModel.FromSerializable(card.ToSerializable()), "save round trip");
            if (card.IsUpgradable) card.UpgradeInternal();
            Check(card, "upgraded");
            Check(CardModel.FromSerializable(card.ToSerializable()), "upgraded save round trip");
        }
        CatalogCheck(ModelDb.Card<SquadBackstab>().Keywords.SetEquals(
            [SquadKeywords.Advance, CardKeyword.Innate, CardKeyword.Exhaust]), "Backstab retains native Innate/Exhaust alongside Advance");
        foreach (var model in new CardModel[] { ModelDb.Card<SquadFeed>(), ModelDb.Card<SquadTheHunt>(), ModelDb.Card<SquadTheScythe>() })
            CatalogCheck(model.Keywords.SetEquals([SquadKeywords.Advance, CardKeyword.Exhaust]), model.Id + " native Exhaust preserved");

        var swap = run.CreateCard<SquadSwap>(owner);
        CatalogCheck(swap.Keywords.SetEquals([CardKeyword.Innate]), "Swap has native Innate before upgrade");
        swap.UpgradeInternal();
        CatalogCheck(swap.Keywords.SetEquals([CardKeyword.Innate, CardKeyword.Retain])
            && swap.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "Swap+ keeps Innate / adds Retain / remains zero cost");
        CatalogCheck(CardModel.FromSerializable(swap.ToSerializable()).Keywords.SetEquals(swap.Keywords), "Swap+ keywords survive native save round trip");

        string language = LocManager.Instance.Language;
        var saved = SquadStore.Get(owner);
        SquadStore.State.Set(run, owner.NetId, SquadRunState.Create(["porno", "joker", "kuma", "tora"]));
        try
        {
            foreach (var (lang, advance, innate, retain) in new[] {
                ("zhs", "突前", "固有", "保留"), ("eng", "Advance", "Innate", "Retain"), ("jpn", "前進", "天賦", "保留") })
            {
                LocManager.Instance.SetLanguage(lang);
                var layer = new CanvasLayer { Layer = 120 };
                GetTree().Root.AddChild(layer);
                try
                {
                    var panel = new ColorRect { Color = new Color(.08f, .09f, .12f), Size = GetViewport().GetVisibleRect().Size };
                    layer.AddChild(panel);
                    var melee = run.CreateCard<SquadUppercut>(owner);
                    var backstab = run.CreateCard<SquadBackstab>(owner);
                    var basicSwap = run.CreateCard<SquadSwap>(owner);
                    backstab.UpgradeInternal();
                    CardModel[] examples = [melee, backstab, basicSwap, swap];
                    Color? plainTitle = null, upgradedTitle = null;
                    for (int i = 0; i < examples.Length; i++)
                    {
                        var model = examples[i];
                        var node = NCard.Create(model)!;
                        panel.AddChild(node);
                        node.Position = new Vector2(panel.Size.X * (i + .5f) / examples.Length, panel.Size.Y * .45f);
                        node.Scale = new Vector2(.9f, .9f);
                        node.UpdateVisuals(PileType.Deck, CardPreviewMode.Normal);
                        var title = node.GetNode<Control>("%TitleLabel").GetThemeColor("font_color");
                        if (i == 0) plainTitle = title;
                        if (i == 1) upgradedTitle = title;
                        if (i == 2) CatalogCheck(title == plainTitle, lang + " melee and non-melee use identical native title color");
                        if (i == 3) CatalogCheck(title == upgradedTitle, lang + " native upgraded title color preserved");
                        var text = model.GetDescriptionForPile(PileType.Deck);
                        if (model is SquadCardModel { IsMelee: true })
                            CatalogCheck(text.Split(advance).Length == 2, lang + " " + model.Id + " one Advance line");
                        if (model is SquadSwap)
                        {
                            CatalogCheck(text.Split(innate).Length == 2 && text.Split(retain).Length == (model.IsUpgraded ? 2 : 1),
                                lang + " Swap native keywords shown once");
                            string front = lang switch { "zhs" => "最前方同伴", "eng" => "frontmost member", _ => "先頭の隊員" };
                            CatalogCheck(text.Contains(front), lang + " Swap identifies the frontmost member, not a relative neighbour");
                        }
                    }
                    var tip = melee.HoverTips.OfType<HoverTip>().Single(t => t.Title == advance);
                    CatalogCheck(!string.IsNullOrWhiteSpace(tip.Description) && !tip.Description.Contains("DOHNA_"), lang + " registered native hover tip");
                    panel.AddChild(new Label { Text = tip.Title + "\n" + tip.Description, Position = new Vector2(80, panel.Size.Y * .8f) });
                    await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
                    await Screenshot("keywords-" + lang + ".png");
                }
                finally { layer.QueueFree(); }
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
        finally
        {
            LocManager.Instance.SetLanguage(language);
            SquadStore.State.Set(run, owner.NetId, saved);
        }
    }
}
