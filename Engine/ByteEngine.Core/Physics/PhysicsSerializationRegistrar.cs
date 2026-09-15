using System.Numerics;
using System.Text.Json.Nodes;

using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Physics;

/// <summary>
/// Registers v0.10 physics components with ByteEngine's scene/Blueprint
/// component serializer.
/// </summary>
public static class PhysicsSerializationRegistrar
{
    public static void Register(
        Serialization.ComponentSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(
            serializer);

        serializer.Register(
            new Rigidbody3DCodec());
    }

    private sealed class Rigidbody3DCodec
        : Serialization.IComponentCodec
    {
        public string TypeName =>
            "Rigidbody3D";

        public Type ComponentType =>
            typeof(Rigidbody3D);

        public ComponentData Serialize(
            Component component,
            Serialization.ComponentSerializationContext context)
        {
            Rigidbody3D body =
                (Rigidbody3D)component;

            return
                new ComponentData
                {
                    Type =
                        TypeName,

                    Properties =
                        new JsonObject
                        {
                            ["bodyType"] =
                                body.BodyType.ToString(),

                            ["mass"] =
                                body.Mass,

                            ["useGravity"] =
                                body.UseGravity,

                            ["gravityScale"] =
                                body.GravityScale,

                            ["linearDamping"] =
                                body.LinearDamping,

                            ["restitution"] =
                                body.Restitution,

                            ["friction"] =
                                body.Friction,

                            ["freezePositionX"] =
                                body.FreezePositionX,

                            ["freezePositionY"] =
                                body.FreezePositionY,

                            ["freezePositionZ"] =
                                body.FreezePositionZ,

                            ["velocity"] =
                                new JsonArray(
                                    body.Velocity.X,
                                    body.Velocity.Y,
                                    body.Velocity.Z)
                        }
                };
        }

        public Component Deserialize(
            ComponentData data,
            Serialization.ComponentSerializationContext context)
        {
            if (!Enum.TryParse(
                    data.Properties["bodyType"]?
                        .GetValue<string>(),
                    true,
                    out RigidbodyBodyType3D bodyType))
            {
                bodyType =
                    RigidbodyBodyType3D.Dynamic;
            }

            return
                new Rigidbody3D
                {
                    BodyType =
                        bodyType,

                    Mass =
                        ReadFloat(
                            data,
                            "mass",
                            1.0f),

                    UseGravity =
                        data.Properties["useGravity"]?
                            .GetValue<bool>() ??
                        true,

                    GravityScale =
                        ReadFloat(
                            data,
                            "gravityScale",
                            1.0f),

                    LinearDamping =
                        ReadFloat(
                            data,
                            "linearDamping",
                            0.05f),

                    Restitution =
                        ReadFloat(
                            data,
                            "restitution",
                            0.0f),

                    Friction =
                        ReadFloat(
                            data,
                            "friction",
                            0.6f),

                    FreezePositionX =
                        data.Properties["freezePositionX"]?
                            .GetValue<bool>() ??
                        false,

                    FreezePositionY =
                        data.Properties["freezePositionY"]?
                            .GetValue<bool>() ??
                        false,

                    FreezePositionZ =
                        data.Properties["freezePositionZ"]?
                            .GetValue<bool>() ??
                        false,

                    Velocity =
                        ReadVector3(
                            data.Properties["velocity"],
                            Vector3.Zero)
                };
        }

        private static float ReadFloat(
            ComponentData data,
            string name,
            float fallback)
        {
            return
                data.Properties[name]?
                    .GetValue<float>() ??
                fallback;
        }

        private static Vector3 ReadVector3(
            JsonNode? node,
            Vector3 fallback)
        {
            if (node is not
                    JsonArray array ||
                array.Count <
                    3)
            {
                return fallback;
            }

            return
                new Vector3(
                    array[0]?
                        .GetValue<float>() ??
                    fallback.X,
                    array[1]?
                        .GetValue<float>() ??
                    fallback.Y,
                    array[2]?
                        .GetValue<float>() ??
                    fallback.Z);
        }
    }
}
