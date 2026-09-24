using System.Globalization;
using System.Numerics;

namespace ByteEngine.Core.Assets.Importers;

public sealed class ObjModelImporter : ModelImporter
{
    public override IReadOnlyCollection<string> Extensions { get; } = new[] { ".obj" };

    public override ImportedModel Import(AssetRecord source, ModelImporterSettings settings)
    {
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var textureCoordinates = new List<Vector2>();
        var vertices = new List<float>();
        var indices = new List<uint>();
        var vertexMap = new Dictionary<VertexKey, uint>();
        string? materialLibrary = null;
        string? selectedMaterial = null;

        foreach (string rawLine in File.ReadLines(source.FullPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0])
            {
                case "v" when parts.Length >= 4:
                    positions.Add(new Vector3(Parse(parts[1]), Parse(parts[2]), Parse(parts[3])));
                    break;
                case "vn" when parts.Length >= 4:
                    normals.Add(Vector3.Normalize(new Vector3(Parse(parts[1]), Parse(parts[2]), Parse(parts[3]))));
                    break;
                case "vt" when parts.Length >= 3:
                    textureCoordinates.Add(new Vector2(Parse(parts[1]), 1f - Parse(parts[2])));
                    break;
                case "mtllib" when parts.Length >= 2:
                    materialLibrary = string.Join(' ', parts.Skip(1));
                    break;
                case "usemtl" when parts.Length >= 2:
                    selectedMaterial ??= string.Join(' ', parts.Skip(1));
                    break;
                case "f" when parts.Length >= 4:
                    var face = new List<uint>();
                    for (int index = 1; index < parts.Length; index++)
                    {
                        VertexKey key = ParseVertex(parts[index], positions.Count, textureCoordinates.Count, normals.Count);
                        if (!vertexMap.TryGetValue(key, out uint vertexIndex))
                        {
                            vertexIndex = (uint)vertexMap.Count;
                            vertexMap[key] = vertexIndex;
                            Vector3 position = positions[key.Position];
                            Vector3 normal = key.Normal >= 0 ? normals[key.Normal] : Vector3.Zero;
                            Vector2 uv = key.TextureCoordinate >= 0 ? textureCoordinates[key.TextureCoordinate] : Vector2.Zero;
                            vertices.AddRange(new[]
                            {
                                position.X, position.Y, position.Z,
                                normal.X, normal.Y, normal.Z,
                                uv.X, uv.Y
                            });
                        }
                        face.Add(vertexIndex);
                    }

                    for (int index = 1; index < face.Count - 1; index++)
                    {
                        indices.Add(face[0]);
                        indices.Add(face[index]);
                        indices.Add(face[index + 1]);
                    }
                    break;
            }
        }

        if (positions.Count == 0 || indices.Count == 0)
            throw new InvalidDataException("OBJ contains no triangle geometry.");
        if (settings.GenerateNormals && !vertexMap.Keys.Any(key => key.Normal >= 0))
            GenerateNormals(vertices, indices);

