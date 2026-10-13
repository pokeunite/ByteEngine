using System.Runtime.CompilerServices;
using OpenTK.Graphics.OpenGL4;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Renderer-owned sampling overrides. Shared texture filtering and wrapping
/// remain untouched, so sprites and UI retain their asset sampling settings.
/// </summary>
internal sealed class MaterialTextureSampling : IDisposable
{
    private sealed class SamplingState
    {
        public int ContentVersion { get; set; } = -1;
    }

    private static readonly ConditionalWeakTable<Texture2D, SamplingState> States = new();
    private static readonly int[] MaterialSlots = { 0, 1, 8, 9, 10, 11, 12 };
    private int _sampler;

    public void Bind(Texture2D texture, int slot)
    {
        // Only rendering requires a GL context; Material setters are pure data.
        texture.Bind(slot);
        SamplingState state = States.GetValue(texture, static _ => new SamplingState());
        if (state.ContentVersion != texture.ContentVersion)
        {
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            state.ContentVersion = texture.ContentVersion;
        }

        if (_sampler == 0)
        {
            _sampler = GL.GenSampler();
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureMinFilter,
                (int)TextureMinFilter.LinearMipmapLinear);
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureMagFilter,
                (int)TextureMagFilter.Linear);
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureWrapS,
                (int)TextureWrapMode.Repeat);
            GL.SamplerParameter(_sampler, SamplerParameterName.TextureWrapT,
                (int)TextureWrapMode.Repeat);
            // Roads and terrain are seen at shallow angles. Isotropic mip sampling
            // otherwise erases markings long before their screen size warrants it.
            GL.GetInteger(GetPName.NumExtensions, out int extensionCount);
            for(int i=0;i<extensionCount;i++)
            {
                string? extension=GL.GetString(StringNameIndexed.Extensions,i);
                if(extension is not ("GL_EXT_texture_filter_anisotropic" or "GL_ARB_texture_filter_anisotropic"))continue;
                float maximum=GL.GetFloat((GetPName)0x84FF);
                GL.SamplerParameter(_sampler,(SamplerParameterName)0x84FE,Math.Min(8f,maximum));
                break;
            }
        }

        GL.BindSampler(slot, _sampler);
    }

    public static void Unbind()
    {
        foreach (int slot in MaterialSlots)
            GL.BindSampler(slot, 0);
    }

    public void Dispose()
    {
        if (_sampler == 0) return;
        Unbind();
        GL.DeleteSampler(_sampler);
        _sampler = 0;
    }
}
