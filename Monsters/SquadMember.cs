using Godot;
using DohnaDohna.Content;
using DohnaDohna.Code.Squad;
using DohnaDohna.Code.Visuals;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;

namespace DohnaDohna.Monsters;

[RegisterMonster]
public sealed class SquadMember : ModMonsterTemplate
{
    public string RoleId { get; set; } = "";
    public override MegaCrit.Sts2.Core.Localization.LocString Title => new("monsters", "DOHNA_ROLE_" + RoleId + ".name");
    public override int MinInitialHp => 25;
    public override int MaxInitialHp => 25;
    public override bool IsHealthBarVisible => true;
    protected override NCreatureVisuals? TryCreateCreatureVisuals() => RoleVisuals.Create(RoleDefinition.Get(RoleId));
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var wait = new MoveState("SQUAD_WAIT", _ => Task.CompletedTask, new UnknownIntent());
        wait.FollowUpState = wait;
        return new MonsterMoveStateMachine([wait], wait);
    }
}