        ImportedMaterial material = ReadMaterial(source, materialLibrary, selectedMaterial);
        string meshKey = $"{source.Guid:N}:mesh:{Path.GetFileNameWithoutExtension(source.FullPath)}";
        return ImportedModelSpace.Apply(new ImportedModel
        {
            Guid = source.Guid,
            SourceAssetGuid = source.Guid,
            Name = Path.GetFileNameWithoutExtension(source.ProjectPath),
            Materials = new List<ImportedMaterial> { material },
            Meshes = new List<ImportedMesh>
            {
                new()
                {
                    Key = meshKey,
                    Name = Path.GetFileNameWithoutExtension(source.ProjectPath),
                    Vertices = vertices.ToArray(),
                    Indices = indices.ToArray(),
                    MaterialKey = material.Key
                }
            },
            Nodes = new List<ImportedNode>
            {
                new()
                {
                    Key = "node:root:0",
                    Name = Path.GetFileNameWithoutExtension(source.ProjectPath),
                    MeshKeys = new List<string> { meshKey }
                }
            }
        }, Matrix4x4.CreateScale(settings.ImportScale));
    }

    private static ImportedMaterial ReadMaterial(AssetRecord source, string? library, string? selectedName)
    {
        string key = $"{source.Guid:N}:material:{selectedName ?? "Default"}";
        if (string.IsNullOrWhiteSpace(library)) return new ImportedMaterial { Key = key, Name = "Default" };
        string path = Path.Combine(Path.GetDirectoryName(source.FullPath)!, library);
        if (!File.Exists(path)) return new ImportedMaterial { Key = key, Name = selectedName ?? "Default" };

        Vector4 color = Vector4.One;
        float roughness = 1f;
        ImportedTexture? texture = null;
        bool active = string.IsNullOrWhiteSpace(selectedName);
        foreach (string rawLine in File.ReadLines(path))
        {
            string[] parts = rawLine.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) continue;
            if (parts[0] == "newmtl") active = selectedName == null || string.Join(' ', parts.Skip(1)) == selectedName;
            else if (active && parts[0] == "Kd" && parts.Length >= 4)
                color = new Vector4(Parse(parts[1]), Parse(parts[2]), Parse(parts[3]), 1f);
            else if (active && parts[0] == "Ns" && parts.Length >= 2)
                roughness = Math.Clamp(1f - MathF.Sqrt(Parse(parts[1]) / 1000f), .04f, 1f);
            else if (active && parts[0] == "map_Kd" && parts.Length >= 2)
            {
                string imagePath = Path.Combine(Path.GetDirectoryName(path)!, string.Join(' ', parts.Skip(1)));
                if (File.Exists(imagePath))
                {
                    texture = new ImportedTexture
                    {
                        Key = $"{key}:baseColor",
                        Name = Path.GetFileNameWithoutExtension(imagePath),
                        SourcePath = imagePath,
                        EncodedData = File.ReadAllBytes(imagePath)
                    };
                }
            }
        }

        return new ImportedMaterial
        {
            Key = key,
            Name = selectedName ?? "Default",
            BaseColor = color,
            Roughness = roughness,
            BaseColorTexture = texture
        };
    }

    private static VertexKey ParseVertex(string token, int positionCount, int uvCount, int normalCount)
    {
        string[] values = token.Split('/');
        return new VertexKey(
            ResolveIndex(values[0], positionCount),
            values.Length > 1 && values[1].Length > 0 ? ResolveIndex(values[1], uvCount) : -1,
            values.Length > 2 && values[2].Length > 0 ? ResolveIndex(values[2], normalCount) : -1);
    }

    private static int ResolveIndex(string value, int count)
    {
        int index = int.Parse(value, CultureInfo.InvariantCulture);
        return index > 0 ? index - 1 : count + index;
    }

    private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);

    private static void GenerateNormals(List<float> vertices, IReadOnlyList<uint> indices)
    {
        var normals = new Vector3[vertices.Count / 8];
        for (int index = 0; index + 2 < indices.Count; index += 3)
        {
            int a = (int)indices[index];
            int b = (int)indices[index + 1];
            int c = (int)indices[index + 2];
            Vector3 pa = ReadPosition(vertices, a);
            Vector3 normal = Vector3.Cross(ReadPosition(vertices, b) - pa, ReadPosition(vertices, c) - pa);
            normals[a] += normal;
            normals[b] += normal;
            normals[c] += normal;
        }

        for (int index = 0; index < normals.Length; index++)
        {
            Vector3 normal = normals[index].LengthSquared() > .000001f
                ? Vector3.Normalize(normals[index])
                : Vector3.UnitY;
            int offset = index * 8 + 3;
            vertices[offset] = normal.X;
            vertices[offset + 1] = normal.Y;
            vertices[offset + 2] = normal.Z;
        }
    }

    private static Vector3 ReadPosition(IReadOnlyList<float> vertices, int index)
    {
        int offset = index * 8;
        return new Vector3(vertices[offset], vertices[offset + 1], vertices[offset + 2]);
    }

    private readonly record struct VertexKey(int Position, int TextureCoordinate, int Normal);
}
