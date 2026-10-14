using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

namespace KarlsonDemo.Plugin;

/// <summary>Course rules only; models, collision, weapons and input stay independently authored.</summary>
public sealed class MilkDepotCourse3D : Component
{
    public Guid PlayerObjectId { get; set; }
    public Guid StatusTextObjectId { get; set; }
    public Guid MilkObjectId { get; set; }
    public bool RequireAllTargets { get; set; } = true;
    public float ObjectiveRadius { get; set; } = 1.8f;
    public float CheckpointRadius { get; set; } = 1.4f;
    public float FallResetHeight { get; set; } = -5;
    public bool Completed { get; private set; }
    public int RemainingTargets { get; private set; }
    public int Respawns { get; private set; }
    public Vector3 CheckpointPosition { get; private set; }
    private GameObject? _player, _milk;
    private GameObject[] _checkpoints = [];
    private HealthComponent[] _targets = [];
    private Guid _lastCheckpoint;
    private float _hud;
    protected override void OnStart()
    {
        var scene=GameObject.Scene;
        _player=scene?.FindGameObject(PlayerObjectId);_milk=scene?.FindGameObject(MilkObjectId);
        CheckpointPosition=_player?.Transform.WorldPosition??Vector3.Zero;
        _checkpoints=scene?.GameObjects.Where(o=>o.Name.StartsWith("Checkpoint_Beacon",StringComparison.Ordinal)).ToArray()??[];
        _targets=scene?.GameObjects.Where(o=>o.Name.StartsWith("Training_Dummy",StringComparison.Ordinal))
            .Select(o=>o.GetComponent<HealthComponent>()).OfType<HealthComponent>().ToArray()??[];
    }
    public void Respawn()
    {
        if(_player==null)return;
        _player.Transform.WorldPosition=CheckpointPosition+Vector3.UnitY*.15f;
        _player.GetComponent<CharacterController3D>()?.SetVelocity(Vector3.Zero);
        _player.GetComponent<HealthComponent>()?.Revive();Respawns++;
    }
    protected override void OnUpdate()
    {
        if(_player==null)return;
        RemainingTargets=_targets.Count(t=>!t.IsDead);
        if(_player.Transform.WorldPosition.Y<FallResetHeight||_player.GetComponent<HealthComponent>()?.IsDead==true)Respawn();
        foreach(var checkpoint in _checkpoints)
            if(checkpoint.Id!=_lastCheckpoint && Vector3.Distance(_player.Transform.WorldPosition,checkpoint.Transform.WorldPosition)<Math.Clamp(CheckpointRadius,.2f,4))
            {CheckpointPosition=checkpoint.Transform.WorldPosition+new Vector3(1,0,0);_lastCheckpoint=checkpoint.Id;}
        if(!Completed&&_milk!=null&&(!RequireAllTargets||RemainingTargets==0)&&
            Vector3.Distance(_player.Transform.WorldPosition+Vector3.UnitY,_milk.Transform.WorldPosition)<Math.Clamp(ObjectiveRadius,.2f,4))
        {Completed=true;_milk.Active=false;}
        _hud-=(float)Time.DeltaTime;
        if(_hud<=0)
        {
            _hud=.1f;
            if(GameObject.Scene?.FindGameObject(StatusTextObjectId)?.GetComponent<UiText>() is {} text)
                text.Text=Completed?"MILK RECOVERED - COURSE COMPLETE":$"MILK DEPOT  /  TARGETS LEFT: {RemainingTargets}  /  REACH THE MILK TOWER";
        }
    }
}

public sealed class WeaponAmmoPickup3D : Component
{
    public Guid EquipmentObjectId { get; set; }
    public int ReserveAmount { get; set; } = 18;
    public float PickupRadius { get; set; } = 1.25f;
    public bool Collected { get; private set; }
    protected override void OnUpdate()
    {
        if(Collected||GameObject.Scene?.FindGameObject(EquipmentObjectId) is not {} player||
            Vector3.Distance(player.Transform.WorldPosition,Transform.WorldPosition)>Math.Clamp(PickupRadius,.1f,4))return;
        if(player.GetComponent<FpsGunLab3D>()?.SelectedWeapon is not {} weapon||weapon.Definition is not {} definition||
            ReserveAmount<=0||weapon.ReserveAmmo>=definition.ReserveCapacity)return;
        weapon.AddReserve(ReserveAmount);Collected=true;GameObject.Active=false;
    }
}
