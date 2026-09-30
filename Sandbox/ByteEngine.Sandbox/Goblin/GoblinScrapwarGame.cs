using System.Numerics;
using BepuPhysics;
using ByteEngine.Core;
using ByteEngine.Core.Construction;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Sandbox.Goblin;

/// <summary>Playable whitebox of the build / smash / earn loop.</summary>
public sealed class GoblinScrapwarGame : ByteEngineApplication
{
    private readonly Random _random = new(73);
    private readonly AssemblyGraph<string> _assembly = new();
    private readonly SwarmHorde _horde = new(600);
    private readonly SwarmHorde _scrapDrops = new(600);
    private readonly SwarmFlowField _greenFlow = new(48, 32, 1, new Vector2(-24, -16));
    private readonly SwarmFlowField _redFlow = new(48, 32, 1, new Vector2(-24, -16));
    private GoblinPhysicsWorld? _physics;
    private AssemblyIntegrity<string>? _integrity;
    private AssemblyEventBus<string>? _events;
    private Scene? _scene;
    private GameObject? _camera;
    private Camera3D? _cameraComponent;
    private GameObject? _chassis;
    private GameObject? _armor;
    private GameObject? _sawblade;
    private GameObject? _catapult;
    private GameObject? _buildGhost;
    private GameObject? _ghostBox;
    private GameObject? _ghostSaw;
    private UiText? _statusText;
    private UiText? _controlsText;
    private ImpactParticles3D? _particles;
    private GameObject? _watchtower;
    private StaticHandle _towerBody;
    private Guid _frontSocket;
    private Guid _armorSawSocket;
    private Guid _topSocket;
    private int _scrap;
    private int _greenBase = 100, _redBase = 100, _brokenParts;
    private float _respawnTimer, _hudTimer;
    private float _hitStop, _hitStopCooldown, _cameraShake;
    private Vector3 _previousChassis;
    private bool _armorAttached, _towerDestroyed;
    private bool _sawAttached;
    private bool _catapultAttached;
    private int _buildSelection = 1;
    private Vector3 _previousSaw;
    private readonly List<Projectile> _projectiles = new();
    private float _fireCooldown;
    private bool _building, _socketHovered;
    private bool _matchEnded;
    private static readonly string BlueprintPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ByteEngine", "GoblinScrapwar", "machine.json");

    public GoblinScrapwarGame() : base(1280, 720, "Goblin Scrapwar") { }
    protected override bool CloseOnEscape => false;

