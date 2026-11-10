using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Characters;
namespace ByteEngine.Core.Net;

public static class NetworkRuntime
{
    public static Func<INetworkLink>? CreateLink { get; set; }
    public static NetworkSession? Session { get; private set; }
    static readonly Dictionary<int, INetworkLink> links = []; static int nextLink;
    public static event Action<int, NetworkSignal>? Signal;
    public static string[] IceServers { get; set; } = [];
    public static Func<PlayerInput>? InputProvider {get;set;}
    public static IReadOnlyCollection<int> Connections=>links.Keys;
    public static void CloseConnection(int id){if(links.Remove(id,out var link))Session?.CloseLink(link);}
    public static void Start(bool host, NetworkCompatibility compatibility) { Stop(); Session = new(host, compatibility);Session.LinkClosed+=link=>{foreach(var id in links.Where(p=>ReferenceEquals(p.Value,link)).Select(p=>p.Key).ToArray())links.Remove(id);}; }
    public static int OpenConnection(bool offerer) { if (Session == null) throw new InvalidOperationException("Start a session first"); var link = CreateLink?.Invoke() ?? throw new InvalidOperationException("This host has no network transport"); int id = ++nextLink; links.Add(id, link); link.Signal += s => Signal?.Invoke(id, s); try{Session.AddLink(link,offerer);}catch{links.Remove(id);link.Dispose();throw;}return id; }
    public static void Accept(int connection, NetworkSignal signal) { if (!links.TryGetValue(connection, out var link)) throw new ArgumentException("Unknown connection"); link.Accept(signal); }
    public static void Update(double seconds){try{Session?.Update(seconds);}catch(Exception e){NetworkDiagnostics.Record(e.ToString(),true);Session?.Dispose();}}
    public static void Stop() { Session?.Dispose(); Session = null; links.Clear(); }
}

