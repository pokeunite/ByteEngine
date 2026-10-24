using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using Component = ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;

/// <summary>Event-driven bindings. Native UI templates retain all authorable styling.</summary>
public sealed class WeaponInventoryHud3D : Component
{
    [SceneObjectReference(typeof(FpsGunLab3D))] public Guid EquipmentObjectId { get; set; }
    [SceneObjectReference(typeof(UiWidget))] public Guid CardTemplateId { get; set; }
    [SceneObjectReference(typeof(UiText))] public Guid KeyTemplateId { get; set; }
    [SceneObjectReference(typeof(UiWidget))] public Guid ReserveTemplateId { get; set; }
    public Vector2 StackOffset { get; set; } = new(-22,-57);
    public string HotkeyLabels { get; set; } = "1,2,3";
    public Vector4 SelectedColor { get; set; } = new(1,.76f,.4f,1);
    public Vector4 StoredColor { get; set; } = new(.72f,.8f,.8f,.9f);
    [Browsable(false)] public int DisplayedWeaponCount => _cards.Count;
    private readonly Dictionary<Guid,(GameObject Card,GameObject Key,GameObject Reserve)> _cards=[];
    private static T Copy<T>(T source) where T:Component,new()
    {
        var copy=new T();
        foreach(var p in typeof(T).GetProperties(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly))
            if(p.CanRead&&p.SetMethod?.IsPublic==true&&p.GetIndexParameters().Length==0)p.SetValue(copy,p.GetValue(source));
        return copy;
    }
    public void Refresh()
    {
        var scene=GameObject.Scene;
        var equipment=scene?.FindGameObject(EquipmentObjectId)?.GetComponent<FpsGunLab3D>();
        var card=scene?.FindGameObject(CardTemplateId)?.GetComponent<UiWidget>();
        var key=scene?.FindGameObject(KeyTemplateId)?.GetComponent<UiText>();
        var reserve=scene?.FindGameObject(ReserveTemplateId)?.GetComponent<UiWidget>();
        if(scene==null||equipment==null||card==null||key==null||reserve==null)return;
        foreach(var id in _cards.Keys.Where(id=>!equipment.CarriedWeapons.Any(w=>w.GameObject.Id==id)).ToArray())
        {var old=_cards[id];scene.DestroyGameObject(old.Card);scene.DestroyGameObject(old.Key);scene.DestroyGameObject(old.Reserve);_cards.Remove(id);}
        var labels=HotkeyLabels.Split(',');
        // Selected is nearest the ammo readout; labels still refer to carried inventory order.
        var ordered=equipment.CarriedWeapons.OrderByDescending(w=>w==equipment.SelectedWeapon).ToArray();
        for(int row=0;row<ordered.Length;row++)
        {
            var weapon=ordered[row];
            if(!_cards.TryGetValue(weapon.GameObject.Id,out var entry))
            {
                GameObject Make<T>(string suffix,T source) where T:Component,new()
                {var o=scene.CreateGameObject("HUD carried "+weapon.GameObject.Id+suffix);o.SetParent(GameObject,false);o.AddComponent(Copy(source));return o;}
                entry=(Make(" card",card),Make(" key",key),Make(" reserve",reserve));_cards.Add(weapon.GameObject.Id,entry);
            }
            var image=entry.Card.GetComponent<UiWidget>()!;
            image.Offset=card.Offset+StackOffset*row;image.Visible=true;
            image.ImageReference=weapon.Definition!.HudIcon;
            image.Color=weapon==equipment.SelectedWeapon?SelectedColor:StoredColor;
            var text=entry.Key.GetComponent<UiText>()!;text.Offset=key.Offset+StackOffset*row;
            int index=equipment.CarriedWeapons.ToList().IndexOf(weapon);text.Text=index<labels.Length?labels[index].Trim():"";
            var ticks=entry.Reserve.GetComponent<UiWidget>()!;ticks.Offset=reserve.Offset+StackOffset*row;ticks.Visible=true;
            BindReserve(ticks,weapon);
        }
    }
    public static void BindReserve(UiWidget widget,WeaponInstance3D weapon)
    {
        int capacity=Math.Max(1,weapon.Definition!.MagazineCapacity);
        widget.Kind=UiWidgetKind.SegmentedBar;
        widget.Maximum=Math.Max(1,(float)Math.Ceiling((double)weapon.Definition.ReserveCapacity/capacity));
        widget.Value=weapon.Definition.InfiniteReserve?widget.Maximum:(float)weapon.ReserveAmmo/capacity;
    }
}