    protected override void OnEngineStart()
    {
        _scene = new Scene("Goblin Scrapwar");
        _physics = new GoblinPhysicsWorld();
        AddBoxVisual("Battlefield", new Vector3(0, -.05f, 0),
            new Vector3(48, .1f, 32), new Vector4(.22f, .27f, .22f, 1));
        AddBoxVisual("Green Base", new Vector3(-22, .8f, 0),
            new Vector3(1.2f, 1.6f, 5), new Vector4(.1f, .6f, .18f, 1));
        AddBoxVisual("Red Base", new Vector3(22, .8f, 0),
            new Vector3(1.2f, 1.6f, 5), new Vector4(.7f, .1f, .1f, 1));
        _watchtower = AddBoxVisual("Iron Watchtower", new Vector3(0, 2f, -9),
            new Vector3(1.5f, 4, 1.5f), new Vector4(.42f, .45f, .49f, 1));
        _towerBody = _physics.AddObstacle(_watchtower.Transform.WorldPosition,
            new Vector3(1.5f, 4, 1.5f));

        GameObject light = _scene.CreateGameObject("Sun");
        light.Transform.EulerAngles = new Vector3(50, -35, 0);
        light.AddComponent(new DirectionalLight { Intensity = 1.1f, AmbientIntensity = .55f });
        _camera = _scene.CreateGameObject("Camera");
        _cameraComponent = _camera.AddComponent(new Camera3D
        {
            FieldOfView = 55,
            ActiveGameCamera = true
        });

        BuildInitialMachine();
        _previousChassis = _chassis!.Transform.WorldPosition;
        _integrity = new AssemblyIntegrity<string>(_assembly, _physics, _chassis.Id);
        _events = new AssemblyEventBus<string>(_assembly, _integrity);
        _events.Published += item =>
        {
            if (item.Kind == AssemblyEventKind.JointBroken)
            {
                _brokenParts++;
                ImpactFeedback(.75f);
                if (_physics != null)
                    _particles?.Emit(_physics.Position(item.PartId), 35, 1.2f);
            }
            if (item.Kind == AssemblyEventKind.Detached)
            {
                Console.WriteLine($"Machine lost {item.DetachedParts?.Count ?? 0} part(s)!");
                if (_armor != null && item.DetachedParts?.Contains(_armor.Id) == true)
                    _armorAttached = false;
                if (_sawblade != null && item.DetachedParts?.Contains(_sawblade.Id) == true)
                    _sawAttached = false;
                if (_catapult != null && item.DetachedParts?.Contains(_catapult.Id) == true)
                    _catapultAttached = false;
            }
        };

        SetTowerFlowObstacle(true);
        for (int i = 0; i < 250; i++)
        {
            Spawn(0);
            Spawn(1);
        }
        _horde.Squished += drop =>
        {
            DropScrap(drop.Position);
            ImpactFeedback(.18f);
            _particles?.Emit(new Vector3(drop.Position.X, .15f, drop.Position.Y), 8, .7f);
        };
        _scrapDrops.Squished += _ => _scrap++;
        _scene.CreateGameObject("Pooled Goblin Crowd").AddComponent(
            new SwarmRenderer3D { Horde = _horde });
        _scene.CreateGameObject("Pooled Scrap Pickups").AddComponent(
            new SwarmRenderer3D
            {
                Horde = _scrapDrops,
                Team0Color = new Vector4(1, .72f, .12f, 1),
                UnitRadius = .13f,
                UnitHeight = .25f
            });
        _particles = _scene.CreateGameObject("Pooled Impact Debris")
            .AddComponent(new ImpactParticles3D());
        GameObject hudCanvas = _scene.CreateGameObject("Scrapwar HUD");
        hudCanvas.AddComponent(new UiCanvas());
        GameObject status = _scene.CreateGameObject("Battle Status");
        status.SetParent(hudCanvas, false);
        _statusText = status.AddComponent(new UiText
        {
            FontSize = 23,
            Color = new Vector4(1, .95f, .72f, 1),
            ShadowColor = new Vector4(0, 0, 0, .9f),
            Text = "GOBLIN SCRAPWAR"
        });
        GameObject controls = _scene.CreateGameObject("Battle Controls");
        controls.SetParent(hudCanvas, false);
        _controlsText = controls.AddComponent(new UiText
        {
            FontSize = 18,
            Anchor = UiAnchor.BottomLeft,
            Offset = new Vector2(24, -64),
            Color = new Vector4(.88f, .96f, 1, 1),
            ShadowColor = new Vector4(0, 0, 0, .9f),
            Text = "WASD drive | Tab build | F5 save | F9 load"
        });

        UpdateCamera(immediate: true);
        Scenes.LoadScene(_scene);
        _scene.SetActiveCamera(_cameraComponent);
        CaptureGameInput();
        Console.WriteLine("Goblin Scrapwar: WASD drive/steer. Tab builds; 1 armor (5), 2 powered saw (12), 3 catapult (15). Point and click the glowing socket.");
        Console.WriteLine("Space fires catapult stones when installed.");
        Console.WriteLine("F5 saves a machine blueprint; F9 loads its armor configuration; Esc releases mouse.");
        Console.WriteLine("Ram red goblins for scrap. Hit the iron watchtower at speed for a large reward.");
    }

