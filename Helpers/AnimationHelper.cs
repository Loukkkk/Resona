using System;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;

namespace Resona.Helpers
{
    public static class AnimationHelper
    {
        public static void ApplyBouncyScale(UIElement element, float scaleValue)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            if (element is FrameworkElement fe)
            {
                visual.CenterPoint = new Vector3((float)fe.ActualWidth / 2, (float)fe.ActualHeight / 2, 0);
            }

            var spring = compositor.CreateSpringVector3Animation();
            spring.Target = "Scale";
            spring.FinalValue = new Vector3(scaleValue, scaleValue, 1.0f);
            spring.DampingRatio = 0.5f;
            spring.Period = TimeSpan.FromMilliseconds(40);

            visual.StartAnimation("Scale", spring);
        }

        /// <summary>
        /// Plays a smooth entrance animation (slide up + fade in) on the given element.
        /// Uses Composition APIs for 60fps performance.
        /// </summary>
        public static void PlayEntranceAnimation(UIElement element, float slideDistance = 40f, int durationMs = 250)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            // Fade in
            var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(0f, 0f);
            fadeAnim.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f)));
            fadeAnim.Duration = TimeSpan.FromMilliseconds(durationMs);

            // Slide up
            var slideAnim = compositor.CreateVector3KeyFrameAnimation();
            slideAnim.InsertKeyFrame(0f, new Vector3(0, slideDistance, 0));
            slideAnim.InsertKeyFrame(1f, Vector3.Zero, compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f)));
            slideAnim.Duration = TimeSpan.FromMilliseconds(durationMs);

            visual.StartAnimation("Opacity", fadeAnim);
            visual.StartAnimation("Offset", slideAnim);
        }

        public static void PlayFadeIn(UIElement element, int durationMs = 150)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(0f, 0f);
            fadeAnim.InsertKeyFrame(1f, 1f, compositor.CreateCubicBezierEasingFunction(new Vector2(0.1f, 0.9f), new Vector2(0.2f, 1f)));
            fadeAnim.Duration = TimeSpan.FromMilliseconds(durationMs);

            visual.StartAnimation("Opacity", fadeAnim);
        }

        public static async System.Threading.Tasks.Task PlayFadeOutAsync(UIElement element, int durationMs = 150)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(1f, 0f, compositor.CreateCubicBezierEasingFunction(new Vector2(0.8f, 0f), new Vector2(0.9f, 0.1f)));
            fadeAnim.Duration = TimeSpan.FromMilliseconds(durationMs);

            visual.StartAnimation("Opacity", fadeAnim);
            await System.Threading.Tasks.Task.Delay(durationMs);
        }

        public static async System.Threading.Tasks.Task PlayExitAnimationAsync(UIElement element, float slideDistance = 20f, int durationMs = 150)
        {
            var visual = ElementCompositionPreview.GetElementVisual(element);
            var compositor = visual.Compositor;

            var fadeAnim = compositor.CreateScalarKeyFrameAnimation();
            fadeAnim.InsertKeyFrame(1f, 0f, compositor.CreateCubicBezierEasingFunction(new Vector2(0.8f, 0f), new Vector2(0.9f, 0.1f)));
            fadeAnim.Duration = TimeSpan.FromMilliseconds(durationMs);

            var slideAnim = compositor.CreateVector3KeyFrameAnimation();
            slideAnim.InsertKeyFrame(1f, new Vector3(0, slideDistance, 0), compositor.CreateCubicBezierEasingFunction(new Vector2(0.8f, 0f), new Vector2(0.9f, 0.1f)));
            slideAnim.Duration = TimeSpan.FromMilliseconds(durationMs);

            visual.StartAnimation("Opacity", fadeAnim);
            visual.StartAnimation("Offset", slideAnim);

            await System.Threading.Tasks.Task.Delay(durationMs);
        }
    }
}
