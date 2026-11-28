using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SkiaSharp;

namespace Vela.Avalonia.Controls;

/// <summary>
/// Exception thrown when an SkSL shader fails to compile.
/// </summary>
public sealed class VelaShaderCompilationException : Exception
{
    public VelaShaderCompilationException(string message) : base(message)
    {
    }
}

/// <summary>
/// Compiled, cached SkSL shader effect for <see cref="VelaShaderBackground"/>.
/// Supports built-in ambient presets, inline SkSL strings (<see cref="FromString"/>),
/// and embedded <c>.sksl</c> resources (<see cref="FromEmbeddedResource"/>).
/// Standard uniforms (<c>iResolution</c>, <c>iTime</c>, <c>iPreset</c>, <c>iIntensity</c>,
/// <c>iIsDark</c>, <c>iAlpha</c>, <c>iBase</c>, <c>iColor1</c>, <c>iColor2</c>, <c>iColor3</c>)
/// are automatically injected when omitted from custom shader code.
/// </summary>
public sealed class VelaShaderEffect
{
    private static readonly (string Name, string Declaration)[] StandardUniforms =
    [
        ("iResolution", "uniform float2 iResolution;"),
        ("iTime", "uniform float iTime;"),
        ("iPreset", "uniform float iPreset;"),
        ("iIntensity", "uniform float iIntensity;"),
        ("iIsDark", "uniform float iIsDark;"),
        ("iAlpha", "uniform float iAlpha;"),
        ("iBase", "uniform float3 iBase;"),
        ("iColor1", "uniform float3 iColor1;"),
        ("iColor2", "uniform float3 iColor2;"),
        ("iColor3", "uniform float3 iColor3;"),
    ];

    [Flags]
    private enum ShaderUniformMask
    {
        None = 0,
        Resolution = 1 << 0,
        Time = 1 << 1,
        Preset = 1 << 2,
        Intensity = 1 << 3,
        IsDark = 1 << 4,
        Alpha = 1 << 5,
        Base = 1 << 6,
        Color1 = 1 << 7,
        Color2 = 1 << 8,
        Color3 = 1 << 9,
    }

    internal const int MaxSourceCacheEntries = 64;
    private static readonly Dictionary<string, VelaShaderEffect> CacheBySource = new(StringComparer.Ordinal);
    private static readonly LinkedList<string> CacheLruOrder = new();
    private static readonly Dictionary<string, LinkedListNode<string>> CacheLruNodes = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, VelaShaderEffect> CacheByResource = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object CacheLock = new();
    private static readonly object LifecycleLock = new();
    private static IControlledApplicationLifetime? _applicationLifetime;
    private static bool _disposed;

    private static readonly Lazy<VelaShaderEffect?> BuiltInAmbientLazy = new(CreateBuiltInAmbient);

    private readonly string _compiledSource;
    private readonly ShaderUniformMask _uniformMask;

    /// <summary>
    /// The underlying compiled Skia <see cref="SKRuntimeEffect"/>.
    /// </summary>
    public SKRuntimeEffect Effect { get; }

    /// <summary>
    /// Raw SkSL source passed to <see cref="FromString"/> or loaded from an embedded resource.
    /// </summary>
    public string RawSource { get; }

    /// <summary>
    /// Shared built-in ambient shader supporting all <see cref="VelaShaderBackgroundPreset"/> values.
    /// </summary>
    public static VelaShaderEffect? BuiltInAmbient => BuiltInAmbientLazy.Value;

