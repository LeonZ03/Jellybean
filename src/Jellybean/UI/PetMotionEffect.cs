using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace Jellybean.UI;

internal sealed class PetMotionEffect : ShaderEffect
{
    private static readonly PixelShader Shader = LoadShader();
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(PetMotionEffect), 0, SamplingMode.Bilinear);
    public static readonly DependencyProperty PreviousProperty = RegisterPixelShaderSamplerProperty("Previous", typeof(PetMotionEffect), 1, SamplingMode.Bilinear);
    public static readonly DependencyProperty MixProperty = Register("Mix", 1.0, 0);
    public static readonly DependencyProperty BreathProperty = Register("Breath", 0.0, 1);
    public static readonly DependencyProperty BlinkProperty = Register("Blink", 0.0, 2);
    public static readonly DependencyProperty PreviousFrozenProperty = Register("PreviousFrozen", 0.0, 3);
    public static readonly DependencyProperty SwayProperty = Register("Sway", new Point(), 4);
    public static readonly DependencyProperty BreathCenterProperty = Register("BreathCenter", new Point(), 5);
    public static readonly DependencyProperty BreathInverseExtentProperty = Register("BreathInverseExtent", new Point(), 6);
    public static readonly DependencyProperty HeadRegionProperty = Register("HeadRegion", new Point4D(), 7);
    public static readonly DependencyProperty TailRegionProperty = Register("TailRegion", new Point4D(), 8);
    public static readonly DependencyProperty DetailShiftProperty = Register("DetailShift", new Point4D(), 9);
    public static readonly DependencyProperty EyesProperty = Register("Eyes", new Point(), 10);
    public static readonly DependencyProperty EyeHeightProperty = Register("EyeHeight", 0.0, 11);
    public static readonly DependencyProperty PickupOffsetProperty = Register("PickupOffset", new Point(), 12);
    public static readonly DependencyProperty GestureRegionProperty = Register("GestureRegion", new Point4D(), 13);
    public static readonly DependencyProperty GestureShiftProperty = Register("GestureShift", new Point(), 14);
    public Brush Previous { get => (Brush)GetValue(PreviousProperty); set => SetValue(PreviousProperty, value); }
    public double Mix { get => (double)GetValue(MixProperty); set => SetValue(MixProperty, value); }
    public double Breath { get => (double)GetValue(BreathProperty); set => SetValue(BreathProperty, value); }
    public double Blink { get => (double)GetValue(BlinkProperty); set => SetValue(BlinkProperty, value); }
    public double PreviousFrozen { get => (double)GetValue(PreviousFrozenProperty); set => SetValue(PreviousFrozenProperty, value); }
    public Point Sway { get => (Point)GetValue(SwayProperty); set => SetValue(SwayProperty, value); }
    public Point BreathCenter { get => (Point)GetValue(BreathCenterProperty); set => SetValue(BreathCenterProperty, value); }
    public Point BreathInverseExtent { get => (Point)GetValue(BreathInverseExtentProperty); set => SetValue(BreathInverseExtentProperty, value); }
    public Point4D HeadRegion { get => (Point4D)GetValue(HeadRegionProperty); set => SetValue(HeadRegionProperty, value); }
    public Point4D TailRegion { get => (Point4D)GetValue(TailRegionProperty); set => SetValue(TailRegionProperty, value); }
    public Point4D DetailShift { get => (Point4D)GetValue(DetailShiftProperty); set => SetValue(DetailShiftProperty, value); }
    public Point Eyes { get => (Point)GetValue(EyesProperty); set => SetValue(EyesProperty, value); }
    public double EyeHeight { get => (double)GetValue(EyeHeightProperty); set => SetValue(EyeHeightProperty, value); }
    public Point PickupOffset { get => (Point)GetValue(PickupOffsetProperty); set => SetValue(PickupOffsetProperty, value); }
    public Point4D GestureRegion { get => (Point4D)GetValue(GestureRegionProperty); set => SetValue(GestureRegionProperty, value); }
    public Point GestureShift { get => (Point)GetValue(GestureShiftProperty); set => SetValue(GestureShiftProperty, value); }

    public PetMotionEffect()
    {
        PixelShader = Shader;
        foreach (var property in new[] { InputProperty, PreviousProperty, MixProperty, BreathProperty, BlinkProperty,
            PreviousFrozenProperty, SwayProperty, BreathCenterProperty, BreathInverseExtentProperty, HeadRegionProperty,
            TailRegionProperty, DetailShiftProperty, EyesProperty, EyeHeightProperty, PickupOffsetProperty,
            GestureRegionProperty, GestureShiftProperty }) UpdateShaderValue(property);
    }
    private static DependencyProperty Register<T>(string name, T initial, int register) where T : struct =>
        DependencyProperty.Register(name, typeof(T), typeof(PetMotionEffect), new UIPropertyMetadata(initial, PixelShaderConstantCallback(register)));
    private static PixelShader LoadShader()
    {
        var shader = new PixelShader { UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets/pet-motion.ps")) };
        shader.Freeze(); return shader;
    }
}
