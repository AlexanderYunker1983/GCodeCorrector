using System;
using System.Drawing;
using System.Windows;
using System.Windows.Media.Effects;

namespace GCodeCorrector.Shaders
{
    public class DisabledEffect : ShaderEffect
    {
        private static readonly PixelShader pixelShader = new PixelShader
        {
            UriSource =
                new Uri(@"pack://application:,,,/GCodeCorrector;component/Shaders/DisabledEffect.ps")
        };

        public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input",
            typeof(DisabledEffect), 0);

        public static readonly DependencyProperty DesaturationFactorProperty =
            DependencyProperty.Register("DesaturationFactor", typeof(double), typeof(DisabledEffect),
                new UIPropertyMetadata(default(double), PixelShaderConstantCallback(0), CoerceDesaturationFactor));

        public DisabledEffect()
        {
            PixelShader = pixelShader;
            UpdateShaderValue(InputProperty);
            UpdateShaderValue(DesaturationFactorProperty);
        }

        public Brush Input
        {
            get => (Brush)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public double DesaturationFactor
        {
            get => (double)GetValue(DesaturationFactorProperty);
            set => SetValue(DesaturationFactorProperty, value);
        }

        private static object CoerceDesaturationFactor(DependencyObject d, object basevalue)
        {
            var effect = (DisabledEffect)d;
            var newFactor = (double)basevalue;
            if (newFactor < 0.0 || newFactor > 1.0) return effect.DesaturationFactor;
            return newFactor;
        }
    }
}