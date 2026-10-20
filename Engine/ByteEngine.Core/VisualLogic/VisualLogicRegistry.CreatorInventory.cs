using ByteEngine.Core.Variables;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static VariableValue InventoryRoot(VisualInstruction i,EventExecutionContext c,bool create)
    {
        var store=Store(i,c);string name=S(i,c,"inventory","Inventory");
        if(store.TryGet(name,out var root)&&root!.Type==VariableType.Structure)return root;
        var result=VariableValue.FromStructure();result.Children["items"]=VariableValue.FromStructure();result.Children["equipment"]=VariableValue.FromStructure();
        if(create)store.Set(name,result);return result;
    }
    private static VariableValue InventoryItem(VisualInstruction i,EventExecutionContext c,bool create)
    {
        var root=InventoryRoot(i,c,create);if(!root.Children.TryGetValue("items",out var items)){items=VariableValue.FromStructure();root.Children["items"]=items;}
        string id=S(i,c,"item");if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("Item ID is required.");
        if(items.Children.TryGetValue(id,out var value))return value;
        var result=VariableValue.FromStructure();result.Children["count"]=VariableValue.FromNumber();result.Children["maximum"]=VariableValue.FromNumber(-1);
        if(create)items.Children[id]=result;return result;
    }
    private static void RegisterCreatorInventory(VisualLogicRegistry r)
    {
        var scope=TextArg("scope","Variable scope","Self");var bag=TextArg("inventory","Inventory variable","Inventory");var item=TextArg("item","Item ID","item");var amount=NumberArg("amount","Amount",1);var output=TextArg("output","Result variable","ItemCount");var slot=TextArg("slot","Equipment slot","Weapon");
        VisualArgumentDefinition[] args=[scope,bag,item];
        Add(r,"inventory.add","Inventory","Add item",(i,c)=>{var entry=InventoryItem(i,c,true);double n=N(i,c,"amount",1);if(!double.IsFinite(n)||n<0)throw new ArgumentOutOfRangeException("amount");var count=entry.Children["count"];double max=entry.Children["maximum"].Number;count.Number=max<0?count.Number+n:Math.Min(max,count.Number+n);},[..args,amount]);
        Add(r,"inventory.remove","Inventory","Remove item",(i,c)=>{double n=N(i,c,"amount",1);if(!double.IsFinite(n)||n<0)throw new ArgumentOutOfRangeException("amount");var entry=InventoryItem(i,c,true);entry.Children["count"].Number=Math.Max(0,entry.Children["count"].Number-n);if(entry.Children["count"].Number==0){var root=InventoryRoot(i,c,true);if(root.Children.TryGetValue("equipment",out var gear))foreach(var key in gear.Children.Where(p=>p.Value.String==S(i,c,"item")).Select(p=>p.Key).ToArray())gear.Children.Remove(key);}},[..args,amount]);
        Add(r,"inventory.setMaximum","Inventory","Set item stack limit (-1 = unlimited)",(i,c)=>{double max=N(i,c,"maximum",99);if(!double.IsFinite(max)||max<0&&max!=-1)throw new ArgumentOutOfRangeException("maximum");var entry=InventoryItem(i,c,true);entry.Children["maximum"].Number=max; if(max>=0)entry.Children["count"].Number=Math.Min(max,entry.Children["count"].Number);if(entry.Children["count"].Number==0&&InventoryRoot(i,c,true).Children.TryGetValue("equipment",out var gear))foreach(var key in gear.Children.Where(p=>p.Value.String==S(i,c,"item")).Select(p=>p.Key).ToArray())gear.Children.Remove(key);},[..args,NumberArg("maximum","Maximum count (-1 unlimited)",99)]);
        Add(r,"inventory.count","Inventory","Copy item count",(i,c)=>Output(i,c,InventoryItem(i,c,false).Children["count"]),[..args,output]);
        Test(r,"inventory.has","Inventory","Has item amount",(i,c)=>InventoryItem(i,c,false).Children["count"].Number>=Math.Max(0,N(i,c,"amount",1)),[..args,amount]);
        Test(r,"inventory.full","Inventory","Item stack is full",(i,c)=>{var entry=InventoryItem(i,c,false);double max=entry.Children["maximum"].Number;return max>=0&&entry.Children["count"].Number>=max;},args);
        Add(r,"inventory.equip","Inventory / Equipment","Equip item in slot",(i,c)=>{if(InventoryItem(i,c,false).Children["count"].Number<=0)throw new InvalidOperationException("Cannot equip an item the inventory does not contain.");var root=InventoryRoot(i,c,true);if(!root.Children.TryGetValue("equipment",out var equipment)){equipment=VariableValue.FromStructure();root.Children["equipment"]=equipment;}equipment.Children[S(i,c,"slot","Weapon")]=VariableValue.FromString(S(i,c,"item"));},[..args,slot]);
        Add(r,"inventory.unequip","Inventory / Equipment","Unequip slot",(i,c)=>{var root=InventoryRoot(i,c,true);if(root.Children.TryGetValue("equipment",out var equipment))equipment.Children.Remove(S(i,c,"slot","Weapon"));},scope,bag,slot);
        Test(r,"inventory.equipped","Inventory / Equipment","Item is equipped in slot",(i,c)=>InventoryRoot(i,c,false).Children.TryGetValue("equipment",out var gear)&&gear.Children.TryGetValue(S(i,c,"slot","Weapon"),out var equipped)&&equipped.String==S(i,c,"item"),[..args,slot]);
        Add(r,"inventory.clear","Inventory","Clear inventory",(i,c)=>Store(i,c).Remove(S(i,c,"inventory","Inventory")),scope,bag);

        // Links are gameplay relationships, independent of transform/socket parenting.
        VariableValue Links(GameObject obj,string name,bool create)
        {
            if(obj.Variables.TryGet("ObjectLinks",out var root)&&root!.Type==VariableType.Structure)
            {if(root.Children.TryGetValue(name,out var values))return values;if(create){var list=VariableValue.FromArray();root.Children[name]=list;return list;}}
            else if(create){var data=VariableValue.FromStructure();obj.Variables.Set("ObjectLinks",data);var list=VariableValue.FromArray();data.Children[name]=list;return list;}
            return VariableValue.FromArray();
        }
        var target=ObjectArg();var other=ObjectArg("other","Linked object");var relation=TextArg("relation","Relationship name","default");
        Add(r,"links.connect","Object / Relationships","Link two objects",(i,c)=>{var a=ResolveObjectTarget(i,c);var b=Other(i,c);if(a==null||b==null)return;var list=Links(a,S(i,c,"relation","default"),true);if(!list.Items.Any(v=>v.String==b.Id.ToString()))list.Items.Add(VariableValue.FromString(b.Id.ToString()));if(B(i,c,"bidirectional",true)){var reverse=Links(b,S(i,c,"relation","default"),true);if(!reverse.Items.Any(v=>v.String==a.Id.ToString()))reverse.Items.Add(VariableValue.FromString(a.Id.ToString()));}},target,other,relation,BoolArg("bidirectional","Link both ways",true));
        Add(r,"links.disconnect","Object / Relationships","Unlink two objects",(i,c)=>{var a=ResolveObjectTarget(i,c);var b=Other(i,c);if(a==null||b==null)return;Links(a,S(i,c,"relation","default"),false).Items.RemoveAll(v=>v.String==b.Id.ToString());if(B(i,c,"bidirectional",true))Links(b,S(i,c,"relation","default"),false).Items.RemoveAll(v=>v.String==a.Id.ToString());},target,other,relation,BoolArg("bidirectional","Unlink both ways",true));
        Add(r,"links.clear","Object / Relationships","Remove all links of object",(i,c)=>{var obj=ResolveObjectTarget(i,c);if(obj==null)return;obj.Variables.Remove("ObjectLinks");foreach(var candidate in c.Scene.GameObjects)if(candidate.Variables.TryGet("ObjectLinks",out var links))foreach(var list in links!.Children.Values)list.Items.RemoveAll(v=>v.String==obj.Id.ToString());},target);
        Test(r,"links.connected","Object / Relationships","Objects are linked",(i,c)=>ResolveObjectTarget(i,c) is {} a&&Other(i,c) is {} b&&Links(a,S(i,c,"relation","default"),false).Items.Any(v=>v.String==b.Id.ToString()),target,other,relation);
        Test(r,"links.pick","Object / Relationships","Pick linked objects",(i,c)=>{var obj=ResolveObjectTarget(i,c);if(obj==null)return false;var ids=Links(obj,S(i,c,"relation","default"),false).Items.Select(v=>Guid.TryParse(v.String,out var id)?c.Scene.FindGameObject(id):null).Where(o=>o!=null&&o.ActiveInHierarchy).Select(o=>o!.Id).ToList();c.PickedSets[S(i,c,"set","Selection")]=ids;return ids.Count>0;},target,relation,TextArg("set","Selection set","Selection"));
    }
}