    private void BuildInitialMachine()
    {
        _chassis = AddBoxVisual("Cockpit", new Vector3(0, 1.05f, 5),
            new Vector3(1.5f, .45f, 2.2f), new Vector4(.45f, .25f, .1f, 1));
        _frontSocket = Guid.NewGuid();
        Vector3[] offsets =
        {
            new(-.95f, -.52f, -.75f), new(.95f, -.52f, -.75f),
            new(-.95f, -.52f, .75f), new(.95f, -.52f, .75f)
        };
        var sockets = offsets.Select(offset => new AssemblySocket(Guid.NewGuid(),
            "WheelAxle", Matrix4x4.CreateTranslation(offset))).ToList();
        sockets.Add(new AssemblySocket(_frontSocket, "Universal",
            Matrix4x4.CreateTranslation(0, 0, -1.15f)));
        _topSocket = Guid.NewGuid();
        sockets.Add(new AssemblySocket(_topSocket, "Universal",
            Matrix4x4.CreateTranslation(0, .3f, 0)));
        _assembly.Add(new AssemblyPart<string>(_chassis.Id, "Cockpit", sockets));
        _physics!.AddBox(_chassis.Id, _chassis, new Vector3(1.5f, .45f, 2.2f), 14);
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3 position = _chassis.Transform.WorldPosition + offsets[i];
            GameObject wheel = AddSphereVisual($"Wheel {i + 1}", position, .46f,
                new Vector4(.12f, .12f, .13f, 1));
            Guid socket = Guid.NewGuid();
            _assembly.Add(new AssemblyPart<string>(wheel.Id, "Wheel",
                new[] { new AssemblySocket(socket, "WheelAxle", Matrix4x4.Identity) }));
            _physics.AddWheel(wheel.Id, wheel, .46f, .32f, 2);
            _assembly.Connect(_chassis.Id, sockets[i].Id, wheel.Id, socket,
                breakForce: 900, breakTorque: 900);
        }
    }

    protected override void OnEngineUpdate()
    {
        if (_physics == null || _integrity == null || _chassis == null || _scene == null) return;
        float dt = Math.Clamp((float)Time.DeltaTime, 0, .1f);
        if (Input.IsKeyPressed(Key.Tab)) ToggleBuilding();
        if (_building && Input.IsKeyPressed(Key.D1)) _buildSelection = 1;
        if (_building && Input.IsKeyPressed(Key.D2)) _buildSelection = 2;
        if (_building && Input.IsKeyPressed(Key.D3)) _buildSelection = 3;
        if (Input.IsKeyPressed(Key.F5)) SaveBlueprint();
        if (Input.IsKeyPressed(Key.F9)) LoadBlueprint();
        _hitStopCooldown = Math.Max(0, _hitStopCooldown - dt);
        _cameraShake = Math.Max(0, _cameraShake - dt * 2.5f);
        if (_hitStop > 0)
        {
            _hitStop = Math.Max(0, _hitStop - dt);
            UpdateCamera(immediate: false);
            return;
        }
        if (!_building && !IsGameInputCaptured && Input.IsMouseButtonPressed(MouseButton.Left))
            CaptureGameInput();
        if (_building) UpdateBuildCursor();
        float drive = _building ? 0 :
            (Input.IsKeyDown(Key.W) ? 1 : 0) - (Input.IsKeyDown(Key.S) ? 1 : 0);
        float steer = _building ? 0 :
            (Input.IsKeyDown(Key.A) ? 1 : 0) - (Input.IsKeyDown(Key.D) ? 1 : 0);
        _physics.SetWheelSpeed(drive * 12f);
        _physics.SetSawSpeed(_sawAttached ? 24f : 0f);
        _physics.SetSteering(_chassis.Id, steer * 1.4f);
        _fireCooldown = Math.Max(0, _fireCooldown - dt);
        if (!_building && _catapultAttached && Input.IsKeyPressed(Key.Space) &&
            _fireCooldown <= 0) FireCatapult();
        _physics.Step(dt);
        _integrity.Evaluate();
        Vector3 current = _physics.Position(_chassis.Id);
        Vector3 velocity = _physics.Velocity(_chassis.Id);
        float speed = new Vector2(velocity.X, velocity.Z).Length();
        _horde.Step(_greenFlow, _redFlow, 1.55f, dt);
        _horde.ResolveClashes(.38f, 24);
        _horde.Squish(new Vector2(_previousChassis.X, _previousChassis.Z),
            new Vector2(current.X, current.Z), 1.15f, speed, 2f, 1);
        if (_sawAttached && _sawblade != null)
        {
            Vector3 sawPosition = _physics.Position(_sawblade.Id);
            _horde.Squish(new Vector2(_previousSaw.X, _previousSaw.Z),
                new Vector2(sawPosition.X, sawPosition.Z), .65f,
                Math.Max(speed, 4f), 2f, 1);
            _previousSaw = sawPosition;
        }
        UpdateProjectiles(dt);
        _scrapDrops.Squish(new Vector2(_previousChassis.X, _previousChassis.Z),
            new Vector2(current.X, current.Z), 1.15f, speed, 0, 0);
        _previousChassis = current;
        ResolveBases();
        _respawnTimer += dt;
        if (!_matchEnded && _respawnTimer >= .25f)
        {
            _respawnTimer = 0;
            for (int i = 0; i < 4 && _horde.Count < 500; i++)
            {
                Spawn(0);
                if (_horde.Count < 500) Spawn(1);
            }
        }
        if (!_towerDestroyed && _watchtower != null &&
            TowerTouched(current, new Vector3(.75f, .225f, 1.1f)) && speed >= 4f)
            SmashTower(current);
        UpdateCamera(immediate: false);
        _hudTimer += dt;
        if (_hudTimer >= .2f)
        {
            _hudTimer = 0;
            Title = $"Goblin Scrapwar | Green {_greenBase} - Red {_redBase} | Scrap {_scrap} | " +
                $"Parts broken {_brokenParts} | " +
                (_building ? $"BUILD: {(_socketHovered ? "CLICK TO PLACE" : "AIM AT SOCKET")} | 1 armor, 2 saw, 3 catapult" :
                "Tab: build | Space: fire");
            if (_statusText != null)
                _statusText.Text = (_matchEnded
                    ? _redBase == 0 ? "GREEN GOBLINS WIN!  " : "RED GOBLINS WIN!  "
                    : string.Empty) +
                    $"GREEN {_greenBase}  |  RED {_redBase}  |  SCRAP {_scrap}  |  " +
                    $"GOBLINS {_horde.Count}  |  DROPS {_scrapDrops.Count}  |  BROKEN {_brokenParts}";
            if (_controlsText != null)
                _controlsText.Text = _building
                    ? _socketHovered
                        ? $"Click socket: install {BuildSelectionName()} | 1/2/3 select"
                        : "Point at glowing socket | 1 armor, 2 saw, 3 catapult | Tab drive"
                    : "WASD drive | Space fire | Tab build | F5 save | F9 load";
        }
    }

    private void DropScrap(Vector2 position)
    {
        if (_scrapDrops.Count < _scrapDrops.Capacity)
        {
            float angle = (float)_random.NextDouble() * MathF.Tau;
            float radius = 1.1f + (float)_random.NextDouble() * .8f;
            _scrapDrops.Spawn(position +
                new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius, 0);
        }
    }

    private void ImpactFeedback(float strength)
    {
        _cameraShake = Math.Min(1.2f, _cameraShake + strength);
        if (_hitStopCooldown <= 0)
        {
            _hitStop = Math.Max(_hitStop, MathF.Min(.055f, strength * .06f));
            _hitStopCooldown = .2f;
        }
    }

    private void SmashTower(Vector3 impact)
    {
        if (_towerDestroyed || _physics == null || _scene == null || _watchtower == null)
            return;
        _towerDestroyed = true;
        ImpactFeedback(1f);
        _particles?.Emit(impact, 130, 1.8f);
        _physics.RemoveObstacle(_towerBody);
        _scene.DestroyGameObject(_watchtower);
        SetTowerFlowObstacle(false);
        for (int i = 0; i < 20; i++)
        {
            float angle = i * MathF.Tau / 20f;
            DropScrap(new Vector2(impact.X + MathF.Cos(angle) * 1.5f,
                impact.Z + MathF.Sin(angle) * 1.5f));
        }
        Console.WriteLine("Watchtower smashed: 20 scrap pickups scattered!");
    }

    private bool TowerTouched(Vector3 center, Vector3 halfExtent)
    {
        if (_towerDestroyed || _watchtower == null) return false;
        Vector3 delta = center - _watchtower.Transform.WorldPosition;
        return MathF.Abs(delta.X) <= .75f + halfExtent.X &&
            MathF.Abs(delta.Y) <= 2f + halfExtent.Y &&
            MathF.Abs(delta.Z) <= .75f + halfExtent.Z;
    }

    private void FireCatapult()
    {
        if (_physics == null || _catapult == null || _chassis == null || _projectiles.Count >= 20)
            return;
        Quaternion orientation = _chassis.Transform.WorldRotation;
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, orientation);
        Vector3 launch = _catapult.Transform.WorldPosition + forward * .65f + Vector3.UnitY * .55f;
        GameObject stone = AddSphereVisual("Catapult Stone", launch, .23f,
            new Vector4(.27f, .29f, .3f, 1));
        Vector3 velocity = _physics.Velocity(_chassis.Id) +
            forward * 17f + Vector3.UnitY * 8f;
        _physics.AddBall(stone.Id, stone, .23f, 1.2f, velocity);
        _projectiles.Add(new Projectile(stone, launch));
        _particles?.Emit(launch, 12, .8f);
        _fireCooldown = .7f;
        ImpactFeedback(.12f);
    }

    private void UpdateProjectiles(float dt)
    {
        if (_physics == null || _scene == null) return;
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            Projectile projectile = _projectiles[i];
            Vector3 current = _physics.Position(projectile.Visual.Id);
            float speed = _physics.Velocity(projectile.Visual.Id).Length();
            if (Math.Min(projectile.Previous.Y, current.Y) < 1.2f)
                _horde.Squish(new Vector2(projectile.Previous.X, projectile.Previous.Z),
                    new Vector2(current.X, current.Z), .35f, speed, 2f, 1);
            if (!_towerDestroyed && _watchtower != null &&
                TowerTouched(current, new Vector3(.23f)) &&
                speed > 5f)
                SmashTower(current);
            projectile.Previous = current;
            projectile.Age += dt;
            if (projectile.Age < 8f && current.Y > -2f) continue;
            _physics.RemoveBody(projectile.Visual.Id);
            _scene.DestroyGameObject(projectile.Visual);
            _projectiles.RemoveAt(i);
        }
    }

    private sealed class Projectile(GameObject visual, Vector3 previous)
    {
        public GameObject Visual { get; } = visual;
        public Vector3 Previous { get; set; } = previous;
        public float Age { get; set; }
    }

    private void SetTowerFlowObstacle(bool blocked)
    {
        // Tower at world (0,-9), grid origin (-24,-16).
        for (int x = 23; x <= 25; x++)
            for (int z = 6; z <= 8; z++)
            {
                _greenFlow.SetBlocked(x, z, blocked);
                _redFlow.SetBlocked(x, z, blocked);
            }
        _greenFlow.Rebuild(44, 16);
        _redFlow.Rebuild(3, 16);
    }

    private void ResolveBases()
    {
        if (_matchEnded) return;
        for (int slot = 0; slot < _horde.Capacity; slot++)
        {
            if (!_horde.TryGetSlot(slot, out SwarmHandle handle,
                out Vector2 position, out byte team)) continue;
            if (team == 0 && position.X >= 21)
            {
                _horde.Despawn(handle);
                _redBase = Math.Max(0, _redBase - 1);
            }
            else if (team == 1 && position.X <= -21)
            {
                _horde.Despawn(handle);
                _greenBase = Math.Max(0, _greenBase - 1);
            }
        }
        if (_redBase == 0 || _greenBase == 0)
        {
            _matchEnded = true;
            ImpactFeedback(1f);
            Console.WriteLine(_redBase == 0
                ? "Green goblins win the Scrapwar!"
                : "Red goblins win the Scrapwar!");
        }
    }

    private List<AssemblyPlacementBody> CurrentAssemblyBodies()
    {
        var bodies = new List<AssemblyPlacementBody>();
        if (_chassis == null || _physics == null) return bodies;
        IReadOnlySet<Guid> connected = _assembly.Component(_chassis.Id);
        foreach (AssemblyPart<string> part in _assembly.Parts)
        {
            if (!connected.Contains(part.Id)) continue;
            (Vector3 size, float mass) = part.Payload switch
            {
                "Cockpit" => (new Vector3(1.5f, .45f, 2.2f), 14),
                "Wheel" => (new Vector3(.92f, .32f, .92f), 2),
                "Iron Armor" => (new Vector3(1.5f, .55f, .35f), 8),
                "Sawblade" => (new Vector3(1.2f, .18f, 1.2f), 4),
                "Catapult" => (new Vector3(.7f, .35f, 1), 6),
                _ => (Vector3.One, 1)
            };
            Matrix4x4 pose = Matrix4x4.CreateFromQuaternion(_physics.Orientation(part.Id)) *
                Matrix4x4.CreateTranslation(_physics.Position(part.Id));
            bodies.Add(new AssemblyPlacementBody(part.Id, pose, size, mass));
        }
        return bodies;
    }

    private void AttachArmor()
    {
        if (_physics == null || _integrity == null || _chassis == null) return;
        if (!_building) _integrity.SetBuildingMode(true);
        try
        {
            Guid armorSocket = Guid.NewGuid();
            var local = Matrix4x4.CreateTranslation(0, 0, .18f);
            Matrix4x4 chassisPose = Matrix4x4.CreateFromQuaternion(_chassis.Transform.WorldRotation) *
                Matrix4x4.CreateTranslation(_chassis.Transform.WorldPosition);
            Matrix4x4 world = SocketPlacement.Align(chassisPose,
                Matrix4x4.CreateTranslation(0, 0, -1.15f), local, Matrix4x4.Identity);
            if (!Matrix4x4.Decompose(world, out _, out Quaternion rotation, out Vector3 position))
                throw new InvalidOperationException("Armor placement failed.");
            List<AssemblyPlacementBody> bodies = CurrentAssemblyBodies();
            var candidate = new AssemblyPlacementBody(Guid.NewGuid(),
                Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position),
                new Vector3(1.5f, .55f, .35f), 8);
            string reason = string.Empty;
            if (!_assembly.IsSocketFree(_frontSocket) ||
                !AssemblyPlacementRules.Validate(candidate, bodies, 50, out reason))
            {
                Console.WriteLine($"Cannot place armor: {(reason.Length == 0 ? "Socket occupied." : reason)}");
                return;
            }
            GameObject armor = AddBoxVisual("Iron Armor", position,
                candidate.Size, new Vector4(.55f, .56f, .58f, 1));
            _armor = armor;
            armor.Transform.WorldPosition = position;
            armor.Transform.WorldRotation = rotation;
            _armorSawSocket = Guid.NewGuid();
            _events!.Add(new AssemblyPart<string>(armor.Id, "Iron Armor",
                new[]
                {
                    new AssemblySocket(armorSocket, "Universal", local),
                    new AssemblySocket(_armorSawSocket, "SawAxle",
                        Matrix4x4.CreateTranslation(0, 0, -.85f))
                }));
            _physics.AddBox(armor.Id, armor, new Vector3(1.5f, .55f, .35f), 8);
            _assembly.Connect(_chassis.Id, _frontSocket, armor.Id, armorSocket,
                breakForce: 180, breakTorque: 180);
            _scrap -= 5;
            _armorAttached = true;
            Console.WriteLine("Iron armor bolted on.");
        }
        finally { if (!_building) _integrity.SetBuildingMode(false); }
    }

    private void AttachSawblade()
    {
        if (_physics == null || _integrity == null || _armor == null || !_armorAttached) return;
        if (!_building) _integrity.SetBuildingMode(true);
        try
        {
            if (!_assembly.IsSocketFree(_armorSawSocket)) return;
            Matrix4x4 armorPose = Matrix4x4.CreateFromQuaternion(_armor.Transform.WorldRotation) *
                Matrix4x4.CreateTranslation(_armor.Transform.WorldPosition);
            Matrix4x4 world = SocketPlacement.Align(armorPose,
                Matrix4x4.CreateTranslation(0, 0, -.85f),
                Matrix4x4.Identity, Matrix4x4.Identity);
            if (!Matrix4x4.Decompose(world, out _, out _, out Vector3 position))
                throw new InvalidOperationException("Saw placement failed.");
            Matrix4x4 candidatePose = Matrix4x4.CreateTranslation(position);
            if (!AssemblyPlacementRules.Validate(
                new AssemblyPlacementBody(Guid.NewGuid(), candidatePose,
                    new Vector3(.18f, 1.2f, 1.2f), 4),
                CurrentAssemblyBodies(), 50, out string reason))
            {
                Console.WriteLine($"Cannot place saw: {reason}");
                return;
            }
            GameObject saw = AddSphereVisual("Powered Sawblade", position, .6f,
                new Vector4(.72f, .74f, .78f, 1));
            // AddWheel rotates cylinder-local Y onto the world axle X.
            saw.Transform.LocalScale = new Vector3(1.2f, .18f, 1.2f);
            _sawblade = saw;
            Guid sawSocket = Guid.NewGuid();
            _events!.Add(new AssemblyPart<string>(saw.Id, "Sawblade",
                new[] { new AssemblySocket(sawSocket, "SawAxle", Matrix4x4.Identity) }));
            _physics.AddWheel(saw.Id, saw, .6f, .18f, 4);
            _assembly.Connect(_armor.Id, _armorSawSocket, saw.Id, sawSocket,
                breakForce: 220, breakTorque: 220);
            _previousSaw = position;
            _scrap -= 12;
            _sawAttached = true;
            Console.WriteLine("Powered sawblade installed. It spins automatically while attached.");
        }
        finally { if (!_building) _integrity.SetBuildingMode(false); }
    }

    private void AttachCatapult()
    {
        if (_physics == null || _integrity == null || _chassis == null) return;
        if (!_building) _integrity.SetBuildingMode(true);
        try
        {
            if (!_assembly.IsSocketFree(_topSocket)) return;
            Matrix4x4 chassisPose = Matrix4x4.CreateFromQuaternion(_chassis.Transform.WorldRotation) *
                Matrix4x4.CreateTranslation(_chassis.Transform.WorldPosition);
            Matrix4x4 mountLocal = Matrix4x4.CreateTranslation(0, -.2f, 0);
            Matrix4x4 world = SocketPlacement.Align(chassisPose,
                Matrix4x4.CreateTranslation(0, .3f, 0),
                mountLocal, Matrix4x4.Identity);
            if (!Matrix4x4.Decompose(world, out _, out Quaternion rotation, out Vector3 position))
                throw new InvalidOperationException("Catapult placement failed.");
            if (!AssemblyPlacementRules.Validate(
                new AssemblyPlacementBody(Guid.NewGuid(), world, new Vector3(.7f, .35f, 1), 6),
                CurrentAssemblyBodies(), 50, out string reason))
            {
                Console.WriteLine($"Cannot place catapult: {reason}");
                return;
            }
            GameObject catapult = AddBoxVisual("Scrap Catapult", position,
                new Vector3(.7f, .35f, 1), new Vector4(.5f, .28f, .12f, 1));
            catapult.Transform.WorldRotation = rotation;
            _catapult = catapult;
            Guid socket = Guid.NewGuid();
            _events!.Add(new AssemblyPart<string>(catapult.Id, "Catapult",
                new[] { new AssemblySocket(socket, "Universal", mountLocal) }));
            _physics.AddBox(catapult.Id, catapult, new Vector3(.7f, .35f, 1), 6);
            _assembly.Connect(_chassis.Id, _topSocket, catapult.Id, socket,
                breakForce: 250, breakTorque: 250);
            _scrap -= 15;
            _catapultAttached = true;
            Console.WriteLine("Catapult installed. Press Space to fire physical stones.");
        }
        finally { if (!_building) _integrity.SetBuildingMode(false); }
    }

    private void RemoveArmor()
    {
        if (_armor == null || _physics == null || _scene == null) return;
        Guid id = _armor.Id;
        _events!.Remove(id);
        if (_sawblade != null && _sawAttached) RemoveSawblade();
        _physics.RemoveBody(id);
        _scene.DestroyGameObject(_armor);
        _armor = null;
        _armorAttached = false;
    }

    private void RemoveSawblade()
    {
        if (_sawblade == null || _physics == null || _scene == null) return;
        Guid id = _sawblade.Id;
        _events!.Remove(id);
        _physics.RemoveBody(id);
        _scene.DestroyGameObject(_sawblade);
        _sawblade = null;
        _sawAttached = false;
    }

    private void RemoveCatapult()
    {
        if (_catapult == null || _physics == null || _scene == null) return;
        Guid id = _catapult.Id;
        _events!.Remove(id);
        _physics.RemoveBody(id);
        _scene.DestroyGameObject(_catapult);
        _catapult = null;
        _catapultAttached = false;
    }

    private void ToggleBuilding()
    {
        if (_integrity == null || _scene == null) return;
        _building = !_building;
        _integrity.SetBuildingMode(_building);
        if (_building)
        {
            ReleaseGameInput();
            _buildGhost ??= AddSphereVisual("Front Socket Build Marker",
                new Vector3(0, -1000, 0), .18f, new Vector4(.1f, .9f, .9f, 1));
            if (_ghostBox == null)
            {
                _ghostBox = AddBoxVisual("Build Ghost Box", Vector3.Zero,
                    new Vector3(1.5f, .55f, .35f), new Vector4(.1f, .9f, .9f, .38f));
                _ghostBox.GetComponent<MeshRenderer>()!.Material.BlendMode = BlendMode3D.AlphaBlend;
                _ghostBox.Active = false;
            }
            if (_ghostSaw == null)
            {
                _ghostSaw = AddSphereVisual("Build Ghost Saw", Vector3.Zero, .6f,
                    new Vector4(.1f, .9f, .9f, .38f));
                _ghostSaw.GetComponent<MeshRenderer>()!.Material.BlendMode = BlendMode3D.AlphaBlend;
                _ghostSaw.Active = false;
            }
        }
        else
        {
            if (_buildGhost != null) _buildGhost.Transform.WorldPosition = new Vector3(0, -1000, 0);
            if (_ghostBox != null) _ghostBox.Active = false;
            if (_ghostSaw != null) _ghostSaw.Active = false;
            CaptureGameInput();
        }
    }

    private void UpdateBuildCursor()
    {
        if (_chassis == null || _cameraComponent == null || _buildGhost == null) return;
        bool sawChoice = _buildSelection == 2;
        bool catapultChoice = _buildSelection == 3;
        GameObject? target = sawChoice ? _armor : _chassis;
        bool available = sawChoice ? _armorAttached && !_sawAttached :
            catapultChoice ? !_catapultAttached : !_armorAttached;
        Vector3 socketWorld = target == null ? new Vector3(0, -1000, 0) :
            target.Transform.WorldPosition + Vector3.Transform(
                sawChoice ? new Vector3(0, 0, -.85f) :
                catapultChoice ? new Vector3(0, .3f, 0) :
                new Vector3(0, 0, -1.15f),
                target.Transform.WorldRotation);
        _buildGhost.Transform.WorldPosition = socketWorld;
        if (_ghostBox != null) _ghostBox.Active = available && !sawChoice;
        if (_ghostSaw != null) _ghostSaw.Active = available && sawChoice;
        if (available && target != null)
        {
            Matrix4x4 targetPose = Matrix4x4.CreateFromQuaternion(target.Transform.WorldRotation) *
                Matrix4x4.CreateTranslation(target.Transform.WorldPosition);
            Matrix4x4 targetSocket = Matrix4x4.CreateTranslation(
                sawChoice ? new Vector3(0, 0, -.85f) :
                catapultChoice ? new Vector3(0, .3f, 0) :
                new Vector3(0, 0, -1.15f));
            Matrix4x4 movingSocket = Matrix4x4.CreateTranslation(
                catapultChoice ? new Vector3(0, -.2f, 0) :
                sawChoice ? Vector3.Zero : new Vector3(0, 0, .18f));
            Matrix4x4 ghostPose = SocketPlacement.Align(targetPose, targetSocket,
                movingSocket, Matrix4x4.Identity);
            if (Matrix4x4.Decompose(ghostPose, out _, out Quaternion rotation,
                    out Vector3 position))
            {
                GameObject preview = sawChoice ? _ghostSaw! : _ghostBox!;
                preview.Transform.WorldPosition = position;
                preview.Transform.WorldRotation = rotation;
                preview.Transform.LocalScale = sawChoice
                    ? new Vector3(.18f, 1.2f, 1.2f)
                    : catapultChoice ? new Vector3(.7f, .35f, 1)
                    : new Vector3(1.5f, .55f, .35f);
            }
        }
        _socketHovered = false;
        if (Input.IsPointerOverGameView && available)
        {
            (Vector3 origin, Vector3 direction) = _cameraComponent.ScreenPointToRay(
                Input.GameViewPointerNormalized, (float)WindowWidth / Math.Max(1, WindowHeight));
            float along = Vector3.Dot(socketWorld - origin, direction);
            Vector3 closest = origin + direction * Math.Max(0, along);
            _socketHovered = along > 0 && Vector3.DistanceSquared(closest, socketWorld) < .45f * .45f;
        }
        if (_socketHovered && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            int cost = sawChoice ? 12 : catapultChoice ? 15 : 5;
            if (_scrap < cost) Console.WriteLine($"Need {cost} scrap for this part.");
            else if (sawChoice) AttachSawblade();
            else if (catapultChoice) AttachCatapult();
            else AttachArmor();
        }
    }

    private string BuildSelectionName() => _buildSelection switch
    {
        2 => "powered saw (12 scrap)",
        3 => "catapult (15 scrap)",
        _ => "iron armor (5 scrap)"
    };

    private void SaveBlueprint()
    {
        if (_chassis == null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BlueprintPath)!);
            File.WriteAllText(BlueprintPath, AssemblyBlueprint<string>.Capture(
                _assembly, _chassis.Id).ToJson());
            Console.WriteLine($"Saved machine blueprint: {BlueprintPath}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"Machine blueprint could not be saved: {error.Message}");
        }
    }

    private void LoadBlueprint()
    {
        if (!File.Exists(BlueprintPath))
        {
            Console.WriteLine("No saved machine blueprint yet; press F5 first.");
            return;
        }
        try
        {
            AssemblyBlueprint<string> saved = AssemblyBlueprint<string>.FromJson(
                File.ReadAllText(BlueprintPath));
            IReadOnlySet<Guid> connected = saved.Instantiate().Component(saved.Core);
            bool wantsArmor = saved.Parts.Any(part =>
                part.Payload == "Iron Armor" && connected.Contains(part.Id));
            bool wantsSaw = saved.Parts.Any(part =>
                part.Payload == "Sawblade" && connected.Contains(part.Id));
            bool wantsCatapult = saved.Parts.Any(part =>
                part.Payload == "Catapult" && connected.Contains(part.Id));
            if (!wantsSaw && _sawAttached) RemoveSawblade();
            if (!wantsCatapult && _catapultAttached) RemoveCatapult();
            if (!wantsArmor && _armorAttached) RemoveArmor();
            if (wantsArmor && !_armorAttached)
            {
                int originalScrap = _scrap;
                _scrap = Math.Max(_scrap, 5);
                AttachArmor();
                _scrap = originalScrap;
            }
            if (wantsSaw && !_sawAttached && _armorAttached)
            {
                int originalScrap = _scrap;
                _scrap = Math.Max(_scrap, 12);
                AttachSawblade();
                _scrap = originalScrap;
            }
            if (wantsCatapult && !_catapultAttached)
            {
                int originalScrap = _scrap;
                _scrap = Math.Max(_scrap, 15);
                AttachCatapult();
                _scrap = originalScrap;
            }
            Console.WriteLine("Loaded supported armor/saw/catapult build state from blueprint.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or
            System.Text.Json.JsonException or
            InvalidOperationException or ArgumentException)
        {
            Console.WriteLine($"Machine blueprint could not be loaded: {error.Message}");
        }
    }

    private void Spawn(byte team)
    {
        float x = team == 0 ? -18f - (float)_random.NextDouble() * 3 :
            18f + (float)_random.NextDouble() * 3;
        float z = ((float)_random.NextDouble() - .5f) * 23f;
        _horde.Spawn(new Vector2(x, z), team);
    }

    private void UpdateCamera(bool immediate)
    {
        if (_camera == null || _chassis == null) return;
        Vector3 target = _chassis.Transform.WorldPosition;
        Vector3 desired = target + new Vector3(0, 19, 20);
        if (_cameraShake > 0)
            desired += new Vector3(
                ((float)_random.NextDouble() - .5f) * _cameraShake,
                ((float)_random.NextDouble() - .5f) * _cameraShake,
                0);
        float amount = immediate ? 1 : 1 - MathF.Exp(-5f * (float)Time.DeltaTime);
        _camera.Transform.WorldPosition = Vector3.Lerp(_camera.Transform.WorldPosition, desired, amount);
        Matrix4x4.Invert(Matrix4x4.CreateLookAt(_camera.Transform.WorldPosition,
            target, Vector3.UnitY), out Matrix4x4 cameraWorld);
        _camera.Transform.WorldRotation = Quaternion.CreateFromRotationMatrix(cameraWorld);
    }

    private GameObject AddBoxVisual(string name, Vector3 position, Vector3 scale, Vector4 color)
    {
        GameObject item = _scene!.CreateGameObject(name);
        item.Transform.WorldPosition = position;
        item.Transform.LocalScale = scale;
        item.AddComponent(new MeshRenderer
        {
            UsePrimitive = true,
            Primitive = PrimitiveMeshType.Cube,
            Material = new Material { BaseColor = color }
        });
        return item;
    }

    private GameObject AddSphereVisual(string name, Vector3 position, float radius, Vector4 color)
    {
        GameObject item = _scene!.CreateGameObject(name);
        item.Transform.WorldPosition = position;
        item.Transform.LocalScale = new Vector3(radius * 2);
        item.AddComponent(new MeshRenderer
        {
            UsePrimitive = true,
            Primitive = PrimitiveMeshType.Sphere,
            Material = new Material { BaseColor = color }
        });
        return item;
    }

    protected override void OnEngineShutdown()
    {
        _events?.Dispose();
        _integrity?.Dispose();
        _physics?.Dispose();
        ReleaseGameInput();
    }
}
