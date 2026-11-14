using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Jellybean.UI;

internal sealed class PetMotionEffect : ShaderEffect
{
    private static readonly PixelShader Shader = LoadShader();
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(PetMotionEffect), 0, SamplingMode.Bilinear);
    public static readonly DependencyProperty PreviousProperty = RegisterPixelShaderSamplerProperty("Previous", typeof(PetMotionEffect), 1, SamplingMode.Bilinear);
    public static readonly DependencyProperty MixProperty = Register("Mix", 1.0, 0);
    public static readonly DependencyProperty BreathProperty = Register("Breath", 0.0, 1);
    public static readonly DependencyProperty EarProperty = Register("Ear", 0.0, 2);
    public static readonly DependencyProperty PreviousFrozenProperty = Register("PreviousFrozen", 0.0, 3);
    public static readonly DependencyProperty SwayProperty = Register("Sway", new Point(), 4);
    public static readonly DependencyProperty BreathCenterProperty = Register("BreathCenter", new Point(), 5);
    public static readonly DependencyProperty BreathInverseExtentProperty = Register("BreathInverseExtent", new Point(), 6);
    public Brush Previous { get => (Brush)GetValue(PreviousProperty); set => SetValue(PreviousProperty, value); }
    public double Mix { get => (double)GetValue(MixProperty); set => SetValue(MixProperty, value); }
    public double Breath { get => (double)GetValue(BreathProperty); set => SetValue(BreathProperty, value); }
    public double Ear { get => (double)GetValue(EarProperty); set => SetValue(EarProperty, value); }
    public double PreviousFrozen { get => (double)GetValue(PreviousFrozenProperty); set => SetValue(PreviousFrozenProperty, value); }
    public Point Sway { get => (Point)GetValue(SwayProperty); set => SetValue(SwayProperty, value); }
    public Point BreathCenter { get => (Point)GetValue(BreathCenterProperty); set => SetValue(BreathCenterProperty, value); }
    public Point BreathInverseExtent { get => (Point)GetValue(BreathInverseExtentProperty); set => SetValue(BreathInverseExtentProperty, value); }

    public PetMotionEffect()
    {
        PixelShader = Shader;
        foreach (var property in new[] { InputProperty, PreviousProperty, MixProperty, BreathProperty, EarProperty,
            PreviousFrozenProperty, SwayProperty, BreathCenterProperty, BreathInverseExtentProperty }) UpdateShaderValue(property);
    }
    private static DependencyProperty Register<T>(string name, T initial, int register) where T : struct =>
        DependencyProperty.Register(name, typeof(T), typeof(PetMotionEffect), new UIPropertyMetadata(initial, PixelShaderConstantCallback(register)));
    private static PixelShader LoadShader()
    {
        var shader = new PixelShader { UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets/pet-motion.ps")) };
        shader.Freeze(); return shader;
    }
}
