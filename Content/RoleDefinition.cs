namespace DohnaDohna.Content;

/// <summary>Stable gameplay identity; original asset aliases are deliberately separate.</summary>
public sealed record RoleDefinition(string Id, string Name, string English, string Japanese,
    string Color, string MotionBase, string StrikeMotion, string SpecialMotion, string SpecialName)
{
    // Original player table's base HP / 10. Run snapshots remain authoritative after creation.
    public int StartingHp => Id switch
    {
        "kuma" => 36, "porno" => 17, "tora" => 25, "medhico" => 29, "kirakira" => 28,
        "antena" => 24, "kikuchiyo" => 30, "alyce" => 12, "zappa" => 51, "joker" => 18,
        _ => throw new InvalidOperationException($"Missing initial HP for {Id}.")
    };
    public static readonly RoleDefinition[] All =
    [
        // Strike: original default attack first, but exactly one positive damage
        // node. Antena/Medhico/Porno use single-node alternatives until retuned.
        new("kuma", "阿熊", "Kuma", "クマ", "#348FEA", "阿熊", "攻击１", "攻击１强", "零距射击"),
        new("alyce", "爱丽丝", "ALyCE", "ALyCE", "#8EC9FF", "爱丽丝", "攻击１", "攻击３", "狙击"),
        new("antena", "安缇娜", "Antena", "アンテナ", "#35D9BB", "安缇娜", "攻击２", "攻击２强", "RADIATION*"),
        new("tora", "虎太郎", "Torataro", "虎太郎", "#F7C844", "虎虎太郎", "攻击１", "攻击３", "彪牙穿棍"),
        new("kikuchiyo", "菊千代", "Kikuchiyo", "菊千代", "#EC4D60", "菊千代", "攻击１", "攻击１强", "旋闪"),
        new("medhico", "梅蒂可", "Medhico", "メディコ", "#B575EB", "梅蒂可", "攻击２", "攻击２强", "糖果史赛克重击"),
        new("joker", "小丑", "Joker", "ジョーカー", "#C9E34C", "小丑", "攻击１", "攻击２", "烈焰投球"),
        new("zappa", "扎帕", "Zappa", "ザッパ", "#F49345", "扎帕", "攻击１", "攻击３", "重型矮人摔"),
        new("kirakira", "绮菈绮菈", "Kirakira", "キラキラ", "#F266B7", "绮菈绮菈", "攻击１", "攻击２", "滚滚毒气弹"),
        new("porno", "珀尔诺", "Porno", "ポルノ", "#DCE4F0", "珀尔诺", "攻击３", "攻击４", "湄公Δ三角")
    ];

    public static RoleDefinition Get(string id) => All.FirstOrDefault(r => r.Id == id)
        ?? throw new ArgumentException($"Unknown DohnaDohna role: {id}", nameof(id));
    public string AssetRoot => $"res://DohnaDohna/roles/{Id}";
    public string GetDisplayName(string language) => language switch
    {
        "eng" => English, "jpn" => Japanese, _ => Name
    };
}
