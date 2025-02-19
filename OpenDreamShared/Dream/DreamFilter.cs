using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;
using System;
using JetBrains.Annotations;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.Manager.Attributes;

namespace OpenDreamShared.Dream;

/// <summary>
/// An object describing type and vars so the client doesn't have to make a ShaderInstance for shaders with the same params
/// </summary>
[Serializable, NetSerializable, ImplicitDataDefinitionForInheritors]
public record DreamFilter {
    /// <summary>
    /// Indicates this filter was used in the last render cycle, for shader caching purposes
    /// </summary>
    public bool Used = false;

    [ViewVariables, DataField("type")]
    public string FilterType;

    [CanBeNull]
    public static Type GetType(string filterType) {
        return filterType switch {
            "alpha" => typeof(DreamFilterAlpha),
            "angular_blur" => typeof(DreamFilterAngularBlur),
            "bloom" => typeof(DreamFilterBloom),
            "blur" => typeof(DreamFilterBlur),
            "color" => typeof(DreamFilterColor),
            "displace" => typeof(DreamFilterDisplace),
            "drop_shadow" => typeof(DreamFilterDropShadow),
            "layer" => typeof(DreamFilterLayer),
            "motion_blur" => typeof(DreamFilterMotionBlur),
            "outline" => typeof(DreamFilterOutline),
            "radial_blur" => typeof(DreamFilterRadialBlur),
            "rays" => typeof(DreamFilterRays),
            "ripple" => typeof(DreamFilterRipple),
            "wave" => typeof(DreamFilterWave),
            "greyscale" => typeof(DreamFilterGreyscale),
            _ => null
        };
    }
}

[Serializable, NetSerializable]
public sealed record DreamFilterAlpha : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
    //[ViewVariables, DataField("icon")] public object Icon; // Icon type?
    [ViewVariables, DataField("render_source")] public string RenderSource; // String that will require special processing
    [ViewVariables, DataField("flags")] public float Flags;
}

[Serializable, NetSerializable]
public sealed record DreamFilterAngularBlur : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
    [ViewVariables, DataField("size")] public float Size = 1f;
}

[Serializable, NetSerializable]
public sealed record DreamFilterBloom : DreamFilter {
    [ViewVariables, DataField("threshold")] public Color Threshold = Color.Black;
    [ViewVariables, DataField("size")] public float Size = 1f;
    [ViewVariables, DataField("offset")] public float Offset = 1f;
    [ViewVariables, DataField("alpha")] public float Alpha = 255f;
}

[Serializable, NetSerializable]
public sealed record DreamFilterBlur : DreamFilter {
    [ViewVariables, DataField("size")] public float Size = 1f;
}

[Serializable, NetSerializable]
public sealed record DreamFilterColor : DreamFilter {
    //[ViewVariables, DataField("color", required: true)] public object Color; // Color matrix TODO
    [ViewVariables, DataField("space")] public float Space; // Default is FILTER_COLOR_RGB = 0
}

[Serializable, NetSerializable]
public sealed record DreamFilterDisplace : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
    [ViewVariables, DataField("size")] public float Size = 1f;
    [ViewVariables, DataField("icon")] public object Icon;
    [ViewVariables, DataField("render_source")] public string RenderSource; // String that will require special processing
}

[Serializable, NetSerializable]
public sealed record DreamFilterDropShadow : DreamFilter {
    [ViewVariables, DataField("x")] public float X = 1f;
    [ViewVariables, DataField("y")] public float Y = -1f;
    [ViewVariables, DataField("size")] public float Size = 1f;
    [ViewVariables, DataField("offset")] public float Offset;
    [ViewVariables, DataField("color")] public Color Color = Color.Black.WithAlpha(128);
}

[Serializable, NetSerializable]
public sealed record DreamFilterLayer : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
    [ViewVariables, DataField("icon")] public object Icon; // Icon type?
    [ViewVariables, DataField("render_source")] public string RenderSource; // String that will require special processing
    [ViewVariables, DataField("flags")] public float Flags; // Default is FILTER_OVERLAY = 0
    [ViewVariables, DataField("color")] public Color Color = Color.Black.WithAlpha(128); // Shit needs to be string or color matrix, because of course one has to be special
    [ViewVariables, DataField("transform")] public Matrix3 Transform = Matrix3.Identity;
    [ViewVariables, DataField("blend_mode")] public float BlendMode;
}

[Serializable, NetSerializable]
public sealed record DreamFilterMotionBlur : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
}

[Serializable, NetSerializable]
public sealed record DreamFilterOutline : DreamFilter {
    [ViewVariables, DataField("size")] public float Size = 1f;
    [ViewVariables, DataField("color")] public Color Color = Color.Black;
    [ViewVariables, DataField("flags")] public float Flags;
}

[Serializable, NetSerializable]
public sealed record DreamFilterRadialBlur : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
    [ViewVariables, DataField("size")] public float Size = 0.01f;
}

[Serializable, NetSerializable]
public sealed record DreamFilterRays : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
    [ViewVariables, DataField("size")] public float Size = 16f; // Defaults to half tile width
    [ViewVariables, DataField("color")] public Color Color = Color.White;
    [ViewVariables, DataField("offset")] public float Offset;
    [ViewVariables, DataField("density")] public float Density = 10f;
    [ViewVariables, DataField("threshold")] public float Threshold = 0.5f;
    [ViewVariables, DataField("factor")] public float Factor;
    [ViewVariables, DataField("flags")] public float Flags = 3f; // Defaults to FILTER_OVERLAY | FILTER_UNDERLAY
}

[Serializable, NetSerializable]
public sealed record DreamFilterRipple : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
    [ViewVariables, DataField("size")] public float Size = 1f;
    [ViewVariables, DataField("repeat")] public float Repeat = 2f;
    [ViewVariables, DataField("radius")] public float Radius;
    [ViewVariables, DataField("falloff")] public float Falloff = 1f;
    [ViewVariables, DataField("flags")] public float Flags;
}

[Serializable, NetSerializable]
public sealed record DreamFilterWave : DreamFilter {
    [ViewVariables, DataField("x")] public float X;
    [ViewVariables, DataField("y")] public float Y;
    [ViewVariables, DataField("size")] public float Size = 1f;
    [ViewVariables, DataField("offset")] public float Offset;
    [ViewVariables, DataField("flags")] public float Flags;
}

[Serializable, NetSerializable]
public sealed record DreamFilterGreyscale : DreamFilter;