/// <summary>Opt-in multiplayer authoring settings. Transport connections are opened explicitly through the lobby/API.</summary>
public sealed class NetworkSettings : Component
{
    public string GameVersion { get; set; } = "1";
    public string PluginVersion { get; set; } = "1";
    public string SceneVersion { get; set; } = "1";
    public float MovementSpeed { get; set; } = 6;
    public string IceServers { get; set; } = "stun:stun.l.google.com:19302";
    public NetworkCompatibility Compatibility() => new(NetworkBuildIdentity.Value + ":" + GameVersion, PluginVersion+":"+NetworkContentIdentity.Plugins(GameObject.Scene?.ProjectRoot), (GameObject.Scene?.NetworkSceneIdentity??GameObject.Scene?.Id.ToString()??"")+":"+SceneVersion);
    public void StartSession(bool host)
    {
        if (GameObject.Scene is { } authoring)
        {
            if (authoring.GameObjects.Count(o => o.GetComponent<NetworkSettings>() != null) != 1) throw new InvalidOperationException("A network scene requires exactly one Network Settings component");
            var identities = authoring.GameObjects.Select(o => o.GetComponent<NetworkIdentity>()).Where(i => i != null).ToArray();
            if(!identities.Any(i=>i!.PeerId==0))throw new InvalidOperationException("A host avatar in peer slot0 is required");
            if (identities.Any(i => i!.PeerId < 0 || i.PeerId >= NetworkSession.MaxPeers) || identities.GroupBy(i => i!.PeerId).Any(g => g.Count() > 1)) throw new InvalidOperationException("Avatar peer slots must be unique values0–3");
            if (identities.Any(i => i!.GameObject.GetComponent<NetworkTransform>() == null)) throw new InvalidOperationException("Every Network Identity requires Network Transform");
        }
        NetworkRuntime.IceServers = IceServers.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        NetworkRuntime.Start(host, Compatibility()); var session = NetworkRuntime.Session!; session.Motor.Speed = Math.Clamp(MovementSpeed, .1f, 100);
        session.ReadInput = NetworkRuntime.InputProvider?? (() => { var movement = new Vector2((Input.IsKeyDown(Key.D) ? 1 : 0) - (Input.IsKeyDown(Key.A) ? 1 : 0), (Input.IsKeyDown(Key.S) ? 1 : 0) - (Input.IsKeyDown(Key.W) ? 1 : 0)); if (movement.LengthSquared() > 1) movement = Vector2.Normalize(movement); return new(0, 0, movement, Vector2.Zero, 0); });
        if (GameObject.Scene is { } scene)
        {
            foreach (var obj in scene.GameObjects) if (obj.GetComponent<NetworkIdentity>() is { } identity) session.SetSpawnPosition(identity.PeerId, obj.Transform.WorldPosition);
            session.Motor.ConstrainPlayerMovement = (id, from, to) =>
            {
                var actor = scene.GameObjects.FirstOrDefault(o => o.GetComponent<NetworkIdentity>()?.PeerId == id); var delta = to - from; float distance = delta.Length(); if (distance < .00001f) return from;
                return GameplayQuery3D.CapsuleCast(scene, from + Vector3.UnitY * .35f, from + Vector3.UnitY * 1.4f, delta / distance, .3f, out var hit, distance + .02f, ignore: actor, source: actor, includeTriggers: false) ? from + delta / distance * Math.Max(0, Math.Min(distance, hit.Distance - .02f)) : to;
            };
        }
    }
    protected override void OnStop() => NetworkRuntime.Stop();
    protected override void OnDestroy() => NetworkRuntime.Stop();
}
public sealed class NetworkIdentity : Component
{
    public int PeerId { get; set; }
    public bool IsLocallyOwned => NetworkRuntime.Session is { } s && s.LocalPeerId == PeerId;
}
/// <summary>Maps a pre-authored avatar to authoritative session state; remote views interpolate.</summary>
public sealed class NetworkTransform : Component
{
    public float InterpolationRate { get; set; } = 20;
    readonly Dictionary<Component, bool> disabled = []; readonly Dictionary<Component, bool> visibility = []; int cameraOwner = int.MinValue; Rigidbody3D? body; RigidbodyBodyType3D previousBody; bool active, placed;
    protected override void OnUpdate()
    {
        var session = NetworkRuntime.Session; var identity = GameObject.GetComponent<NetworkIdentity>();
        if (session is not { IsActive: true } || identity == null) { Restore(); return; }
        if (!active)
        {
            active = true; body = GameObject.GetComponent<Rigidbody3D>(); if (body != null) { previousBody = body.BodyType; body.BodyType = RigidbodyBodyType3D.Kinematic; }
            foreach (var c in new Component?[] { GameObject.GetComponent<CharacterController3D>(), GameObject.GetComponent<PlayerController3D>() }) if (c != null) { disabled[c] = c.Enabled; c.Enabled = false; }
        }
        bool present = session.Players.ContainsKey(identity.PeerId);
        void Visit(GameObject item)
        {
            foreach (var c in item.Components)
            {
                if (c is ByteEngine.Core.Graphics.ThreeD.MeshRenderer or Collider3D) { if (!visibility.ContainsKey(c)) visibility[c] = c.Enabled; c.Enabled = present && visibility[c]; }
                if (session.LocalPeerId >= 0 && cameraOwner != session.LocalPeerId && c is ByteEngine.Core.Graphics.Camera3D) { if (!disabled.ContainsKey(c)) disabled[c] = c.Enabled; c.Enabled = identity.IsLocallyOwned && disabled[c]; }
            }
            foreach (var child in item.Children) Visit(child);
        }
        Visit(GameObject); if (session.LocalPeerId >= 0) cameraOwner = session.LocalPeerId;
        if (!session.Players.TryGetValue(identity.PeerId, out var state)) return;
        var target = identity.IsLocallyOwned && !session.IsHost ? session.PredictedLocal.Position+session.PredictionErrorOffset : state.Position;
        Transform.WorldPosition = identity.IsLocallyOwned || session.IsHost || !placed ? target : Vector3.Lerp(Transform.WorldPosition, target, 1 - MathF.Exp(-Math.Clamp(InterpolationRate, 1, 100) * (float)Time.DeltaTime)); placed = true;
    }
    void Restore() { if (!active) return; foreach (var pair in disabled) pair.Key.Enabled = pair.Value; disabled.Clear(); foreach (var pair in visibility) pair.Key.Enabled = pair.Value; visibility.Clear(); cameraOwner = int.MinValue; if (body != null) body.BodyType = previousBody; active = false; placed = false; }
    protected override void OnStop() => Restore(); protected override void OnDestroy() => Restore();
}