    static VelaShaderEffect()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeAll();
        EnsureExitSubscription();
    }

    private VelaShaderEffect(SKRuntimeEffect effect, string compiledSource, string rawSource)
    {
        Effect = effect;
        _compiledSource = compiledSource;
        RawSource = rawSource;
        ShaderUniformMask mask = ShaderUniformMask.None;
        foreach (var name in effect.Uniforms)
        {
            mask |= name switch
            {
                "iResolution" => ShaderUniformMask.Resolution,
                "iTime" => ShaderUniformMask.Time,
                "iPreset" => ShaderUniformMask.Preset,
                "iIntensity" => ShaderUniformMask.Intensity,
                "iIsDark" => ShaderUniformMask.IsDark,
                "iAlpha" => ShaderUniformMask.Alpha,
                "iBase" => ShaderUniformMask.Base,
                "iColor1" => ShaderUniformMask.Color1,
                "iColor2" => ShaderUniformMask.Color2,
                "iColor3" => ShaderUniformMask.Color3,
                _ => ShaderUniformMask.None,
            };
        }
        _uniformMask = mask;
    }

    /// <summary>
    /// Compiles and caches an SkSL shader from source code.
    /// Throws <see cref="VelaShaderCompilationException"/> if compilation fails.
    /// </summary>
    public static VelaShaderEffect FromString(string sksl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sksl);

        if (!TryFromString(sksl, out var effect, out var error) || effect is null)
            throw new VelaShaderCompilationException(error ?? "Failed to compile SkSL shader.");

        return effect;
    }

    /// <summary>
    /// Attempts to compile and cache an SkSL shader from source code without throwing.
    /// </summary>
    public static bool TryFromString(string sksl, out VelaShaderEffect? effect, out string? error)
    {
        if (string.IsNullOrWhiteSpace(sksl))
        {
            effect = null;
            error = "Shader source cannot be empty.";
            return false;
        }

        EnsureExitSubscription();

        lock (CacheLock)
        {
            if (_disposed)
            {
                effect = null;
                error = "Shader cache has been disposed.";
                return false;
            }

            if (CacheBySource.TryGetValue(sksl, out var cached))
            {
                if (CacheLruNodes.TryGetValue(sksl, out var node))
                {
                    CacheLruOrder.Remove(node);
                    CacheLruOrder.AddLast(node);
                }
                effect = cached;
                error = null;
                return true;
            }

            var prepared = PrepareSourceWithStandardUniforms(sksl);
            var compiled = SKRuntimeEffect.CreateShader(prepared, out var compileError);
            if (compiled is null)
            {
                effect = null;
                error = compileError;
                return false;
            }

            var created = new VelaShaderEffect(compiled, prepared, sksl);
            CacheBySource[sksl] = created;
            CacheLruNodes[sksl] = CacheLruOrder.AddLast(sksl);

            while (CacheBySource.Count > MaxSourceCacheEntries && CacheLruOrder.First is { } oldest)
            {
                var oldestKey = oldest.Value;
                CacheLruOrder.RemoveFirst();
                CacheLruNodes.Remove(oldestKey);
                if (CacheBySource.Remove(oldestKey, out var evicted))
                {
                    if (!BuiltInAmbientLazy.IsValueCreated || !ReferenceEquals(evicted, BuiltInAmbientLazy.Value))
                    {
                        evicted.Effect.Dispose();
                    }
                }
            }

            effect = created;
            error = null;
            return true;
        }
    }

    /// <summary>
    /// Loads, compiles, and caches an embedded <c>.sksl</c> resource from the specified assembly,
    /// entry assembly, or <see cref="VelaShaderEffect"/> assembly.
    /// </summary>
    public static VelaShaderEffect FromEmbeddedResource(string resourceName, Assembly? assembly = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);

        var normalized = resourceName.Trim();
        if (!normalized.EndsWith(".sksl", StringComparison.OrdinalIgnoreCase))
            normalized += ".sksl";

        var cacheKey = assembly is null ? normalized : $"{assembly.FullName}::{normalized}";
        lock (CacheLock)
        {
            if (CacheByResource.TryGetValue(cacheKey, out var cached))
                return cached;
        }

        var (resolvedAssembly, manifestName) = ResolveEmbeddedResource(normalized, assembly);
        if (resolvedAssembly is null || manifestName is null)
            throw new FileNotFoundException($"Unable to find embedded SkSL resource '{normalized}'.");

        using var stream = resolvedAssembly.GetManifestResourceStream(manifestName)
            ?? throw new FileNotFoundException($"Unable to open embedded SkSL resource '{manifestName}'.");
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd();

        var effect = FromString(source);
        lock (CacheLock)
        {
            if (!_disposed)
                CacheByResource[cacheKey] = effect;
        }
        return effect;
    }

    /// <summary>
    /// Attempts to load and compile an embedded <c>.sksl</c> resource without throwing.
    /// </summary>
    public static bool TryFromEmbeddedResource(
        string resourceName,
        out VelaShaderEffect? effect,
        out string? error,
        Assembly? assembly = null)
    {
        try
        {
            effect = FromEmbeddedResource(resourceName, assembly);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            effect = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Creates an <see cref="SKShader"/> instance populated with all declared standard uniforms.
    /// </summary>
    internal SKShader? CreateShader(
        float width,
        float height,
        float time,
        int preset,
        float intensity,
        bool isDark,
        float alpha,
        SKColor baseColor,
        SKColor color1,
        SKColor color2,
        SKColor color3,
        float[] resBuf,
        float[] vec3Buf)
    {
        if (_disposed)
            return null;

        using var uniforms = new SKRuntimeEffectUniforms(Effect);

        if ((_uniformMask & ShaderUniformMask.Resolution) != 0)
        {
            resBuf[0] = width;
            resBuf[1] = height;
            uniforms["iResolution"] = resBuf;
        }

        if ((_uniformMask & ShaderUniformMask.Time) != 0)
            uniforms["iTime"] = time;

        if ((_uniformMask & ShaderUniformMask.Preset) != 0)
            uniforms["iPreset"] = (float)preset;

        if ((_uniformMask & ShaderUniformMask.Intensity) != 0)
            uniforms["iIntensity"] = intensity;

        if ((_uniformMask & ShaderUniformMask.IsDark) != 0)
            uniforms["iIsDark"] = isDark ? 1.0f : 0.0f;

        if ((_uniformMask & ShaderUniformMask.Alpha) != 0)
            uniforms["iAlpha"] = alpha;

        if ((_uniformMask & ShaderUniformMask.Base) != 0)
            uniforms["iBase"] = FillVec3(baseColor, vec3Buf);

        if ((_uniformMask & ShaderUniformMask.Color1) != 0)
            uniforms["iColor1"] = FillVec3(color1, vec3Buf);

        if ((_uniformMask & ShaderUniformMask.Color2) != 0)
            uniforms["iColor2"] = FillVec3(color2, vec3Buf);

        if ((_uniformMask & ShaderUniformMask.Color3) != 0)
            uniforms["iColor3"] = FillVec3(color3, vec3Buf);

        return Effect.ToShader(uniforms);
    }

    public override bool Equals(object? obj) =>
        obj is VelaShaderEffect other && string.Equals(_compiledSource, other._compiledSource, StringComparison.Ordinal);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_compiledSource);

    public override string ToString() => RawSource;

    internal static string PrepareSourceWithStandardUniforms(string sksl)
    {
        var sb = new StringBuilder(sksl.Length + 256);
        foreach (var (name, declaration) in StandardUniforms)
        {
            if (!HasUniformDeclaration(sksl, name))
                sb.AppendLine(declaration);
        }

        sb.Append(sksl);
        return sb.ToString();
    }

    private static bool HasUniformDeclaration(string sksl, string uniformName)
    {
        var index = 0;
        while ((index = sksl.IndexOf(uniformName, index, StringComparison.Ordinal)) >= 0)
        {
            var lineStart = sksl.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
            var prefix = sksl.AsSpan(lineStart, index - lineStart).TrimStart();
            if (prefix.StartsWith("uniform ", StringComparison.Ordinal))
                return true;

            index += uniformName.Length;
        }
        return false;
    }

    private static (Assembly? Assembly, string? ResourceName) ResolveEmbeddedResource(string normalized, Assembly? preferred)
    {
        Assembly?[] candidates =
        [
            preferred,
            Assembly.GetEntryAssembly(),
            typeof(VelaShaderEffect).Assembly,
        ];

        foreach (var candidate in candidates)
        {
            if (candidate is null)
                continue;

            var match = candidate.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(normalized, StringComparison.OrdinalIgnoreCase)
                                     || n.Contains(normalized, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return (candidate, match);
        }

        return (null, null);
    }

    private static float[] FillVec3(SKColor color, float[] buffer)
    {
        buffer[0] = color.Red / 255f;
        buffer[1] = color.Green / 255f;
        buffer[2] = color.Blue / 255f;
        return buffer;
    }

    private static void EnsureExitSubscription()
    {
        if (Application.Current?.ApplicationLifetime is not IControlledApplicationLifetime controlled)
            return;

        lock (LifecycleLock)
        {
            if (ReferenceEquals(_applicationLifetime, controlled))
                return;

            _applicationLifetime = controlled;
            controlled.Exit += (_, _) => DisposeAll();
        }
    }

    internal static void DisposeAll()
    {
        lock (CacheLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            foreach (var effect in CacheBySource.Values)
                effect.Effect.Dispose();

            CacheBySource.Clear();
            CacheByResource.Clear();
        }
    }

    private static VelaShaderEffect? CreateBuiltInAmbient()
    {
        const string sksl = """
            float hash12(float2 p) {
                float3 p3 = fract(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return fract((p3.x + p3.y) * p3.z);
            }

            half4 main(float2 fragCoord) {
                float2 uv = fragCoord / max(iResolution, float2(1.0));
                float aspect = iResolution.x / max(iResolution.y, 1.0);
                float2 p = (uv - 0.5) * float2(aspect, 1.0);
                float t = iTime * 0.35;

                float w1 = 0.0;
                float w2 = 0.0;
                float w3 = 0.0;

                if (iPreset < 0.5) {
                    // Preset 0: Soft floating Mesh blobs
                    float2 c1 = float2(sin(t * 0.85) * 0.48 * aspect, cos(t * 0.62) * 0.36);
                    float2 c2 = float2(cos(t * 0.71 + 2.1) * 0.44 * aspect, sin(t * 0.91 + 1.2) * 0.38);
                    float2 c3 = float2(sin(t * 0.54 + 4.0) * 0.36 * aspect, cos(t * 0.78 + 3.4) * 0.32);

                    w1 = exp(-dot(p - c1, p - c1) * 2.15);
                    w2 = exp(-dot(p - c2, p - c2) * 2.35);
                    w3 = exp(-dot(p - c3, p - c3) * 2.65);
                } else if (iPreset < 1.5) {
                    // Preset 1: Sweeping Aurora ribbons
                    float2 q = p;
                    q.y += 0.18 * sin(q.x * 2.2 + t * 1.1) + 0.09 * cos(q.x * 3.7 - t * 0.8);
                    float r1 = exp(-pow(q.y - 0.12 * sin(t + q.x), 2.0) * 14.0);
                    float r2 = exp(-pow(q.y + 0.16 + 0.14 * cos(t * 0.9 + q.x * 1.6), 2.0) * 11.0);
                    float r3 = exp(-pow(q.y - 0.22 + 0.10 * sin(t * 1.3 - q.x * 2.1), 2.0) * 16.0);
                    float shimmer = 0.75 + 0.25 * sin(q.x * 5.0 - t * 1.8);
                    w1 = r1 * shimmer;
                    w2 = r2 * (0.8 + 0.2 * cos(q.x * 4.0 + t));
                    w3 = r3;
                } else if (iPreset < 2.5) {
                    // Preset 2: Luminous diagonal Silk waves
                    float2 q = p;
                    float waveA = sin(q.x * 2.4 + q.y * 1.9 + t * 1.05);
                    float waveB = cos(q.x * 1.8 - q.y * 2.6 - t * 0.85);
                    float waveC = sin((q.x - q.y) * 3.1 + t * 0.7 + waveA * 0.7);
                    w1 = smoothstep(-0.35, 0.95, waveA) * 0.75;
                    w2 = smoothstep(-0.25, 0.95, waveB) * 0.70;
                    w3 = smoothstep(-0.15, 0.95, waveC) * 0.65;
                } else if (iPreset < 3.5) {
                    // Preset 3: Restrained studio Gradient (Suki-inspired calm corner wash)
                    float2 c1 = float2(-0.42 * aspect + sin(t * 0.35) * 0.08, -0.34 + cos(t * 0.28) * 0.06);
                    float2 c2 = float2(0.45 * aspect + cos(t * 0.31) * 0.08, 0.38 + sin(t * 0.25) * 0.06);
                    w1 = exp(-dot(p - c1, p - c1) * 1.35) * 0.78;
                    w2 = exp(-dot(p - c2, p - c2) * 1.45) * 0.68;
                    w3 = smoothstep(1.1, 0.0, length(p)) * 0.22;
                } else {
                    // Preset 4: Ambient radial Glow halo
                    float2 center = float2(sin(t * 0.45) * 0.18 * aspect, -0.15 + cos(t * 0.38) * 0.12);
                    float d = length(p - center);
                    w1 = exp(-d * d * 2.4);
                    w2 = exp(-pow(d - 0.36, 2.0) * 7.5) * 0.65;
                    w3 = exp(-dot(p + center * 0.7, p + center * 0.7) * 1.8) * 0.55;
                }

                float strength = (iIsDark > 0.5 ? 0.58 : 0.44) * iIntensity;
                float3 col = iBase;
                col = mix(col, iColor1, clamp(w1 * strength, 0.0, 0.92));
                col = mix(col, iColor2, clamp(w2 * strength, 0.0, 0.88));
                col = mix(col, iColor3, clamp(w3 * strength * 0.85, 0.0, 0.82));

                // Subtle vignette + micro-dither to prevent 8-bit banding
                float vig = 1.0 - dot(uv - 0.5, uv - 0.5) * (iIsDark > 0.5 ? 0.35 : 0.08);
                col *= vig;
                col += (hash12(fragCoord) - 0.5) / 255.0;

                float a = clamp(iAlpha, 0.0, 1.0);
                return half4(clamp(col, 0.0, 1.0) * a, a);
            }
            """;

        return TryFromString(sksl, out var effect, out _) ? effect : null;
    }
}
